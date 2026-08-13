# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Whisparr ("eros" branch, v2-develop) is an adult movie/scene collection manager (a *arr sibling of Sonarr/Radarr) — Usenet/BitTorrent automation with indexer search, download client integration, quality profiles, and library management. It's a fork of Sonarr: the backend still lives under the `NzbDrone` root namespace/directory names even though the product and public APIs are branded `Whisparr`. Don't be surprised by `NzbDrone.*` project names — they map 1:1 to `Whisparr.*` concepts.

Backend: C#/.NET 8, ASP.NET-hosted API server with SQLite/PostgreSQL storage.
Frontend: TypeScript/React (with legacy `.js` files still present), Redux, served by the backend and built with webpack.

## Build & dev commands

Backend (from repo root, requires .NET 8 SDK — see `global.json`):
```
dotnet msbuild -restore src/Whisparr.sln -p:SelfContained=True -p:Configuration=Release -p:Platform=<Windows|Posix> -t:PublishAllRids
```
For local iteration it's normally faster to open `src/Whisparr.sln` in Rider/VS/`dotnet build` and run the `NzbDrone.Console` or `Whisparr` startup project directly rather than using the full multi-RID publish above (that's what CI/`build.sh` does).

Frontend (from repo root, requires Node 20 + Yarn — see `volta` field in `package.json`):
```
yarn install --frozen-lockfile
yarn start        # webpack --watch, for local dev
yarn build         # production webpack build into _output/UI
```

Linting:
```
yarn lint          # ESLint over frontend/
yarn lint-fix       # ESLint --fix
yarn stylelint-windows   # or stylelint-linux, depending on host OS
```

`build.sh` is the canonical CI build script (bash) — it wraps `dotnet msbuild`, `yarn install`, webpack, and lint steps behind functions like `Build`, `YarnInstall`, `RunWebpack`, `LintUI`. Read it before changing build/CI behavior.

### Tests

Backend tests use NUnit and run via `test.sh <Platform> <Type> <Coverage|Test>`, e.g.:
```
./test.sh Windows Unit Test
./test.sh Linux Integration Test
```
`Type` is `Unit`, `Integration`, or `Automation`; this maps to NUnit `Category` filters (`Category!=IntegrationTest&Category!=AutomationTest`, etc.). The script expects pre-built test assemblies under `_tests/` (produced by the dotnet build), and targets fixed assembly names (`Whisparr.*.Test.dll`).

To run a single backend test class/method directly against a built test assembly, use `dotnet test` with an NUnit filter, e.g.:
```
dotnet test _tests/NzbDrone.Core.Test.dll --filter "FullyQualifiedName~MovieServiceFixture"
```
(Test projects are still under the `NzbDrone.*.Test` names — `NzbDrone.Core.Test`, `NzbDrone.Api.Test`, `NzbDrone.Integration.Test`, `NzbDrone.Automation.Test`, etc.)

There is no dedicated frontend unit test runner configured in `package.json` — frontend correctness is currently verified via lint/typecheck (`tsc`, wired through webpack's `fork-ts-checker-webpack-plugin`) and manual/automation testing.

## Architecture

### Backend layout (`src/`)

- **`NzbDrone.Core`** — the domain/business logic layer: movies, scenes, indexers, download clients, quality profiles, notifications, metadata, history, etc. Organized by feature folder (e.g. `Movies/`, `Indexers/`, `Download/`, `Notifications/`, `Profiles/`, `Datastore/`). This is where most feature work happens.
- **`Whisparr.Api.V3`** — REST API controllers and resources exposed to the frontend/external clients, one folder per resource area, mirroring `NzbDrone.Core`'s feature folders. Controllers translate between API resources and `NzbDrone.Core` models/services.
- **`Whisparr.Http`** — cross-cutting HTTP concerns: auth, versioned controller/feed attributes, REST base classes, client schema, frontend asset serving.
- **`NzbDrone.Core/Datastore`** — the ORM/data-access layer (custom, Dapper-based: `BasicRepository`, `TableMapping`, `SqlBuilder`, `WhereBuilder*`) supporting both SQLite and PostgreSQL (`WhereBuilderSqlite`/`WhereBuilderPostgres`, `PostgresOptions`).
- **`NzbDrone.Core/Datastore/Migration`** — numbered C# migrations (`NNN_description.cs`, zero-padded, monotonically increasing — check the highest existing number before adding one). Both schema and data migrations live here; this is the only place the DB schema changes.
- **`NzbDrone.Common`** — OS/platform abstraction (env, HTTP, disk, processes) used across projects.
- **`NzbDrone.Host`** — application bootstrap/composition root.
- **`NzbDrone.Windows` / `NzbDrone.Mono`** — platform-specific implementations.
- **`NzbDrone.Update`** — the self-updater, built/shipped as a separate app.
- **`NzbDrone.SignalR`** — real-time push to the frontend (used for progress/queue/notification updates).
- Each of the above has a matching `*.Test` project using NUnit; integration tests (`NzbDrone.Integration.Test`) spin up a real instance and hit the HTTP API, automation tests (`NzbDrone.Automation.Test`) drive the UI with Selenium.

Domain naming note: internally movies/releases are still modeled largely as "Movie" (see `NzbDrone.Core/Movies`), while adult-content-specific concepts (performers, studios, scenes, credits) layer on top (`Performers`, `Studios`, `Credits` API areas; `Scene` frontend area). When working across the stack, expect "Movie" (backend/model) and "Scene" (frontend UI) to sometimes refer to the same underlying entity.

### Frontend layout (`frontend/src/`)

- Feature folders mirror backend resource areas: `Movie/`, `Scene/`, `Performer/`, `Studio/`, `Calendar/`, `Wanted/`, `Settings/`, `Activity/`, `AddMovie/`, `InteractiveImport/`, `InteractiveSearch/`, `System/`, etc.
- **`Store/`** — Redux setup: `Actions/`, `Middleware/`, `Migrators/` (client-side persisted-state migrations, separate from backend DB migrations), `Selectors/`, `createAppStore.js`.
- **`Components/`** — shared/reusable UI components.
- Mixed `.js`/`.jsx`/`.ts`/`.tsx` — the codebase is mid-migration to TypeScript (see recent commit history, e.g. "Typescript Conversion Fixes"); new/touched code should generally be TS, but don't be surprised by adjacent legacy JS in the same feature folder.
- Webpack config: `frontend/build/webpack.config.js`. Backend serves the built `_output/UI` bundle in production.

### Cross-cutting

- API versioning: controllers under `Whisparr.Api.V3` are versioned via `VersionedApiControllerAttribute`/`VersionedFeedControllerAttribute` in `Whisparr.Http`.
- `ThingiProvider` (`NzbDrone.Core/ThingiProvider`) is the shared plugin-style base for provider-pattern features (indexers, download clients, notifications, import lists, metadata) — each of those areas follows a `Definition` + `Provider` + `Factory` pattern rather than being bespoke per feature.
- Code style is enforced at build time: StyleCop analyzers + `stylecop.json`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true` (see `src/Directory.Build.props`) — backend builds fail on style violations, not just warn.
