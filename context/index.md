---
okf_version: "0.2"
---

# DbMapper Desktop — project context

This is the authoritative entry point for AI coding agents working on the existing `kieran-a-egan/dbmapper` repository. Read only the concepts relevant to the requested bounded task. `stable` means the decision is approved for this project, **not** that the implementation is complete or verified.

## Product

- [Product scope](product/overview.md) — users, purpose, non-goals, MVP acceptance criteria.
- [Workflows](product/workflows.md) — project selection, discovery, generation, offline SQL, failures and cancellation.

## Architecture

- [System architecture](architecture/system.md) — .NET 10 solution, ownership and communication boundaries.
- [Data and local state](architecture/data-and-state.md) — per-project selections, endpoint identity, metadata cache and diagnostics.
- [Generation and consistency](architecture/generation.md) — selection semantics, parity, previews and transactional writes.
- [Security boundary](architecture/security.md) — permitted network access, secrets, metadata restrictions and WebView isolation.

### Significant decisions

- [ADR-001: shared core and CLI](architecture/decisions/001-shared-core-and-cli.md)
- [ADR-002: desktop host](architecture/decisions/002-desktop-host.md) — provisional; requires validation.
- [ADR-003: selective OKF generation](architecture/decisions/003-selective-generation.md)
- [ADR-004: mutual exclusion](architecture/decisions/004-process-exclusivity.md)

## Engineering and UI

- [Engineering standards](standards/engineering.md) — naming, folders, validation, errors, logging, configuration and dependencies.
- [Verification standards](standards/testing.md) — unit, golden, database and platform tests.
- [Instructions for coding agents](standards/ai-workflow.md) — mandatory workflow and completion checklist.
- [Visual design](ui/design-system.md) — minimal developer utility, tokens, accessibility and themes.
- [UI interactions](ui/interactions.md) — navigation, explorer, preview, progress and states.

## Delivery and references

- [Build plan](delivery/build-plan.md) — dependency graph for 51 bounded implementation assignments.
- [Assignment index](delivery/assignments/index.md) — load only the relevant phase file for the next unit.
- [Pi Factory runbook](delivery/factory-runbook.md) — invocation, clean-tree and verification procedure.
- [Delivery progress](delivery/progress.md) — current phase and implementation state.
- [Open questions](delivery/open-questions.md) — unresolved decisions and validation gates; do not guess.
- [Source baseline](references/source-baseline.md) — current repository and upstream references.
- [Knowledge change log](log.md) — durable context changes, not a task journal.

## Non-negotiable invariants

1. A database connection occurs only after explicit **Connect & Discover** (or a separately confirmed live regeneration); repository selection itself must not cause network traffic.
2. The installed desktop application has no telemetry, external content, analytics, automatic updates or non-SQL-server egress. Configured SQL Server connections are the sole approved application network activity, subject to the unresolved ancillary-network question.
3. GUI and CLI use the same core SQL catalog reader, SQL parser, OKF rendering and protected bundle writer. The GUI never shells out to the CLI.
4. The GUI never writes before a current scan, a change preview, validation and explicit user confirmation. Failed scans, cancellation, changed destinations or rejected files leave the prior bundle intact.
5. No application-data rows, SQL definitions, expressions, credentials or raw driver failures are exported or logged.
6. The desktop application and CLI are mutually exclusive at process level. This is the expressly approved exception to the CLI's normal compatibility contract.
7. The generated OKF bundle and the development-project `context/` bundle are **different artifacts**. Never mix their ownership, locations or schemas.
