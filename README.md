# ClickMap

A small always-on Windows floating widget that maps keyboard keys to mouse clicks at
saved screen points. Press a key → a click fires at the exact spot you assigned to that
key, **system-wide**, even when the widget isn't focused.

Intended for general productivity, accessibility, and legitimate workflow automation.

Built with **.NET 10 + WPF (C#)**. Low footprint, single-process, no external services.

---

## Features

- Define and save **multiple click targets** by clicking a spot on screen.
- Assign a **key or hotkey** (with Ctrl/Alt/Shift/Win modifiers) to each target.
- Trigger a click at the target whenever the key is pressed — global, low-latency.
- Per-target **click type**: left / right / middle / double.
- **Floating widget**: always-on-top, draggable, remembers position, hides to tray.
- **System tray** menu: show/hide, pause, add target, settings, exit.
- **Pause** dispatch any time, plus a global **panic key** (default `Ctrl+Alt+P`).
- **Conflict detection** when a key is assigned to more than one target.
- **Flash preview** shows where a target clicks and which key fires it; optional visual/sound **click feedback**.
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

1. **Add a target** — click **Add** on the widget (or the tray menu). The screen dims and
   a crosshair follows the cursor; **click the spot** you want, then **press the key** to
   assign. `Esc` cancels.
2. **Trigger it** — press that key anywhere; a click fires at that exact spot.
3. **Edit / delete** — select a target and click **Edit** (or double-click it) to rename,
   reassign the key, **re-pick** the point, change the click type, enable/disable, or delete.
4. **Flash** — briefly marks the selected target on screen, labelled with its key, so you can
   see where it clicks. The same marker appears on each click when visual feedback is on.
5. **Pause** — the toggle (or panic key) stops all dispatch instantly.

Targets, settings, and logs are stored under `%APPDATA%\ClickMap\`:

| File | Purpose |
|------|---------|
| `targets.json` | Saved click targets (editable by hand; changes are picked up on next launch). |
| `settings.json` | Widget position, pause state, and preferences. |
| `logs\clickmap-YYYYMMDD.log` | Diagnostic log. |

> **Upgrading from 1.0:** the first launch converts `regions.json` (rectangles) to
> `targets.json` — each old region becomes its center point, which is exactly where 1.0
> clicked — and keeps the old file as `regions.json.migrated`.

### Settings

Open via the **⚙** button or tray **Settings…**:

- **Launch at Windows startup**
- **Default click type** for new targets
- **Cursor behaviour** — what happens to your mouse cursor when a target fires:
  - *Click and put the cursor back* (default) — the move to the target, the click, and
    the move back are one input batch, so the cursor ends where it was and is away for
    well under a frame. Works with any app.
  - *Click without touching the cursor* — posts the click straight to the window under
    the target. The cursor never moves, but only apps that trust message coordinates
    respond (classic Win32 controls); WPF apps, games, and elevated windows ignore it.
  - *Move the cursor to the target* — moves the cursor there and leaves it.
- **Visual / sound feedback** on each click
- **Panic key** — global key that instantly toggles pause (default `Ctrl+Alt+P`)

---

## How it works

| Layer | Role |
|-------|------|
| `HotkeyService` | Global low-level keyboard hook (`WH_KEYBOARD_LL`) on a dedicated message-pump thread. The callback is O(1) and offloads work so input is never blocked. |
| `TargetStore` | Loads/saves targets; O(1) key→target index; atomic writes; corrupt-file quarantine; migrates pre-1.1 `regions.json`. |
| `ClickEngine` | Matches a key to a target and dispatches the click; handles pause and the panic key. |
| `ClickService` | Delivers the click per the chosen cursor behaviour: `SendInput` (with or without restoring the cursor) or `PostMessage` straight to the window. |
| UI | Floating `WidgetWindow`, click-to-pick `TargetOverlay`, `TargetEditorWindow`, `SettingsWindow`, tray icon. |

Coordinates are stored in **physical pixels** and the app is **Per-Monitor-V2 DPI aware**,
so targets stay accurate across multi-monitor / mixed-DPI setups.

---

## Troubleshooting

- **Keys don't trigger clicks** — make sure dispatch isn't paused (widget toggle / tray).
  Check the log in `%APPDATA%\ClickMap\logs\`.
- **"keyboard hook failed"** — another tool may be interfering; restart the app.
- **The click goes through but the app doesn't react** — if cursor behaviour is set to
  *Click without touching the cursor*, the app may be one that ignores posted clicks
  (WPF, games, elevated windows). Switch to *Click and put the cursor back*.
- **A key fires the wrong/no target** — check for a conflict warning (a key assigned to
  multiple targets only fires the first). Give them distinct keys.
- **The overlay crosshair looks slightly off on a second monitor** — the saved coordinates are
  correct; only the drawn crosshair is best-effort on mixed-DPI monitors.

---

## Roadmap

Profiles / per-app activation, click-and-hold, action sequences, and import/export. The
service separation (`HotkeyService` / `ClickService` / `TargetStore`) keeps these additive.

---

## License

Released under the [MIT License](LICENSE).

## Note on responsible use

ClickMap synthesizes standard input for productivity and accessibility. Don't use it to
violate the terms of service of games or other software.
