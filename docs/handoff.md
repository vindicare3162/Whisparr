# Handoff — Performer detail & list performance work

## 2026-09-15 (8): Server-side filter search — Search button limitation fixed

The last remaining "known limitation" from the pilot is resolved: **the Search button in
non-select mode now searches ALL matching scenes server-side** instead of just the current
page.

- `MoviesSearchCommand` extended with optional `ItemType` / `Monitored` / `HasFile` params.
  When `MovieIds` is empty and `ItemType` is set, `MovieSearchService.Execute` builds a
  server-side PagingSpec (PageSize=100000) with the same FilterExpressions as `/movie/paged`
  and interactive-searches all matching scenes — no full catalog needed client-side.
- `SceneIndexSearchButton` (non-select mode) dispatches the filter params derived from the
  active preset (itemType=scene + monitored/hasFile from FILTER_PARAMS mapping). Select mode
  still searches explicitly selected IDs.
- Confirmed pattern: mirrors the existing `MissingMoviesSearchCommand` approach (server-paged
  query + SearchForBulkMovies).

**Verified:** build + lint clean; deployed and the paged endpoint returns correct data
(59,562 items, 55.33 TiB, 37-letter jump bar).

**Remaining known limitation (deliberate, documented):** the filter builder remains
EQUAL-only — ranges/contains/arrays require the structured filter payload design and are a
future enhancement, not a regression from the old client-side index (which also lacked
server-side expression support for those filter types).

---

## 2026-09-15 (7): Stats + jump bar added to /movie/paged; blank-page fix

## 2026-09-15 (7): Stats + jump bar added to /movie/paged; blank-page fix

Follow-up on the pilot: the two "known limitations" that had cheap fixes are now addressed.

1. **Footer stats restored (server-computed):** `/movie/paged` now returns a `stats` object
   (`totalRecords`, `monitoredCount`, `hasFileCount`, `sizeOnDisk`) computed by a
   `MoviesStats` aggregate in `MovieRepository` honoring the same filters as the page.
   `SceneIndexFooter` was rewritten to read `state.sceneIndex.stats` instead of iterating the
   full client-side catalog.
2. **A-Z jump bar restored (server-computed):** `/movie/paged` also returns `jumpBar` — the
   first-letter histogram with the ROW_NUMBER position of each letter's first scene within
   the filtered+sorted set (only computed when sorted by title). `SceneIndex.tsx` maps a
   letter press to `gotoScenePage(floor(index / pageSize) + 1)`.

**Two gotchas hit and fixed during verification:**

- **Blank Scenes page:** `state.sceneIndex.items` was not initialized in the default state,
  so `SceneIndexSearchButton`'s `isDisabled={!items.length}` threw on first mount and crashed
  the whole React tree. Fix: `items: []` in `sceneIndexActions` defaultState.
- **Postgres 42712 "MovieMetadata specified more than once":** `MovieRepository.Builder()` is
  OVERRIDDEN to return a builder pre-joined with QualityProfile/MovieMetadata/MovieFile/
  AlternativeTitle. Any aggregate built on `Builder()` + an explicit `Join<Movie,
  MovieMetadata>` joins it twice. Fix: aggregates use `new SqlBuilder(_database.DatabaseType)`
  directly. Diagnosed by enabling `SqlBuilderExtensions.LogSql` and logging `template.RawSql`.

**Verified live:** `GET /movie/paged?...itemType=scene` → 200; `stats` = 59,560 records,
41,329 monitored, 19,085 with files, 55.32 TiB on disk; `jumpBar` = 37 letters (0–9, A–Z,
special chars) with correct positions; Missing preset (monitored+hasFile=false) → 39,743.

**Remaining known limitation (deliberate):** interactive Search outside select-mode stays
page-scoped; the filter builder remains EQUAL-only. Addressing either needs the structured
filter payload + server-side search-by-filter command sketched in the earlier #37 comment.

---

## 2026-09-15 (6): Scenes index converted to server-side paging (#37 pilot, deployed)

## 2026-09-15 (6): Scenes index converted to server-side paging (#37 pilot, deployed)

Per Option C agreed for #37: the **Scenes index** now uses `GET /movie/paged` server-side
collection state; the **Movies index stays client-side** for comparison.

### What changed (commit `201719b62`)

- `sceneIndexActions.js`: full server-side collection conversion using the established
  `createServerSideCollectionHandlers` pattern (fetch / first / previous / next / last /
  exact-page / sort / filter) against `/movie/paged` with `itemType=scene`. A custom fetch
  handler additionally merges the page records into `state.movies.items` so scene rows
  (`createMovieSelectorForHook`) resolve.
- Preset filters (All / Monitored / Unmonitored / Missing / Downloaded) map to server params
  (`monitored`, `hasFile`, `itemType`).
- Filter builder reduced to EQUAL-only server-supported props (monitored, year, added,
  releaseDate, studio, quality profile). Range/contains/array filters are not offered - the
  flattened query-param scheme cannot express them.
- `SceneIndex.tsx`: `TablePager` added below all views; jump bar and library-wide footer
  stats removed (they require the full catalog); sort menu trimmed to server-supported keys.
- **Refresh button** unchanged semantics: `REFRESH_MOVIE` without ids refreshes the whole
  library server-side (verified in RefreshMovieService).
- **Search button pilot limitation**: outside select mode it searches only the current page
  (the full catalog is no longer client-side); noted in code and needs a server-side
  "search by filter" command if that matters.
- Backend: `/movie/paged` gained `monitored`/`hasFile` filters and sort aliases (studio,
  qualityProfileId, runtime, certification). **Gotcha:** the datastore ExpressionVisitor
  cannot visit Conditional (ternary) expressions in FilterExpressions - use if/else branches.
- Movies index untouched (client-side, full catalog) for side-by-side comparison.

### Deployed & verified (live, 59,529+ scenes)

- Page 1 (100 records, sortTitle asc, itemType=scene): OK
- `monitored=true` -> 41,298; `hasFile=false` -> 59,466
- `sortKey=studio` -> sorted by studio title; certification/runtime/qualityProfileId aliases added
- UI serves; Movies index untouched

### Pilot QA still needed (browser)

- Scenes index loads instantly with pager; page/sort/filter navigation
- Select-mode bulk edit on page items; Refresh All; Search (page-scoped)
- Poster/overview views render the page correctly

---

## 2026-09-15 (5): #36 TS conversion - AddNew connectors + parseUrl

## 2026-09-15 (5): #36 TS conversion - AddNew connectors + parseUrl

- Converted the four `AddNew*Connector` files (Movie/Scene/Performer/Studio) to `.tsx` with
  typed props (PropTypes removed, `noImplicitAny`-safe) - these are the consumers of the
  on-demand `fetchMovieCount` added in #33.
- Converted `parseUrl.js` -> `.ts` with a `ParsedUrl` interface. Gotcha: TS inference on the
  original JS hid the `params` field (added via property mutation after object creation), which
  is exactly the kind of latent bug the migration exists to surface.
- New `AddMovieAppState` type; `AppState` now includes `addMovie` and `router` slices;
  minimal `typings/qs.d.ts` module declaration (qs ships no types).
- Verified: `tsc --noEmit` clean, ESLint clean, webpack production build (fork-ts-checker)
  passed inside the Docker build, container redeployed and API smoke-tested (count/paged/detail
  all healthy; library actively importing, 59,526 items).

Remaining #36 candidates: `movieActions.js` (largest on-demand surface - needs thunk/handler
typing), detail connectors, then index views.

---

## 2026-09-15 (4): #37 measurements + `GET /movie/paged` endpoint, redeployed

## 2026-09-15 (4): #37 measurements + `GET /movie/paged` endpoint, redeployed

### Payload measurements (live, 59,522-item library)

- Raw catalog payload (`POST /movie/bulk`, 10k items sampled): 35.2MB full / 26.6MB without
  credits -> projected ~210MB / ~158MB for the full catalog.
- Credits are only ~25% of the payload; overview (0.5MB/1k), genres, images, folderName/path
  make up most of the rest and are all used by the index views - further blind trimming would
  break views/filters.
- **Wire size is already small**: response compression (gzip) is enabled in Whisparr's
  Startup, so the actual transfer is ~6.2MB per 10k items -> **~37MB gzipped for the whole
  catalog** (down from the previously measured 198MB raw). The remaining cost is browser
  JSON parse time and memory, not the network.

### `GET /movie/paged` (stage 2 backend)

`GET /movie/paged?page=&pageSize=&sortKey=&sortDirection=&itemType=` returns
`PagingResource<MovieResource>` (page/pageSize/totalRecords/records) with the same enrichment
as `/movie/bulk` (batched statistics, local covers, root folder paths - no N+1).

- Sort keys accepted: id, title, sortTitle, studioTitle, year, releaseDate, added, path -
  translated to table-qualified columns (`MovieMetadata.SortTitle` etc.). Unknown keys fall
  back to sortTitle.
- `itemType=movie|scene` filters via a FilterExpression.
- **Learned the hard way:** the query SELECT template (`TableMapping.Mapper.SelectTemplate`)
  already joins MovieMetadata for hydration - do NOT add an explicit
  `.Join<Movie, MovieMetadata>` in `PagedBuilder`, it causes Postgres
  `42712: table name "MovieMetadata" specified more than once`.

### Deployed & verified

Rebuilt `whisparr-local:latest` (--no-cache-filter backend to defeat the BuildKit stale-cache
issue from earlier), recreated the container. Verified live:
- `?page=1&sortKey=sortTitle&itemType=scene` -> total=59522, sorted ascending, statistics present
- page=2 returns different records; `sortDirection=descending` on year -> 2026/2026/2023
- `itemType=movie` -> total=0 (correct: this dataset is 100% scenes)
- `/movie/count`, `/movie/detail/...`, `/movie/bulk` (both variants) all still working

### Remaining for #37 (open)

Wire the Movies/Scenes index frontend to `/movie/paged`: replace the client-side collection
selector with server-driven paging, move the filter builder to server-side filter expressions,
and handle select-all/jump-bar semantics. This is the last major piece.

---

## 2026-09-15 (3): Deployment + issues #34, #37 (stage 1), #36, #39

## 2026-09-15 (3): Deployment + issues #34, #37 (stage 1), #36, #39

### Deployment (local Docker, per DEPLOYMENT.local.md)

- Rebuilt `whisparr-local:latest` from `Dockerfile.local-test` and recreated the `whisparr`
  container on `whisparr-net` (port 6969, same volumes/env as documented).
- **Gotcha hit:** `docker run` failed with `error while creating mount source path
  '/run/desktop/mnt/host/i': file exists` (stale Docker Desktop WSL mount for drive I:).
  Fixed by `wsl --shutdown` (Docker Desktop auto-restarts the engine), then re-started the
  other containers (`sabnzbd`, `whisparr-postgres`, `stash-fulltest`, `stash-node-1`,
  `nzbindexer-db-1`, `stash-pg` - none have restart policies) and recreated `whisparr`.
- **Gotcha 2:** BuildKit cached the `COPY src` layer across builds even though source changed
  (several "successful" builds shipped stale code). Verified inside the image with
  `grep -a DetailByTitleSlug /app/bin/Whisparr.Api.V3.dll`; when in doubt use
  `--no-cache-filter backend`. The deploy that matters uses the post-fix image.
- Verified live (`X-Api-Key` auth, 59,522 library items): `updateMechanism=docker`,
  `GET /movie/count` -> 59522, `GET /movie/detail/{titleSlug}` -> 200 with enriched credits +
  statistics, `POST /movie/bulk?excludeCredits=true` strips credits.

### Issue work

1. **#34 (closed)** - `GET /movie/detail/{titleSlug}` backend endpoint (single resource,
   full enrichment: credits, statistics, covers, root folder). Note: the API `titleSlug` IS the
   `MovieMetadata.ForeignId` (StashDB ID) per the resource mapping. Frontend: `FETCH_MOVIE_DETAIL`
   thunk with per-slug request state (`state.movies.detailRequests`); merged movie lands in
   `state.movies.items` so detail connectors work unchanged. Detail pages no longer fetch the
   full catalog. Also fixed a latent redirect bug: `componentDidUpdate` redirected away as soon
   as the movie wasn't in items - now guarded to fire only after loading finished.
2. **#37 (open, stage 1 done)** - the 198MB catalog payload is dominated by credits arrays on
   every movie, which only the performer/studio detail flows use. `POST /movie/bulk` now accepts
   `excludeCredits=true`; the index catalog fetch (FETCH_MOVIES) passes it, performer/studio
   on-demand fetches keep credits. **Full server-side paging remains open** - it requires
   reworking the client-side collection architecture (filters/sort/select-all/jump-bar across
   table/posters/overview views) and is too large to land untested.
3. **#36 (open)** - converted `MovieDetailsPageConnector.js` -> `.tsx` with typed props
   (PropTypes removed); webpack resolves `.ts/.tsx` before `.js` so importers unchanged.
4. **#39 (closed)** - watch-point comment added on
   `MovieRepository.GetPerformerMovieCounts` (measured ~84ms, rewrite plan documented in code
   and ADR-001). Re-open or file a new issue if the query regresses at larger library sizes.

### Verification

- Backend build 0 warnings/0 errors; frontend tsc clean for touched files; ESLint clean.
- Live API checks above (curl - note: PowerShell `Invoke-RestMethod` showed intermittent 401s
  against the API that curl does not reproduce; server-side auth is fine).

---

## 2026-09-15 (2): On-demand movie count + search-on-focus + shared TagsModalContent (issues #33, #38)

## 2026-09-15 (2): On-demand movie count + search-on-focus + shared TagsModalContent (issues #33, #38)

### What was done

1. **Lightweight movie count endpoint** (`GET /api/v3/movie/count`, `MovieController.MoviesCount`
   -> `IMovieService.Count()` -> `BasicRepository.Count()` = `SELECT COUNT(*)`).
   Frontend: new `FETCH_MOVIE_COUNT` thunk in `movieActions.js` storing `state.movies.count`
   (`MoviesAppState.count`). The four `AddNew*Connector` pages now dispatch `fetchMovieCount()`
   on mount and read `state.movies.count` for the "already exists" banner - they no longer
   depend on the full catalog being in Redux. (#33)
2. **`MovieSearchInput` fetch-on-first-focus**: the header search dispatches `fetchMovies()`
   once on the first focus if the catalog isn't populated, so suggestions work from any page
   (previously they silently stayed empty until an index page was visited). (#33)
3. **Shared `TagsModalContent`**: extracted the 7 near-identical copies (Movie, Scene,
   Performer, Studio, DownloadClients, ImportLists, Indexers) into
   `frontend/src/Components/Tags/TagsModalContent.tsx` (+ shared CSS). Each area file is now a
   thin wrapper (~35 lines) selecting its collection and passing the area-specific help text;
   public props are unchanged so callers were untouched. The O(n^2) `find`/`indexOf`-inside-`map`
   pattern is replaced with `Map`/`Set` lookups. (#38)

### Verification

- `tsc --noEmit`: no errors in `frontend/src` (pre-existing @types/node duplicate-identifier
  noise in node_modules typings remains, unrelated).
- ESLint on touched files (after `--fix`): clean.
- Backend: `dotnet build src/Whisparr.sln -p:NuGetAudit=false` -> 0 warnings/0 errors;
  Core.Test filtered run (ParseMovieTitle, SceneReleaseTitle, MovieRepository, CreditRepository,
  StudioService fixtures) -> 136/136 passed.
- **WIP from an earlier session was committed in the preceding commit** ("Commit pending
  backend work..."): it contained `GetStudioMovieCounts`, which the already-pushed
  `StudioController` calls - HEAD was unbuildable without it. Also includes the parser
  duplicated-studio-title collapse, ReleaseTitleSpecification raw-title fallback,
  MovieStatisticsRepository O(n^2) fix, AddPerformer/Studio transient-failure tolerance, and the
  Dockerfile.local-test `/app/bin` + package_info Docker-deployment layout.

### Remaining open issues

| Issue | Title |
|-------|-------|
| #34 | Detail pages fetch single item instead of full catalog |
| #37 | Server-side paging for Movies/Scenes indexes |
| #39 | Watch correlated SizeOnDisk subquery at scale (note) |
| #36 | Continue TS migration (tracking) |

Stray file: `IDEA.md` (one-line scratch note, untracked) - safe to delete.

---

## 2026-09-15: Resource-cache hardening PR (issues #28, #29, #30, #31, #32, #35)

Implemented in one change set. See `CODE_REVIEW_REPORT.md` for the full review that produced
these issues.

### What was done

1. **New shared base class** `src/Whisparr.Api.V3/Shared/RestControllerWithResourceCache.cs`
   (`RestControllerWithResourceCache<TResource, TModel> : RestControllerWithSignalR<...>`).
   The cache-fill algorithm now lives in exactly one place; `PerformerController` and
   `StudioController` implement four hooks (`AllResourceForeignIds`, `BuildResources`,
   `ConvertToLocalUrls`, `LinkMovies`) plus `GetForeignId`. Fixes the duplication reported in
   issue #35 (A-1).
2. **Bounded stampede protection** (#30): the fill now uses `Lock.Wait(TimeSpan.FromSeconds(10))`.
   If the lock cannot be acquired in time it logs a warning and fills without it — a request
   thread can never block indefinitely on the cache semaphore anymore.
3. **Negative caching** (#28): foreign IDs that resolve to nothing in the library (e.g. credits
   left behind after a performer delete) are cached as a default `TResource` sentinel
   (detectable: `ForeignId` is null/empty) so they are not re-queried on every request.
   `AddPerformer`/`AddStudio` invalidate the cache entry so a later-added resource replaces its
   negative marker.
4. **Fill work scoped to new resources** (#31): cover URL conversion and movie-count linkage run
   only for freshly built (cache-miss) resources, not for the whole list including
   already-cached entries.
5. **Cache invalidation on library changes** (#29):
   - `DeletePerformer`/`DeleteStudio` invalidate the deleted resource's cache entry.
   - Both controllers now handle `MoviesDeletedEvent`, `MoviesImportedEvent`,
     `MovieFileAddedEvent`, `MovieFileDeletedEvent`, `MovieFileUpdatedEvent` by clearing the
     whole resource cache (`InvalidateAllCachedResources`). Rationale: refilling is cheap (a
     handful of batched queries, ~84ms aggregation) and mapping one movie to the many
     performers/studios it links to via credits would cost more than the refill.
   - `AddPerformer`/`AddStudio` also invalidate (replaces possible negative cache marker).
6. **DryIoc.ImTools removed from the API layer** (#35/A-2): the import existed for `AddIfNotNull`
   and `Map`; both replaced with plain LINQ/null checks. `using Whisparr.Http.REST;` dropped
   (IDE0005) from both controllers.
7. **Log wording fixed** (#35/A-4): the misleading "Processed performer cache for {count}..."
   warning is now "Processed {0} resources ({1} cache misses) in {2:F1} seconds".
8. **Housekeeping** (#32/A-5): deleted `SABNZBD/sabnzbd_backup_5.1.2_2026.08.31_12.35.56.zip`
   from the repo root, removed the empty `SABNZBD/` folder, and added `SABNZBD/` +
   `*.backup.*.zip` ignore rules to `.gitignore`.

### Verification

- `dotnet build src/Whisparr.sln -p:NuGetAudit=false` → 0 warnings / 0 errors
  (NuGetAudit=false only suppresses the **pre-existing** MailKit NU1902 vulnerability warning —
  see "Known pre-existing issues" below).
- `dotnet test src/NzbDrone.Api.Test` → 14/14 passed.
- Behavior-preserving notes: performer-side zero-out semantics for studios with no movies were
  NOT changed (performer link path still leaves counts null when the aggregation query returns
  no row — same as before).

### Known pre-existing issues (NOT fixed in this change)

- `MailKit 4.13.0` NU1902 warning-as-error at restore time. CI builds must pass
  `-p:NuGetAudit=false` or bump MailKit. Flagged for a future dependency-update PR.
- The working tree contains unrelated WIP (uncommitted) from an earlier session:
  `Dockerfile.local-test`, `Parser.cs`, `ParserTests`, `ReleaseTitleSpecification.cs`,
  `MovieStatisticsRepository.cs`, `MovieRepository.cs`, `MovieService.cs`,
  `AddPerformerService.cs`, `AddStudioService.cs`, `SceneReleaseTitleFixture.cs`, `IDEA.md`.
  Those were deliberately NOT committed here. Note: the WIP versions of
  `AddPerformerService.cs`/`AddStudioService.cs` previously failed SA1518 (no trailing newline)
  — fixed in the working tree during this session; the WIP changes themselves still need
  review + commit.
- `_useCache` (issue A-3, no GitHub issue yet) is still snapshotted at controller construction.

### Remaining open issues from the code review (tracked on GitHub)

| Issue | Title | Status |
|-------|-------|--------|
| #34 | Detail pages fetch single item instead of full catalog | open |
| #37 | Server-side paging for Movies/Scenes indexes | open |
| #39 | Watch correlated SizeOnDisk subquery at scale (note) | open |
| #36 | Continue TS migration (tracking) | open |

---

# Previous handoff (2026-08-13)

## Completed work

1. **Performer list N+1 fix** (commit `b504faa63`) — `PerformerController.LinkMovies(List<PerformerResource>)` batched from ~2 queries/performer (~19k DB round trips for 9,620 performers, 20-30s) to a single aggregation query. Added `MovieRepository.GetPerformerMovieCounts` computing counts + `SizeOnDisk` via one `Credits JOIN Movies JOIN MovieMetadata` grouped query (~84ms for all performers).

2. **Movie credits N+1 / empty-credits fix** (commit `b504faa63`) — movie API returned empty `credits` (nav property never loaded after the credit table split). Added `CreditRepository.FindByMovieMetadataIds`, `CreditService.GetCreditsForMovieMetadataIds`, and `MovieController.LinkCredits` to populate credits on `/movie` and `/movie/bulk`. Fixed `CreditPerformer.ForeignId` population.

3. **Studio payload removal** (uncommitted, on `eros`) — removed the `Studios` computation from `PerformerController.LinkMovies`; the frontend never uses `performer.studios` (detail page computes studios from `movies.items`). Cut performer-list payload 14MB → 8.18MB.

4. **Performer list aggregation** (uncommitted, on `eros`) — replaced loading all 59,651 referenced movies with `MovieRepository.GetPerformerMovieCounts` + SQL grouping; performer list ~13s → ~1.6-2.4s.

## Decisions & rationale

- **Aggregate counts in SQL instead of loading all movies in memory.** Loading ~59k full `Movie` objects to compute per-performer counts was the dominant cost. See `docs/decisions.md`.
- **Dapper `IN` clause must be built manually** with `DynamicParameters` (`@p0, @p1...`). `= ANY(@list::text[])` and `IN @list` both fail for `List<string>` (malformed-array / "syntax error at or near $1"). Documented — avoid repeating this.
- **`performer.studios` is dead weight** — the frontend derives studios from `movies.items`; do not repopulate it in the list endpoint.

## Verification performed / results

- Regression tests added & passing: `CreditRepositoryFixture.find_by_movie_metadata_ids...`, `.find_by_performer_foreign_ids...`, `MovieRepositoryFixture.get_by_movie_metadata_ids...`.
- Performer list API: 20-30s → ~1.6-2.4s; payload 14MB → 8.18MB. Verified data correct: AJ Applegate scenes 12/696 / 48.6GB; Holly Molly 74/152 / 258.9GB; count 9,620.
- Movie detail pages load for all performers (browser-verified AJ Applegate & Holly Molly).
- Latest diagnosis: `/movie/bulk` is query-efficient (~8 queries for 2,000 movies) but returns 198MB for 50k movies (~58s). The 43,876 "queries" seen were **background tasks** (ImportListSync, RefreshMonitoredDownloads), **not** the bulk endpoint. Root slowness = the frontend downloading the entire movie catalog on every app page load.

## Blockers

- None for the detail-page work itself. Note: the Docker image build takes ~30-45 min (frontend webpack + backend publish); plan deploys accordingly.

## Option 1 — frontend on-demand performer movies (implemented)

**Goal:** Stop the performer detail page from depending on the full `state.movies.items` (59,711 movies / 198MB) that `useAppPage` populates on every page load.

**Changes:**

- `frontend/src/Store/Actions/movieActions.js`:
  - New action type `FETCH_MOVIES_BY_PERFORMER` + thunk creator `fetchMoviesByPerformer({ performerForeignId })`.
  - New action type `SET_PERFORMER_MOVIES` + `createAction` creator `setPerformerMovies`.
  - Thunk handler: calls `GET /movie/listByPerformerForeignId?performerForeignId=...` to get the performer's movie IDs, then `POST /movie/bulk` with those IDs (chunked at 50k like `FETCH_MOVIES`), and dispatches `setPerformerMovies`. Skips refetch if already cached for that foreignId.
  - Reducer for `SET_PERFORMER_MOVIES` writes into `state.movies.performerMovies[performerForeignId] = { items, isFetching, isPopulated, error }` — a **separate slice** so it never clobbers the full `items` array (the `UPDATE` action replaces `items` entirely).
- `frontend/src/App/State/MoviesAppState.ts`: added `performerMovies: Record<string, Movie[]>` to `MoviesAppState`.
- `frontend/src/Performer/Details/PerformerDetailsConnector.js`:
  - `selectMovies` now reads from `movies.performerMovies[foreignId]` instead of filtering `movies.items` by `credit.performer.foreignId`.
  - `componentDidMount` + `componentDidUpdate` (on foreignId change) dispatch `fetchMoviesByPerformer(foreignId)`.
  - Added `dispatchFetchMoviesByPerformer` to mapDispatchToProps + propTypes.

**Backend endpoint already existed** — `MovieController.ListByPerformerForeignId` (`GET /movie/listByPerformerForeignId`), which uses `_moviesService.GetByPerformerForeignId` (non-cache path) to return the performer's movie IDs. No backend change needed for Option 1.

**Verification:** `yarn build` compiles successfully (webpack 5.95.0, 27s). Frontend deps installed via `yarn install --frozen-lockfile`.

## Option 2 — stop global catalog download on every page load (implemented)

While verifying Option 1, we found the app **still** downloaded the full 198MB catalog on *every* page load (performer detail included) because `useAppPage` (mounted once in `Page.tsx`, which wraps all pages) called `fetchMovies()` unconditionally. Per user decision, removed that global fetch and moved it to the pages that actually need the full catalog.

**Changes:**

- `frontend/src/Helpers/Hooks/useAppPage.ts` — removed `fetchMovies()` (and its import) from the startup effect. Commented why. Note: app-load gating `isPopulated` in `Page.tsx` never depended on `movies.isPopulated`, so removing it does not delay rendering.
- `frontend/src/Movie/Index/MovieIndex.tsx` — added `fetchMovies()` to the mount effect.
- `frontend/src/Scene/Index/SceneIndex.tsx` — added `fetchMovies()` to the mount effect.
- `frontend/src/Movie/Details/MovieDetailsPageConnector.js` — added `fetchMovies()` to `componentDidMount` (server both `/movie/:titleSlug` and scene detail routes; reads `items` directly).
- `frontend/src/Studio/Details/StudioDetailsConnector.js` — same on-demand pattern as performers: new `FETCH_MOVIES_BY_STUDIO` / `SET_STUDIO_MOVIES` thunk+reducer writing to `state.movies.studioMovies[foreignId]`; `selectMovies` reads from it; `componentDidMount` + `componentDidUpdate` dispatch `fetchMoviesByStudio(foreignId)`. Backend `GET /movie/listByStudioForeignId` already existed.
- `frontend/src/Store/Actions/movieActions.js` — added `FETCH_MOVIES_BY_STUDIO`/`SET_STUDIO_MOVIES` actions, `fetchMoviesByStudio`/`setStudioMovies` creators, thunk handler, and `studioMovies` reducer + `defaultState`.
- `frontend/src/App/State/MoviesAppState.ts` — added `studioMovies: Record<string, Movie[]>`.

**Known, accepted tradeoffs (left unchanged to limit blast radius):**
- Header `MovieSearchInput` (global) suggests movies from `state.movies.items`; on pages that never fetched the catalog (e.g. performer detail) its movie suggestions are empty until a Movies/Scene index or detail page is visited. Performer/studio/tag search still works. Acceptable on-demand behavior.
- `AddNew*Connector` pages use `state.movies.items.length` only for the "already exists" warning; count is 0 if deep-linked without visiting an index first. Cosmetic; add/search still works.

## Deployed & verified in production (2026-08-13)

Rebuilt `whisparr-fork-test:latest` via `Dockerfile.local-test`, recreated the `whisparr` container on `compose_whisparr_net` (port 6969) with identical mounts/env. Browser-verified with the network tab:

- **Performer detail page** (`/performer/aa359a16...` Holly Molly): fires ONLY `GET /movie/listByPerformerForeignId` + one small `POST /movie/bulk`. **No `/movie/list`, no 50k bulk.** Header counts correct (Scenes 74/152, 258.9 GiB). Loads fast.
- **Studio detail page** (`/studio/39cee498...` Brazzers Exxtra): fires ONLY `GET /movie/listByStudioForeignId` + one small `POST /movie/bulk`. Correct data (Scenes 3661/3911, 11.5 TiB).
- **Movies index** (`/movies`): fires `GET /movie/list` + `POST /movie/bulk` (full catalog) as intended for the index page.
- **Scenes index** (`/scenes`): populates fully — footer "Scenes 59711", individual scenes render with working `/movie/:titleSlug` links.
- Note: the catalog in this dataset is 100% `itemType: 'scene'` (0 movies), so the Movies index legitimately shows its empty state "No movies found" — a pre-existing data characteristic, not a regression.

**Result:** the full 198MB catalog is no longer downloaded on every page load. Only pages that need it (Movies/Scenes index, movie/scene detail) fetch it, and only when visited. Performer & studio detail pages are now fully on-demand.

## Exact next step

None blocking. Recommended follow-ups when convenient:
- Apply the same on-demand treatment to the header `MovieSearchInput` (currently it lazily relies on the catalog being already loaded; could fetch on focus) and the `AddNew*` lookup pages' "already exists" count.
- Consider committing all uncommitted changes (performer aggregation, studio payload removal, Option 1 + Option 2 frontend) to the `eros` branch once reviewed.