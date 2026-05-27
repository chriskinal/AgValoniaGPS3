# Navigation & UI Redesign — Proposal for Discussion

**Status:** proposal / design discussion — Screen & Alerts built; rest not started.
**Branch:** `feature/ui-redesign` — the whole redesign ships as **one branch / one
PR** (Screen & Alerts folded in). Per-surface **commits** for reviewability, but
one delivery. Rebase onto `develop` **regularly** (it's active — don't let this
long-lived branch drift into one giant end-of-line rebase).
**Stance:** map-centric. This is *not* Pages-v2 — the map stays the home you
always return to; these are organized menus/panels over it, not a Home launcher.

---

## 1. The problem

Menus are organized by *kind of thing*, not by *when you use it*, so single
workflows are scattered and you bounce between surfaces:

- **Field Ops vs Field Tools** — starting a field and working a field are split
  across two menus; you jump back and forth.
- **Tracks menu** mixes *creating* tracks (setup) with *selecting* them
  (driving) — only selection is an in-motion action.
- **Camera controls** live in a full-screen menu that *hides the map* — you're
  adjusting a view you can't see.
- **"Application Settings" fly-out** is a junk drawer: app config + connections +
  diagnostics + info all in one list.
- **Top status strip** is too tall; the G/I/A/M letter indicators are weak; dev
  readouts (FPS/latency/Lat-Lon) are permanent chrome.

## 2. Organizing principle

Sort every surface by **the question it answers / when you reach for it**:

1. *Get into a field* — stopped, start of work.
2. *Build the field* — stopped, roughly once per field.
3. *Drive the field* — in motion, repeated → keep this minimal.
4. *See & hear* — set-and-forget presentation.
5. *Configure the app* — system preferences.
6. *Talk to the outside world* — corrections + hardware connectivity.
7. *Diagnose* — tools.

## 3. Proposed homes (end state)

| Home | Question | Holds |
|------|----------|-------|
| **Start-Session hub** | get into a field | pick existing / create (drive-in, from-existing) / import (KML, ISO-XML) field; resume or start a job; open-field-only |
| **Field Tools** | build the field | boundary, headland, **track create/edit**, tram setup, field builder, offset fix, delete applied area, close field |
| **Bottom HUD** | drive the field | track **select**/cycle/auto, nudge, snap, U-turn skip, headland on/off, section-in-headland, flags, reset tool heading, tram display |
| **Screen & Alerts** | see & hear *(shipping now)* | display prefs, theme, day/night, sounds, on-screen buttons, hardware messages |
| **App Settings** | configure the app | units, keyboard, fullscreen, elevation log, **App Directories**, Language, Reset (TBD Hotkeys) — one dialog, sections |
| **Network panel** | talk to the outside world | NTRIP profiles, expanded module status (G/I/A/M + IPs + rates, from `State.Connections`), IP / PGN-send config |
| **Tools panel** | diagnose | Steer Wizard, Heading/XTE charts, **Log Viewer**, (likely Bug Report) |
| **Camera pad** *(on map, not a menu)* | aim the view | zoom + tilt + camera-mode, where you can see the map |

### Interaction archetypes (how a surface behaves)

Not everything is menu-vs-modal. Four behaviors:

1. **Menu fly-out** — transient launcher; mutually exclusive; auto-collapses.
2. **Modal dialog** — takes over the screen (setup done stopped); backdrop.
3. **Floating HUD / on-map overlay** — always-available over the live map
   (status strip, camera pad, manual U-Turn/Lateral, sim bar).
4. **Field-work tool** *(new archetype)* — *initiated as setup, executed while
   driving.* Launched from a menu → **its menu collapses** → tool becomes a
   **floating panel over the live map** (not modal — you see + drive) → on
   **close, the launching menu reopens** so you can chain the next setup step
   (e.g. record boundary → done → Field Tools reopens → build headland).
   - Applies to: **boundary recording**, recorded path (record/drive), drive-in
     AB creation, boundary player — anything captured/followed by driving.
   - Composes with idle auto-close: while the tool is up its menu is closed; on
     tool close the menu reopens and its idle timer restarts.
   - *Open:* always reopen the menu on close, or also offer "done → back to the
     map" (drop straight to driving without the menu popping back)?
5. **Wizard** *(new archetype)* — a guided multi-step task that **takes over
   full-frame** while running. Launched from a menu → menu collapses → wizard
   runs full-frame → on finish/cancel, returns to the map and the launching menu
   **stays closed** (the wizard *is* the whole task, not a setup step to chain).
   Applies to: Steer Wizard, setup wizards.
   - Differs from the field-work tool on two axes: **takeover vs float**, and
     **stay-closed vs reopen-menu** on completion.

## 4. Surface-by-surface moves

| Today | → Proposed |
|-------|-----------|
| **Field Operations** menu | **retired** — create/select/resume → Start-Session hub; Close/Drive-In/AgShare → Field Tools |
| **Start Work Session** dialog | becomes the **Start-Session hub** (adds field pick/create/import, not just job) |
| **Tracks / AB-line flyout** | keep *select/cycle/auto/nudge/snap*; **create + edit → Field Tools** |
| **Bottom bar** | trim to immediate (driving) actions only |
| **Camera (tilt/2D-3D/north-up) in Screen & Alerts** | **out** → on-map **4-way pad** |
| **Right-edge zoom +/- and mode button** | replaced by the 4-way pad |
| **App Directories** (File menu) | → section of **App Settings** |
| **Language, Reset All Settings** (File menu) | → **App Settings** (TBD) |
| **NTRIP Profiles** (File menu) | → **Network panel** |
| **Log Viewer** (File menu) | → **Tools panel** |
| **Bug Report, Simulator** (File menu) | → Tools panel (TBD) |
| **Dev readouts** (top strip) | → floating **Dev overlay** |

After this, the "Application Settings" fly-out is down to App Settings +
connections (NTRIP→Network, AgShare?) + info (Help/About) — possibly not worth a
top-level menu anymore.

## 5. Component designs

### Camera 4-way pad *(on the map; replaces right-edge zoom + mode cluster)*
```
        ▲ tilt (toward overhead → 2D at full-up)
        │
 ◄── [ H ] ──►    L/R = zoom out / in   (Right = in)
 zoom   │  zoom   center = H→N→M→C mode cycle (shows CameraModeLabel)
        ▼          press-and-hold to repeat; tap = one step
```
2D/3D is automatic at the tilt extremes — no separate toggle. North-up stays in
the mode cycle.

### Start-Session hub
One dialog: choose a field (existing list, or create/import) **and** its job
(resume / start new / open-field-only) in one place. Folds in today's field
creation/import/resume buttons + the Start Work Session dialog.

### Top status strip + Dev overlay
- Shorter strip.
- Module status as **G/R/Y dots** (green ok / yellow stale / red absent) for
  GPS·IMU·AutoSteer·Machine; tap a dot → Network panel.
- FPS / latency / Lat-Lon → toggleable **floating Dev overlay**.
- Mine `feature/toolbar-redesign` for the strip concept.

### U-Turn controls (rework the right-rail cluster)

Today the right rail stacks a direction+distance button and pop-in/out manual
L/R buttons between YouTurn and AutoSteer — confusing (number appears only
sometimes; manual buttons come and go). Break it up the AgOpenGPS way:

- **Distance to next turn / boundary → the steer HUD (LightBar).** A glanceable
  readout next to XTE. Data exists: `DistanceToTrigger` / `DistanceToHeadland`
  (and a `DistanceToBoundary()` helper) — show turn distance when auto-U-Turn is
  armed, else distance to boundary.
- **Manual U-Turn → transparent on-map overlay** (AgOpen-style), gated by the
  **existing `UTurnButtonVisible`** "Screen Button" toggle in Screen & Alerts
  (On-Screen Buttons group, bottom-left). Today that toggle wrongly gates the
  *right-rail* buttons — re-point it at the real on-map overlay.
- **Lateral → on-map overlay too**, gated by `LateralButtonVisible`. That toggle
  exists in Screen & Alerts but **nothing consumes it today** (orphaned) — wire
  it to an actual on-map lateral-shift button.
- **Remove the right-rail direction+distance button cluster.** The YouTurn
  *arm* toggle stays on the rail.
- So the Screen & Alerts "On-Screen Buttons" toggles become the genuine
  show/hide for the on-map U-Turn + Lateral overlays (their intended job).
- *Open:* where the **next-turn direction toggle** (flip L/R for the auto turn)
  lands — into the on-map U-Turn overlay, or beside the YouTurn arm button?

### Network panel
NTRIP profiles + expanded module status (backed by `State.Connections`) + IP /
PGN-send config. The status-bar dots are the glanceable view; this is the detail.

## 6. Decisions needed (discussion)

1. **Four-tier sort** — does immediate-vs-build match how you work, or recut?
2. **App Settings depth** — Directories only, or also Language / Reset / Hotkeys?
3. **AgShare** — join Network panel (it's network) or stay separate (it's data
   sharing)?  *(lean: separate)*
4. **Machine-module config** (pins/relays) — stays in Configuration (implement
   setup); Network panel takes only connection status/IP. Confirm?
5. **Field Tools size** — it grows large (build + track create/edit + tram +
   boundary + headland); does it need section sub-nav like Screen & Alerts?
6. **Dev overlay** — toggle via hotkey / dev flag? persist across restart?
7. **G/R/Y dots** — need a 3-state connection model (today `IsXxxDataOk` is bool);
   define "yellow."
8. **Does the "Application Settings" menu survive** once it's down to
   connections + info?
9. **Camera pad conventions** — Right=in, Up=overhead, hold-to-repeat (defaults).

## 7. Sequencing & scope

- **One branch (`feature/ui-redesign`), one PR.** Screen & Alerts is already
  built on it; the rest layers on. Suggested commit order (each a coherent,
  buildable step): camera pad (+ remove camera group from Screen & Alerts) →
  U-Turn/Lateral rework (HUD distance + on-map overlays) → bottom-HUD trim +
  track-creation→Field Tools → Start-Session hub (Field Ops retired) → status
  strip + Dev overlay → Network panel → App Settings / File-menu cleanup
  (Directories/Language/Reset in; Log Viewer→Tools).
- **Rebase onto `develop` regularly** — it's active; keep the branch current
  rather than facing one giant rebase at the end.
- **Device pass at the end** covers the whole redesign (bigger surface, inherent
  to shipping as one unit).
- **Non-goals:** no Home-launcher/page model; map stays primary; no new
  guidance/field-data behavior — this is navigation/IA only.
