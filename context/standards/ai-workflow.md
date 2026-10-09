---
type: AgentInstruction
title: AI Coding Workflow
description: Direct instructions for agents using Pi Software Factory or other coding tools.
status: stable
tags: [standards, agents, pi-factory]
---

# Instructions to AI coding agents

## Mandatory retrieval

1. **Read `context/index.md` first.** Load only concepts relevant to your assigned bounded unit, following links only where additional detail is needed. Do not ingest the entire context bundle by default.
2. Check applicable statuses: `stable` is approved knowledge; `draft` cannot justify guessing. Check [open questions](../delivery/open-questions.md) and stop/ask for approval when ambiguity affects safety, output compatibility or implementation choices.
3. Inspect current code and tests before modifying them. The source repo implementation is authoritative for *current behaviour*; OKF describes the *approved target*. If the two conflict, do not silently rewrite behaviour—identify the migration and add regression tests.

## Implementation discipline

4. Work from one bounded [build-plan](../delivery/build-plan.md) unit with objective, prerequisites and named file scope. No speculative refactoring, features or unrelated edits.
5. Respect dependency direction: CLI/Desktop → Core; UI does not access SQL, secrets or output files directly. Preserve fixed metadata-only SQL, strict privacy and bundle safety invariants.
6. Capture backward-compatibility fixtures before changing existing CLI paths. Keep default CLI behaviours intact; do not allow GUI selection rules to leak into old CLI modes.
7. Do not introduce cloud services, telemetry, external JS/fonts/CDNs, runtime package downloads, HTTP listeners, arbitrary SQL execution or Git operations inside DbMapper. Pi Software Factory itself may use external models during *development* only.
8. Keep security-relevant changes small. For new dependencies, justify license, data handling, OS support and package provenance; the desktop host stays provisional until the feasibility gate passes.
9. When code cannot satisfy a stable requirement, flag it clearly and request a decision; do not downgrade requirements or silently replace Photino with another host.

## Verification and maintenance

10. Run deterministic relevant tests, static checks and format verification. Run integration/platform tests when appropriate; distinguish tests actually run from those deferred. Never claim security, portability or byte parity without evidence.
11. Update the authoritative relevant concept(s) when an approved implementation changes project knowledge; avoid duplicating rules elsewhere. Update affected indexes only if concepts move or are added.
12. Update [delivery progress](../delivery/progress.md) upon completion. Record meaningful durable knowledge changes in `context/log.md`, newest date first; **the log is not a progress tracker**. Preserve unresolved questions and drafts until approved/verified.

## Completion checklist

- [ ] Read index and task-relevant concepts; checked unresolved decisions.
- [ ] Identified smallest bounded code change and architectural owners.
- [ ] Preserved CLI contracts and object-selection semantics.
- [ ] Verified secrets, privacy, SQL metadata and filesystem boundaries.
- [ ] Added/updated deterministic tests including failure paths.
- [ ] Ran relevant checks and reported exact results/limits.
- [ ] No unrelated edits, hidden network calls or new runtime dependencies without review.
- [ ] Updated affected OKF concepts, progress and log when applicable.

## Pi Software Factory notes

Treat `/factory` as a development controller (scout → architect → implementer → verification → reviewer), not a runtime component. Use the factory's preflight, read-only roles and verification policies; do not let agent decisions supersede failing deterministic checks. Its local configuration lives under `.pi/software-factory.json` as specified by its repository. Include `context/index.md` in agent entry instructions and prefer a small root `AGENTS.md`.
