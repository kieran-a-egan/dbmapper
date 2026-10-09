---
type: DeliveryAssignment
title: Project discovery, selection and generated scope
description: Bounded Pi Software Factory assignments covering U4, U5.
status: draft
tags: [delivery, factory, assignments]
---

# Project discovery, selection and generated scope

**Original milestones:** U4, U5. Every assignment is a proposed unit, not verified implementation. Read [the build plan](../build-plan.md), [factory runbook](../factory-runbook.md), and the cited architecture concepts. Check [open questions](../open-questions.md) before coding.

**Invocation pattern:** `/factory Execute assignment Axx from context/delivery/assignments/02-discovery-and-projection.md. Read context/index.md first. Obey scope, dependencies, acceptance and architecture invariants; run relevant deterministic tests; report blockers rather than inventing decisions.`

## A14 — Add recursive project discovery service

- **Prerequisites:** A09.
- **Owned scope (planning guidance):** `src/DbMapper.Core/ProjectDiscovery/`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Enumerate candidate `.csproj` files under a user-selected root without evaluating MSBuild, running app code or network calls; report malformed/conditional secrets IDs without exposing values.
- **Acceptance:** Multi-project repo lists valid candidates; inherited/conditional ID can be explicitly overridden; no connection attempt.
- **Verification:** Use local synthetic repo trees and malformed XML tests.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A15 — Add safe named-connection selection API

- **Prerequisites:** A14.
- **Owned scope (planning guidance):** `src/DbMapper.Core/ConnectionResolution/`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Expose connection-key names and sole-entry selection; support explicit arbitrary secret key, return ephemeral secret only at connection boundary. Do not add appsettings/env lookup.
- **Acceptance:** No API/UI-facing DTO, diagnostic event or snapshot contains a secret value; ambiguity is explicit.
- **Verification:** Synthetic User Secrets provider tests with canary secrets.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A16 — Implement explicit server discovery and fallback

- **Prerequisites:** A09, A15.
- **Owned scope (planning guidance):** `src/DbMapper.Core/ServerDiscovery/`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Expose an explicit, cancellable discovery operation over existing ServerReader. Report unsupported enumeration/permission errors and attempt only configured database fallback when named and accessible.
- **Acceptance:** No network work during project/connection selection. Fallback never guesses, escalates permissions or lists invisible databases.
- **Verification:** Inject fake connection/catalog ports; SQL container verification deferred to A47.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A17 — Add per-database catalog loading and progress

- **Prerequisites:** A16.
- **Owned scope (planning guidance):** `src/DbMapper.Core/Catalog/`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Represent database scan status and load supported catalog metadata only after database selection; expose phase/count progress and CancellationToken. Retain sequential/controlled connection policy.
- **Acceptance:** A failed selected database blocks GUI-plan readiness; missing permission cannot yield a false complete scan.
- **Verification:** Fake SQL reader success, denied metadata, timeout and cancel tests.
- **Decision gate:** Q10 for deletion confidence. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A18 — Implement explicit selection/filter domain

- **Prerequisites:** A17.
- **Owned scope (planning guidance):** `src/DbMapper.Core/Selections/`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Model database/schema/type/object identities, selection restoration, filtered Select All/Deselect All, and new-object default unselected. Keep selection independent of UI list virtualization.
- **Acceptance:** Selecting `Order*` affects only filtered matches; same name in different schemas remains distinct.
- **Verification:** Unit tests with 10k synthetic identifiers; no UI or network.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A19 — Project selected table/view pages and outbound FK

- **Prerequisites:** A08, A18.
- **Owned scope (planning guidance):** `src/DbMapper.Core/Rendering/`, relevant `OkfBundle`/`ServerBundle` implementation, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Render only chosen tables/views from full schema, retaining plain-text escaped outbound FK targets if their pages are excluded. No broken links; preserve full-scope bytes.
- **Acceptance:** Single Orders page with omitted Customers still contains FK identity; all-selected output equals CLI golden.
- **Verification:** Golden fixture tests for explicit selections, Unicode and missing link targets.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A20 — Project routines, indexes and inbound relation coverage

- **Prerequisites:** A19.
- **Owned scope (planning guidance):** `src/DbMapper.Core/Rendering/`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Add individual stored procedure/function projection; ensure indexes/counts/schema pages/status correctly reflect published scope. Investigate inbound FK references from excluded objects before choosing disclosure semantics.
- **Acceptance:** All object categories supported; no dead links, no misleading coverage; inbound relation behaviour approved before final implementation.
- **Verification:** Snapshot tests for each object kind and inbound/outbound relations.
- **Decision gate:** Q9. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A21 — Implement full and targeted server-scope plans

- **Prerequisites:** A12, A20.
- **Owned scope (planning guidance):** `src/DbMapper.Core/Generation/`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Plan full server hierarchy (even one GUI database) versus targeted one-database replacement; retain CLI single-database and server-wide modes. Refuse GUI conversion of incompatible existing single-database bundles.
- **Acceptance:** Full scope removes stale intact generated pages; targeted scope leaves all other database subtrees byte-identical.
- **Verification:** Golden parity and targeted-update unit tests, including incompatible bundle refusal.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

**Next:** Return to [the dependency graph](../build-plan.md) and choose an unblocked assignment.
