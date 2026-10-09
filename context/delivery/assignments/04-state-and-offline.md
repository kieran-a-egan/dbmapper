---
type: DeliveryAssignment
title: Preferences, structural cache, SQL history and diagnostics
description: Bounded Pi Software Factory assignments covering U6.
status: draft
tags: [delivery, factory, assignments]
---

# Preferences, structural cache, SQL history and diagnostics

**Original milestones:** U6. Every assignment is a proposed unit, not verified implementation. Read [the build plan](../build-plan.md), [factory runbook](../factory-runbook.md), and the cited architecture concepts. Check [open questions](../open-questions.md) before coding.

**Invocation pattern:** `/factory Execute assignment Axx from context/delivery/assignments/04-state-and-offline.md. Read context/index.md first. Obey scope, dependencies, acceptance and architecture invariants; run relevant deterministic tests; report blockers rather than inventing decisions.`

## A28 — Resolve endpoint identity and cache format

- **Prerequisites:** A15, A18.
- **Owned scope (planning guidance):** `context/delivery/open-questions.md`, `context/architecture/data-and-state.md`, prototype/test notes. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Evaluate how to separate repo/project/secret/server identity without storing secret material; decide canonicalization, collision and local corruption behaviour. Seek approval before implementing.
- **Acceptance:** Approved identity key and versioned storage contract; endpoint re-point from Development to Production is isolated.
- **Verification:** Threat-model and synthetic endpoint-change examples.
- **Decision gate:** Q3; human approval. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A29 — Persist repository/connection selections

- **Prerequisites:** A28.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/DbMapper.Desktop.csproj` (compilation-only project skeleton), `src/DbMapper.Desktop/Services/`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Introduce the compilation-only Desktop project skeleton without selecting a UI host, then implement local app-data settings for recent repositories, project/key selections and object identities under approved endpoint key. Atomic writes; one active repository; no raw secrets.
- **Acceptance:** Changing endpoint starts unchecked; old settings remain; settings not inside repository.
- **Verification:** Temporary isolated app-data-root fixtures and secret-canary assertions.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A30 — Cache complete schema for read-only offline browsing

- **Prerequisites:** A17, A29.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/Services/`, storage DTOs, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Persist metadata-only full catalog snapshot separately from rendered bundle, with verified-scan provenance and cache clearance by project/all. Offline mode exposes read-only objects and unverified badge.
- **Acceptance:** No database rows, expressions or credentials cached; no live generation allowed from cache.
- **Verification:** Cache round-trip/version/corruption tests and offline no-network tests.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A31 — Design private full-model SQL history and recovery

- **Prerequisites:** A10, A20, A30.
- **Owned scope (planning guidance):** `src/DbMapper.Core/OfflineSql/`, local-store adapter, `context/delivery/open-questions.md`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Establish complete structural baseline and script-replay ledger independent of filtered Markdown pages. Handle cache deletion, stale history, script editing and full-server bundle compatibilities without silent data loss.
- **Acceptance:** Approved versioned recovery behavior; a filtered bundle alone cannot be mistaken for full schema.
- **Verification:** Replay tests for edited-last-script and invalid earlier script against private baseline.
- **Decision gate:** Q4. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A32 — Implement selective offline SQL updates

- **Prerequisites:** A31.
- **Owned scope (planning guidance):** `src/DbMapper.Core/OfflineSql/`, rendering orchestration, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Apply supported SQL locally to full model; report changes to unselected objects while emitting only previously selected pages. Preserve CLI SQL semantics and output on old full scopes.
- **Acceptance:** New Invoices remains unselected; selected Orders updates; unselected Customers changes retained internally.
- **Verification:** Golden SQL replay and selected-scope file comparisons, zero SQL connection.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A33 — Implement local redacted rotating diagnostics

- **Prerequisites:** A29.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/Services/`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Add structured event categories/results/durations with sanitizer-first mapping, 30-day and 50-MB joint cap, manual Clear Logs. No names, server addresses, raw SQL or stack dumps.
- **Acceptance:** Secret/object-name canaries never appear in files; eviction occurs at age/size limits.
- **Verification:** Fake clock/scratch store rotation tests, no network calls.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A34 — Reconcile cache and bundle commit state

- **Prerequisites:** A26, A30, A31.
- **Owned scope (planning guidance):** local-cache adapter, core commit coordinator, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Define success boundary for metadata snapshot/settings/history versus output commit, recover from crash or cache loss without silently trusting stale data.
- **Acceptance:** A crash between output commit and cache update yields safe, explicit recovery, never incorrect automatic regeneration.
- **Verification:** Fault injection at each write boundary and synthetic cache corruption.
- **Decision gate:** Q5, Q4. Stop before implementing an unresolved choice; a proof/prototype is not approval.

**Next:** Return to [the dependency graph](../build-plan.md) and choose an unblocked assignment.
