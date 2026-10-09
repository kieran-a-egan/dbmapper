---
type: DeliveryAssignment
title: Mutual exclusion, previews, cancellation and recovery
description: Bounded Pi Software Factory assignments covering U3.
status: draft
tags: [delivery, factory, assignments]
---

# Mutual exclusion, previews, cancellation and recovery

**Original milestones:** U3. Every assignment is a proposed unit, not verified implementation. Read [the build plan](../build-plan.md), [factory runbook](../factory-runbook.md), and the cited architecture concepts. Check [open questions](../open-questions.md) before coding.

**Invocation pattern:** `/factory Execute assignment Axx from context/delivery/assignments/03-safe-write-and-process.md. Read context/index.md first. Obey scope, dependencies, acceptance and architecture invariants; run relevant deterministic tests; report blockers rather than inventing decisions.`

## A22 — Specify CLI/Desktop gate contract

- **Prerequisites:** A12.
- **Owned scope (planning guidance):** `context/delivery/open-questions.md`, `context/architecture/decisions/004-process-exclusivity.md`, tests plan only. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Document atomic cross-platform process ownership protocol, all-CLI-invocation policy, startup races and proposed refusal exit code. Request approval before implementing unanswered compatibility detail.
- **Acceptance:** There is an approved refusal-code/lock protocol and reproducible test scenarios; no production change made under a guessed policy.
- **Verification:** Review decision against existing CLI exit mappings and OS capabilities.
- **Decision gate:** Q6; human approval. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A23 — Implement process-wide exclusivity primitive

- **Prerequisites:** A22.
- **Owned scope (planning guidance):** `src/DbMapper.Core/ProcessCoordination/`, CLI startup adapter, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Use OS-local interprocess lock for full Desktop lifetime and each CLI invocation; reject conflicts atomically, clean after crash, never kill another process. Wire CLI entry at earliest safe point.
- **Acceptance:** CLI cannot start while fake Desktop gate held; parallel CLI invocations cannot overlap; crashes release ownership.
- **Verification:** Multi-process tests on Windows and portable abstractions for Linux/macOS.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A24 — Add pure generation change preview

- **Prerequisites:** A11, A21.
- **Owned scope (planning guidance):** `src/DbMapper.Core/Generation/`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Plan immutable Added/Modified/Deleted relative file lists from rendered bytes and existing intact bundle without writing. Make missing object and protected-file warnings representable.
- **Acceptance:** No filesystem changes from preview; identical content produces no modified file.
- **Verification:** Temp-directory fixtures and side-effect assertions.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A25 — Reject changes since preview

- **Prerequisites:** A24.
- **Owned scope (planning guidance):** `src/DbMapper.Core/Generation/`, existing/updated BundleWriter, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Bind confirmed plan to an output snapshot (contents and relevant metadata) and revalidate while holding output lock before install. Detect add/modify/delete including hostile symlinks.
- **Acceptance:** Any mutation after preview causes zero output writes and a stale-preview result.
- **Verification:** Fault-injection tests mutate each file category and cross-process lock races.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A26 — Implement cancellation and crash-safe commit

- **Prerequisites:** A25.
- **Owned scope (planning guidance):** `src/DbMapper.Core/Storage/`, BundleWriter, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Harden staging, rollback and recovery on cancellation, exception or crash, including backups, same-filesystem assumptions and never deleting unverified backups. Do not claim universal atomic rename semantics.
- **Acceptance:** Fault-injected failures leave old logical bundle recoverable; deliberate cancellation leaves no partial published output.
- **Verification:** Crash/failure matrix on supported filesystems; record unverified cases.
- **Decision gate:** Q5. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A27 — Gate missing-object deletion after complete scan

- **Prerequisites:** A17, A24.
- **Owned scope (planning guidance):** `src/DbMapper.Core/Generation/`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Compare selected identities to a successfully completed catalog, record missing ones separately, require explicit deletion acknowledgement. Never treat failed/permission-limited results as proof of absence.
- **Acceptance:** No missing-page removal from scan failures; confirmed removals occur only with explicit approval.
- **Verification:** Unit tests for missing/deleted/denied/offline scenarios.
- **Decision gate:** Q10. Stop before implementing an unresolved choice; a proof/prototype is not approval.

**Next:** Return to [the dependency graph](../build-plan.md) and choose an unblocked assignment.
