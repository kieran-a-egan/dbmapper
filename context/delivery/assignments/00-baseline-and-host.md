---
type: DeliveryAssignment
title: Baseline capture and desktop-host feasibility
description: Bounded Pi Software Factory assignments covering U0, U1.
status: draft
tags: [delivery, factory, assignments]
---

# Baseline capture and desktop-host feasibility

**Original milestones:** U0, U1. Every assignment is a proposed unit, not verified implementation. Read [the build plan](../build-plan.md), [factory runbook](../factory-runbook.md), and the cited architecture concepts. Check [open questions](../open-questions.md) before coding.

**Invocation pattern:** `/factory Execute assignment Axx from context/delivery/assignments/00-baseline-and-host.md. Read context/index.md first. Obey scope, dependencies, acceptance and architecture invariants; run relevant deterministic tests; report blockers rather than inventing decisions.`

## A01 — Freeze CLI behaviour matrix

- **Prerequisites:** None.
- **Owned scope (planning guidance):** `tests/DbMapper.SelfTest/`, `context/references/source-baseline.md`. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Inventory current help/version, argument validation, secrets failure cases, exit codes and supported modes. Add targeted characterisation tests without production-code changes.
- **Acceptance:** A runnable, documented CLI contract test suite covers both success and sanitizer/error paths.
- **Verification:** `dotnet run --project tests/DbMapper.SelfTest -c Release`
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A02 — Capture byte-exact OKF fixtures

- **Prerequisites:** A01.
- **Owned scope (planning guidance):** `tests/DbMapper.SelfTest/`, `tests/fixtures/` (new). Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Create checked-in deterministic expected file trees for existing single-database, selected/server-wide and targeted-update renders using synthetic schemas. Capture CRLF/LF behavior and integrity footer expectations.
- **Acceptance:** Golden fixtures are deterministic, do not contain environment-dependent paths/credentials, and fail on a byte change.
- **Verification:** Run self-tests twice and compare fixture hashes; no Docker.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A03 — Freeze offline SQL and writer safety cases

- **Prerequisites:** A01.
- **Owned scope (planning guidance):** `tests/DbMapper.SelfTest/`, `tests/fixtures/`. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Add characterisation tests for SQL replay, script order, parser rejection, partial server output exit code 4, modified/foreign files, cancellation and bundle locking. No refactoring.
- **Acceptance:** Existing semantics are visible in tests, including failure preserving pre-existing output.
- **Verification:** Run existing self-tests with fresh scratch directories; assert no SQL connection needed.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A04 — Prove Windows native host viability

- **Prerequisites:** None.
- **Owned scope (planning guidance):** `experiments/desktop-host/` (new), `context/delivery/open-questions.md`. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Build disposable .NET 10 Photino.Blazor Windows x64 host with bundled local UI, single native window, close/reopen and directory picker. Record package/license, required OS dependencies and exact build/run commands; never merge prototype into product project.
- **Acceptance:** Windows native window launches without opening a browser, hosted HTTP listener or runtime downloads; otherwise record a blocker.
- **Verification:** Build/publish and manually launch Windows x64 spike; attach reproducible observations.
- **Decision gate:** Q1. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A05 — Probe WebView network isolation

- **Prerequisites:** A04.
- **Owned scope (planning guidance):** `experiments/desktop-host/`, `context/delivery/open-questions.md`. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Exercise startup/navigation/assets/folder selection; inspect host interception capabilities, CSP, DNS and outbound requests. Implement a local-only fail-closed proof where possible; never use real secrets.
- **Acceptance:** Document actual observed requests, interception gaps, OS-specific exceptions and prevention evidence; no untested "zero egress" claim.
- **Verification:** Repeat in monitored network environment both online and with egress blocked.
- **Decision gate:** Q2. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A06 — Validate remaining host targets

- **Prerequisites:** A04, A05.
- **Owned scope (planning guidance):** `experiments/desktop-host/`, `context/delivery/open-questions.md`. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Publish and launch the minimal host on Linux x64, macOS x64/ARM64 and named Linux families; test required WebView packages and local directory picker. Report untested targets as blocked.
- **Acceptance:** For each intended platform, record pass/fail/untested and prerequisites; obtain approval if Photino cannot satisfy the constraints.
- **Verification:** Run real platform smoke checks; cross-compiling alone is not a pass.
- **Decision gate:** Q1, Q2, Q7. Stop before implementing an unresolved choice; a proof/prototype is not approval.

**Next:** Return to [the dependency graph](../build-plan.md) and choose an unblocked assignment.
