---
type: Architecture
title: Local State and Metadata
description: Local persistence, cache provenance and selection identity boundaries.
status: draft
tags: [architecture, data, cache, privacy]
---

# Local state and metadata

## Authoritative vs derived state

1. **SQL Server** is the source of truth for live catalog metadata; the current permission-limited snapshot is not a guarantee of globally complete server visibility.
2. **Full metadata model** retains supported structural information about scanned objects, including unselected objects needed for relationships and offline updates. It must never contain row contents, executable SQL text or credentials.
3. **Selections** are a separate set of explicit identifiers per database and object category. Changing a filter or viewing metadata never changes selections implicitly.
4. **Projection/output** includes only selected object pages and references; it does not become the sole source for reconstructing a full metadata model.
5. **Cache** is a local, unverified copy after disconnection; it may support browsing, never substitute for the required fresh scan before live generation.

## Isolation and identity

Preferences and metadata cache belong under the operating system's per-user application-data location, **outside** the target repository and generated `database-context` directory. Keep contexts distinct by canonical repository identity, selected `.csproj`, resolved secret-key name and actual SQL endpoint identity. A changed server/instance or other endpoint identity results in a new context with no selections, preserving the old context for later use.

Never persist a raw connection string, password, access token or secrets ID value unnecessarily. The exact stable endpoint fingerprinting algorithm, identity sensitivity, canonicalization of repo symlinks and atomic storage format are [open questions](../delivery/open-questions.md). Until resolved, no implementation may key selections only by a connection-string key name. A locally keyed fingerprint of a canonical, noncredential endpoint is a candidate—not an approved design.

## Lifecycle

- On startup/repository switch, load prior preferences and cached structures *locally*; never connect automatically.
- First-time context: databases and objects unselected. After a successful discovery, restore prior selections by exact identity; missing objects remain in saved preferences for review, not silently discarded.
- Cache stores a verification indicator/time from successful scans when available. It may not claim recent verification while offline.
- A connection failure makes cached Explorer state read-only; absence of a current object in a failed/incomplete scan is not deletion evidence.
- Settings supports clearing per-project or all schema caches and local diagnostics. Full cache retention continues until explicitly cleared.
- Local structured redacted logs retain at most 30 days **and** 50 MB total across rotated files. Never include object names, server addresses, raw errors, SQL source, secrets or schema values in logs.

## Offline SQL complication

The current CLI's SQL reader reconstructs schema from rendered OKF pages. A selectively generated bundle cannot encode all unselected schema state. The GUI must therefore track a complete structural baseline separately, apply supported SQL changes to it, then project only selected pages; replay/edit-history semantics must remain correct. The design must explicitly validate history/cache migration, stale baselines, data corruption and output/cache consistency before implementation. **Do not treat a partial generated bundle as a complete offline baseline.**

## Related

[Workflows](../product/workflows.md) · [Generation](generation.md) · [Open questions](../delivery/open-questions.md)
