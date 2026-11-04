---
type: DeliveryStatus
title: Current Delivery State
description: Current phase, completed planning, work in progress, next work and blockers.
status: draft
tags: [delivery, progress]
---

# Current delivery state

**Phase:** Initial specification prepared. Baseline run status: A01–A02 done; A03 partial; A04 observed pass with manual checks pending; A05 partial; A06 untested. Baseline evidence is uncommitted.

**Current goal:** Establish frozen CLI regression baseline and prove the provisional desktop host can satisfy platform/privacy constraints before substantial desktop development.

## Completed

- Requirements interview and decisions recorded in this initial OKF v0.2 bundle.
- Existing DbMapper and Pi Software Factory repositories inspected at a high level; current CLI source layout and documented behaviour identified.
- Expanded original U0–U11 milestone plan into 51 small, dependency-ordered A01–A51 factory assignments across seven phase files with prerequisites, ownership, evidence and decision gates.
- Implemented live CLI `--include-definitions` for views, routines and table/view triggers. Default OKF goldens still match byte for byte. Offline SQL refuses an opted-in target and preserves its files. Verification passed 1,861 checks with the disposable SQL Server fixture and packaged CLI, three Windows junction guards, and independent OKF validation of the definition example and default goldens.

## In progress

- None claimed. Uncommitted working-tree work from the baseline run is listed below; no source refactor was performed.

### Baseline run (00-baseline-and-host.md), uncommitted

- **A01 — done (characterisation tests):** `tests/DbMapper.SelfTest/CliContractTests.cs`. Covers help/version, argument validation, secret-store refusals, exit codes 0/1/2, and the offline success path. A planted fake password never appears in stdout or stderr.
- **A02 — done (golden fixtures):** `tests/DbMapper.SelfTest/BundleGoldenTests.cs` and `tests/fixtures/okf/{single-database,server-wide,selected,targeted-update}/` (86 golden files plus `tests/fixtures/.gitattributes` with `* -text`). A one-byte append fails; normal runs compare only. Goldens regenerate only with `DBMAPPER_UPDATE_GOLDENS=1`, an addition beyond the spec.
- **A03 — partial (offline safety tests):** `tests/DbMapper.SelfTest/OfflineSafetyTests.cs`. Covers script order, parser rejection, foreign and edited files, writer lock and cancelled targeted update. **Blocked, need a live SQL connection:** exit 4 (partial server export), exit 3 (SQL error mapping), cancellation during catalog reads (exit 130), happy-path catalog modes and the real user-secrets read. Symlink/junction refusal is uncovered (needs privilege on Windows). `--integration` not run.
- Self-test: 1475 checks, PASS, two consecutive runs. Build: 0 warnings, 0 errors. `src/` unchanged.
- **A04 — observed pass on Windows x64 (disposable spike, `experiments/desktop-host/`):** Photino.Blazor 4.0.13 on net10 via the net9 lib, single native window, no browser or hosted listener observed. Manual picker and close/reopen checks outstanding. See `experiments/desktop-host/FINDINGS.md`.
- **A05 — partial, online only:** unattributed outbound TCP :443 from the WebView2 tree. No zero-egress claim. Egress-blocked run awaits approval.
- **A06 — untested:** no Linux, macOS, Debian or Fedora run. WSL distros present but stopped.

## Next up

1. Review and commit the uncommitted baseline work (tests, fixtures, `experiments/desktop-host/`, context updates); the runbook forbids the factory from committing, so this is a manual step.
2. Elevated egress-blocked A05 run (approved by user; needs admin shell — see `experiments/desktop-host/FINDINGS.md`).
3. Manual checks: directory picker and close/reopen on the Windows spike (user).
4. Resolve A03 blockers (live SQL needed for exit 3/4, catalog-read cancel and happy-path modes) before A03 is considered complete.
5. Do not start A35 desktop product scaffolding until the host/privacy go/no-go gate is supported by evidence.

## Blocked / risk gates

- Desktop host portability and outbound WebView network isolation unverified.
- Full-model cache/history design for selective offline updates unresolved.
- Crash-safe write/recovery details and endpoint identity persistence require approval.
- Fedora and Debian supported release numbers and measurable large-catalog budgets undecided.

## Open questions

See [open questions](open-questions.md). Important unknowns must remain visible and cannot be silently resolved by a coding agent.

## Related

[Build plan](build-plan.md) · [Assignments](assignments/index.md) · [Factory runbook](factory-runbook.md) · [Index](../index.md)
