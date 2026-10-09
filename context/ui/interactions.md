---
type: UIWorkflow
title: Desktop UI Interaction Contract
description: Navigation, selection, preview, operation progress and accessible UI states.
status: stable
tags: [ui, interactions, explorer]
---

# Desktop UI interaction contract

## Navigation

Persistent sidebar sections: **Projects**, **Database Explorer**, **Generate**, **Offline SQL**, **Settings**. Only one repository is active. Switching repositories retains their local saved settings/cache and clears incompatible live session state. Selections are not lost simply because the user navigates away.

## Projects

- Recent repositories list plus OS folder picker. Recursively discover `.csproj` candidates without running them. Allow explicit UserSecretsId override for inherited/conditional settings.
- Show `ConnectionStrings` key names only; automatically choose a sole nonempty key, otherwise require a named selection or exact manual key.
- Display selected project and connection identity, then **Connect & Discover**. Nothing on this page initiates a SQL connection without that action.
- Failed server enumeration offers clearly limited configured-database fallback when it has a valid target. Show sanitized permission or connectivity guidance.

## Explorer

- Left panel shows database tree and categories; right panel shows filtered, scrollable and virtualized objects. Rows have **independent** checkbox and preview-name targets. Temporary details sheet presents allowed structural metadata only.
- Groups: Tables, Views, Stored Procedures, Functions, with Select All / Deselect All acting only on filtered matches. Show matching/total/selected counts. Unfiltered selections remain unchanged on filter edits.
- New context: all unchecked. Restored selections require fresh discovery; missing ones are displayed distinctly and preserved in settings.
- Cached offline mode is visibly **Unverified**, and checkboxes and live Generate/Refresh actions are disabled. Reconnecting makes fresh state available.

## Generate

- Show output path prefilled as `<repository>/database-context`, optional alternate folder, selection summary, status and mode: Full Generation or Targeted Database Refresh.
- Fresh catalog scan precedes **every** preview. Display stages and cancellation, with truthful completed/total counts when known.
- Review lists Added / Modified / Deleted paths; warnings distinguish missing-after-verified-scan, modified/foreign files and changed output directory. No write occurs until explicit confirmation. Missing-object deletion needs explicit separate acknowledgement.
- An existing single-database CLI bundle at destination is not migrated; explain conflict and require a new directory.

## Offline SQL

Independent page: choose SQL file or folder (`in.sql`), target OKF bundle and target database when needed. Explain that files are parsed but **SQL is never executed**. Preview affected selected pages and report changes to unselected objects; require confirmation before writing.

## Settings

Theme selector: System, Light, Dark. Inspect/clear per-project or all schema caches. Clear local redacted logs and show retention (30 days; 50 MB). No telemetry or update toggles because these features do not exist.

## Progress and transient states

- Required states: not connected, discovering, loaded, empty database, empty filter, permission denied, missing selection, cached/offline, partially visible database listing, operation failed, cancelled, preview stale, protected output, success.
- Show stage, current database/object when safe *in active UI only* (not persistent logs), warnings and Cancel. UI remains usable but must prevent conflicting operations.
- Closing during work asks Cancel Operation & Close or Keep Running. A second desktop launch focuses the first instance if supported; otherwise explain and exit. CLI conflicts produce clear refusal.

## Related

[Product workflows](../product/workflows.md) · [Design](design-system.md) · [Generation](../architecture/generation.md)
