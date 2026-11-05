---
type: DeliveryStatus
title: Current Delivery State
description: Current phase, completed planning, work in progress, next work and blockers.
status: draft
tags: [delivery, progress]
---

# Current delivery state

**Phase:** Initial specification and .NET 10 CLI baseline in place. The disposable desktop host was removed. Desktop feasibility is open.

**Current goal:** Establish frozen CLI regression baseline and prove the provisional desktop host can satisfy platform/privacy constraints before substantial desktop development.

## Completed

- Requirements interview and decisions recorded in this initial OKF v0.2 bundle.
- Existing DbMapper and Pi Software Factory repositories inspected at a high level; current CLI source layout and documented behaviour identified.
- Expanded original U0–U11 milestone plan into 51 small, dependency-ordered A01–A51 factory assignments across seven phase files with prerequisites, ownership, evidence and decision gates.
- Implemented live CLI `--include-definitions` for views, routines and table/view triggers. Default OKF goldens still match byte for byte. Offline SQL refuses an opted-in target and preserves its files. Verification passed 1,861 checks with the disposable SQL Server fixture and packaged CLI, three Windows junction guards, and independent OKF validation of the definition example and default goldens.
- Added a CLI workflow for Windows x64, Ubuntu x64 and both macOS architectures. Windows and Ubuntu x64 (WSL) passed the deterministic suite and an installed-package offline export. The macOS jobs await their first run.
- **A07 — done (2026-11-05):** Retargeted the existing CLI and self-tests to .NET 10, kept package/command/version and major roll-forward, and changed the CLI workflow to install only the .NET 10 SDK. SqlClient 6.1.8 resolves the transitive cryptography audit failure exposed by retargeting. No core extraction or desktop implementation is included.

### A07 verification (2026-11-05)

- Windows x64, SDK 10.0.400: the .NET 8 baseline passed 1,491 deterministic checks and format verification before edits. The .NET 10 restore/build passed with zero warnings/errors; the same 1,491 checks and format verification passed. Goldens were compared without regeneration.
- Packed and freshly installed `DbMapper.Tool` 0.3.0; help, version and an offline SQL export passed. Package inspection verified `tools/net10.0/any/DbMapper.runtimeconfig.json`, framework version `10.0.0` and `Major` roll-forward. Independent OKF validation passed for the seven-file smoke bundle and all 86 golden files. Transitive dependency audit reported no vulnerable packages from the configured sources.
- `scripts/Test-Integration.ps1 -ToolPath <fresh packaged tool>` passed 1,861 checks against a disposable SQL Server 2022 container, including real user-secrets reads from the retained `net8.0` application fixture, live catalog modes, sanitized authentication failures, partial exports and packaged-tool execution. Three Windows junction guards passed and preserved their target content.
- Linux and macOS have not been rerun for this upgrade; the updated four-platform CLI workflow must provide that evidence. The .NET 10 tool requires .NET 10 or a later major runtime permitted by its existing roll-forward policy.

## In progress

- None claimed.

### Baseline run (00-baseline-and-host.md)

- **A01 — done (characterisation tests):** `tests/DbMapper.SelfTest/CliContractTests.cs`. Covers help/version, argument validation, secret-store refusals, exit codes 0/1/2, and the offline success path. A planted fake password never appears in stdout or stderr.
- **A02 — done (golden fixtures):** `tests/DbMapper.SelfTest/BundleGoldenTests.cs` and `tests/fixtures/okf/{single-database,server-wide,selected,targeted-update}/` (86 golden files plus `tests/fixtures/.gitattributes` with `* -text`). A one-byte append fails; normal runs compare only. Goldens regenerate only with `DBMAPPER_UPDATE_GOLDENS=1`, an addition beyond the spec.
- **A03 — partial (safety tests):** `tests/DbMapper.SelfTest/OfflineSafetyTests.cs` covers script order, parser rejection, foreign and edited files, writer lock and cancelled targeted update. The 2026-11-05 integration run verifies exit 4 (partial server export), exit 3 (authentication failure), happy-path catalog modes, real user-secrets reads and Windows junction refusals. Existing cancellation tests use pre-cancelled tokens; cancellation during an active catalog read remains unverified, so A03 is not marked complete.
- Historical A01–A03 run: 1475 checks, PASS, two consecutive runs; build: 0 warnings, 0 errors; `src/` unchanged at that stage. See A07 verification above for current results.
- **A04–A06 — desktop feasibility open:** the removed spike provided partial Windows launch and online egress observations. Desktop testing on Linux and macOS remains outstanding. See [Q1, Q2 and Q7](open-questions.md) for the remaining checks.

## Next up

1. Collect .NET 10 CLI workflow results for Linux x64 and both macOS architectures.
2. Resolve remaining A03 coverage gaps before marking A03 complete.
3. Resolve the desktop host and privacy feasibility gate before starting A35 desktop product scaffolding.

## Blocked / risk gates

- Desktop host portability and outbound WebView network isolation unverified.
- Full-model cache/history design for selective offline updates unresolved.
- Crash-safe write/recovery details and endpoint identity persistence require approval.
- Fedora and Debian supported release numbers and measurable large-catalog budgets undecided.

## Open questions

See [open questions](open-questions.md). Important unknowns must remain visible and cannot be silently resolved by a coding agent.

## Related

[Build plan](build-plan.md) · [Assignments](assignments/index.md) · [Factory runbook](factory-runbook.md) · [Index](../index.md)
