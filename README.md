# ClickMap

A small always-on Windows floating widget that maps keyboard keys to mouse clicks at
defined screen regions. Press a key → a left click fires at the center of the region you
assigned to that key, system-wide.

Intended for general productivity, accessibility, and legitimate workflow automation.

## Status

Early development. Built with **.NET 10 + WPF (C#)**.

| Phase | Scope | State |
|-------|-------|-------|
| 0 | Project scaffolding, DPI manifest, MVVM base | ✅ |
| 1 | Input & click core: global keyboard hook + `SendInput` clicking | 🚧 |
| 2 | Region model & JSON persistence | ⬜ |
| 3 | Widget UI, region capture overlay, tray icon | ⬜ |
| 4 | Reliability, settings, panic key, polish | ⬜ |
| 5 | Single-file self-contained packaging | ⬜ |

See the full development plan for architecture and decisions.

## Build & run

Requires the .NET 10 SDK.

```sh
dotnet build
dotnet run --project ClickMap
```

### Phase 1 manual harness

The current build launches a small harness window. Press the configured test key
(default **F8**) anywhere — even with another app focused — and a left click fires at a
hardcoded screen point. Use this to validate the hook + click pipeline (try a second
monitor and a monitor with different DPI scaling).
