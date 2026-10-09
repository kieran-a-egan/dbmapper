---
type: ArchitectureDecision
title: ADR-002 — Cross-platform Desktop Host (Provisional)
description: Desktop window strategy, trade-offs and mandatory viability checks.
status: draft
tags: [architecture, decision, desktop, photino]
---

# ADR-002: Cross-platform Desktop Host (Provisional)

**Decision.** Target .NET 10 and use **Photino.Blazor provisionally** to render bundled local Blazor/CSS content inside a standalone OS desktop window; do not open a default browser. No hosted Blazor server, CDN or network UI. Do not finalize dependencies before proving the host can satisfy privacy, startup and distribution requirements on every supported platform.

**Context.** User explicitly rejected Windows-only WPF, Avalonia and browser-launched interfaces. They accept OS WebView embedding provided runtime data never leaves the system except explicit SQL Server connections.

**Rationale.** Keeps the UI in C#/Blazor, retains a separate native window, and avoids writing four bespoke OS native UIs. A third-party UI library is permitted; third-party services are not.

**Alternatives considered.** WPF (Windows-only); .NET MAUI (no official Linux desktop support); Uno Platform (separate native/XAML UI model); browser-hosted Blazor (fails separate-window intent); per-platform native wrappers (high complexity). If current Photino.Blazor cannot meet the requirements, return for approval of a different host; **do not silently adopt a replacement**. A newer PhotinoX library is a research candidate, not an approved substitution.

**Consequences.** Platform WebView prerequisites may exist even in self-contained .NET distributions. Need window lifecycle, single-instance coordination, folder picker, CSP, outbound navigation/resource blocking, system theme detection and cross-platform packaging/smoke tests.

**Mandatory feasibility gate.** Test Windows x64, Linux x64 (Ubuntu 22.04, 24.04, chosen Debian/Fedora releases), macOS x64 and ARM64. Prove a self-contained portable app opens a window, renders bundled UI, can select a directory, does not start HTTP listeners or make unrelated network requests, and can close/reopen normally. Verify licensing/version/.NET 10 dependencies and WebView availability without runtime downloads. Official package compatibility metadata is **not** sufficient proof.

**Constraints.** First functional vertical slice is on Windows x64; no release before the full matrix passes. Signing/notarization and installers are deferred; document platform security warnings and manual workarounds where permitted.

**Related.** [Security](../security.md) · [Open questions](../../delivery/open-questions.md) · [Build plan](../../delivery/build-plan.md)
