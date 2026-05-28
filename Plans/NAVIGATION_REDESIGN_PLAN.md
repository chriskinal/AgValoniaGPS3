# Navigation & UI Redesign — Proposal for Discussion

**Status:** in progress — built: Screen & Alerts, camera 4-way pad, U-Turn/Lateral
overlays, **Field Tools sectioned modal + bottom-HUD trim** (field creation moved
into Field Tools; idle auto-close is now an opt-in Screen & Alerts toggle, default
off). All §6 decisions resolved (2026-05-27). Next commit: Start-Session hub
(retire Field Operations).
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
| **Start-Session hub** | start work on a field | pick an **existing** field; start a job; resume last job / resume job; open-field-only. *(AgShare — TBD.)* Field **creation** moved to Field Tools (see below). |
| **Field Tools** | create + build the field | **Creation** (new / from-existing / import KML + ISO-XML / drive-in), **Boundaries** (field builder, drive-around, draw, import KML, from-tracks, inner, delete), **Tracks** (create/edit), **Other** (offset fix, recorded path, import tracks, delete applied, close field). Sectioned modal. |
| **Bottom HUD** | drive the field | track **select**/cycle/auto, nudge, snap, U-turn skip, headland on/off, section-in-headland, flags, reset tool heading, tram display |
| **Screen & Alerts** | see & hear *(shipping now)* | display prefs, theme, day/night, sounds, on-screen buttons, hardware messages |
| **App Settings** | configure the app | units, keyboard, fullscreen, elevation log, **App Directories**, Language, Reset, **Hotkeys** — one dialog, sections |
| **Network panel** | talk to the outside world | NTRIP profiles, expanded module status (G/I/A/M + IPs + rates, from `State.Connections`), IP / PGN-send config |
| **Tools panel** | diagnose | Steer Wizard, Heading/XTE charts, **Log Viewer**, Bug Report, **Help / About (incl. app version)** |
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
   - ✅ *Resolved:* on close, **always reopen the launching menu** (so you can
     chain the next setup step); idle timer restarts.
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
| **Field Operations** menu | **retired** — create/select/resume + **AgShare** → Start-Session hub; Close/Drive-In → Field Tools |
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

After this the "Application Settings" fly-out has nothing left of its own —
**retired** (§6.8): App Directories/Language/Reset/Hotkeys → App Settings, NTRIP
→ Network, AgShare → Start-Session hub, Log Viewer + Help/About (incl. version) →
Tools.

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
- Module status as **one live aggregate button** (not per-module dots): Green =
  all *configured* modules present · Yellow = ≥1 configured module absent · Red =
  all absent. Tap → Network panel. Needs a per-module "installed/configured" tick
  in the Network panel so the button knows which modules to expect. (See §6.7.)
- FPS / latency / Lat-Lon → **floating Dev overlay**, toggled by a cross-platform
  **file flag** (marker file), not a hotkey (mobile has no keyboard). (See §6.6.)
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
- ✅ *Resolved:* next-turn direction is **two separate on-map buttons — one L,
  one R** (the AgOpen-style yellow curved arrows), not a single flip toggle.
  They live on the **on-map U-Turn overlay** (all turn controls together on the
  map; the right rail keeps only the arm toggle).
  - **Overlay layout (from Chris's reference shot):** yellow curved L + R turn
    arrows (manual trigger / direction); a next-turn indicator like `1R`
    (sequence/skip count + direction); the **Lateral** overlay is the separate
    cyan L/R shift arrows below, gated by `LateralButtonVisible`.
  - Skip-row **count** stays where it is for now; this commit moves only the
    L/R direction + manual-trigger arrows on-map.

### Network panel
NTRIP profiles + expanded module status (backed by `State.Connections`) + IP /
PGN-send config. The status-bar dots are the glanceable view; this is the detail.

## 6. Decisions — RESOLVED (2026-05-27)

1. **IA sort** — ✅ Keep **Start-Session hub** and **Field Tools** as *separate*
   homes. **Revised 2026-05-27:** the line is **create+build (Field Tools)** vs
   **start a work session on an existing field (Start-Session hub)** — field
   *creation* (new/from-existing/import/drive-in) lives in **Field Tools**, not
   the hub. Field Tools is sectioned Creation → Boundaries → Tracks → Other.
2. **App Settings depth** — ✅ Fold **Hotkeys in too**. App Settings is the one
   config dialog: units, keyboard, fullscreen, elevation log, App Directories,
   Language, Reset, **+ the full hotkey-binding editor**.
3. **AgShare** — ✅ Neither Network nor a separate top-level menu. AgShare
   **belongs with field create/setup** — it's part of getting a field in/out,
   so it lives on the **Start-Session hub** side, not Network.
4. **Machine-module config** (pins/relays) — ✅ Stays in **implement
   Configuration**. Pins/relays, section on/off, disc lift/lower are all
   *implement behavior* (sprayer/grain-drill/etc.). Network panel takes only
   connection status + IP.
5. **Field Tools size** — ✅ **Section sub-nav** like Screen & Alerts (group
   into Boundary / Tracks / Tram / Field). Scales as it grows.
6. **Dev overlay** — ✅ Toggle via a **cross-platform file flag** (marker file,
   like `.use_skia_map`). Hotkeys won't work (mobile has no keyboard).
7. **Module status = ONE live aggregate button**, not four dots. Color rolls up
   presence of the **configured** module set: **Green** = all configured modules
   present · **Yellow** = ≥1 configured module absent · **Red** = all absent.
   Requires a per-module **"installed/configured" tick in the Network panel** so
   the button knows which modules to expect. (Supersedes the §3 "G/R/Y dots, one
   per module" sketch.)
   - *Reuse what exists:* the **Module configuration** surface already has a
     per-module enable (IMU / AutoSteer / GPS / Machine, each a red-✗ toggle;
     GPS shows in/out rates `← 12 / → ---`). The "configured set" = the modules
     enabled there; the Network panel surfaces that state + rates rather than
     introducing a new flag.
8. **Application Settings menu** — ✅ **Retired.** Help / About (incl. the app
   **version number**) move to the **Tools panel**; everything else has a new
   home (Directories/Language/Reset/Hotkeys → App Settings, NTRIP → Network,
   Log Viewer → Tools).
9. **Camera pad conventions** — ✅ Right=in, Up=overhead, tap=step / hold=repeat.
   *Shipped* (commit 1).

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
