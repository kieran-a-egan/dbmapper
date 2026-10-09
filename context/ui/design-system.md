---
type: UIDesign
title: Desktop Visual Design System
description: Minimal developer utility styling, themes, tokens and accessibility principles.
status: draft
tags: [ui, design, accessibility]
---

# Desktop visual design

## Direction

A focused database utility, **not** an IDE, SSMS clone or dashboard-heavy web app. Dense but readable UI, restrained accent color, subtle dividers, simple navigation, compact toolbars and high information visibility. Do not introduce nested docking systems or large decorative cards.

Use bundled Razor/CSS/icons and OS WebView rendering only. No remote fonts, icon APIs, CDNs, fonts or scripts. Favor OS-system sans-serif stacks and bundled fallback assets if needed.

## Visual primitives

- **Themes:** System (default), Light, Dark. Update appearance immediately and persist preference locally. Follow OS changes while System mode is active.
- **Colors:** Define semantic tokens (`background`, `surface`, `surface-raised`, `text`, `muted`, `border`, `accent`, `focus`, `danger`, `warning`, `success`), with light/dark values. Never hardcode status meaning by color alone.
- **Typography:** One simple sans-serif UI family, clear label hierarchy, monospace for names/paths/types as needed. Prefer consistent weights/sizes to many headline styles.
- **Spacing:** Compact token scale, e.g. 4/8/12/16/24 CSS pixels as **proposed tokens**, not yet approved final measurements. Preserve keyboard target sizes and readable line height.
- **Components:** Sidebar item, repository/connection selector, breadcrumb, tree node, object row, tri-state group checkbox, search field, compact toolbar, details sheet, warning banner, progress row, modal confirmation and neutral status chip.
- **Icons:** Local SVG/icon sprites with consistent stroke. Pair critical icons with labels. No external asset lookups.

## Layout and responsiveness

- Left fixed-width app sidebar: Projects, Database Explorer, Generate, Offline SQL, Settings.
- Explorer itself uses a **resizable two-panel layout**: database/category tree left; filtered selectable object list right. Object details appear in an overlay/temporary sheet, never a permanently docked third panel.
- Remember splitter sizing per app/device if convenient; do not compromise available row width or discoverability.
- Window resizing must preserve usable navigation and scroll behaviour; define minimum usable viewport after the first platform prototype, not speculatively.
- Virtualize very large tree/list populations. Keep checkbox state independent of row virtualization and search filtering.

## Accessibility

- Every control has a visible label/name; keyboard navigation and focus indicators must work across sidebar, trees, object list, dialogs and details sheet.
- Respect system theme and preferably reduced-motion preferences. Ensure sufficient text/focus/status contrast under both themes; meet WCAG 2.2 AA-aligned interaction and contrast goals where WebView controls permit.
- Support screen-reader accessible control roles, selection counts and live updates for scanning/progress without excessively noisy announcements.
- All actions remain accessible without pointing device. Never rely only on color, location or iconography to indicate errors or selected state.

## Open details

Concrete hex values, final component library choice, exact type scale, shortcuts and window minimums are implementation-stage design decisions and remain draft. No bespoke design framework is required until evidence justifies it.

## Related

[Interactions](interactions.md) · [Engineering](../standards/engineering.md) · [Open questions](../delivery/open-questions.md)
