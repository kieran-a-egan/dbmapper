---
type: DeliveryAssignment
title: Focused .NET desktop workflows
description: Bounded Pi Software Factory assignments covering U7, U8.
status: draft
tags: [delivery, factory, assignments]
---

# Focused .NET desktop workflows

**Original milestones:** U7, U8. Every assignment is a proposed unit, not verified implementation. Read [the build plan](../build-plan.md), [factory runbook](../factory-runbook.md), and the cited architecture concepts. Check [open questions](../open-questions.md) before coding.

**Invocation pattern:** `/factory Execute assignment Axx from context/delivery/assignments/05-desktop-ui.md. Read context/index.md first. Obey scope, dependencies, acceptance and architecture invariants; run relevant deterministic tests; report blockers rather than inventing decisions.`

## A35 — Create desktop shell and sidebar

- **Prerequisites:** A06, A23.
- **Owned scope (planning guidance):** existing `src/DbMapper.Desktop/` skeleton (created in A29), host/bootstrap, local assets, UI smoke tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** After host go/no-go approval, add the verified host and standalone window to the existing Desktop skeleton, then scaffold five sections Projects/Database Explorer/Generate/Offline SQL/Settings with local static assets only. No database work yet.
- **Acceptance:** Window starts locally, navigation works and no HTTP listener or remote content is required.
- **Verification:** Windows x64 launch smoke; inspect network activity.
- **Decision gate:** Q1/Q2 feasibility must pass. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A36 — Build Projects picker and connection UI

- **Prerequisites:** A14, A15, A29, A35.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/Pages/Projects*`, components/services, UI tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Add recent repositories, OS folder picker, recursive project list, secrets ID override and names-only connection selector; no network side effects.
- **Acceptance:** Switching repos restores saved context and does not connect; key values never shown.
- **Verification:** Component interaction tests and canary secret screenshots/log checks.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A37 — Wire explicit Connect & Discover with progress

- **Prerequisites:** A16, A17, A36.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/Pages/Projects*`, UI orchestration, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Only button action initiates server discovery. Display permission/unsupported fallback, cancellable progress and sanitized error state.
- **Acceptance:** No network before click; configured database fallback is visible and never implicit.
- **Verification:** Stubbed catalog UI tests and local SQL container smoke when available.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A38 — Build virtualized two-panel Explorer

- **Prerequisites:** A18, A30, A37.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/Pages/Explorer*`, components, UI tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Left database/type tree, right searchable checkboxes, counts, filtered select/deselect, resizable divider, virtualization, offline-readonly indication.
- **Acceptance:** 10k+ object rows remain interactable; filter operations do not affect hidden selections.
- **Verification:** Component tests + synthetic large list; no live DB in unit gate.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A39 — Add read-only object metadata sheet

- **Prerequisites:** A38.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/Components/ObjectDetails*`, UI tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Click object name to open temporary sheet for allowed structural metadata; checkbox remains independent; prohibit row/definition access.
- **Acceptance:** Closing sheet preserves selections; no extra SQL query triggered by opening it.
- **Verification:** UI interaction tests for tables, views, procedures, functions and keyboard use.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A40 — Wire fresh scan, preview and confirm UI

- **Prerequisites:** A24, A25, A26, A38.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/Pages/Generate*`, orchestration, UI tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Implement output chooser/default, fresh selected-database scan, added/modified/deleted preview, explicit Generate, stale-output refusal and Cancel with stage reporting.
- **Acceptance:** Nothing writes before confirmation; any selected scan failure blocks; cancellation preserves existing output.
- **Verification:** End-to-end Windows synthetic test with fault-injected writer.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A41 — Add targeted refresh and missing-object confirmation

- **Prerequisites:** A27, A40.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/Pages/Generate*`, UI tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Allow one database refresh without changing others, show missing selected objects separately with explicit deletion acknowledgement, retain missing selections in settings.
- **Acceptance:** Other database subtrees identical, new objects unchecked, incomplete scans never authorize deletion.
- **Verification:** UI state tests and actual output fixture diff.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A42 — Build independent Offline SQL page

- **Prerequisites:** A32, A35.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/Pages/OfflineSql*`, UI tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Choose SQL file/ticket and bundle/database without a project or server, preview changes and confirm; show effects on unselected objects.
- **Acceptance:** No connection requested; old CLI SQL behavior unaffected; cancellation safe.
- **Verification:** UI integration tests with local SQL fixtures.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A43 — Build Settings and theme controls

- **Prerequisites:** A30, A33, A35.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/Pages/Settings*`, assets/theme services, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Implement System/Light/Dark persistent theme and OS changes, cache inspect/clear by project/all and Clear Logs; no update/telemetry settings.
- **Acceptance:** Themes switch immediately; settings persist locally; clear actions do not damage generated bundle.
- **Verification:** Component/theme tests under both OS themes; local settings fixtures.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A44 — Finish closing, mutex UX and recovery messaging

- **Prerequisites:** A23, A40, A43.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/` host/window lifecycle, UI tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Second launch focuses existing instance if supported, CLI ownership blocks Desktop start, active work asks Keep Running or Cancel & Close and waits for safe cleanup.
- **Acceptance:** No two active app instances; no forced termination; crash lock recoverability.
- **Verification:** Multi-process window/startup and cancellation tests.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A45 — Accessibility and empty/error-state pass

- **Prerequisites:** A38, A39, A40, A41, A42, A43.
- **Owned scope (planning guidance):** `src/DbMapper.Desktop/` UI components and tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Complete keyboard navigation, focus, screen-reader labels, dark/light contrast, progress announcements, empty/offline/permission/error/preview-stale views.
- **Acceptance:** All primary actions usable without mouse; critical status not conveyed only by color.
- **Verification:** Automated accessibility checks where supported plus manual keyboard pass.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

**Next:** Return to [the dependency graph](../build-plan.md) and choose an unblocked assignment.
