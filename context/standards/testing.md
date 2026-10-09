---
type: EngineeringStandard
title: Testing and Release Gates
description: Deterministic verification, golden outputs, integration, platform and privacy checks.
status: stable
tags: [standards, testing, ci]
---

# Testing and release gates

## Local deterministic verification

- Run unit, parser, renderer, preview and CLI regression tests without Docker, SQL Server, secrets or outbound network. Keep these checks suitable as Pi Software Factory baseline and post-change `verificationCommands`.
- Snapshot all existing CLI argument/exit-code paths and representative OKF outputs **before** refactoring to the core. Include single-database, selected, all-database, update-database, SQL-file replay, errors, cancellation and integrity checks.
- Golden tests compare bytes for equal server-wide inputs via CLI and GUI core paths; preserve frontmatter, ordering, producer metadata and no-change idempotency. Explicitly test partial object exports, plain-text references to excluded objects, no broken links, missing object acknowledgement, saved selection isolation and per-database targeted preservation.
- Property/edge tests should include names with punctuation, Unicode, escaped identifiers, schemas with equal object names, duplicate/case-sensitive names, malicious Markdown/paths, malformed SQL, foreign/symlink files, mutated preview destinations and very large synthetic catalogs.

## SQL Server integration

- Provision a disposable local **SQL Server 2022 container** for integration tests; do not require Docker for the ordinary verification gate.
- Cover catalog completeness permissions (`VIEW DEFINITION`, denied access), server enumeration, configured database fallback, SQL Server feature detection, selected scopes, cancellation, timeouts and no data reads/writes. Use synthetic test schema and synthetic secrets only.
- Run integration tests as a separate explicit pipeline/job and during manual acceptance where appropriate. Never connect CI to real production/development customer databases.

## Cross-platform verification

- GitHub Actions builds and tests self-contained `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64` portable distributions. Suggested archive formats: ZIP, TAR.GZ, ZIP `.app`, ZIP `.app` respectively.
- Launch/close native-window smoke tests in CI where runner capabilities permit. Verify bundled assets, directory selection, native WebView startup and no unexpected download.
- Before MVP release, manually test each architecture and Ubuntu 22.04/24.04 plus selected Debian and Fedora versions. Signing/notarization are deferred for internal use; document launch warnings.
- Instrument outbound activity on fresh startup, settings and UI interactions, successful/failed SQL discovery, and offline SQL flow. Release is blocked if the binary or WebView transmits project/schema/secrets/logs or makes unapproved requests. Document any platform/OS ancillary network exceptions before accepting them.

## Resilience and load

- Simulate failure before/after scan, during preview validation, staging, commit and recovery; cancellation must preserve existing output. Test process crashes and backup recovery; do not assert atomicity from a directory swap alone on every filesystem.
- Test 200+ discovered databases and tens of thousands of synthetic objects with virtualized UI, bounded memory and responsive cancellation. Precise latency/memory budgets remain open and must be agreed before enforcing performance gates.

## Pi Software Factory and CI

- Configure `.pi/software-factory.json` for repository-local bounded work and fast deterministic unit/format/regression checks; no Docker requirement in its default preflight. Run `/factory` per bounded [build-plan](../delivery/build-plan.md) unit.
- GitHub Actions can fetch build dependencies during CI. Neither CI services nor Pi agents are linked or bundled into the shipped runtime.
- Do not record green tests, supported OS claims or privacy verification until commands have actually run and evidence exists.

## Related

[Build plan](../delivery/build-plan.md) · [Security](../architecture/security.md) · [Open questions](../delivery/open-questions.md)
