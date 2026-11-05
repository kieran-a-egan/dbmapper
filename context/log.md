# Knowledge change log

## 2026-11-05

- Completed A07: the existing CLI and self-tests now target .NET 10, and CI installs only the .NET 10 SDK. Package ID `DbMapper.Tool`, command `dbmapper`, version 0.3.0 and major runtime roll-forward are preserved. The installed tool now requires .NET 10 or a later major runtime allowed by that policy; older application projects remain supported for user-secrets discovery. Shared-core extraction and desktop feasibility remain separate work.
- Updated SqlClient 6.1.7 to 6.1.8 because retargeting enabled transitive auditing that rejected its vulnerable `System.Security.Cryptography.Pkcs` 9.0.11 dependency. The patch resolves that dependency to 9.0.21 without audit suppression. Windows deterministic/golden, packaging, live SQL Server and junction checks passed; Linux/macOS upgrade verification remains pending. See delivery progress for exact evidence and the remaining A03 active-read cancellation gap.

## 2026-11-04

- Removed the disposable desktop host experiment and added CLI build, self-test and installed-package smoke coverage for Windows x64, Linux x64 and Intel/Apple Silicon macOS. Desktop host feasibility remains governed by ADR-002.
- Approved and implemented explicit live CLI opt-in to stored SQL definitions. The default catalog and bundle stay metadata-only. Definition text is excluded from logs, cached metadata and offline history. Offline SQL updates require a metadata-only target.

## 2026-10-10

- Baseline run (00-baseline-and-host): added CLI characterisation tests (A01), byte-exact golden fixtures (A02) and offline safety tests (A03, partial: exit 3/4 and catalog-read cancellation need a live SQL connection). Self-test: 1475 checks. Uncommitted.
- Desktop host spike (A04, disposable, `experiments/desktop-host/`): observed Windows x64 launch with no browser or hosted listener. Partial online egress observation (A05); egress-blocked run approved but not performed. Linux/macOS untested (A06).
- User approved two spike decisions: WebView2 Evergreen as a Windows prerequisite, and the net9-computed Photino.Blazor path on net10. ADR-002 stays provisional; Q1 and Q2 stay open.

## 2026-10-08

- Expanded the delivery plan from 12 high-level milestones into 51 bounded, dependency-ordered Pi Software Factory assignments. Added phase-specific assignment documents, progressive-retrieval index, execution runbook and explicit approval/verification gates. No implementation completion or test success is claimed.

- Created initial OKF v0.2 development context from the approved architecture interview. Defined product scope, GUI workflows, security constraints, CLI compatibility, selective generation, persistence, verification, and delivery units.
- Inspected the existing DbMapper CLI repository and the Pi Software Factory README. Recorded implementation baseline separately from target behaviour.
- Marked desktop-host feasibility, exact Linux support versions, non-SQL network enforcement, crash recovery and performance budgets as unresolved. No implementation validation or user approval of those technical details is claimed.
