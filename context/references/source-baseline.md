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

## Upstream technical references

- [Photino.Blazor package](https://www.nuget.org/packages/Photino.Blazor/) — published builds advertise net8/net9 direct support and net10 computed compatibility; this is **not** evidence of MVP validation.
- [Photino.Blazor documentation](https://docs.tryphotino.io/Photino-Blazor) — desktop WebView architecture.
- [Microsoft SqlClient](https://learn.microsoft.com/en-us/sql/connect/ado-net/microsoft-ado-net-sql-server), [SQL Server catalog visibility](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-databases-transact-sql) — follow existing safety assumptions.
- [Microsoft ScriptDOM](https://github.com/microsoft/SqlScriptDOM) — existing offline SQL parser.
- [GitHub Actions .NET workflows](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-net) — cross-platform build support.

**Use policy:** These are discovery pointers, not an assertion that every dependency/platform has passed a compatibility or security test. Pin package versions and record run evidence in implementation work; never invent an observed result.
