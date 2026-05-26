# Page-based navigation — concrete click comparison vs AgOpenGPS

Companion to [PLAN.md](PLAN.md). Purpose: counter the natural
"this feels like more clicks" reaction with **hard, verifiable click
counts** for real tasks.

## Why the objection happens

A long-time AgOpenGPS operator has *muscle memory* for the existing
path. A memorized 4-click sequence feels instant; an unfamiliar 1–2-click
one feels slower because it's new — not because it's longer. The fix is
to count clicks on both apps, side by side, and let the numbers speak.

**Structural reason pages-v2 wins:** AgOpenGPS buries each setting behind
`dropdown → form → tab → sub-tab`. pages-v2 makes each task a single
destination with everything on one surface, so reaching a control is one
tap and you never have to remember which tab it lives under.

## Ground rules for honest counts

- A "click" is a discrete button/tab tap. Scrolling a visible page is not
  a click.
- **Starting point matters and is stated.** AgOpenGPS launches to the
  **map**; pages-v2 launches to **Home**. For the *first setup task of a
  session* (when you configure the tractor) the fair comparison is from
  each app's launch state. If you bounce back from the map mid-session,
  pages-v2 costs +1 (the Home button) — also shown.
- ✅ = demonstrable in the current PoC. 🔶 = designed but the target page
  is still a stub (don't demo this one yet — it would undercut the point).

## Examples

### 1. Set antenna height ✅ demonstrable today

| | Path | Clicks |
|---|---|---|
| AgOpenGPS | Settings ▸ Configuration ▸ Vehicle ▸ Antenna → field | **4** |
| pages-v2 | Tractor tile → (Antenna section is on the page) | **1** from Home · 2 from map |

### 2. Change U-turn radius / extension ✅ demonstrable today

| | Path | Clicks |
|---|---|---|
| AgOpenGPS | Settings ▸ Configuration ▸ U-Turn → field | **3** |
| pages-v2 | Tractor tile → scroll to U-TURN section | **1** from Home · 2 from map |

### 3. Open a saved field 🔶 designed (Fields & Jobs page not built yet)

| | Path | Clicks |
|---|---|---|
| AgOpenGPS | Job ▸ Open ▸ pick field | **3** |
| pages-v2 | Fields & Jobs tile → pick field | **2** |

## How to demo (Examples 1–2)

Both pages exist and are clickable in the PoC, so a skeptic can count for
themselves:

1. On AgOpenGPS: from the map, count taps to the antenna-height field
   (Settings → Configuration → Vehicle → Antenna = 4).
2. On pages-v2: from Home, tap the **Tractor** tile — the Antenna section
   is already on screen (1).
3. Repeat for U-turn (3 vs 1).

The Tractor page is a single cardless scrolling surface (Geometry,
Antenna, U-Turn, Tram, Machine Module all visible), which is why several
settings that are separate tabs in AgOpenGPS's `FormConfig` collapse to
zero inter-setting navigation here.

## Notes / honesty caveats

- AgOpenGPS click counts are from static analysis of `FormGPS` +
  `FormConfig`; modal flows can vary slightly by configuration.
- The +1 "Home hop" when returning from the map is real; it's bounded and
  justified by the plan's guardrail (configuration happens while stopped;
  leaving the Moving Map page disengages autosteer). See PLAN.md.
- Implement and Fields & Jobs pages are stubs today, so their tasks are
  shown as *designed*, not demonstrated. Update this doc to ✅ as those
  pages land.
