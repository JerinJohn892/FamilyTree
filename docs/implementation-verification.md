# Implementation verification — 2026-10-06

## Assessment

The existing application already followed WPF/MVVM with Core DTOs, validation and business services, SQLite repositories, and operation-scoped transactions. MemberEditor/RelationshipEditor commands call services, validate and commit SQL before refreshing MainViewModel. Startup already loaded a persisted graph. No database technology change was needed.

The previous database was `%LOCALAPPDATA%/FamilyTree/familytree.db`, not the project directory. Physical inspection found FamilyMember (5 rows), MemberRelationship (6 rows), RelationshipType (3 rows), schema version 1. Previous builds used each project's bin folder. No RelationshipRule table is needed: rules reside in Core.

## Changes and files

- `Directory.Build.props`: all project builds use Output/Builds/<project>/<configuration>/<framework>. CI publishing now uses Output/Builds/win-x64.
- `Data/ApplicationPaths.cs`: one executable-relative path resolver; Debug/Release/publish share Output/Data. Explicit absolute FAMILYTREE_DB_PATH remains available for testing. A standalone package outside Output/Builds uses its own Output/Data.
- `Data/LegacyDatabaseImporter.cs`: SQLite online backup imports the old database only when the target is absent; validates before atomic publication, retains the source, and never overwrites a destination.
- `Data/SqliteDatabase.cs`, `Data/Migrations/Schema.cs`: version guard plus transactional, idempotent creation of missing tables/indexes and missing relationship master rows. No startup table drops or replacement databases.
- `UI/App.xaml.cs`: initializes/imports before showing the app and registers tree services with DI.
- `Core/DTOs/TreeLayout.cs`, `Core/Abstractions/IFamilyTreeLayoutService.cs`, `Core/Services/FamilyTreeLayoutService.cs`: WPF-independent graph layout; active spouses grouped, member-specific child anchors, iterative generation calculation, deterministic spacing, disconnected people and generation-skipping links.
- `UI/ViewModels/FamilyTreeViewModel.cs`: loads a fresh persisted graph through IFamilyMemberService, applies layout, exposes cards/connector paths and refresh/error state. Reuses canonical Parent/Spouse facts; no second relationship system or stored coordinates.
- `UI/ViewModels/ITreeWindowService.cs`, `UI/Views/TreeWindowService.cs`: opens/reuses an owned window on demand, refreshes on every request.
- `UI/Views/FamilyTreeWindow.xaml` and code-behind: scrollable canvas with light-blue male and light-pink female sections, arrows, names, birth dates/life status, refresh and empty/error text. Code-behind only wires DataContext.
- `UI/ViewModels/MainViewModel.cs`, `UI/Views/MainWindow.xaml`: Show family tree command and visible database path.
- `tests/.../OutputPersistenceTests.cs`, `tests/.../TreeLayoutTests.cs`, updated editor test setup: path/import/schema repair, persistence, correct spouse ordering and child anchors, three generations, refresh, disconnected members, former spouses and contracted graph cycle coverage.
- `README.md`, `.gitignore`: usage, locations, portability, migration and limitations; Output excluded from source control.

## Verified

- Locked restore succeeded.
- Full Debug and Release solution builds: zero warnings/errors.
- All 49 tests passed in Debug and Release, including existing CRUD, validation, transaction rollback and concurrency tests.
- New Output/Data database has rows identical to the original in all three tables. SQLite integrity_check = ok; foreign_key_check returned no violations.
- Database SHA-256 unchanged across dotnet clean and Debug/Release builds.
- Actual Debug startup imported and displayed 5 saved members and 6 relationships. Closed app, built Release, and launched Release: same records loaded from the same database.
- Actual WPF tree window opened only through Show family tree. Existing family displayed with spouse cards and correct incoming son arrow.
- Separate temporary sample database rendered John/Mary, married son Michael/Susan, married daughter David/Anna, and their next generation. Visually inspected both incoming half-card anchors and grandchild branch.
- Through the sample UI, created Restart check and saved “Restart check is the Son of John”. Physical SQL inspection confirmed both records. Closed and restarted the sample app: 8 members and 10 relationships reloaded, including Restart check and John as Father. Sample data never entered the user's database.

## Limits

This MVP uses generation rows, not a crossing-minimizing graph solver. Complex families may have crossing/shared line segments. Former spouses are separate cards; their historical spouse link remains in the management view. Valid ancestry whose spouse contraction creates a cycle falls back to individual cards with a notice. Same-gender couples retain recorded colors and use name order. Long names truncate with full-name tooltips. Zoom/export and automatic live refresh are not implemented; use Refresh tree.

The retained legacy database is a backup, not a synchronized second store. Continue using the new Output/Builds executable. Do not delete Output/Data when manually replacing builds.
