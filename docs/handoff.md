# Handoff — Performer detail & list performance work

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