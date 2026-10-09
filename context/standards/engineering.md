---
type: EngineeringStandard
title: .NET Engineering Conventions
description: Code organization, interfaces, errors, logging, dependencies, formatting and configuration.
status: draft
tags: [standards, dotnet, csharp]
---

# .NET engineering conventions

## Code organization and naming

- Target `net10.0` unless a platform-specific target is proven necessary. Keep `DbMapper.Core` UI-free and `DbMapper.Cli` desktop-dependency-free. Namespaces track `DbMapper.Core.*`, `DbMapper.Cli.*`, `DbMapper.Desktop.*`; preserve public tool/package identity.
- Use idiomatic C#: `PascalCase` types, public members and methods; `camelCase` locals/parameters; `_camelCase` private instance fields; `I`-prefixed interfaces where an abstraction is justified. Avoid layers/interfaces that have no concrete ownership or testing value.
- Prefer focused types by responsibility: `IProjectDiscovery`, `IConnectionResolver`, `IServerCatalog`, `IExportPlanner`, `IBundleStore` as *candidate boundaries*, not a mandate for gratuitous interfaces. Host adapters remain outside core.
- All I/O operations are async/cancellable where supported. Thread `CancellationToken` through database calls, metadata discovery, planning and staged filesystem writing. No fire-and-forget work or UI-thread blocking.
- Keep command/query creation, selection and renderer as separate responsibilities. Database names go through validated `SqlConnectionStringBuilder`, never SQL interpolation.

## Contracts, validation and errors

- Input paths are canonicalized and scoped. Reject invalid `.csproj` files, unsafe output targets, symlinks/junctions as appropriate, unsupported bundle shapes and nonmatching database/object selections before writing.
- Model expected failures with typed domain/result categories and sanitized messages; do not leak `SqlException.Message`, connection arguments or raw exception strings. Do not catch-and-ignore errors. UI shows actionable error categories, but logs remain stricter.
- CLI maps shared result categories to its existing exit codes and text contracts; add any exclusivity-refusal mapping only after an explicit decision. UI maintains its own progress/state machine and never depends on CLI output text.
- Report indeterminate progress honestly; do not fabricate object counts until known. Centralize progress stages and cancellation semantics.

## Formatting and quality

- Use `dotnet format` / .editorconfig for stable C# formatting, nullable reference types and compiler analysis. Treat warning policy as a project-level decision documented before enabling blanket warnings-as-errors.
- Unit-test all public domain behaviour; new branching/security paths require failure and cancellation tests. Prefer deterministic fixtures over external state, and no sleeping/time-based tests without necessity.
- Keep dependencies pinned or centrally managed; every new runtime package requires justification, license/security review and portability validation. No CDN/runtime downloads. A change to the WebView host requires revisiting ADR-002.

## Configuration and secrets

- Runtime configuration is local settings plus the selected project's `.NET User Secrets`; do not broaden connection resolution to environment, `appsettings` or arbitrary repositories. Developer-only `.pi/software-factory.json` and GitHub Actions configuration are not shipped as runtime integrations.
- Never write secrets to the output, cache, logs, crash dump attachments or test snapshots. Test with synthetic secrets and assert full redaction.
- Local logging uses structured event identifiers, sanitizer-first error mappings, 30-day/50-MB rotation, and Clear Logs. No raw stack traces in persistent diagnostics when they may contain paths or identifiers.

## Still to define

Exact `.editorconfig`, automated analyzer set, application settings/cache serialization format, retry/timeout UI affordances and process-lock refusal exit code remain draft until implementation units specify them. Respect the [open questions](../delivery/open-questions.md); never fill gaps by assumption.

## Related

[Architecture](../architecture/system.md) · [Security](../architecture/security.md) · [AI workflow](ai-workflow.md)
