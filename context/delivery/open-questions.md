---
type: OpenQuestionRegister
title: Open Technical Questions
description: Unresolved choices and verification gates that constrain implementation.
status: draft
tags: [delivery, questions, risk]
---

# Open technical questions

These are **not** implicit authorizations. Affected implementation units must stop for clarification or produce a bounded proof before choosing a final design. All items below are open.

## Q1 — Desktop host and .NET 10 native compatibility

**Why it matters:** Photino.Blazor's package declares .NET 10 as *computed compatible*, but full host-runtime/packaging behaviour and supported Linux WebView library versions are not proven. **Options:** validate existing Photino.Blazor; evaluate alternative host such as PhotinoX.Blazor *only with explicit approval*; revise framework choice. **Dependent:** U1/U7/U10. **Status:** open, release blocker.

**Evidence (A04, disposable spike, `experiments/desktop-host/FINDINGS.md`):** Photino.Blazor 4.0.13 has no net10 group; a net10 spike consumes the net9 lib with no restore warning and launched a single native window on Windows 11 x64 (WebView2 Evergreen 154 present). Requires `[STAThread]`. User decision (recorded): WebView2 Evergreen accepted as a Windows prerequisite, and the net9-computed Photino.Blazor path accepted for the spike. Still open: WebView2 absence behaviour, directory picker and close/reopen not manually verified, Linux/macOS untested. Q1 stays open because of the untested items above (Linux/macOS, WebView2 absence, manual checks); the approval does not close it.

## Q2 — Enforced network isolation

**Why:** WebView, TLS chain verification, DNS and network-integrated SQL authentication can make OS-level ancillary requests. The rule permits only configured SQL Server traffic; extra egress requires explicit product approval or a technical prevention strategy. **Options:** blocking host APIs/CSP, OS process firewall containment, supported database-auth exceptions. **Dependent:** security architecture, U1/U9/U11. **Status:** open, release blocker.

**Evidence (A05, partial, online only):** outbound TCP :443 observed while online: `52.98.145.66` owned by WebView2 browser process 7632 (which sits in the host's process tree); `150.171.27.11` owned by PID 27424, which was not shown to be in the host tree. Owner attribution per destination is only as strong as stated here. App page has no external references. Control probe not time-matched. No zero-egress claim is made. Egress-blocked run approved by the user but not yet performed (requires an elevated shell to create the firewall rule).

## Q3 — Private metadata store and connection identity

**Why:** Restoring by project/secret name is unsafe if the secret changes to Production; full schema cache must not expose credentials or collide across endpoints. **Options:** keyed hash of normalized noncredential endpoint with local app key; encrypted local metadata store; other noncredential identity scheme. Also decide physical cache format, corruption recovery, same-host/database identity behaviour, and repo symlink canonicalization. **Dependent:** U6. **Status:** open; do not implement guessed identity.

## Q4 — Offline SQL history with filtered output

**Why:** Current CLI reconstructs full schema from generated pages; GUI filtered pages omit required objects. **Options:** versioned private full-model baseline plus history ledger; projection-aware replay on complete cache; explicit failure when no sound baseline exists. Define recovery when cache is deleted but filtered bundle remains, and whether a CLI update to GUI-filtered bundle is compatible or must refuse safely. **Dependent:** U5/U6. **Status:** open, correctness gate.

## Q5 — Crash-consistent output and cache state

**Why:** Existing writer supports staged replacement/rollback but abrupt termination can leave backup/staging folders; app cache and bundle may disagree. **Options:** recovery journal; transactional filesystem manifest; fail-safe detection requiring human review. Need realistic cross-filesystem/OS atomicity and handling of rename failure. **Dependent:** U3/U6/U9. **Status:** open, release blocker.

## Q6 — Mutual-exclusion refusal code and invocation scope

**Why:** User requires all CLI calls blocked while Desktop runs, but old exit codes should retain meaning where feasible. **Options:** reuse existing generic failure code with fixed sanitized message; dedicate a new explicit refusal code (compatibility change). Define special-case `--help`/`--version` behaviour if any (user said no CLI during Desktop, so default is block all). **Dependent:** U3. **Status:** open; no silent new exit code.

## Q7 — Exact supported Debian/Fedora versions and WebView prerequisites

**Why:** Distributor/runtime support depends on distro-specific WebKitGTK and native packages. **Options:** choose specific supported stable releases compatible with chosen host, or narrow Linux matrix with explicit user agreement. **Dependent:** U1/U10/U11. **Status:** open; Ubuntu 22.04/24.04 are confirmed.

**Evidence (A06):** untested. This machine has only stopped WSL distros (Ubuntu, docker-desktop); no Linux, macOS, Debian or Fedora smoke run performed.

## Q8 — Measurable large-catalog budgets

**Why:** “Large” means roughly 200+ DBs and tens of thousands of objects; responsiveness requires measurable thresholds. **Options:** agree upper-bound synthetic datasets, time-to-first-discovery, UI input latency, memory ceilings and cancellation deadlines after profiling. **Dependent:** U9/U11. **Status:** open; do not fabricate target numbers.

## Q9 — Metadata semantics for filtered inbound references

**Why:** Selected objects may have inbound FK references from unselected objects. Preserve explicit outbound references, but exact inbound reference summary/content could expose names of unselected objects or create misleading counts. **Options:** include plain-text inbound identifier summary; omit inbound references not originating from selected objects with explicit coverage notes. **Dependent:** U5. **Status:** open; outbound FK behavior is already approved.

## Q10 — Partial catalog visibility vs successful scan

**Why:** Database-wide metadata permission can be verified, but SQL Server can still hide individual objects due to object-level rights. **Options:** document verified-scan limitations and treat ambiguous absences conservatively; add warnings/explicit administrator permission check where feasible. **Dependent:** U4/U9 and missing-object deletion confirmation. **Status:** open, security/correctness gate.
