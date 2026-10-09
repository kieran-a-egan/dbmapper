---
type: Architecture
title: Security and Privacy Boundaries
description: Secret handling, database access, filesystem integrity and network isolation.
status: draft
tags: [architecture, security, privacy]
---

# Security and privacy boundaries

## Approved outbound communication

Only user-initiated connections to the configured SQL Server endpoint, using credentials/TLS from the selected project's .NET User Secrets, are permitted during normal application runtime. No analytics, diagnostic upload, remote UI assets, CDN references, external hyperlinks, automatic update calls, embedded web server, remote fonts, cloud model calls or other outbound network destinations. This restricts **the distributed app**, not developer-only GitHub Actions/Pi Software Factory used to build it.

**Verification gate:** Block external navigation, remote resource requests and downloads in the WebView, apply a restrictive Content Security Policy and package all assets locally. Instrument/test outbound traffic under Windows, Linux and macOS, including initial WebView startup. Current host APIs and OS-initiated traffic have not yet been verified; see [open questions](../delivery/open-questions.md). Do not claim blanket egress prevention based solely on configuration flags. DNS, certificate validation, authentication federation and OS WebView activity must be identified explicitly if required by a configured database connection.

## Connection and catalog access

- Load only .NET user secrets via the existing provider; never read `appsettings`, environment overrides or raw CLI connection arguments. A `.csproj` is parsed as XML without compiling, starting, executing targets or evaluating imports; inherited/conditional secrets IDs require explicit override.
- Never persist/display connection values; connection selection UI exposes key **names only**. Memory secrets should have minimum feasible lifetime. Never place strings in exceptions or diagnostics.
- Preserve existing `SqlConnectionStringBuilder` policy: explicitly required server/database for normal scans; `master` for supported enumeration, no attach-file/user-instance, no weakened TLS, no SQL string interpolation for database names, pooling/enlist/retry disabled as in baseline.
- `VIEW DEFINITION` database-wide check is mandatory; use only fixed, reviewed metadata-only `sys` catalog queries. No data rows, arbitrary SQL, SQL expressions/default literals, routine bodies, DDL/DML, procedure execution, elevated permission requests or inferred cross-database relationships.
- Treat identifiers as potentially sensitive and untrusted: escape Markdown/YAML/paths and guard filesystem traversals. Object names legitimately enter OKF output only after user selection and confirmation, never logs.

## Filesystem and process integrity

- Validate every generated file through existing integrity footers before replacing, and refuse foreign/modified content, symbolic links/junctions or invalid output paths.
- Preview only from an immutable, validated plan and destination snapshot. Before committing, verify snapshot is unchanged; stage in sibling temp tree and use recoverable replacement. Cancellation, exceptions and rejected destinations preserve previous output.
- Keep cross-process bundle locks even with the global Desktop/CLI exclusion (defence in depth); lock races cannot permit concurrent modification. Never delete a leftover backup without verifying which version is authoritative.
- Offline SQL is interpreted locally and never executed. Treat SQL files and captured database identifiers as untrusted input, with bounded parsing/resource usage and sanitized errors.
- Logs are strictly local, pre-redacted structured events; include stage/result codes and durations rather than sensitive names or raw stack/driver messages. Retain at most 30 days and 50 MB, with Clear Logs.

## Invariants

1. Selecting a repo/project/connection never triggers network I/O.
2. Remote database connectivity is allowed only when explicitly configured and initiated; OS-specific ancillary network behaviours are release-blocking until understood.
3. No UI code can emit arbitrary SQL or open remote URLs.
4. The app never requests permission escalation or disables SQL/TLS safeguards for convenience.
5. No credentials or database structures in CI artifacts, runtime logs or crash reports.

## Related

[System](system.md) · [State](data-and-state.md) · [Verification](../standards/testing.md)
