---
type: Architecture
title: System Architecture
description: Runtime structure, module ownership and cross-platform boundaries.
status: stable
tags: [architecture, dotnet, boundaries]
---

# System architecture

## Target shape

```text
DbMapper.slnx
  src/DbMapper.Core/         net10.0 class library; domain and IO services
  src/DbMapper.Cli/          net10.0 console/tool front end; existing command contract
  src/DbMapper.Desktop/      net10.0 desktop host + local Blazor UI
  tests/                     unit, golden, CLI, integration and desktop checks
  context/                   this project's engineering OKF, NOT generated DB output
```

These are proposed project names and responsibilities; the exact refactoring path must preserve existing package ID (`DbMapper.Tool`), command (`dbmapper`) and user workflows. See [ADR-001](decisions/001-shared-core-and-cli.md). Photino.Blazor is a [provisional host](decisions/002-desktop-host.md), not verified as deployable on all targets yet.

## Ownership and communication

| Component | Owns | May call | Must not own |
| --- | --- | --- | --- |
| Core: project/secrets | `.csproj` discovery, literal secrets ID, named secret lookup | Local XML and user-secrets provider | UI, arbitrary project evaluation, appsettings/env fallback |
| Core: discovery/catalog | Fixed SQL Server catalog reads, per-database metadata, permission checks, cancellation | Explicitly selected SQL Server | Object selection widgets, raw query editor |
| Core: selection/renderer | Full metadata model; object filtering; deterministic OKF Markdown and links | In-memory structural schema only | GUI-specific state storage, database I/O |
| Core: offline SQL | ScriptDOM parsing, complete schema simulation, replay/history | Local SQL files and structural metadata | SQL execution or network access |
| Core: bundle storage | Integrity validation, preview, immutable plan, staging, locks, commit/rollback | Allowed filesystem | UI confirmation decisions |
| Desktop application services | Active repo/connection, local preferences/cache, diagnostic lifecycle, orchestration | Core APIs, OS folder picker and guarded WebView | SQL query construction or Markdown generation |
| Desktop Blazor views | Presentation, input, selection, progress, accessibility | Application services only | Direct SQL/file/network operations |
| CLI | Existing option/exit contract, terminal messages | Core operations | Desktop dependencies |

Unidirectional dependency invariant: `Desktop` and `Cli` depend on `Core`; `Core` never depends on either. Do not introduce web-hosted APIs or localhost HTTP services merely to connect the UI to core. All UI assets are embedded/distributed locally.

## Primary data flows

- **Read:** repository path → project XML → user-secrets identity/key → ephemeral connection string → configured SQL Server (`master` discovery if allowed; target catalog connection per database) → in-memory full structural model → selection projection → rendered OKF plan.
- **Write:** rendered plan + destination snapshot → added/modified/deleted preview → explicit confirmation → validation/lock → sibling staging directory → protected atomic replacement/rollback.
- **Offline:** SQL file + existing full local structural metadata/bundle history → ScriptDOM simulation → filtered projection → same preview/commit pipeline; no SQL connection.
- **App settings:** repository/project/connection/endpoint identity → locally persisted selections/cache/logs. Never inject these files into a selected project's repository.

## Runtime and deployment

- Windows x64 primary development and first vertical slice; Linux x64, macOS x64 and macOS ARM64 are equally mandatory MVP distribution targets.
- Portable, self-contained .NET 10 distributions; platform WebView libraries remain OS-specific dependencies. No automatic runtime or WebView downloads.
- GitHub Actions runs cross-platform builds/tests; local Pi Software Factory orchestrates implementation, using deterministic checks. Neither system runs within the delivered application.
- No Git integration inside runtime. Desktop and CLI are mutually exclusive processes ([ADR-004](decisions/004-process-exclusivity.md)); existing bundle-level lock remains a second safety boundary.

## Invariants

- The CLI and GUI must produce identical server-wide output for equal inputs, with a common renderer and writer.
- Object filtering cannot mutate the underlying full catalog model needed for relationship integrity and offline processing.
- Views do not call the SQL client, file writer, user-secrets API, or OS process APIs directly.
- No UI/navigation event implicitly creates network traffic. A confirmed live operation is the only initiator.
- The output bundle's generated `CLAUDE.md` is distinct from any developer-authored `AGENTS.md` and this `context/` bundle.

## Related

[Security](security.md) · [Generation](generation.md) · [Engineering standards](../standards/engineering.md)
