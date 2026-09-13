# Changelog

All notable changes to this project are documented in this file.

## [1.2.0] - 2026-07-03

### Architect

- Introduce MVVM architecture with CommunityToolkit.Mvvm and dependency injection

### UI / layout

- Introduce View layer and `LayoutService` for `MainForm`
- Slim toolbar; edit score header directly on canvas
- Remove toolbar buttons duplicated in menu

### Editing

- **Increase duration (+) / Decrease duration (−)** six-step duration cycling (1/16 → 1/8 → 1/4 → tied +1/+2/+3 beats)
- **Ctrl / Shift** multi-note selection with cross-measure range sync
- **Raise key / Lower key** pitch transpose within current key
- **Split / Merge** note duration tools
- **Undo (Ctrl+Z)** undo stack via score snapshots

### Assets / docs

- Updated `Screen Sample.png`
- Updated Canon sample score (`sample/卡农 Canon in D.jianpu`)
- README updated for new features

### Tests

- 124 unit tests passing

## [1.1.0]

- MVVM refactor, chord transpose, playback, dark mode, and installer release