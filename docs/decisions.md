# Architecture Decision Records

## ADR-001: Aggregate performer movie counts in SQL (2026-08-13)

**Status:** Accepted

**Context:** The performer list endpoint computed per-performer scene/movie counts and `SizeOnDisk` by loading every referenced `Movie` object (~59,651) into memory and iterating per performer. This dominated the ~13s response time.

**Decision:** Compute counts and `SizeOnDisk` in a single SQL aggregation (`Credits JOIN Movies JOIN MovieMetadata`, grouped by `PerformerForeignId`, `ItemType`) in `MovieRepository.GetPerformerMovieCounts`. The controller consumes lightweight `PerformerMovieCount` rows instead of full `Movie` objects.

**Consequences:** Performer list ~13s → ~1.6-2.4s. Cost: a small hand-written ADO/Dapper query that must be maintained in `MovieRepository`.

---

## ADR-002: Build Dapper `IN` clauses manually with DynamicParameters (2026-08-13)

**Status:** Accepted

**Context:** Passing `List<string>` to a raw-SQL query for a filter failed in two ways: `= ANY(@list::text[])` produced "malformed array literal", and `IN @list` produced "syntax error at or near $1". Dapper does not reliably expand `List<string>` in this codebase's query building.

**Decision:** In raw hand-written Dapper queries, expand list filters manually: generate one `@pN` placeholder per element and pass via `DynamicParameters`.

**Consequences:** Reliable cross-`List<T>` behavior; slight query-builder verbosity. Reuse this pattern for any future raw-SQL list filtering.

---

## ADR-003: Do not populate `performer.studios` on the list endpoint (2026-08-13)

**Status:** Accepted

**Context:** `PerformerResource.Studios` was computed for all 9,620 performers in the list response (~53,730 entries). The frontend never reads `performer.studios` — the performer detail page derives studios from `movies.items`. This added ~4MB to the payload and significant per-performer computation.

**Decision:** Remove the `Studios` computation from the list `LinkMovies` path. The field remains on the resource (null in the list) for API compatibility.

**Consequences:** Performer-list payload cut ~38% with no frontend behavior change. If a future client needs per-performer studios, fetch them on-demand (e.g. the detail page).

---

## ADR-004: Whole-cache clear on movie-library events; negative-cache sentinel for missing foreign IDs (2026-09-15)

**Status:** Accepted

**Context:** The performer/studio resource caches (`PerformerController`/`StudioController`) held full resources (counts, `SizeOnDisk`, `Years`) with no expiry and were only invalidated on resource update/refresh events. Deleted performers kept serving stale payloads, and any movie-file change left every cached count stale until restart. Separately, foreign IDs absent from the library were re-queried on every request.

**Decision:**
1. Movie-library events (`MoviesDeletedEvent`, `MoviesImportedEvent`, `MovieFileAddedEvent`, `MovieFileDeletedEvent`, `MovieFileUpdatedEvent`) clear the whole resource cache via `RestControllerWithResourceCache.InvalidateAllCachedResources()`. A one-movie → many-performers credits mapping would cost more than the refill (one ~84ms aggregation + one resource query).
2. Foreign IDs not found in the library are cached as a default `TResource` sentinel (`ForeignId` null/empty), and are invalidated by Add so a resource added later is served correctly.
3. The stampede lock is now bounded (`Lock.Wait(10s)`); on timeout the fill proceeds uncached.

**Consequences:** Counts served from cache are fresh after any library mutation (at the cost of one refill per clear during bulk imports — bounded, cheap). Unknown foreign IDs stop costing DB round-trips per request. The shared fill algorithm lives in one base class (`src/Whisparr.Api.V3/Shared/RestControllerWithResourceCache.cs`), removing the PerformerController/StudioController duplication.
