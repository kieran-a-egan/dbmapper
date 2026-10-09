---
type: ArchitectureDecision
title: ADR-001 — Shared .NET 10 Core and Preserved CLI
description: One repository, shared implementation, two user interfaces and compatibility constraints.
status: stable
tags: [architecture, decision, cli, dotnet]
---

# ADR-001: Shared .NET 10 Core and Preserved CLI

**Decision.** Extend `kieran-a-egan/dbmapper` as one repository. Extract existing catalog, secrets, SQL parser, renderer and protected writer code into `DbMapper.Core` targeting .NET 10. Keep `DbMapper.Cli` as the installable .NET tool and add `DbMapper.Desktop` as a second front end. The desktop UI calls shared core APIs directly, not a CLI process. Pi Software Factory remains a separate development tool.

**Context.** The current `.NET 8` executable already separates major functions (`ProjectSecrets`, `ServerReader`, `CatalogReader`, `SqlFileUpdate`, `OkfBundle`, `BundleWriter`). The GUI adds discovery phases and per-object selection but must not change established CLI exports.

**Rationale.** Single ownership of security-sensitive SQL and file logic; one testable renderer and transactional writer; atomic refactoring and regression review; no cross-repository version skew.

**Alternatives considered.** Separate GUI repository/library package (creates coordination overhead); GUI calling CLI subprocess (weak API, poor cancellation/progress, complicates mutual exclusion); duplicate engines (high drift and security risk).

**Consequences.** Engine APIs require refactoring; CLI tests and golden outputs must be captured before movement. Desktop dependencies cannot leak into CLI/core. SDK/runtime advances to .NET 10; packaging and version strategy must preserve the existing `dbmapper` command and `DbMapper.Tool` package identity.

**Constraints.** Existing CLI options, permitted modes and exit codes remain compatible, with one approved exception: refuse CLI invocation while Desktop is open and refuse Desktop startup while CLI is active. No existing CLI mode silently adopts GUI object filtering. Preserve independent CLI distribution.

**Related.** [System architecture](../system.md) · [ADR-004](004-process-exclusivity.md) · [Tests](../../standards/testing.md)
