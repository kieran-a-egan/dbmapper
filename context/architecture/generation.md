---
type: Architecture
title: Selective Generation and Commit Protocol
description: Object projection, SQL history, parity, safe previews and file replacement.
status: stable
tags: [architecture, generation, okf]
---

# Selective generation and commit protocol

## Selection model

Store explicit object identities for selected tables, views, stored procedures and functions, grouped within selected databases. Schema/type are identity components; same-named objects across schemas must remain distinct. `Select All` / `Deselect All` operate only on currently filtered objects. New objects never become selected automatically. Do **not** recursively include FK dependencies.

The core uses a complete scanned `SchemaModel` to compute references, then projects selected objects for rendering. For references to unselected objects, retain schema/object identifiers as escaped plain text, **not** links to missing files. Generated indexes, schema counts, reverse relations and status pages must correspond only to published pages and accurately describe coverage. Do not invent cross-database dependencies.

## Modes and compatibility

| Entry point | Operation | Scope outcome |
| --- | --- | --- |
| CLI existing options | Single database, server-wide, selected database(s), targeted database and offline SQL | Exactly existing semantics, args and exit codes apart from approved process-exclusivity refusal |
| GUI Full Generate | Selected databases and selected objects | Replaces generated server-wide bundle scope, deleting stale intact generated pages for deselected objects after preview/confirmation |
| GUI Targeted Refresh | One already exported database | Re-scans and replaces only the object's previous selected pages in that database; all other database subtrees preserved |
| GUI Offline SQL | One chosen SQL file/ticket plus selected bundle/database and saved object scope | Apply supported SQL to full structural baseline and project selected output; report unselected effects, no new object auto-selection |

For any equal full-selection server-wide schema, GUI and CLI must generate **byte-identical files** using shared renderer settings. Preserve existing paths/frontmatter, integrity footers, escaping, ordering and deterministic output; changes to version/provenance fields require explicit compatibility review. GUI live output is always server-wide even with one database. Refuse GUI overwrite/conversion of an existing single-database CLI bundle and request another destination.

## Preview and commit lifecycle

1. Require user-initiated live discovery and a **fresh complete** metadata re-scan of each selected database immediately before planning. Failure in *any* selected database blocks the entire GUI write. CLI still supports partial exports and exit code `4`.
2. Validate target bundles, read current snapshot, build filtered rendered files and calculate the full `Added / Modified / Deleted` list; report missing selections and protection conflicts.
3. If previously selected objects are absent from a **successful** scan, retain them in local settings and require a separate deletion acknowledgement before proceeding. Unsuccessful discovery never proves deletion.
4. Require user confirmation of the preview. Detect destination mutations after preview; abort and rebuild preview rather than overwrite.
5. Lock output and re-check integrity; stage files safely; commit via guarded replacement. Cancellation/failure must preserve the previous logical output. Avoid TOCTOU gaps between last check and swap; enforce lock discipline.
6. Report result; update cache/selection state only at a well-defined success boundary. The crash-consistency and cache-vs-output recovery protocol remains [draft](../delivery/open-questions.md).

## Specific regression contracts

- Empty selection does not silently generate every object; the UI must require an explicit nonempty generation scope.
- Selected `dbo.Orders` with FK to unselected `dbo.Customers` generates `Orders` with a plain-text target, but no `Customers` page or dead link.
- Switching from `Orders + Products` to only `Orders` deletes only protected generated `Products` pages (full mode) after confirmation.
- A failed or cancelled scan leaves existing bundle byte-for-byte unchanged; no misleading deletion preview is committed.
- If output mutates after preview, generate nothing and require a new preview.
- In offline mode, filtered pages cannot be used to reconstruct complete underlying metadata; maintain a full private baseline and existing replay/ordering semantics.

## Related

[ADR-003](decisions/003-selective-generation.md) · [Product workflows](../product/workflows.md) · [Tests](../standards/testing.md)
