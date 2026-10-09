---
type: ProductWorkflow
title: Desktop Workflows
description: User journeys and business rules for discovery, selection, generation and offline SQL.
status: stable
tags: [product, workflows]
---

# Desktop workflows

## Projects and live discovery

1. Launch standalone app. Only one desktop instance can exist; if the CLI currently owns the cross-process gate, show a reason and do not start.
2. Select a repository folder or a previously used repository. Only one repository is active; switching restores local settings and cached state without connecting.
3. Discover candidate `.csproj` files recursively; choose a project and resolve its `UserSecretsId` from its literal unconditional element, or supply the existing explicit ID override. Never run a build or project code to discover configuration.
4. Resolve named `ConnectionStrings` secrets from the selected project's user-secrets store. Auto-select a sole nonempty entry; if multiple, show **names only**. Allow exact alternative secret key input. Do not display or persist values.
5. Click **Connect & Discover** explicitly. Attempt server enumeration against `master` only where supported and permitted. If unavailable, explain the reason and offer the configured single-database fallback when accessible; do not guess a database.
6. Show databases unchecked for a previously unseen endpoint. Saved database selections are restored only after fresh discovery. Fetch object metadata only for chosen databases; prior object selections are restored after their fresh catalogs load.
7. Present tables, views, procedures and functions in separate filterable groups. Selecting objects does not include their dependencies. Checkbox changes are distinct from clicking an object's name to open its read-only metadata sheet.

## Full generation and targeted refresh

1. **Full:** Select databases and objects, and destination (default repository's `database-context`). **Targeted:** choose an existing database scope; preserve selection for that database and other databases' files unchanged.
2. Re-scan *every database involved in the operation* immediately before generation planning. If any selected scan fails, abort this GUI operation without changing output. Newly found objects remain unselected.
3. Compare previous selections to successful fresh catalogs. Missing objects stay in preferences and trigger an explicit warning and deletion confirmation. Failed or incomplete scans are **never** proof of deletion.
4. Compute proposed generated files and show Added/Modified/Deleted counts and paths, plus scan outcomes, missing objects and protected destination issues.
5. Require explicit **Generate** action. Revalidate source snapshot/plan where needed and check that the output directory has not changed since preview. On conflict, abort and require another preview.
6. Protect manually edited/foreign files and symlinks; stage a complete new snapshot and install atomically with rollback guarantees. A failure/cancel leaves prior bundle as it was. Show success and warnings without logging object names.
7. A full GUI export uses server-wide hierarchy, including when only one database is chosen. A single-database CLI bundle at the chosen output path is incompatible; prompt for another destination, no migration.

## Offline SQL update

- Accessible from sidebar even when no repository or connection is selected; select `.sql` or a ticket directory's `in.sql`, target output bundle and database when the existing server-wide bundle has multiple scopes.
- Parse **without connecting or executing SQL** using the existing ScriptDOM behaviour and supported-DDL constraints. Review changes before writing; apply destination-change verification and protected writer.
- Maintain complete metadata state in the private cache/history while rendering only already selected objects. Report effects on unselected objects without adding their pages. Preserve the existing standalone CLI offline behaviour.
- Existing script replay/order protections and the generated `sql-history.md` semantics must remain valid. An algorithm to ensure filtered bundle/history round trips is subject to a bounded implementation investigation; never silently reconstruct missing metadata from a filtered output.

## Failures, offline and shutdown

- An unreachable server may expose cached metadata marked **Offline / Unverified**; Explorer selections are read-only. No live refresh or generation from that cached snapshot. Offline SQL remains available.
- Display sanitized failures and progress with a Cancel control. Shutdown during work asks to Keep Running or cancel-and-close after cleanup; do not abandon staging files deliberately.
- The UI must distinguish "not selected", "not visible", "scan failed", "missing after verified scan" and "cached/unverified".

## Related

[Selective generation](../architecture/generation.md) · [UI interactions](../ui/interactions.md) · [State](../architecture/data-and-state.md)
