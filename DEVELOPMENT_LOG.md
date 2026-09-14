# Untouch — development log

A chronological record of how this project actually happened: the original problem, every
idea that got tried, what broke, why it broke, and what replaced it. Written after the fact
from the session transcript, in the order things really occurred (not reorganized into a
clean "how it works" explanation — that's what the source code and tray menu are for).

## 1. The original problem

Laurent's complaint: laptop touchpad palm rejection is bad. While typing, parts of both palms
rest on the touchpad surface, and it's misread as a finger, moving the cursor. Tweaking
touchpad settings never fixed it. Disabling the touchpad and using a mouse instead works, but
isn't sustainable — not always carrying a mouse, and still wanting to use the touchpad when
no mouse is present. Manually toggling it on/off in Settings every time is painful.

**First-thought design:** hold a key on the keyboard; touchpad is enabled only while the key
is held, disabled the instant it's released. Plus a tray icon to toggle the whole thing on/off.

## 2. Brainstorm: picking the mechanism

Two candidate implementations were discussed for "hold key → touchpad live":

- **A — actually disable/enable the touchpad's hardware device node** (the same thing Device
  Manager's "Disable device" button does), triggered on key down/up. Simple, universal, but
  assumed to have a re-init lag on every press/release.
- **B — leave the touchpad always on at the OS level, but intercept and drop its
  movement/click events in software** unless the key is held. Assumed to be instant, but
  needs to tell the touchpad's input apart from an external mouse's, which is fiddly.

An existing off-the-shelf tool ("Touchpad Blocker" — auto-disables while typing) was offered
as a maybe-good-enough alternative. Rejected: distrust of the automatic heuristic, and
dislike of the delay it introduces by design.

Also rejected up front: simulating the laptop's own Fn+touchpad-toggle hotkey. Laurent had
already tried it and found it's friction — wrong hand position (Fn is not ergonomically where
he wants the trigger), which is a different objection than "it doesn't work." He wanted the
hold-key to sit near the *opposite* hand from the one operating the touchpad (right hand on
touchpad → trigger key reachable by the left hand, as close and unspecialized as possible).
Ctrl was proposed as that key.

**Decision at this point:** go with mechanism B (instant suppression) using Left Ctrl as the
hold key, accepting the added engineering risk over mechanism A's assumed lag.

## 3. Environment setup

Checked the machine: no .NET SDK, but Visual Studio Build Tools 2022 was present (MSBuild +
Roslyn `csc.exe`). Installed .NET 8 SDK via `winget` for a clean, supported toolchain rather
than relying on the Build Tools' bundled compiler directly.

Scaffolded a WinForms project, `TouchpadHoldToUse` (the project's original name).

## 4. Figuring out what touchpad this actually is

Enumerated PnP devices and found: **ASUS Precision Touchpad**, instance ID
`HID\ASUF1209&COL02\4&206411f&0&0001`, its own HID device node separate from the keyboard —
meaning it could be targeted for enable/disable without risking the keyboard.

**First real technical test — and first wrong guess:** tried toggling the touchpad by writing
`HKCU\...\PrecisionTouchPad\Status\Enabled` and broadcasting `WM_SETTINGCHANGE` — the
mechanism guessed to be what Windows Settings' own Touchpad on/off switch uses. Tested live:
**it did nothing.** The touchpad kept responding. Whatever Settings actually calls internally,
it isn't just this registry flip. Assumption discarded, tested rather than argued about.

**Second test, mechanism A properly:** called `CM_Disable_DevNode` / `CM_Enable_DevNode`
directly via P/Invoke from a resident process (not by shelling out to `Disable-PnpDevice`,
which pays PowerShell's process-spawn cost every call). Measured: 236ms on the very first
(cold) call, 27ms on the next. Tested live with fingers on the touchpad: **it stopped and
started instantly**, no perceptible lag either direction.

This directly overturned the plan from step 2 — mechanism A, assumed to be laggy, turned out
to be both simplest and fast enough. Pivoted away from mechanism B before writing any of its
(harder) code.

## 5. First working version: hold Left Ctrl

Built:
- `KeyboardHook` — `WH_KEYBOARD_LL`, watching Left Ctrl down/up.
- `TouchpadController` — wraps `CM_Disable_DevNode`/`CM_Enable_DevNode` for the discovered
  device, with a resync safety net (`GetAsyncKeyState`) in case a key-up is ever missed (e.g.
  across a lock-screen transition).
- `TrayAppContext` — tray icon (green/grey), double-click to toggle the whole scheme,
  "Start automatically at login" via `schtasks`, config persisted to `%AppData%`.
- Required `requireAdministrator` in the app manifest, since disabling a device node needs
  admin rights (same as Device Manager prompting UAC).

**Tested: worked exactly as designed.** Touchpad off by default, on while Ctrl held, off on
release.

## 6. Ctrl turns out to be the wrong key

Laurent caught something the design had missed: **Ctrl is a global OS modifier.** Holding it
for the whole duration of a touchpad interaction means every click made *while using the
touchpad* is silently a Ctrl+click — breaking multi-select (can't click away to deselect),
Ctrl+drag-copies-instead-of-moves, and double-clicking a folder opens it in a new window. This
isn't a settings quirk, it's baked into Explorer and most apps. Any modifier key (Ctrl, Shift,
Alt, Win) has this same problem — the hold-key had to be something with no OS-level meaning
when held during a click.

**Pivot:** Caps Lock. Not a modifier, physically close to the same corner, and — since the
app's own hook sees every keystroke first — its real toggle/LED effect could be swallowed
before Windows ever sees it (`return 1` from the hook instead of forwarding the event).

## 7. Caps Lock doesn't feel right either

Rebuilt the hook to watch Caps Lock and swallow it. Functionally correct, but Laurent's
verdict: "not as smooth as Ctrl," "too far away," "too specialized a key to give up." He also
asked directly: **what about Fn?**

Answer given: on virtually all laptops, **Fn isn't a real key from the OS's point of view** —
it's consumed entirely inside the keyboard's embedded controller, which uses it only to remap
*other* keys' codes (Fn+F5 sends "brightness down," not "F5"). Pressed alone, nothing reaches
Windows at all.

Rather than assert that from memory, built a tiny standalone diagnostic (`KeyDiag`) — a raw
`WH_KEYBOARD_LL` logger printing every event. Laurent ran it and pressed Fn alone, then Fn+F5:
**zero log lines for either.** Confirmed empirically, not by assumption. Also visible in that
same log: Caps Lock **auto-repeats** while held (a burst of KEYDOWNs with no KEYUP between
them) where Ctrl does not — a plausible partial explanation for "not as smooth," though by
that point the ergonomic objection (reach, specialness) mattered more than any residual
smoothness question.

Windows key was floated as the next candidate (no click-modifying semantics, thumb-reachable
from the same corner) — but before testing it, Laurent proposed something different.

## 8. The idea that actually shipped: four-finger swipe

Laurent's observation: a four-finger swipe currently does the same thing as a three-finger
swipe on this touchpad (redundant), so **why not repurpose the unused four-finger gesture
as the toggle**, instead of any key at all? Explicitly framed as "not hold-to-enable anymore,"
but wanted to explore it anyway.

This is a materially different, harder problem than a keyboard hook: Windows' own multi-finger
gesture *recognition* (the thing that turns raw finger movement into "open Task View") is only
exposed to whichever window is currently focused and has explicitly opted in
(`RegisterPointerInputTarget`/`WM_POINTER`) — not usable from a background app. The lower-level
path that *is* available in the background is the touchpad's **raw multi-touch HID reports**
via Raw Input — but that meant reimplementing finger-counting and swipe-direction detection
from scratch, a real step up in scope from a keyboard hook. Flagged clearly as such before
starting, and Laurent chose to go ahead and build it anyway.

Two things needed to be true for this to work at all, checked before writing the detector:

1. **Can a background app even see the touchpad's raw data**, or is it exclusively reserved by
   Windows' own gesture engine (the way mouse/keyboard HID collections are reserved)? Built a
   second diagnostic, `RawHidDiag`, registering for Raw Input on the Digitizer/TouchPad usage
   page. Result: **yes** — real HID report bytes arrived regardless of window focus.
2. **Can Windows' native four-finger action be turned off** so the app's own handling doesn't
   double-fire alongside it? Checked Settings → Touchpad → Four-finger gestures → Swipes:
   a **"Nothing"** option exists. Both prerequisites confirmed before investing further.

## 9. Decoding the touchpad's raw reports

Rather than guess byte offsets from hex dumps, used Windows' own HID parsing API
(`HidD_GetPreparsedData` + `HidP_GetValueCaps`) to ask the device what its report actually
means.

**First attempt failed**: `CreateFile` on the device path returned "file not found." The
device path string printed as a run of `?????` characters. Root cause: a classic P/Invoke
ANSI/Unicode mismatch — `GetRawInputDeviceInfo` without an explicit `CharSet` resolved to the
ANSI entry point, while the code read the result back as UTF-16. Fixed by forcing
`CharSet.Unicode` and the explicit `GetRawInputDeviceInfoW` entry point.

With that fixed, `HidP_GetValueCaps` returned a clean, semantic field list:
- **Contact Count**: Digitizer page (`0x0D`), usage `0x54`, top-level.
- **Per-finger X/Y**: Generic Desktop page (`0x01`), usages `0x30`/`0x31`, one pair per
  `LinkCollection` 1–5 (up to 5 simultaneous contacts).

No vendor-specific guessing required — this is the standard Microsoft-defined Precision
Touchpad HID layout, discovered dynamically rather than hardcoded.

**Second bug**: wired up per-report decoding of contact count and per-finger X/Y. Contact
count decoded perfectly, exactly matching live 1/2/3/4-finger tests. X/Y decoding produced
nothing at all, silently. Added explicit status-code logging and found the real cause: the
code checked `rc == 0` for success, but `HIDP_STATUS_SUCCESS` is actually `0x00110000`, not
zero. Fixed the comparison; X/Y values started flowing correctly.

**Logging problem**: the console window used to watch these tests was itself receiving mouse
input from the touchpad — trying to *select text to copy* out of the console generated more
touchpad events, corrupting the very capture being read. Fixed by writing to a log file
instead, which could be read directly rather than relying on screenshots or copy/paste.

With real per-finger Y data flowing, a genuine four-finger swipe-up showed a clean, large,
synchronized Y decrease across all four contacts (~1700 device units in ~600ms) — clearly
distinguishable from rest/noise. The detection approach was validated on real data before any
production code was written around it.

## 10. Realizing device-disable and gesture-detection can't coexist

Before wiring the swipe detector into the real app, a structural conflict surfaced: the
shipped Ctrl/Caps-Lock version disabled the touchpad's **hardware device node**. A disabled
device sends **no data at all** — including the raw HID reports the new gesture detector needs
to see the very swipe meant to turn it back on. Device-disable and swipe-based re-enable are
mutually exclusive under the existing design.

**Resolution:** stop disabling the hardware device entirely. Keep raw digitizer data flowing
at all times (for gesture detection), and separately suppress actual pointer *output*
(movement/clicks) via a `WH_MOUSE_LL` hook whenever "blocked." This also meant the earlier
"mechanism B" concerns (distinguishing touchpad from an external mouse) no longer applied,
because a modifier key is no longer being held during clicks — so the hook could simply block
*all* pointer input uniformly, which Laurent explicitly accepted (blocking a plugged-in mouse
too, in exchange for simplicity) after being asked directly.

This also meant the app no longer needed admin rights at all (`CM_Disable_DevNode` was the
only thing that had required elevation) — removed `requireAdministrator` from the manifest.

Built: `MouseSuppressor` (the `WH_MOUSE_LL` hook) and `SwipeDetector` (a hidden window
registered for raw digitizer input, reusing the validated HidP parsing, with a baseline/window
state machine: track centroid Y while 4 fingers are down, fire when it moves past a threshold
within a time window, cool down until fingers lift below 4 again). Added a "Force enable
(safety)" tray menu item up front, reachable via Windows' own keyboard-only tray navigation
(Win+B), anticipating that a fully-blocked mouse needs *some* guaranteed way out.

## 11. The lockout incident

First live test of the new mechanism: **the touchpad and mouse got stuck fully blocked, with
no way to re-enable them.** A real incident, not a hypothetical — Laurent was locked out of
pointer input entirely.

Recovery, talked through in real time:
1. `Ctrl+Shift+Esc` (Task Manager) — the process wasn't visible in the default view (a
   tray-only app with no window sits under "Background processes," easy to miss).
2. Fell back to `Win+R` → `cmd` → `taskkill /IM TouchpadHoldToUse.exe /F` — fully
   keyboard-driven, worked immediately. (Killing a process automatically releases any hooks it
   installed, which is why this works as a universal escape hatch.)

**Root cause, diagnosed after recovery:** the "Force enable" safety net was itself reachable
only by clicking the tray menu with a mouse — and the mouse-block hook was blocking *all*
mouse input, including clicks on the app's own tray icon. The safety net could lock itself
out. A self-inflicted bug, not just a discoverability gap.

**Fix:** added `EmergencyHotkey` — a *separate* `WH_KEYBOARD_LL` hook (Ctrl+Alt+Shift+F9) that
forces the mouse unblocked unconditionally. Deliberately independent of the mouse hook's
state, since keyboard and mouse low-level hooks are entirely separate subsystems — this can't
be blocked by the same failure mode that caused the lockout.

## 12. Second bug: swipe wouldn't re-lock

After adding the emergency hotkey, retested: swipe-up correctly turned the touchpad **on** —
but swiping a second time did **not** turn it back off. Root cause: the detector's cooldown
flag was only cleared when contact count reported exactly `0`, but this touchpad never sends
an explicit "0 fingers" report — it simply stops sending reports the moment all fingers lift.
The cooldown check for `== 0` therefore never fired again after the very first swipe,
permanently blocking further detection. Fixed by re-arming as soon as contact count drops
*below* the target (4), which does happen naturally as fingers lift.

Retested: full on → off → on toggle cycle via swipe confirmed working.

## 13. Wanting the other three directions back — cleanly

With "Four-finger gestures" set to "Nothing" to avoid double-firing, Laurent noted the
trade-off: losing native down/left/right swipe animations too, when only "up" actually needed
to be intercepted. Asked if just the up-direction's native action (Task View) could be
suppressed while leaving the other three native gestures untouched.

Two options laid out: (A) let native gestures fire and dismiss the side-effect afterward with
a synthetic Escape keystroke (simple, but a possible visible flicker), or (B) turn off native
four-finger gestures entirely and have the app's own detector handle *all four* directions
itself, sending the equivalent keyboard shortcuts for down/left/right (Win+D, Ctrl+Win+Left/
Right) instead of relying on Windows' native animated gesture. Laurent chose B — full control,
no flicker, at the cost of losing the native animation feel for the other three directions.

Extended `SwipeDetector` to track both X and Y centroids and classify direction by whichever
axis moved further past the threshold; added `KeySender` (a `SendInput` wrapper) for
synthesizing the keyboard shortcuts.

**Bug on first test:** up worked, down/left/right did nothing. Root cause: the C# `INPUT`
struct's union only declared the keyboard-event variant (`KEYBDINPUT`), but Win32's real
`INPUT` union is sized by its *largest* member (`MOUSEINPUT`, which is bigger) — so the struct
was undersized, `SendInput` received the wrong `cbSize`, and silently rejected every call.
"Up" doesn't call `SendInput` at all (it just flips the mouse-block flag directly), which is
exactly why it alone kept working. Fixed by adding the unused `MOUSEINPUT` arm to the union
purely to pad it to the correct native size.

**Second bug on retest:** down now worked, but left and right were swapped. Fixed by flipping
the sign convention in the direction classification.

Retested again: all four directions confirmed working correctly.

## 14. Visible status, then a name that fit

Asked for a visible on/off indicator. The tray icon already changed color, but only reflected
the *master* scheme toggle, not the live blocked/unblocked state that actually changes far
more often (via swipes and Force Enable) — fixed to update on every state change. Also
redrew the icon from a generic colored circle with a "T" into an actual rounded-rectangle
touchpad shape with a click-bar, at the same time.

With the mechanism now entirely swipe-based, the name "TouchpadHoldToUse" no longer described
what the project did. Renamed to **Untouch** (Laurent's choice, after a few suggested
alternatives). The rename hit two filesystem snags: a lingering `dotnet.exe` build-server
process was holding file handles inside the tree (killed it), and a second, never-fully-
diagnosed "Access is denied" persisted on the top-level folder rename even after that — worked
around by creating the new `Untouch` folder fresh and copying/text-replacing the source files
into it individually, rather than fighting the lock further.

## 15. Two polish requests, one bigger fix

- **Exe file icon**: the tray icon at runtime is generated in memory and can change color, but
  a `.exe`'s own icon (Explorer, taskbar, Alt-Tab) is a static embedded resource. Built a small
  one-off tool, `IconGen`, that renders the same touchpad shape at multiple resolutions
  (16–256px) and hand-writes a proper multi-image `.ico` container (manually constructing the
  `ICONDIR`/`ICONDIRENTRY` binary format with PNG-compressed frames, since .NET has no
  built-in multi-res ICO writer), then wired it in via `<ApplicationIcon>` in the project file.

- **Tray icon kept sliding into "hidden icons"**: Windows remembers the "always show in tray"
  preference per *executable path*, and the app's path had changed several times during
  development (different drive, rename) — so Windows kept treating each version as a new,
  unknown icon and defaulting it back to hidden. Offered the cheap fix (drag it out once more,
  now that the path is stable) versus a permanent one; Laurent asked for the permanent fix.
  Rewrote the tray icon from WinForms' `NotifyIcon` to a hand-rolled `Shell_NotifyIcon` wrapper
  (`GuidTrayIcon`) carrying a fixed, hardcoded GUID identity — so the visibility preference is
  now pinned to that GUID forever, independent of the exe's path, surviving any future move,
  rename, or rebuild. (Minor cleanup along the way: renamed a couple of members that were
  accidentally hiding inherited `Control.ContextMenuStrip`/`Control.DoubleClick` members.)

## 16. Moving the project, and startup behavior

Moved the entire project from `Documents\Untouch` into this Magento working directory's
`19. Additionals\Untouch` folder, at Laurent's request (one file lock hit again mid-move,
from the exe still running — resolved by closing it first).

Asked whether it would work unmodified on another laptop: yes, if that laptop also has a
genuine Windows Precision Touchpad (the HID layout is discovered dynamically, not hardcoded to
this ASUS) and the .NET 8 Desktop Runtime, with the whole `publish` folder copied (not just the
`.exe`) and the same "Four-finger gestures → Nothing" setting applied there too. Won't work
as-is on an older/proprietary (non-Precision) touchpad driver.

Final behavior change requested: the touchpad should start **enabled** on every launch
(including autostart at login), not blocked-by-default waiting for a swipe. Fixed
`ApplyInitialState()` to always start unblocked.

Attempting to register the login-startup scheduled task directly (via `schtasks`) from this
session's own shell failed with "Access is denied" — the sandboxed shell this assistant runs
commands in doesn't have rights to talk to Task Scheduler, a reasonable restriction on a
persistence mechanism. Handed off to Laurent to do himself via the tray menu's own "Start
automatically at login" checkbox, which runs in his normal session and isn't subject to that
restriction.

## Where things stand

`Untouch.exe` (in `Untouch\publish\`): four-finger swipe up toggles touchpad+mouse on/off
(starting enabled on launch), down shows desktop, left/right switch virtual desktop, tray icon
shows live state with a permanent GUID identity, Ctrl+Alt+Shift+F9 always forces it back on.
Source and the HID diagnostic tools built along the way (`RawHidDiag`, `KeyDiag`, `IconGen`,
and the early PowerShell test scripts) are kept alongside it in this folder.
