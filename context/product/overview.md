---
type: Product
title: DbMapper Desktop MVP
description: Purpose, users, scope and acceptance criteria for the local cross-platform DbMapper application.
status: stable
tags: [product, mvp, dbmapper]
---

# DbMapper Desktop MVP

## Purpose and user

Turn the existing SQL Server-to-OKF `.NET` CLI into a self-contained, offline-first desktop utility for a developer operating on existing repositories. The desktop app lives outside the selected repository, discovers its `.csproj` projects and user-secrets connection configuration, lets the user choose precise database objects, and writes the same class of OKF v0.2 documentation as DbMapper. Retain the independent CLI and all existing capabilities.

The initial audience is personal/internal developer use. Distribution to the general public, signing, installers, and update services are deferred.

## MVP scope

- .NET 10 shared engine, backwards-compatible CLI, and a desktop app with its **own** cross-platform window (no default-browser UI).
- Windows x64, Linux x64, macOS x64 and macOS ARM64; portable self-contained builds. Linux test families: Ubuntu 22.04/24.04, Debian and Fedora; exact Debian/Fedora versions are [unresolved](../delivery/open-questions.md).
- Choose one active repository; recursively locate `.csproj` files and select an unconditionally declared `UserSecretsId` or provide the existing explicit override. Use .NET User Secrets only; names of connections may be displayed, values may not.
- Explicit connect, server/database discovery (with configured-database fallback), object-level checkboxes for tables, views, procedures and functions, metadata-only previews.
- All object categories start unchecked for a new context. Filters, type grouping, Select All / Deselect All affect **visible matching objects only**; no dependency auto-selection.
- Separate Offline SQL workflow, without requiring a repository or database connection, preserving GUI selections while maintaining full internal structural state.
- Always emit the **existing server-wide** OKF layout for GUI live exports; default destination `<repository>/database-context`, user-overridable. Never auto-convert a pre-existing single-database CLI bundle.
- Local per-repository/project/connection/endpoint selections, schema cache, offline read-only browsing, Settings for theme, cache clearing and diagnostic log clearing.
- Fresh pre-generation scan, change preview, confirmation, safe replacement, targeted database refresh, warning and confirmation for missing objects, cancellation and progress.
- System/Light/Dark themes; minimalist developer utility, sidebar navigation and two-panel explorer with temporary details sheet.

## Out of scope

- Executing DDL, DML, migration scripts, stored routines, or inspecting application table rows.
- Non-.NET project configuration sources (`appsettings.json`, `.env`, environment overrides, embedded/raw connection strings).
- Git staging, diffs from Git, commits, pushes, hooks or branch manipulation in the application.
- Multiple simultaneous active repositories or parallel app instances.
- External service integration, hosted processing, telemetry, CDN assets and automatic software updates.
- Native installers, Windows/Linux ARM64, public release signing and macOS notarization in MVP.
- General-purpose database browser, arbitrary SQL editor or ER modelling.

## Success and release criteria

1. Both front ends complete all pre-existing DbMapper behaviours without duplicating their domain implementation; CLI arguments and existing exit codes keep their semantics, except the approved process-exclusivity refusal.
2. Identical GUI and CLI **server-wide** scopes produce byte-identical OKF outputs. Object-filtered output contains no excluded pages or broken links; repeat runs with unchanged input produce no diff.
3. A selected subset generates only requested object pages; foreign-key references to excluded targets remain as non-links. Fresh full exports remove obsolete generated pages only after explicit approval. Targeted GUI updates affect only the selected database.
4. Security and cancellation constraints hold in automated tests. The distributed app attempts no network communication other than permitted SQL Server connections; actual OS/WebView egress must be tested, not assumed.
5. CI produces all four portable targets; automated smoke tests where possible and manual acceptance on each supported target, including designated Linux distributions, gate MVP readiness.
6. Large catalogs (200+ databases; tens of thousands of objects) remain responsive through lazy scans, list virtualization and bounded memory/concurrency. Exact measurable budgets are a draft question, not invented thresholds.

## Related

[Workflows](workflows.md) · [System architecture](../architecture/system.md) · [Delivery plan](../delivery/build-plan.md)
