---
type: DeliveryAssignment
title: Load, cross-platform verification and portable releases
description: Bounded Pi Software Factory assignments covering U9, U10, U11.
status: draft
tags: [delivery, factory, assignments]
---

# Load, cross-platform verification and portable releases

**Original milestones:** U9, U10, U11. Every assignment is a proposed unit, not verified implementation. Read [the build plan](../build-plan.md), [factory runbook](../factory-runbook.md), and the cited architecture concepts. Check [open questions](../open-questions.md) before coding.

**Invocation pattern:** `/factory Execute assignment Axx from context/delivery/assignments/06-scale-ci-and-acceptance.md. Read context/index.md first. Obey scope, dependencies, acceptance and architecture invariants; run relevant deterministic tests; report blockers rather than inventing decisions.`

## A46 — Create large-catalog performance harness

- **Prerequisites:** A21, A38.
- **Owned scope (planning guidance):** `tests/Performance/` (new), test fixtures, `context/delivery/open-questions.md`. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Generate reproducible 200+ database/tens-of-thousands-object fixtures; measure first-render, filter response, cancellation and memory under Windows. Propose thresholds from evidence for approval.
- **Acceptance:** Benchmarks reproducible and non-sensitive; measured results reported without invented pass budgets.
- **Verification:** Run workload several times and document hardware/method.
- **Decision gate:** Q8. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A47 — Add disposable SQL Server 2022 integration suite

- **Prerequisites:** A17, A21, A32.
- **Owned scope (planning guidance):** `tests/Integration/`, `scripts/`, synthetic SQL fixture data. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Run isolated catalog/permission/fallback/targeted-update/offline cases against a disposable SQL Server 2022 container; keep no-Docker unit gate independent.
- **Acceptance:** Container suite cleans up and never contacts real dev/prod DB; partial permission tests work.
- **Verification:** Run dedicated integration command/job; list host prerequisites.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A48 — Add cross-platform GitHub Actions build matrix

- **Prerequisites:** A06, A13, A35.
- **Owned scope (planning guidance):** `.github/workflows/` (new or updated), packaging scripts, CI docs. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Build/test/publish `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64` with deterministic tests and no real secrets; platform compilation does not count as runtime proof.
- **Acceptance:** All four artifacts created and named clearly; job permissions minimized.
- **Verification:** Inspect run logs/artifacts; no database credentials in artifacts.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A49 — Automate window-startup and egress smoke tests

- **Prerequisites:** A37, A40, A48.
- **Owned scope (planning guidance):** `tests/Desktop.Smoke/`, `.github/workflows/`, scripts. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Add feasible native-window launch/close, local-asset and navigation checks for CI runners; capture unintended network destinations and document unsupported runner cases.
- **Acceptance:** Each target has pass/fail/untested evidence; egress policy violations block acceptance.
- **Verification:** Run on real OS runners with network monitoring; no blanket claims.
- **Decision gate:** Q2, Q7. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A50 — Produce portable self-contained packages and support docs

- **Prerequisites:** A44, A48.
- **Owned scope (planning guidance):** `scripts/`, README and platform release notes, `.github/workflows/`. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Publish ZIP `win-x64`, TAR.GZ `linux-x64`, macOS `.app` ZIP Intel/ARM64. Document OS WebView prerequisites, offline start, Linux distro packages and unsigned macOS launch warnings.
- **Acceptance:** Extract-and-run without separate .NET, installer or runtime downloads, subject to documented OS components.
- **Verification:** Smoke-test unpacking and launching on each platform.
- **Decision gate:** Q1, Q7. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A51 — Run MVP manual acceptance and record evidence

- **Prerequisites:** A45, A46, A47, A49, A50.
- **Owned scope (planning guidance):** `context/delivery/progress.md`, `context/delivery/open-questions.md`, release acceptance report (new). Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Execute cross-platform manual tests for all agreed OS families/architectures, inspect egress, output parity, failure recovery, process exclusivity, UI accessibility and SQL permissions. Do not change architecture to make failing checks pass silently.
- **Acceptance:** MVP is accepted only with recorded evidence for all release gates; otherwise status stays blocked/draft.
- **Verification:** Link actual CI run outputs, commands and manual checks without fabricating results.
- **Decision gate:** Q1, Q2, Q5, Q7, Q8 remaining blockers. Stop before implementing an unresolved choice; a proof/prototype is not approval.

**Next:** Return to [the dependency graph](../build-plan.md) and choose an unblocked assignment.
