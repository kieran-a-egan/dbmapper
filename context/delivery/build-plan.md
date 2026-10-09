---
type: DeliveryPlan
title: Dependency-Ordered Pi Factory MVP Build Plan
description: Small implementation assignments, dependency edges and cross-platform release gates.
status: draft
tags: [delivery, plan, factory]
---

# Dependency-ordered MVP build plan

This expands the original U0–U11 milestone plan into **51 bounded assignments (A01–A51)** in seven focused files. They are **planned, not completed**. Work one assignment per Pi Software Factory invocation, preserving the clean-tree preflight and deterministic verification. Each spec names objective, dependencies, likely file ownership, acceptance evidence, tests and unresolved decisions. Read [the runbook](factory-runbook.md) before the first run.

## Retrieval and command

1. Read [the assignment index](assignments/index.md); open only the phase file containing the chosen unit.
2. Read `context/index.md` and concept links required by that unit. Follow [open questions](open-questions.md) only when they constrain the unit.
3. Start a bounded factory objective, for example:

```text
/factory Execute assignment A01 from context/delivery/assignments/00-baseline-and-host.md. Read context/index.md first. Only modify the specified scope plus relevant tests/OKF delivery state. Preserve all invariants, verify deterministically and report blockers.
```

4. Inspect factory output, run extra checks if required, review diff, and **commit explicitly yourself** before starting the next clean-tree run. The factory neither commits nor pushes.

## Seven delivery tracks

| Track | Assignments | Original milestones | Entry condition | Exit evidence |
| --- | --- | --- | --- | --- |
| [Baseline capture and desktop-host feasibility](assignments/00-baseline-and-host.md) | A01–A06 | U0, U1 | None | Golden CLI behaviours, host evidence and privacy viability decision |
| [Shared .NET 10 engine and compatible CLI](assignments/01-core-extraction.md) | A07–A13 | U2 | Baseline characterisations captured | No-Docker CLI parity and packaging preserved |
| [Project discovery, selection and generated scope](assignments/02-discovery-and-projection.md) | A14–A21 | U4, U5 | Shared catalog services extracted | Explicit discovery and selected OKF projections correct |
| [Mutual exclusion, previews, cancellation and recovery](assignments/03-safe-write-and-process.md) | A22–A27 | U3 | CLI adapter available | Process gate, immutable preview and recoverable output write |
| [Preferences, structural cache, SQL history and diagnostics](assignments/04-state-and-offline.md) | A28–A34 | U6 | Local connection/selection contracts defined | Isolated preferences, full private metadata baseline, selective SQL updates |
| [Focused .NET desktop workflows](assignments/05-desktop-ui.md) | A35–A45 | U7, U8 | Host feasibility approved and shared APIs ready | Complete accessible Windows desktop workflows |
| [Load, cross-platform verification and portable releases](assignments/06-scale-ci-and-acceptance.md) | A46–A51 | U9, U10, U11 | UI and core functionality integrated | Evidence-backed cross-platform portable release or explicit blockers |

## Dependency rules and gates

- An assignment is eligible only when **all prerequisites** listed in its phase file are complete, the working tree is clean, and no required decision remains open. Assignment IDs are topologically ordered, but independent tasks may be performed in either order once their prerequisites pass. Parallel implementations are allowed only if the factory can prove disjoint file scopes and its existing isolated-worker safeguards; otherwise remain sequential.
- **G-HOST:** A04–A06 demonstrate native window, local resources and privacy on the required platforms. If Photino.Blazor cannot satisfy requirements, **stop for user approval** before changing host technology (Q1/Q2/Q7). Basic code extraction A07–A13 can proceed while host feasibility is investigated, but A35 cannot.
- **G-CLI:** A01–A03 freeze tests before A07. A07–A13 must not change existing args/output/exit semantics except an approved process-exclusivity refusal. Full-selection output must remain byte-identical.
- **G-FILTER:** A19–A21 must establish complete-model projection and coverage with no dangling links. Q9 (inbound references) must be resolved before A20 finishes. The GUI's server-wide format always applies even for one selected database.
- **G-SAFE:** Q6 refusal code/protocol before A23. Q5 crash consistency before A26. Q10 partial visibility/deletion semantics before A27. Never let preview or scan failures write to a bundle.
- **G-STATE:** Q3 endpoint identity/storage contract before A29. Q4 private full-model SQL ledger/recovery before A31–A32. Preserve old CLI offline SQL behaviour.
- **G-RELEASE:** Q2 egress, Q7 distro versions, Q8 measured scale thresholds, actual WebView launch on each target, and manual acceptance are release blockers. Build/publish success alone is insufficient.

## First usable vertical slice

**Foundation:** A01 → A02/A03 → A07–A13 (byte compatibility), while A04–A06 prove the desktop host. **Implementation:** A14–A21 define discovery and selection; A22–A26 enable safe operation; A35–A40 supply a Windows window, project selection, explicit connect, selectable objects and a confirmed preview/write. **Completion:** A27–A34, A41–A45 finish missing-object, cache, offline SQL, settings, and accessibility; A46–A51 gate all releases.

A mock-only demonstration is not evidence of a live SQL Server test. No bundle write from the GUI is permitted before A24–A26 safety passes.

## Pi Software Factory verification discipline

- The project's baseline `verificationCommands` must be fast, deterministic and executable on a clean checkout **without Docker**. Keep a separate SQL Server 2022 container integration command/job.
- Configure the factory to retrieve `AGENTS.md`/`context/index.md` and the chosen assignment file, not the entire context library. `.pi/software-factory.json` is developer configuration, not shipped runtime code.
- Each factory invocation must finish with exact command/test evidence, changed files, unresolved risks and no unrequested Git operations. Do not claim green CI or platform checks without run output.
- Use synthetic SQL schema fixtures and fake secrets in tests and CI; development cloud/model usage must never cause actual target project metadata, secrets or database schemas to leave the user's system.

## Per-assignment definition of done

- All prerequisites and decision gates satisfied; one bounded outcome completed without scope creep.
- Build/tests relevant to the unit actually pass, including negative, cancellation and sensitive-data cases when applicable.
- Existing CLI behaviour remains compatible; byte-exact golden tests and protected bundle constraints hold.
- No hidden runtime network activity, unapproved runtime dependency, Git integration or new config source.
- Affected authoritative OKF concept (if approved knowledge changed), [progress](progress.md), and meaningful [log](../log.md) entries maintained; open questions preserved.

## Deferred

Public signing/notarization, installers, Windows/Linux ARM64, Git integration, simultaneous active repositories and configuration providers beyond .NET User Secrets.
