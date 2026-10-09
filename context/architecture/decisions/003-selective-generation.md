---
type: ArchitectureDecision
title: ADR-003 — Selective, Deterministic OKF Projection
description: Explicit object selection with complete internal schema state and protected outputs.
status: stable
tags: [architecture, decision, generation]
---

# ADR-003: Selective, Deterministic OKF Projection

**Decision.** Render only explicitly selected tables, views, stored procedures and functions from a complete catalog model. Keep metadata for unselected objects in local structural state when required to resolve relationships and offline DDL changes. For references to unselected tables, render escaped identifiers instead of broken links. GUI live exports always use the existing server-wide OKF v0.2 hierarchy, even for one database.

**Context.** CLI supports database-level selection and offline SQL history, but not object-level selection. Existing SQL update code relies on recovering the model from generated pages, which is insufficient for a selective output.

**Rationale.** User-controlled scope with dependency accuracy, reproducibility and safe offline updates; minimizes accidental disclosure of unselected object pages.

**Alternatives considered.** Auto-include dependencies (contradicts explicit scope); render all catalog objects regardless of checkboxes (contradicts selective export); reconstruct full state from filtered Markdown (lossy); separate new Markdown format (breaks parity).

**Consequences.** Selection projection, full-state cache/history design, index/link generation and reverse-relationship handling require new tests. Exact byte parity between GUI and CLI for identical full server-wide selections is mandatory. Full GUI regeneration replaces intact generated selected scope; targeted refresh preserves all other databases. Missing selected objects after a successful scan require explicit deletion confirmation.

**Constraints.** An incomplete selected database scan blocks GUI writes. Before every preview, re-scan selected databases. Require an Added/Modified/Deleted preview, explicit approval and destination snapshot recheck. Refuse conversion of an existing single-database CLI bundle. Existing CLI behaviour is not changed by GUI filtering.

**Related.** [Generation](../generation.md) · [Data and state](../data-and-state.md) · [Testing](../../standards/testing.md)
