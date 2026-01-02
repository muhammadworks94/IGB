# IGB Theme System

This folder contains the centralized theme system used across the MVC/Razor UI.

## File structure

- `tokens.css`: **design tokens** (colors, typography, spacing, radii, shadows)
- `base.css`: base typography defaults + surface helper
- `components/buttons.css`: button variants (bootstrap-compatible + ghost)
- `utilities/spacing.css`: gap/stack/padding helpers
- `utilities/shadows.css`: elevation helpers
- `theme.css`: single entrypoint imported in `_Layout.cshtml`

## How to use

### 1) Page header + breadcrumbs

Use the shared header partial:

- `Views/Shared/_PageHeader.cshtml`
- `ViewModels/PageHeaderViewModel.cs`

### 2) Buttons

Use normal Bootstrap markup:

- Primary: `btn btn-primary`
- Secondary: `btn btn-secondary`
- Outlined: `btn btn-outline-primary` / `btn btn-outline-secondary`
- Ghost: `btn btn-ghost btn-ghost-primary`

### 3) Spacing utilities

- `u-gap-*` for flex/grid gaps
- `u-stack` for vertical stacks
- `u-pad-*` for padding

### 4) Shadows / elevation

- `elevation-0` .. `elevation-4`


