# FamilyTree

A local, single-user Windows desktop application for recording family members and exploring their relationships. Built with .NET 10, WPF, CommunityToolkit.Mvvm, and SQLite.

## Run on Windows

Install the **.NET 10 SDK** (10.0.100 or a later stable .NET 10 feature band). From the repository root:

```powershell
dotnet restore FamilyTree.sln --locked-mode
dotnet build FamilyTree.sln -c Release --no-restore
dotnet test tests/FamilyTree.Tests -c Release --no-build --no-restore
dotnet run --project src/FamilyTree.UI -c Release --no-build
```

Alternatively, open `FamilyTree.sln` in an IDE supporting .NET 10/WPF and set `FamilyTree.UI` as the startup project. No database server, account, credentials, or web service is required. The application starts with an empty database; it does not insert example people into your family.

### Use the application

1. Choose **Add member**, enter a name and statuses, optionally set a birth date, and save.
2. Select a member, choose **Add relationship**, then complete the sentence: **[selected member] is the [relationship] of [other member]**. For example, *Michael is the Son of John*.
3. Recorded relationships show the other person's relationship to the selected member. Reverse labels and the **Across generations** section update automatically.
4. Use **Mark as former spouse** to record a divorce and preserve the connection. Use **Remove incorrect relationship** to correct an erroneous entry.
5. Remove all of a member's relationships before deleting that member. Deletion and relationship removal ask for confirmation.

6. Choose **Show family tree** to open a separate visualization window. Current spouses share a card: male left/light blue, female right/light pink. Parent arrows terminate at the child's own section, including married children. Children and grandchildren appear in successive rows. The window supports horizontal/vertical scrolling; **Refresh tree** or reopening reloads saved data.

The deterministic layout also includes isolated members, single parents, and separate former spouses. Same-gender spouses retain their recorded genders/colors and use name order. In unusual cross-generation marriages where combining couples would create a layout cycle, individual cards are displayed with an explanatory notice. This MVP avoids overlapping cards but does not guarantee crossing-free connectors in complex families.

Changes are saved immediately after a successful operation. **Refresh** reloads the database. Switching members or refreshing discards an unsaved editor form; **Cancel** also discards it.

### Database and backups

The default database is `Output/Data/familytree.db` under the repository root. All projects build beneath `Output/Builds/<project>/<configuration>/<framework>/`; Debug, Release, and publish builds inside that tree share the same data folder. Clean/rebuild never targets `Output/Data`.

Startup creates missing tables/indexes and seeds missing relationship types transactionally, without dropping tables or replacing saved records. The three SQL tables are `FamilyMember`, `RelationshipType`, and `MemberRelationship`; relationship rules remain in Core code. Existing members and relationships load automatically through the member service and MainViewModel.

On first use, if the new database is absent, the app imports the previous `%LOCALAPPDATA%\FamilyTree\familytree.db` using SQLite's backup API (including committed WAL records). The original remains untouched. Once the new database exists it is authoritative; later changes made with an old executable are not merged. The resolved database path is visible in the main window footer.

A distributed executable outside an `Output/Builds` tree uses `<executable folder>/Output/Data`, independent of the working directory. Keep the package in a writable location and retain its data folder when upgrading. Set `FAMILYTREE_DB_PATH` to an absolute file path to use a different database:

```powershell
$env:FAMILYTREE_DB_PATH = Join-Path $env:TEMP 'FamilyTree-smoke.db'
dotnet run --project src/FamilyTree.UI -c Release --no-build
```

Close all app instances before copying the database for backup or replacing it to restore a backup. The SQLite file is local and unencrypted; use an appropriately protected Windows account/folder. Do not commit family databases to Git.

## Linux cloud development

Linux supports Core, SQLite, portable ViewModel tests, and compilation/packaging of the WPF application. **WPF cannot run on Linux.** Desktop interaction must be checked on Windows.

```bash
bash scripts/setup-cloud.sh
source scripts/cloud-env.sh
dotnet test tests/FamilyTree.Tests -c Release --no-build --no-restore
```

The script installs the official .NET SDK 10.0.401 under `/workspace/tooling/familytree`, verifies its SHA-512 checksum, restores locked dependencies, builds the complete solution, and runs the tests. It requires Linux x64, `curl`, `tar`, `sha512sum`, and standard .NET native dependencies (including ICU and OpenSSL), all available in the prepared Debian cloud image. Set `FAMILYTREE_TOOLING_DIR` before running/sourcing the scripts to use a different tools directory. Each shell needs to source `cloud-env.sh`; no background service is needed. Use the existing checkout in each isolated cloud task; a separate Git worktree is unnecessary.

Downloads use Microsoft's `builds.dotnet.microsoft.com` and NuGet (`api.nuget.org`, `globalcdn.nuget.org`). The current cloud package-manager network preset supplies this access. Normal local use needs no network access.

## Package for Windows x64

```powershell
dotnet publish src/FamilyTree.UI -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o Output/Builds/win-x64
```

Distribute the **entire** `Output/Builds/win-x64` folder; launch `FamilyTree.UI.exe` on Windows. This self-contained package includes the runtime and SQLite native library. Packaging from Linux is supported, but does not validate execution on Windows. An installer, signing, auto-update, and ARM64 distribution are outside this MVP.

## Architecture

| Project | Responsibility |
| --- | --- |
| `FamilyTree.Core` | DTOs, enums, contracts, validators, member/relationship use cases, relationship catalog, resolution and traversal |
| `FamilyTree.Data` | Parameterized SQLite repositories, numbered schema migration, operation-scoped connections/transactions |
| `FamilyTree.UI` | WPF views, MVVM editors, DI composition in `App.xaml.cs` |
| `FamilyTree.Tests` | Acceptance, validation, concurrency, database, and portable ViewModel tests |

Core references no implementation packages. Data references Core. ViewModels use Core service contracts; only the UI composition root references Data. SQLite work uses synchronous operation-scoped APIs (Microsoft.Data.Sqlite does not provide asynchronous database I/O); async ViewModel commands dispatch that work to a worker thread and expose loading/errors on the UI thread.

Writes acquire an immediate SQLite transaction **before** reading validation state. The same transaction validates the graph, writes facts, and updates affected marital statuses. Failures roll back the operation. Read snapshots also use a transaction. Connections enable foreign keys individually and wait up to 15 seconds for competing locks.

Only `Parent`, `Spouse`, and `FormerSpouse` are stored. Parent points from parent to child; spouse endpoints use normalized GUID text order. The centralized catalog validates gender-specific entry labels before normalization. Display labels, siblings, grandparents, grandchildren, ancestors, and descendants are calculated from an in-memory snapshot; iterative traversal deduplicates paths. See [domain rules](docs/domain-rules.md) for the enforced policies.

## Validation and remaining platform check

Run `dotnet test tests/FamilyTree.Tests`. Tests use isolated real SQLite files and an injected clock; they do not touch your family database. The portable ViewModel source is linked into the test project so command/error/refresh behavior is exercised on Linux without loading WPF. XAML, bindings, dialogs, and actual window interaction require the [Windows acceptance checklist](docs/windows-acceptance.md).

GitHub Actions is configured to build and test on Linux and Windows and produce a self-contained Windows artifact. The workflow's presence does not establish that a remote run or Windows desktop check has passed.
