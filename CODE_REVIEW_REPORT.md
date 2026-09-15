# Code Review Report — Whisparr (`eros` branch)

**Date:** 2026-09-15
**Scope:** Whole-codebase high-level review (backend `src/` C#/.NET 8, frontend `frontend/` React/Redux), with deep-dives into the custom performer/studio/movie API work and the areas touched by recent commits. Method: architecture walkthrough, repo-wide anti-pattern scans (sync-over-async, `async void`, blocking waits, N+1 patterns, `Thread.Sleep` in request paths), and targeted reads of the hot files (`PerformerController`, `StudioController`, `MovieController`, `MovieRepository`, `MovieService`, frontend store/hook code).

**Overall assessment:** The codebase is in good shape for its size. The recent performance work (SQL aggregation for performer counts, on-demand catalog loading) is well-executed, documented in `docs/decisions.md` / `docs/handoff.md`, and covered by regression tests. The findings below are mostly incremental hardening, a few correctness gaps, and one large strategic item (frontend catalog payload).

---

## 1. Performance findings

### P-1. Cache invalidation gaps for performer/studio resource caches (correctness + perf) — **Priority 1 (small fix, real impact)**

`PerformerController` / `StudioController` cache full `PerformerResource`/`StudioResource` objects (including `MovieCount`, `SceneCount`, `SizeOnDisk`, `Years`) in `_performerResourceCache` / `_studioResourceCache` with no lifetime (never expires). Invalidation happens only on:

- `RestPutById` Update → cache removed ✅
- `Handle(PerformerUpdatedEvent)` / `StudioUpdatedEvent` → cache removed ✅
- **`DeletePerformer` / `DeleteStudio` → cache NOT removed ❌** — a deleted performer/studio keeps serving stale data (with full movie counts) until process restart, and re-populating from a stale `stashId` lookup resurrects the old payload.
- **Movie/scene changes (file import, file delete, movie delete, refresh) do NOT invalidate ❌** — `SizeOnDisk`/counts in the cache silently go stale for every performer/studio whose library changed.

**Work item:**
- Remove from cache in `DeletePerformer`/`DeleteStudio`.
- Subscribe to movie-lifecycle events (`MovieDeletedEvent`, `MovieFileAddedEvent`/`MovieFileDeletedEvent`/`MovieImportedEvent`) and invalidate the affected performer/studio cache keys (or clear the whole resource cache on batch operations — the fill is cheap now that `GetPerformerMovieCounts` is one ~84ms query).
- Alternative/simpler: give `cache.Set` a short lifetime (e.g. 5–10 min) so staleness self-heals.

### P-2. No negative caching for missing foreign IDs — **Priority 2 (small)**

In `GetPerformerResources`/`GetStudioResources`, IDs that resolve to nothing in the DB (e.g. performer foreign IDs left behind in `Credits` after a performer is deleted, or stale client state) are re-queried on **every** request and never cached. With a large credits table this is a permanent per-request DB tax.

**Work item:** cache a sentinel/empty marker for not-found foreign IDs (with the same lifetime as normal entries), or filter requested IDs against `AllPerformerForeignIds()` before hitting `FindByForeignIds`.

### P-3. Blocking `SemaphoreSlim.Wait()` in the API request path — **Priority 2 (small)**

`PerformerController`/`StudioController` cache-fill uses `_performerResourceCache.Lock.Wait()` — a synchronous, **unbounded** block on an ASP.NET request thread while holding the cache. Under concurrent cold-start requests (first UI load after restart with 9,620 performers), all request threads queue on this semaphore; combined with the sync controller stack this risks thread-pool starvation and the >60s warns the code already logs for.

**Work item:**
- Make the action async and use `await Lock.WaitAsync(TimeSpan.FromSeconds(X))` with a graceful fallback (serve non-cached resources instead of blocking forever).
- Reconsider the `> 100 missing` threshold: a single-user UI fetch of all performers will almost always exceed it, so the "rare stampede" path is actually the common path; a single-flight `Get(key, factory)` pattern would be simpler and always correct.

### P-4. Cache fill re-processes already-cached entries — **Priority 3 (small)**

When filling missing IDs, the code calls `ConvertToLocalPerformerUrls(...)` and `LinkMovies(performerResources)` over the **entire** result list, including entries that came from cache (already linked/converted). Harmless today because the linkage is one batched query, but it's redundant work on every cold fill and will grow with library size.

**Work item:** track which resources were newly built and scope cover-URL conversion + `LinkMovies` to just those before `cache.Set`.

### P-5. Frontend still ships/fetches the full catalog for index & detail pages — **Priority 2 (largest long-term win)**

Per `docs/handoff.md`: `POST /movie/bulk` returns **198MB for 50k movies**, and the root cause of UI slowness was "the frontend downloading the entire movie catalog on every app page load." The on-demand fix (Options 1 & 2) removed this from performer/studio detail pages, but:

- `MovieIndex` / `SceneIndex` mount effects still `fetchMovies()` the whole catalog into Redux.
- Movie/scene **detail** pages still fetch the full catalog (`MovieDetailsPageConnector.componentDidMount`) just to read one item.
- Known tradeoffs accepted in the handoff: global `MovieSearchInput` suggestions empty until an index is visited; `AddNew*` "already exists" count is 0 when deep-linked.

**Work item (staged):**
1. Quick wins from the handoff's own follow-ups: fetch-on-focus for `MovieSearchInput`; replace the `AddNew*` existence check with a lightweight API call.
2. Detail pages: fetch by `titleSlug`/id instead of hydrating the whole catalog.
3. Strategic: introduce server-side paging + table virtualization for the Movies/Scenes indexes (the `Table` + `TablePager` components already exist in `frontend/src/Components`). This eliminates the 198MB Redux payload entirely and is the single biggest remaining perf lever.

### P-6. O(n²) tag lookups in `TagsModalContent` (×4 copies) — **Priority 3 (trivial)**

`frontend/src/{Movie,Scene,Performer}/Index/Select/Tags/TagsModalContent.tsx` (+ DownloadClients variant) each do `tagList.find((t) => t.id === id)` inside a `.map` over tags. Trivial at current tag counts, but the pattern is O(n²) and — more importantly — the same component is **copy-pasted four times** (see A-1).

**Work item:** build a `Map<number, Tag>` via `useMemo` once; while touching it, consolidate the four copies into one shared component.

### P-7. `SizeOnDisk` correlated subquery in `GetPerformerMovieCounts` — **Priority 4 (note only)**

The hand-written aggregation runs a per-group correlated subquery for `SizeOnDisk` plus a `COUNT(DISTINCT)` join. It measures ~84ms today (ADR-001), so **no action needed now** — but if it regresses at larger library sizes, rewrite as a single `GROUP BY` join (`Credits → Movies → MovieFiles`) and reuse the ADR-002 `@pN` DynamicParameters pattern.

### P-8. Sync-over-async in the HTTP/indexer stack — **Priority 4 (inherited, do not churn)**

Repo-wide scan found `GetAwaiter().GetResult()` / `.Wait()` in `HttpClient`, `IndexerBase`, search services, `MediaCoverService`, etc. This is the upstream Sonarr design (synchronous pipeline end-to-end) and is consistent — flagging it only so nobody "fixes" one call site in isolation. **Recommendation:** leave as-is until/unless an upstream async migration happens; partial conversions create deadlock risk, not performance.

---

## 2. Coding anomalies

### A-1. Heavy duplication: `PerformerController` ≈ `StudioController`, and 4× `TagsModalContent`

The cache-stampede logic (`GetPerformerResources`, lock acquire/re-check/release, batch `LinkMovies`, cover conversion) is duplicated nearly line-for-line between `PerformerController.cs` (404 lines) and `StudioController.cs` (367 lines). Same for `TagsModalContent` on the frontend (4 near-identical copies). Any future fix must be applied in 2 places (backend) and 4 (frontend) — classic shotgun-surgery setup, and behaviors have already started to diverge (studio `Years` handling exists only on the studio side).

**Work item:** extract a shared generic cache-fill helper (e.g. `ResourceCacheFiller<TResource>` in `Whisparr.Http` or a common API base) parameterized by service + cover-mapper + count-linker; consolidate `TagsModalContent` into `Components`.

### A-2. `using DryIoc.ImTools;` in API controllers

Both controllers import `DryIoc.ImTools` solely for the `AddIfNotNull` list extension. This leaks a DI-container internals package into the API layer.

**Work item:** use the repo's own `NzbDrone.Common.Extensions` (or a one-line local guard) and drop the import.

### A-3. `_useCache` snapshotted at controller construction

`_useCache = configService.WhisparrCachePerformerAPI;` is captured once. Unlike other config read live per request, toggling this setting requires an app restart. Either document that, or read the flag live inside the actions.

### A-4. Misleading warning log text

`"Processed performer cache for {count} after {seconds} seconds"` reads oddly ("for {count}" — count of what?). Same in both controllers. Minor copy fix while consolidating (A-1).

### A-5. Stray backup artifact in repo root

`SABNZBD/sabnzbd_backup_5.1.2_2026.08.31_12.35.56.zip` — an unrelated SABnzbd backup zip sitting in the working tree. `.gitignore` only covers `Whisparr_*.zip`, so this can be accidentally committed.

**Work item:** delete it (or move out of the repo) and add `SABNZBD/` / appropriate `*.zip` rules to `.gitignore`.

### A-6. Legacy JS/TS mix (informational)

521 `.js`/`.jsx` files vs 1108 `.ts`/`.tsx` in `frontend/src` — mid-migration, as documented in `CLAUDE.md`. Convention is being followed (new/touched code is TS). **Recommendation:** keep converting opportunistically; prioritize `Store/Actions` and connectors that feed the on-demand movie fetching (P-5), since that's where type errors bite hardest.

### A-7. `Thread.Sleep` in disk/download-client paths (informational)

Hits in `DiskTransferService`, download-client proxies (qBittorrent/rTorrent/Aria2), `RateLimitService`, etc. are inherited Sonarr patterns (retry/polling backoff) — no action. Listed for completeness so the sweep result is on record.

---

## 3. What's done well (keep doing)

- **ADRs and handoff docs** (`docs/decisions.md`, `docs/handoff.md`) — measured before/after numbers, verified in production, root-caused the "43,876 queries" red herring to background tasks. Exemplary.
- **Parameterized SQL everywhere** — the manual `@pN` + `DynamicParameters` IN-clause pattern (ADR-002) avoids injection *and* is documented for reuse.
- **Regression tests added alongside every fix** (parser, quality, spec, repository, task-manager fixtures).
- **Batch-then-link pattern** in the controllers with clear comments explaining the N+1 they replaced.

---

## 4. Prioritized work plan

| # | Item | Effort | Impact |
|---|------|--------|--------|
| 1 | **P-1** Cache invalidation on delete + movie/scene file events (or TTL on the resource caches) | Small | Fixes stale counts/SizeOnDisk shown to users |
| 2 | **A-5** Remove `SABNZBD/*.zip`, fix `.gitignore` | Trivial | Hygiene; prevents accidental commit |
| 3 | **P-3** Async cache-fill with `WaitAsync(timeout)` + single-flight fill (removes unbounded thread block) | Small | Robustness under cold-start load |
| 4 | **P-2** Negative caching of missing foreign IDs | Small | Removes permanent per-request DB tax |
| 5 | **A-1/A-2/A-4** Consolidate Performer/Studio cache logic into shared helper; drop `ImTools` import; fix log text | Medium | Maintainability; prevents divergence |
| 6 | **P-5(1)** `MovieSearchInput` fetch-on-focus; `AddNew*` lightweight existence check | Small | Closes known UX tradeoffs |
| 7 | **P-5(2)** Detail pages fetch single movie/scene instead of full catalog | Medium | Big payload win for detail views |
| 8 | **P-5(3)** Server-side paging for Movies/Scenes indexes + virtualized table | Large | Eliminates 198MB catalog payload; biggest remaining perf lever |
| 9 | **P-4** Scope cache-fill work to newly built resources only | Small | Minor |
| 10 | **P-6/A-1(frontend)** One shared `TagsModalContent` + Map lookup | Small | Maintainability |
| 11 | **A-6** Continue TS migration opportunistically | Ongoing | Type safety |

Items 1–4 make a natural single PR ("hardening pass on performer/studio resource caches"); item 5 a second ("deduplicate resource cache fill"); items 6–8 a staged frontend initiative.
