# Untouch

A tray utility that fixes bad touchpad palm rejection: instead of your palms accidentally
moving the cursor while you type, a **four-finger swipe up** switches off just the part of the
touchpad (and mouse) that causes it — while three- and four-finger gestures, including the
swipe that turns it back on, keep working the whole time. No keys to hold, no settings menu to
dig into.

## What it does

Swipe with four fingers on the touchpad:

| Gesture | Action |
|---|---|
| **Swipe up** | Toggle touchpad + mouse pointer input on/off |
| **Swipe down** | Show desktop (`Win+D`) |
| **Swipe left** | Switch to previous virtual desktop |
| **Swipe right** | Switch to next virtual desktop |

When "off," only **single-finger movement/clicks and two-finger scroll** are blocked — the
exact motions that cause accidental cursor jumps and stray clicks when your palms rest on the
touchpad while typing. This is blocked system-wide, for any pointer device (internal touchpad
or a plugged-in external mouse), not just the specific finger that touched down — the
simplest way to guarantee it's actually safe to rest your palms, without trying to guess which
device a given movement came from.

Everything multi-finger keeps working even while "off": three-finger swipes still do whatever
you have them set to in Windows (task switching, etc.), and all four Untouch gestures — up,
down, left, right, including the swipe-up that turns it back on — behave identically whether
the touchpad is on or off. "Off" only removes the part of the touchpad that causes problems
while typing; it doesn't turn the touchpad into a dead slab.

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

Two things worth doing the very first time you run it, so it stays out of your way afterward:

1. **Turn off Windows' own four-finger gestures**, so they don't fight with Untouch's: Settings
   → Bluetooth & devices → Touchpad → Four-finger gestures → Swipes → **Nothing**.
2. **Drag the tray icon out of the "hidden icons" overflow** into the always-visible tray area
   (Windows defaults every new tray icon to hidden). The icon has a fixed identity, so you only
   need to do this once, ever — it'll stay put across restarts, rebuilds, and even if you move
   the app to a different folder later.

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
