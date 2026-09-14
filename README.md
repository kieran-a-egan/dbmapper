# DbMapper

An installable .NET command-line tool that exports SQL Server catalog metadata as a **Google Open Knowledge Format (OKF) v0.2** Markdown bundle for Claude Code and humans. Scan a database using an existing project's .NET user secrets, or create/update a bundle offline from local SQL files.

## Build and install

Requires a .NET 8 or newer SDK/runtime. SQL Server 2016 or newer and Azure SQL Database are the intended catalog targets; the integration check uses SQL Server 2022.

```powershell
dotnet restore DbMapper.slnx
dotnet build DbMapper.slnx -c Release --no-restore
dotnet run --project tests/DbMapper.SelfTest -c Release --no-build
dotnet pack src/DbMapper -c Release --no-build -o artifacts/packages
dotnet tool install --global --add-source ./artifacts/packages DbMapper.Tool --version 0.3.0
```

The `.slnx` build entry point requires a recent SDK (9.0.200+). With an older .NET 8 SDK, build/pack the individual `.csproj` files instead. The installed tool targets .NET 8 and permits major runtime roll-forward.

For a project-local tool, run `dotnet new tool-manifest` if needed, then replace `--global` with `--local` in the install command (use the absolute package directory if installing from another repository). Invoke the local tool with `dotnet tool run dbmapper -- ...`. This repository does not publish the package to NuGet.

For an existing installation, use `dotnet tool update` with the same source and version options.

## Run inside your application project

```powershell
dbmapper
dbmapper --connection DefaultConnection
dbmapper --project ./src/Web/Web.csproj --connection AppDb --output ./database-context
dbmapper --secret-key Database:ConnectionString
```

The project must already have a literal, unconditional `<UserSecretsId>` and its user-secrets store must contain the connection string. The default selects the only non-empty entry under `ConnectionStrings`; ambiguity is an error. `--connection Name` selects `ConnectionStrings:Name`, while `--secret-key` accepts an exact configuration key. Keys and values are never listed in diagnostics.

`--project` accepts one `.csproj` or a directory containing exactly one `.csproj`. Project XML is read without invoking MSBuild, building the app, starting it or evaluating imports. If the ID is inherited, conditional or computed, supply `--user-secrets-id <existing-id>` explicitly. The tool uses Microsoft's user-secrets configuration provider, including its standard Windows/Linux/macOS locations. It never reads appsettings, environment configuration overrides or secrets from command-line values.

By default, the connection must explicitly name a server and database, and only that database is inspected. Authentication and TLS come from the selected secret; certificate validation is not weakened. Attach-file and user-instance connections are refused. `--timeout 30` bounds each connection/query operation (1–300 seconds); Ctrl+C cancels database work. `--help` and `--version` do not read secrets or connect.

## Scan every database on a server

```powershell
dbmapper --all-databases --connection DefaultConnection --output ./server-context
```

`--all-databases` connects to `master` on the same server with the selected secret's existing authentication and TLS settings, enumerates `sys.databases`, and scans each visible online database it can access. User databases, system databases (`master`, `model`, `msdb`, `tempdb`) and visible snapshots are included. Microsoft-shipped objects remain excluded by the existing catalog filter. This mode supports SQL Server and Azure SQL Managed Instance; Azure SQL Database continues to use single-database mode. The connection secret may omit `Database` in this mode because discovery explicitly selects `master`.

The login needs access to `master`, server `VIEW ANY DATABASE` permission, and database `CONNECT` plus `VIEW DEFINITION` wherever structure should be exported. No data-reading or database-changing permissions are needed. Scans run sequentially, with a fresh connection for each database; database names are set through the connection-string builder, never interpolated into SQL. The tool does not bring databases online or change permissions.

Server-wide output **includes database names as structural identifiers**, with escaped Markdown/YAML and safe, stable folder names. Server addresses, connection strings, credentials and raw SQL errors remain excluded. Review database names before committing, just as you review table/column names. Single-database exports continue to omit database names.

```text
server-context/
  index.md                 # One OKF v0.2 root index
  server.md                # Per-database scan status and coverage
  CLAUDE.md                # Choose a database before reading schemas
  databases/
    index.md
    db-application-<stable-hash>/
      index.md             # Database section; no index frontmatter
      database.md
      CLAUDE.md
      schemas/...
    db-reporting-<stable-hash>/...
```

Offline, inaccessible or failed databases receive a status page. A partially successful run writes the accessible results and returns **exit code 4**, with the omissions listed in `server.md`. Refresh replaces the previous generated snapshot: old schema pages for a now-unavailable database are removed and replaced with its status page. Those removals must not be interpreted as database object deletions. Discovery failure, cancellation or an unexpected application error preserves the previous bundle. Use separate output directories if you want to retain both a single-database and server-wide snapshot.

`VIEW ANY DATABASE` is checked to avoid an accidentally restricted server listing, but SQL Server can still hide offline databases from low-privilege logins. Exit `0` means all **discovered** databases were exported; it does not guarantee visibility of every database that exists. No additional privileges are requested automatically. See [Microsoft's catalog visibility documentation](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-databases-transact-sql).

For Claude Code, import `@server-context/CLAUDE.md` from the project's `CLAUDE.md`. Names and relationships are scoped to each database; cross-database dependencies are not inferred. An [example server-wide bundle](examples/server-context/index.md) demonstrates two exported databases and an offline database's status page.

## Select databases to scan

```powershell
dbmapper --database Application --database Reporting --connection DefaultConnection --output ./server-context
```

Repeat `--database <name>` for each database you want in the bundle, including when you want just one named database. Names must match exactly, including case; quote names containing spaces or shell punctuation. Each argument is one literal name, so a comma is part of the name, not a separator. Select system databases explicitly if you need them.

Selection uses the same `master` discovery and permissions as `--all-databases`, but opens catalog connections only for the selected databases. An unknown or invisible selection fails before scanning and preserves the existing bundle. A selected database that is offline, inaccessible or missing metadata permissions gets a status page and exit code `4`. Unselected databases do not appear in the output or affect the exit code.

This command creates or **replaces the entire bundle** with the selected scope. Use the same selections on subsequent full refreshes. Choose one mode: `--all-databases`, repeated `--database`, or `--update-database`.

## Update one database after a piece of work

After applying your schema changes or migrations to the database and completing a discrete piece of work, refresh that database's context before committing:

```powershell
dbmapper --update-database Application --connection DefaultConnection --output ./server-context
# Continue only if dbmapper succeeded; inspect the generated changes before staging.
git diff -- ./server-context
git status --short -- ./server-context
```

`--update-database <name>` requires an existing server bundle created with `--all-databases` or `--database`, and the exact database name must already be listed in it. Existing version 0.2.0 server bundles are supported. The command connects directly to that database using the selected secret's server, authentication and TLS settings, without server discovery or `VIEW ANY DATABASE` permission. It still requires database `CONNECT` and `VIEW DEFINITION`.

The update replaces that database's generated subtree, removes its stale object pages, and updates its status in the shared indexes and `server.md`. Other databases retain their previous files and scan outcomes; they are not rescanned. No connection strings, timestamps or extra state files are added. Identical metadata produces no content diff.

A failed or cancelled targeted update preserves the entire previous bundle. Edited/foreign files, symbolic links and concurrent exports are refused through the usual bundle protections. Exit `0` means the chosen database was refreshed; other databases can still have incomplete or stale results, as shown in `server.md`. Review both the diff and newly created files before staging. The command only refreshes catalog documentation; it does not apply migrations, stage files or create a commit.

For the default single-database bundle, rerun the original `dbmapper` command after work; it already refreshes only the database named in the secret. Use a full or selected scan when you want to add/remove databases from a server bundle.

## Update from a ticket or SQL file before committing

Use `--sql` when a ticket's database changes have not been applied yet:

```powershell
# A ticket folder selects its in.sql file.
dbmapper --sql ./tickets/12345 --output ./database-context

# An explicit SQL file works too.
dbmapper --sql ./tickets/12345/in.sql --output ./database-context

# Choose one database in an existing server bundle.
dbmapper --sql ./tickets/12345 --update-database Application --output ./server-context

# Review before staging the bundle with the ticket changes.
git diff -- ./database-context
git status --short -- ./database-context
```

This updates the same schema pages, relationships and normal export status as a database refresh. It reads no project settings or user secrets, opens no database connection, and executes no SQL. It changes bundle files only: **it does not stage files, create a commit, or install a Git hook**. Run it after editing the ticket and before staging/committing. It reads the working-tree SQL, so stage the same SQL and bundle versions together.

`--sql` accepts one file or folder per invocation, including paths outside the current repository. A folder must contain `in.sql`; `out.sql` is left alone. To apply a rollback, explicitly pass `--sql ./tickets/12345/out.sql`. Quote paths containing spaces. Apply multiple tickets in migration order.

An existing single-database bundle supplies the starting model. If the output is missing or empty, the command starts with an empty model, allowing an initial export from `CREATE` statements. For a server bundle, `--update-database` must name an already exported database; other databases retain their files and status. `USE` and database-qualified names must stay within one database and agree with the target. Connection/project options, `--all-databases`, and `--database` cannot be combined with `--sql`.

### Editing and rerunning a ticket

An unchanged script is a no-op. Editing and rerunning the **most recently applied script** rebuilds its changes from its saved starting model. Earlier `ADD`/`CREATE` statements are not applied twice, and changes removed from the script are removed from the model too. A folder and its `in.sql` identify the same script.

Commit the generated `sql-history.md` with the bundle. It contains structural metadata baselines and source/content hashes; no SQL text, expressions, values, timestamps or source paths. Run from the same project directory and keep ticket paths stable so source identities match. A database refresh replaces that scope's history with current catalog metadata.

Editing an earlier script after applying later scripts is refused. Restore the bundle from before that script and reapply the scripts in order, or refresh from the database after the migrations run.

### Supported local SQL

The parser is [Microsoft SQL ScriptDOM](https://github.com/microsoft/SqlScriptDOM). Supported changes include:

* Table creation, column additions/alterations/drops, and table drops.
* Named primary/unique keys, foreign keys, checks, default-presence flags and `CHECK`/`NOCHECK` state.
* Ordinary clustered/nonclustered indexes, ordered keys, included columns, filters, drops and supported rebuild/disable operations.
* Procedure/function signatures and table/view trigger names through `CREATE`, `ALTER`, `CREATE OR ALTER`, and `DROP`; executable bodies are omitted.
* Simple views selecting named columns from one known table/view, with aliases or explicit view-column names.
* `GO`, `USE`, `BEGIN`/`END`, transaction begin/commit wrappers, common `SET` options, and constant `IF OBJECT_ID(...) IS NULL` / `IS NOT NULL` guards evaluated against local metadata.

Data-only statements (`INSERT`, `UPDATE`, `DELETE`, `MERGE`, etc.) are ignored and never executed. Comments, values, default/check/filter expressions and executable definitions stay out of the bundle.

Use exact schema/object/column names; unqualified objects default to `dbo`. Specify `NULL` or `NOT NULL` for new columns (primary keys and identity columns can imply non-nullability), and name keys/checks explicitly. Unspecified column collations remain unspecified; use explicit `COLLATE` where needed. Alias types retain their declared names without resolving their definitions.

Unsupported constructs fail the entire update with a sanitized statement type and line number, preserving the bundle. These include dynamic SQL/procedure execution, arbitrary control flow, `SELECT INTO`, computed columns, complex views, temporary/cross-database objects, temporal/specialist tables, partitioned index definitions, and dropping pre-existing default constraints by name (catalog bundles only retain default presence). A `ROLLBACK` statement is not interpreted; rollback files must contain supported DDL. Use a database refresh when SQL Server must resolve omitted metadata.

## Output and Claude Code

```text
database-context/
  index.md                 # Root OKF index with okf_version: "0.2"
  database.md              # Coverage and export limitations
  CLAUDE.md                # OKF concept with agent retrieval guidance
  schemas/
    s-dbo-<stable-hash>/
      index.md             # Progressive navigation; no frontmatter
      schema.md
      table-orders-<stable-hash>.md
      view-openorders-<stable-hash>.md
      routine-getorders-<stable-hash>.md
```

Each concept has YAML `type`, `title`, `description`, `status: draft`, producer identity and a source scope descriptor. No human verification is claimed. Index files follow OKF's reserved-file rules. Tables/views contain columns, primary and unique keys, indexes, explicit include columns, key direction, partition ordinals, foreign keys with composite column order, inbound relationships, and check/trigger state. Routine files contain catalog parameter signatures. Names are escaped in Markdown/YAML and converted to bounded, hash-suffixed filenames; object IDs and timestamps do not enter the output. Database names appear only in server-wide mode. Unchanged metadata and scan outcomes produce identical bytes.

Add this line to your application's existing `CLAUDE.md` (adjust the relative path):

```markdown
@database-context/CLAUDE.md
```

Or tell Claude Code to start with `database-context/index.md` and follow the relevant links. The tool does not edit existing project instructions. Keep human-authored notes outside the generated directory. A small generated example lives in [examples/database-context](examples/database-context/index.md).

## Safety contract

* **Fixed catalog queries only.** The embedded, reviewable [Catalog.sql](src/DbMapper/Catalog.sql) selects only explicit fields from `sys` catalogs and checks metadata permission; [ServerReader.cs](src/DbMapper/ServerReader.cs) adds fixed server/database discovery queries. No dynamic SQL, user-table/view row reads, stored-code execution, DDL or DML. Session settings bound lock waits to five seconds and lower deadlock priority. No transactions are enlisted; pooling and connection retries are disabled. `ApplicationIntent=ReadOnly` is a routing hint, not a database-enforced read-only mode.
* **Metadata login.** Use a dedicated login with `CONNECT` and database `VIEW DEFINITION`, with no application-data read/write or DDL permissions. A DBA can grant `VIEW DEFINITION TO [your_metadata_user]` in the selected database. The tool refuses exports without database-level metadata visibility. Object-level denials can still affect visibility; it does not claim a complete or transactionally consistent snapshot. Run during a quiet schema window.
* **Exclude risky fields at the query boundary.** No rows, samples, row counts, statistics, current identity/sequence values, SQL definitions, expression text, default literals, comments, extended properties, users, server names or file paths. Database names are used/exported only for server bundles (`--all-databases`, `--database`, `--update-database`). Defaults/computed/check/filter expressions can hold sensitive literals, so only their presence/state is exported. There is no unsafe include-definitions switch. SQL exceptions, configuration errors and raw arguments are never printed.
* **Review identifiers.** Table/column/index/routine names and declared SQL types remain visible because they are essential development context. They can themselves be confidential. This is suitable for review and committing as schema documentation, not a guarantee that arbitrary names are safe to publish. Catalog names are treated as untrusted data, including in the agent guide.
* **Preserve existing files.** All generated files carry an integrity footer. Refresh first checks every existing file, writes a complete sibling staging directory, then swaps directories, with rollback on a failed install. It refuses edited/foreign files and symbolic links/junctions, removes stale intact generated files, and uses a per-output lock to prevent competing exports. CRLF checkout line endings are normalized for integrity comparison. The footer detects accidental edits; it is not an authenticity signature. An interrupted process can leave a `.dbmapper-*` staging/backup directory; preserve any backup until you have verified the output. Add `.dbmapper-*` to your application's `.gitignore` to exclude these temporary artifacts.

This is context, not a DDL backup: empty schemas, sequences, synonyms, user-defined type definitions, table-valued results, full-text options, partition boundaries, security policies and specialist index internals are outside this version's coverage. Expressions and routine behavior cannot be reconstructed from flags. No relationships or business rules are inferred. Existing server-side connection auditing/logon behavior remains under the database administrator's control.

## Verification

```powershell
dotnet run --project tests/DbMapper.SelfTest -c Release
pwsh -File scripts/Test-Integration.ps1
dotnet format DbMapper.slnx --verify-no-changes
dotnet list src/DbMapper package --vulnerable --include-transitive
uv run --no-project --with pyyaml python scripts/Validate-Okf.py examples/database-context
uv run --no-project --with pyyaml python scripts/Validate-Okf.py examples/server-context
```

The standalone self-check uses no test framework. The integration script needs Docker, starts a disposable SQL Server 2022 container bound only to loopback, creates a synthetic database and metadata-only login, and tests the real CLI and user-secrets path. It never uses a real project's secrets. Synthetic row values, SQL definitions and error details are checked for leakage, alongside key/index correctness, deterministic refresh and protection of hand-written files. Test database/secret resources are removed when the script finishes normally. The optional independent OKF check uses Python/PyYAML via `uv` and validates actual YAML, index rules, links and integrity footers; the installed tool has no Python dependency.

The server integration checks also cover multiple read-only databases, system databases, escaped database names, duplicate table names, missing permissions, offline/inaccessible databases, deterministic partial results, database selection, and targeted updates after live schema changes. They verify that unknown selections, failed updates and cancellation preserve output, that unselected databases retain their files, and that targeted updates work without server discovery permission. Pass `-ToolPath <installed-dbmapper-command>` to `Test-Integration.ps1` to include actual packaged-process exports in all modes and, on Windows, junction protection checks.

The offline checks cover file/folder selection, initial and existing bundles, edited/repeated scripts, explicit rollback files, server-target isolation, redaction and atomic failure. Integration also applies synthetic scripts to a disposable database and compares the resulting catalog bundle with the offline output.

Exit codes: `0` success/help; `1` sanitized configuration/filesystem/unexpected failure; `2` invalid arguments, a safety precondition, or unsupported/malformed local SQL; `3` sanitized SQL/discovery failure; `4` server export written with one or more databases skipped/failed; `130` cancellation before bundle replacement.

## Format and platform references

* [Google OKF v0.2 specification](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/main/okf/SPEC.md)
* [Microsoft .NET user-secrets configuration](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
* [SQL Server metadata visibility](https://learn.microsoft.com/en-us/sql/relational-databases/security/metadata-visibility-configuration)
* [SQL Server catalog index columns](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-index-columns-transact-sql)
* [Claude Code context imports](https://code.claude.com/docs/en/memory#import-additional-files)
