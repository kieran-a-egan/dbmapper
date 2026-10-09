---
type: DeliveryAssignment
title: Shared .NET 10 engine and compatible CLI
description: Bounded Pi Software Factory assignments covering U2.
status: draft
tags: [delivery, factory, assignments]
---

# Shared .NET 10 engine and compatible CLI

**Original milestones:** U2. Every assignment is a proposed unit, not verified implementation. Read [the build plan](../build-plan.md), [factory runbook](../factory-runbook.md), and the cited architecture concepts. Check [open questions](../open-questions.md) before coding.

**Invocation pattern:** `/factory Execute assignment Axx from context/delivery/assignments/01-core-extraction.md. Read context/index.md first. Obey scope, dependencies, acceptance and architecture invariants; run relevant deterministic tests; report blockers rather than inventing decisions.`

## A07 — Move baseline solution to .NET 10

- **Prerequisites:** A01, A02, A03.
- **Owned scope (planning guidance):** `DbMapper.slnx`, `Directory.Build.props`, existing `src/DbMapper/DbMapper.csproj`, `tests/DbMapper.SelfTest/`. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Update target framework/tooling only, preserving current command and package contract. Keep all existing sources in place for this unit.
- **Acceptance:** The existing CLI builds and all baseline/golden tests still pass on .NET 10.
- **Verification:** `dotnet restore DbMapper.slnx`; `dotnet build DbMapper.slnx -c Release`; run self-tests.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A08 — Extract model and OKF renderer

- **Prerequisites:** A07.
- **Owned scope (planning guidance):** `src/DbMapper.Core/` (new), existing `SchemaModel.cs`, `OkfBundle.cs`, `ServerBundle.cs`, relevant `.csproj`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Create a UI-free class library and move schema model/rendering code behind minimal callable APIs; temporary forwarding wrappers only if necessary to keep CLI green.
- **Acceptance:** CLI compiles and byte-exact output fixtures remain identical; no desktop dependencies enter core.
- **Verification:** Build; golden tests; assembly/project dependency check.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A09 — Extract SQL catalog and secrets access

- **Prerequisites:** A08.
- **Owned scope (planning guidance):** `src/DbMapper.Core/`, existing `CatalogReader.cs`, `ServerReader.cs`, `Catalog.sql`, `ProjectSecrets.cs`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Relocate existing fixed catalog queries and XML/User Secrets resolution to core. Preserve permission checks, TLS/connection policy and errors.
- **Acceptance:** No SQL query/security change, no new config providers, old CLI tests green.
- **Verification:** Build; unit tests and current synthetic connection-string/secret tests.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A10 — Extract offline SQL parser and history

- **Prerequisites:** A08.
- **Owned scope (planning guidance):** `src/DbMapper.Core/`, existing `SqlScript.cs`, `SqlFileUpdate.cs`, `SqlModelReader.cs`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Move parser, model reader, SQL history and replay into core without changing current CLI offline behavior. Defer selective-output changes to A31–A32.
- **Acceptance:** Existing scripts/replay/unsupported construct tests continue unchanged.
- **Verification:** Run baseline SQL-file fixtures and golden-output comparison.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A11 — Extract protected bundle writer

- **Prerequisites:** A08.
- **Owned scope (planning guidance):** `src/DbMapper.Core/`, existing `BundleWriter.cs`, tests. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Move writer and existing per-output locking/integrity/backup code into core behind a narrow interface. Preserve existing CLI snapshot semantics for now.
- **Acceptance:** No edited/foreign files overwritten; success/failure baseline tests remain green.
- **Verification:** Writer unit tests including cancellation and intact output comparison.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A12 — Complete CLI adapter and packaging split

- **Prerequisites:** A09, A10, A11.
- **Owned scope (planning guidance):** `src/DbMapper.Cli/` (new), `src/DbMapper/` (legacy cleanup), `DbMapper.slnx`, `.csproj`, tests, README as needed. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Finish separating option handling and console/exit mapping into CLI. Preserve installed command `dbmapper`, package ID `DbMapper.Tool` and existing modes. Remove obsolete duplication only after parity.
- **Acceptance:** The CLI uses core and has no desktop dependency; command/package identity and output parity preserved.
- **Verification:** Build, self-tests, `dotnet pack` and smoke-run packed tool.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

## A13 — Harden post-extraction regression gate

- **Prerequisites:** A12.
- **Owned scope (planning guidance):** `tests/DbMapper.SelfTest/`, `tests/fixtures/`, optional `.pi/software-factory.json` only if configured. Update only directly relevant tests and approved OKF delivery state; do not rewrite unrelated code.
- **Factory objective:** Make golden, syntax/exit and writer tests callable as fast, no-Docker deterministic checks for Pi factory preflight. Record baseline commands without implying tests ran on other platforms.
- **Acceptance:** Default factory verification remains Docker-free; regression failures gate later units.
- **Verification:** Run configured commands with clean tree; verify a deliberate golden change fails.
- **Decision gate:** None. Stop before implementing an unresolved choice; a proof/prototype is not approval.

**Next:** Return to [the dependency graph](../build-plan.md) and choose an unblocked assignment.
