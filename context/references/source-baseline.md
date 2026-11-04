---
type: Reference
title: Existing Code and External Specifications
description: Current implementation and upstream references to inspect before editing.
status: draft
tags: [references, baseline, external]
---

# Existing code and external specifications

## Existing repositories

- [DbMapper](https://github.com/kieran-a-egan/dbmapper) (`main` inspected during planning). Existing implementation is a .NET 8 command-line tool; proposed .NET 10 library, CLI and desktop projects **do not yet exist** as verified source in this exercise.
- [Pi Software Factory](https://github.com/kieran-a-egan/pi-software-factory) — local Pi extension used for controlled developer work; configuration documented as `.pi/software-factory.json`, with role stages, deterministic verification and optional parallel isolated workers. It is not a runtime dependency.

## DbMapper source landmarks

- [`src/DbMapper/Cli.cs`](https://github.com/kieran-a-egan/dbmapper/blob/main/src/DbMapper/Cli.cs) — command options, supported modes, error/exit mapping.
- [`src/DbMapper/ProjectSecrets.cs`](https://github.com/kieran-a-egan/dbmapper/blob/main/src/DbMapper/ProjectSecrets.cs) — XML UserSecretsId resolution and user secrets key selection.
- [`src/DbMapper/ServerReader.cs`](https://github.com/kieran-a-egan/dbmapper/blob/main/src/DbMapper/ServerReader.cs) — master discovery and database scanning.
- [`src/DbMapper/CatalogReader.cs`](https://github.com/kieran-a-egan/dbmapper/blob/main/src/DbMapper/CatalogReader.cs) and [`Catalog.sql`](https://github.com/kieran-a-egan/dbmapper/blob/main/src/DbMapper/Catalog.sql) — read-only metadata projection.
- [`src/DbMapper/OkfBundle.cs`](https://github.com/kieran-a-egan/dbmapper/blob/main/src/DbMapper/OkfBundle.cs) and [`ServerBundle.cs`](https://github.com/kieran-a-egan/dbmapper/blob/main/src/DbMapper/ServerBundle.cs) — generated markdown paths, links, statuses and content.
- [`src/DbMapper/BundleWriter.cs`](https://github.com/kieran-a-egan/dbmapper/blob/main/src/DbMapper/BundleWriter.cs) — existing integrity/staging/swap behaviour.
- [`src/DbMapper/SqlFileUpdate.cs`](https://github.com/kieran-a-egan/dbmapper/blob/main/src/DbMapper/SqlFileUpdate.cs), [`SqlModelReader.cs`](https://github.com/kieran-a-egan/dbmapper/blob/main/src/DbMapper/SqlModelReader.cs), [`SqlScript.cs`](https://github.com/kieran-a-egan/dbmapper/blob/main/src/DbMapper/SqlScript.cs) — offline SQL parsing, history and full model recovery.
- [`tests/DbMapper.SelfTest`](https://github.com/kieran-a-egan/dbmapper/tree/main/tests/DbMapper.SelfTest), [`scripts/Test-Integration.ps1`](https://github.com/kieran-a-egan/dbmapper/blob/main/scripts/Test-Integration.ps1), [README](https://github.com/kieran-a-egan/dbmapper/blob/main/README.md) — baseline verification and release behaviour.

## CLI contract matrix (A01, characterised by `tests/DbMapper.SelfTest/CliContractTests.cs`)

Source of truth: `src/DbMapper/Cli.cs`. Characterisation only; not a product-behaviour approval.

**Help and version:** `--help` / `-h` print the help text and exit 0 with empty stderr; `--version` prints `dbmapper 0.3.0` and exits 0. Either must be the only argument (otherwise exit 2). Neither reads secrets or connects.

**Supported modes** (from help text): scan a project's secret-selected connection (`--connection`, `--secret-key`, `--project`, `--user-secrets-id`); scan one database (`--database`, repeatable); scan all visible databases (`--all-databases`); refresh one database in an existing server bundle (`--update-database`); offline SQL update from a file or a folder's `in.sql` (`--sql`, with `--output` and optionally `--update-database`). `--sql` cannot combine with connection, project, timeout or scan options.

**`--sql` incompatibilities (exit 2):** combined with `--all-databases`, `--database`, `--project`, `--user-secrets-id`, `--connection`, `--secret-key` or `--timeout`.

**Argument-validation refusals (exit 2, application message, no echo of input):** unknown option; raw connection-string argument; option without value, or value starting with `--`, or blank; repeated options other than `--database`; `--timeout` outside 1–300; both `--connection` and `--secret-key`; no selection mode when one is required; duplicate `--database`; store ID that is `.`, `..` or contains a separator; missing project file; directory without a `.csproj`; project without `UserSecretsId` or with a conditional one; output that is an existing file or the current directory.

**Exit-code table:**

| Code | Trigger | Offline coverage |
|---|---|---|
| 0 | Success (help, version, offline SQL update, server or catalog export) | Covered: help, version, offline update. Live export blocked. |
| 1 | Any unexpected exception; generic message with no path or driver text | Covered: invalid UTF-8 SQL file via `--sql` |
| 2 | Application `UsageException` (argument and selection validation) | Covered: large set listed above |
| 3 | `SqlException` mapped to a fixed hint | **Blocked:** needs a live SQL connection |
| 4 | Server export incomplete (some databases lack a schema); `server.md` explains | **Blocked:** reachable only via `ServerReader` with a live connection |
| 130 | Cancellation (`OperationCanceledException`) | Covered offline via `Cli.RunAsync` with a pre-cancelled token before any write (`OfflineSafetyTests`, through `SqlFileTests.RunSql`). Mid-run cancellation during catalog reads: **blocked**, needs a connection. |

Stdout and stderr use the application-authored text only. The planted-secret checks confirm the planted value does not appear through argument refusal, missing-project refusal or the in-memory secret-selection failures.

## Upstream technical references

- [Photino.Blazor package](https://www.nuget.org/packages/Photino.Blazor/) — published builds advertise net8/net9 direct support and net10 computed compatibility; this is **not** evidence of MVP validation.
- Historical versions in the removed Windows x64 spike were Photino.Blazor 4.0.13, Photino.NET 4.0.16, Photino.Native 4.0.22 and Microsoft.AspNetCore.Components.WebView 9.0.1. See [ADR-002](../architecture/decisions/002-desktop-host.md).
- Baseline self-test before this run: 797 checks; after A01–A03 characterisation: 1475 checks (`dotnet run --project tests/DbMapper.SelfTest -c Release --no-build`).
- [Photino.Blazor documentation](https://docs.tryphotino.io/Photino-Blazor) — desktop WebView architecture.
- [Microsoft SqlClient](https://learn.microsoft.com/en-us/sql/connect/ado-net/microsoft-ado-net-sql-server), [SQL Server catalog visibility](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-databases-transact-sql) — follow existing safety assumptions.
- [Microsoft ScriptDOM](https://github.com/microsoft/SqlScriptDOM) — existing offline SQL parser.
- [GitHub Actions .NET workflows](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-net) — cross-platform build support.

**Use policy:** These are discovery pointers, not an assertion that every dependency/platform has passed a compatibility or security test. Pin package versions and record run evidence in implementation work; never invent an observed result.
