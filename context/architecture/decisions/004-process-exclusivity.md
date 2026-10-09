---
type: ArchitectureDecision
title: ADR-004 — Desktop and CLI Process Exclusivity
description: Cross-platform mutual exclusion and intentional CLI compatibility exception.
status: stable
tags: [architecture, decision, concurrency]
---

# ADR-004: Desktop and CLI Process Exclusivity

**Decision.** One Desktop instance may be running. While Desktop is open, all CLI invocations are refused; while any CLI operation is active, Desktop startup is refused. Neither may terminate the other. The desktop process owns the shared exclusivity token for its entire lifetime; CLI owns it for its invocation. Preserve independent per-bundle write locks in core.

**Context.** The user explicitly prefers app-wide exclusivity, beyond preventing two operations writing the same output. This intentionally modifies CLI runtime availability.

**Rationale.** Eliminates concurrent UI/CLI use and accidental mixed-state operations without a daemon or network endpoint. Per-output locks remain defence in depth and are needed for file safety independently of app startup policy.

**Alternatives considered.** Allow parallel CLI runs with destination locks only (rejected); auto-kill conflicting process (unsafe); use an HTTP/local IPC coordination server (unnecessary networking).

**Consequences.** Launchers need consistent refusal messages, tests across Windows/Linux/macOS and robust cleanup after crashes. A second Desktop launch should activate the existing window if the platform allows it without creating another instance; otherwise notify and exit without competing work. Startup races must be handled atomically.

**Constraints.** Local OS inter-process coordination only. A lock must not persist permanently following a crash. A desktop close during active work requests cancellation and waits for safe cleanup; operating-system termination still requires recoverable bundle semantics. Maintain existing CLI argument and exit-code meanings where possible; the precise refusal exit code is [unresolved](../../delivery/open-questions.md), and must be explicitly set before implementation.

**Related.** [System](../system.md) · [Security](../security.md) · [Engineering](../../standards/engineering.md)
