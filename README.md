# ClickMap

A small always-on Windows floating widget that maps keyboard keys to mouse clicks at
defined screen regions. Press a key → a left click fires at the center of the region you
assigned to that key, **system-wide**, even when the widget isn't focused.

Intended for general productivity, accessibility, and legitimate workflow automation.

Built with **.NET 10 + WPF (C#)**. Low footprint, single-process, no external services.

---

## Features

- Define and save **multiple screen regions** by dragging a rectangle on screen.
- Assign a **key or hotkey** (with Ctrl/Alt/Shift/Win modifiers) to each region.
- Trigger a click inside the region whenever the key is pressed — global, low-latency.
- Per-region **click type**: left / right / middle / double.
- **Floating widget**: always-on-top, draggable, remembers position, hides to tray.
- **System tray** menu: show/hide, pause, add region, settings, exit.
- **Pause** dispatch any time, plus a global **panic key** (default `Ctrl+Alt+P`).
- **Conflict detection** when a key is assigned to more than one region.
- **Flash preview** to locate a region; optional visual/sound **click feedback**.
- **Launch at Windows startup** (optional).
- Robust: single-instance, atomic config saves, corrupt-file recovery, file logging.

---

## Install / run

### Option A — download the release exe (recommended)

Grab `ClickMap.exe` from the release. It's **self-contained** — no .NET install required.
Double-click to run; a blue icon appears in the system tray and the widget shows in the
bottom-right.

### Option B — build from source

Requires the **.NET 10 SDK**.

```sh
# Run in development
dotnet run --project ClickMap

# Produce the self-contained single-file release exe
dotnet publish ClickMap/ClickMap.csproj -p:PublishProfile=win-x64
# -> ClickMap/bin/publish/win-x64/ClickMap.exe
```

For a much smaller exe that instead requires the **.NET 10 Desktop Runtime** to be
installed, set `SelfContained=false` in
[win-x64.pubxml](ClickMap/Properties/PublishProfiles/win-x64.pubxml).

> Note: the publish is not trimmed — trimming is unsupported for WPF and would break
> XAML/reflection.

---

## Usage

1. **Add a region** — click **Add** on the widget (or the tray menu). The screen dims;
   drag a rectangle over the target, then **press the key** to assign. `Esc` cancels.
2. **Trigger it** — press that key anywhere; a click fires at the region's center.
3. **Edit / delete** — select a region and click **Edit** (or double-click it) to rename,
   reassign the key, change the click type, enable/disable, or delete.
4. **Flash** — highlights the selected region on screen so you can find it.
5. **Pause** — the toggle (or panic key) stops all dispatch instantly.

Regions, settings, and logs are stored under `%APPDATA%\ClickMap\`:

| File | Purpose |
|------|---------|
| `regions.json` | Saved regions (editable by hand; **Reload** is automatic on change). |
| `settings.json` | Widget position, pause state, and preferences. |
| `logs\clickmap-YYYYMMDD.log` | Diagnostic log. |

### Settings

Open via the **⚙** button or tray **Settings…**:

- **Launch at Windows startup**
- **Default click type** for new regions
- **Click strategy** — move cursor to target then click (most compatible), or send an
  absolute click without moving the cursor
- **Visual / sound feedback** on each click
- **Panic key** — global key that instantly toggles pause (default `Ctrl+Alt+P`)

---

## How it works

| Layer | Role |
|-------|------|
| `HotkeyService` | Global low-level keyboard hook (`WH_KEYBOARD_LL`) on a dedicated message-pump thread. The callback is O(1) and offloads work so input is never blocked. |
| `RegionStore` | Loads/saves regions; O(1) key→region index; atomic writes; corrupt-file quarantine. |
| `ClickEngine` | Matches a key to a region and dispatches the click; handles pause and the panic key. |
| `ClickService` | Synthesizes the click via `SendInput` with correct virtual-desktop absolute mapping. |
| UI | Floating `WidgetWindow`, drag-to-select `RegionOverlay`, `RegionEditorWindow`, `SettingsWindow`, tray icon. |

Coordinates are stored in **physical pixels** and the app is **Per-Monitor-V2 DPI aware**,
so regions stay accurate across multi-monitor / mixed-DPI setups.

---

## Troubleshooting

- **Keys don't trigger clicks** — make sure dispatch isn't paused (widget toggle / tray).
  Check the log in `%APPDATA%\ClickMap\logs\`.
- **"keyboard hook failed"** — another tool may be interfering; restart the app.
- **A key fires the wrong/no region** — check for a conflict warning (a key assigned to
  multiple regions only fires the first). Give them distinct keys.
- **Region clicks land slightly off on a second monitor** — the saved coordinates are
  correct; only the drawn selection guide is best-effort on mixed-DPI monitors.

---

## Roadmap

Profiles / per-app activation, per-region custom target point and click-and-hold, action
sequences, and import/export. The service separation (`HotkeyService` / `ClickService` /
`RegionStore`) keeps these additive.

---

## Note on responsible use

ClickMap synthesizes standard input for productivity and accessibility. Don't use it to
violate the terms of service of games or other software.
