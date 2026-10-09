---
type: DeliveryProcess
title: Pi Software Factory Execution Runbook
description: One-unit-at-a-time prompts, clean-tree constraints and verification evidence.
status: draft
tags: [delivery, factory, workflow]
---

# Pi Software Factory execution runbook

This is the **development workflow** for the external [Pi Software Factory](https://github.com/kieran-a-egan/pi-software-factory), not an installed DbMapper feature. Each assignment is one objective for `/factory`; the factory's own scout/architect/implementer/reviewer and deterministic gates remain authoritative.

## Before the first run

1. Copy this updated `context/` and `AGENTS.md` into the existing `dbmapper` checkout, review/commit the context change manually, and start from a clean working tree.
2. Make sure Pi Software Factory is installed/configured separately and `.pi/software-factory.json` has a sensible no-Docker baseline verification command for the **current** solution state. Do not invent the existing command path after refactoring; update it when A12/A13 changes project names.
3. Read [`context/index.md`](../index.md), [the build plan](build-plan.md), and the one relevant assignment file. Check approved decisions/open questions. Do not automatically load all seven phase files.
4. Use fake secrets and synthetic schema fixtures. CI and external development agents must not ingest real project secrets, live database metadata or actual database-generated customer OKF bundles.

## Prompt pattern

```text
/factory Execute assignment Axx from context/delivery/assignments/<phase>.md. Read context/index.md first. Follow the stated prerequisites, ownership scope, acceptance tests, architecture invariants and decision gates. Make no unrelated edits. Do not perform Git stage/commit/push. Run deterministic verification and report the exact results and blockers.
```

Replace `Axx` and `<phase>` with the actual ID and filename. For example:

```text
/factory Execute assignment A01 from context/delivery/assignments/00-baseline-and-host.md. Read context/index.md first. Freeze CLI behaviour through tests only, with no product code modifications. Run existing no-Docker self-tests; report exact results and uncovered scenarios.
```

## After a run

- Review changed files and the factory's verification/reviewer reports. Require evidence that every acceptance assertion is met; an AI review does not supersede failing deterministic tests.
- Manually run the most relevant extra integration or platform checks when required. Clean up disposable tests and inspect for unintended secret/data/remote access.
- Fix or reject the bounded change. Commit only after your own review; the factory does not commit/push or rewrite Git history. Do **not** start the next run on a dirty tree if the factory requires clean preflight.
- Update [progress](progress.md) after completion and [log](../log.md) only for durable approved knowledge changes. Preserve failed/blocked units as incomplete.

## Decision stops

| Before | Resolve | Owner |
| --- | --- | --- |
| A23 | Q6 — mutual-exclusion refusal code/protocol | User approval after technical investigation |
| A20 | Q9 — inbound references from unselected objects | User approval |
| A26 | Q5 — crash consistency/recovery design | User approval after fault-injection investigation |
| A27 | Q10 — incomplete metadata visibility/deletion confidence | User approval |
| A29 | Q3 — endpoint identity and cache format | User approval |
| A31 | Q4 — full-model SQL history and missing-cache recovery | User approval |
| A35 | Q1/Q2 — host compatibility and WebView egress feasibility | User approval if deviations required |
| A50/A51 | Q7/Q8 — distro versions and measured performance budgets | User approval |

A design/prototype assignment may collect evidence for a decision; it may **not** mark that decision stable without approval.

## Suggested baseline checks

For the existing checkout, the documented no-Docker check is:

```powershell
dotnet restore DbMapper.slnx
dotnet build DbMapper.slnx -c Release --no-restore
dotnet run --project tests/DbMapper.SelfTest -c Release --no-build
```

These commands are **suggestions based on the inspected baseline, not execution evidence**. Adjust verified paths if an approved refactor changes them. Keep the SQL Server 2022 container suite and UI native launch tests as separately triggered checks. A CI step that fetches dependencies is developer/build traffic; it must not introduce any outbound traffic into the distributed application.

## Related

[Assignment index](assignments/index.md) · [Testing standards](../standards/testing.md) · [AI workflow](../standards/ai-workflow.md)
