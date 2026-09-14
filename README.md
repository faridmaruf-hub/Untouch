# Untouch

A tray utility that fixes bad touchpad palm rejection: instead of your palms accidentally
moving the cursor while you type, the touchpad (and mouse) can be switched fully on or off
with a **four-finger swipe up** — no keys to hold, no settings menu to dig into.

## What it does

Swipe with four fingers on the touchpad:

| Gesture | Action |
|---|---|
| **Swipe up** | Toggle touchpad + mouse pointer input on/off |
| **Swipe down** | Show desktop (`Win+D`) |
| **Swipe left** | Switch to previous virtual desktop |
| **Swipe right** | Switch to next virtual desktop |

When "off," **all** pointer input is blocked system-wide — not just the internal touchpad, but
also any plugged-in external mouse — until the next swipe-up turns it back on. This is
intentional: the point is to make the touchpad safe to rest your palms on while typing, and
the simplest way to guarantee that is to block pointer input uniformly rather than try to
guess which device a given movement came from.

The app starts with the touchpad **enabled** every time it launches (including at login, if
autostart is on). Swipe up toggles between on and off from there; whichever state you land in
stays until the next swipe up.

## Tray icon

A small touchpad-shaped icon sits in the system tray:
- **Green** — touchpad/mouse input is on.
- **Grey** — blocked.

Right-click it for the menu; double-click it to turn the whole feature on/off (leaving the
touchpad always on, untouched, until you turn it back on).

## Safety: if input ever gets stuck blocked

Press **Ctrl+Alt+Shift+F9** at any time — this forces pointer input back on immediately,
independent of everything else the app is doing. It works even if the mouse is completely
unresponsive, since it's a keyboard shortcut. The tray menu also has a "Force enable (safety)"
item, reachable without a mouse via Windows' built-in tray keyboard navigation
(**Win+B**, then arrow keys, then the Menu key or Shift+F10).

## Requirements

- Windows 10 or 11 with a **Windows Precision Touchpad** (check: Settings → Bluetooth &
  devices → Touchpad — if that page exists and looks like the standard Windows touchpad
  settings rather than a vendor's own app, you have one).
- **.NET 8 Desktop Runtime** installed (one-time; `winget install Microsoft.DotNet.SDK.8` or
  just the runtime if you don't need to build from source).

## One-time setup

So the app's own swipe handling doesn't fight with Windows' built-in four-finger gestures,
set: **Settings → Bluetooth & devices → Touchpad → Four-finger gestures → Swipes → Nothing.**

## Building it

```
cd Untouch
dotnet publish -c Release -o ../publish
```

## Running it

Launch `publish\Untouch.exe`. No admin prompt, no installer.

To run it automatically at every login: right-click the tray icon → check **"Start
automatically at login."**

To stop it: right-click the tray icon → **Exit** (this always restores normal pointer input
before closing, even if it was mid-"blocked").

## Uninstalling

1. If "Start automatically at login" is checked, uncheck it first (removes the scheduled
   task).
2. Exit the app.
3. Delete this folder. Nothing else on the system is touched — no registry entries, no
   drivers, no services.

## Using it on a different laptop

Should work unmodified on any Windows 10/11 laptop with a genuine Windows Precision Touchpad —
the app discovers that touchpad's raw input layout at runtime rather than assuming this
specific one, so it isn't hardcoded to one machine. To move it:

1. Copy the whole `publish` folder (not just the `.exe` — it needs its accompanying files).
2. Set Four-finger gestures to "Nothing" on that laptop too (see One-time setup above).

It will **not** work on a laptop using an older or vendor-proprietary touchpad driver
(pre-Precision-Touchpad Synaptics/Elan control panel instead of the modern Windows Settings
page) — it'll just silently never detect anything. The `RawHidDiag` tool included in this
folder can confirm one way or the other on any given machine before you rely on it.

## What's in this folder

- **`publish\`** — the built app. `Untouch.exe` is what you run.
- **`Untouch\`** — C# source code.
- **`RawHidDiag\`, `KeyDiag\`, `IconGen\`** — small diagnostic/build tools used while
  developing this (checking raw touchpad HID data, testing keyboard events, generating the
  icon). Not needed to run the app; kept for reference.
- **`DEVELOPMENT_LOG.md`** — the full story of how this was built, including the ideas that
  didn't work and why.
