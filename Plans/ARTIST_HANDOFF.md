# Artist Handoff — Designing AgValoniaGPS Pages in Inkscape

This guide is for the designer producing page/dialog layouts for AgValoniaGPS in
**Inkscape**. You draw the page as ordinary SVG; we run it through
[SvgTools](https://github.com/danielanywhere/SvgTools) (`ImpliedDesignToAvaloniaXaml`)
to get a labeled Avalonia XAML **starting point**, then hand-author the real,
responsive, data-bound component in the app.

## What this workflow is — and isn't

- **It is** a way for you to design in the tool you know (SVG/Inkscape) and hand us
  a layout we can read structurally: which control is which, what it's named, how
  things stack, what the fonts/sizes/colors are.
- **It is not** a one-click "drawing becomes the app" pipeline. The generated XAML
  is a **draft we throw away** after lifting the look and feel into AgV's own
  MVVM components. You never need to touch the XAML, and we never round-trip it
  back to you. Redraw freely; we re-generate from scratch each time.

Because of that, **your only job is a clean, well-labeled SVG.** Everything below
is about making that SVG translate cleanly.

## The golden rule

> **Work from the starter template, not from a blank canvas.**
> Copy [`Drawings/_TEMPLATE_DialogStarter.svg`](../Drawings/_TEMPLATE_DialogStarter.svg),
> rename it, and replace its contents. The converter reliably reproduces the
> patterns in that file, and tends to **drop or garble** anything structured
> differently.

In testing, two things made the difference between clean output and broken
output: (1) writing caption text as `<text><tspan>…</tspan></text>` (Inkscape's
real text structure) rather than plain `<text>…</text>`, and (2) grouping each
button's rectangle with its caption text. Get those wrong and labels merge into
the heading and **buttons disappear entirely**. The template already has both
right — which is why you should start from it.

## The conventions that drive the output

The converter reads three things off each Inkscape object: its **label**, its
**ID**, and (optionally) an **Anchor** attribute.

### 1. Object label = what control it becomes

Set an object's **label** (Inkscape: Object Properties → *Label*, or the XML
editor's `inkscape:label`) to the control's name:

| Label you set | Control you get |
|---|---|
| `Button` | `Button` |
| `TextBox` | `TextBox` |
| `Label` | `Label` / `TextBlock` |
| `ComboBox` | `ComboBox` |
| `CheckBox` | `CheckBox` |
| `RadioButton` | `RadioButton` |
| `ListView` / `ListBox` | list control |
| `Slider`, `ProgressBar`, `TabControl`, `GroupBox`, `MenuBar`, `StatusBar` | as named |

### 2. Group label = how children are laid out

To get **responsive layout** instead of fixed pixel positions, **group** the
related objects and set the *group's* label to a layout intent:

| Group label | Layout produced |
|---|---|
| `VerticalStackPanel` | `<StackPanel Orientation="Vertical" Spacing="10">` |
| `HorizontalStackPanel` | `<StackPanel Orientation="Horizontal" Spacing="10">` |
| `Grid` / `HorizontalGrid` / `VerticalGrid` | `<Grid>` |
| `GroupBox`, `ScrollPanel`, `SplitPanel`, `FlowPanel` | as named |

Nest them freely: a vertical stack of rows, where each row is a horizontal stack
of a label + input, produces exactly that structure. **This is the single
biggest quality lever** — grouped+labeled content comes out as clean nested
`StackPanel`s; ungrouped content comes out as brittle fixed-pixel positioning.

### 3. Object ID = the control's name (our shared vocabulary)

Set each object's **ID** (Inkscape: Object Properties → *ID*) to something
meaningful — `btnSave`, `txtAntennaHeight`, `cmbVehicleType`, `stackFields`.

- These become the `x:Name` we wire commands and bindings to.
- **Leave the default IDs (`rect8472`, `text12`) and they get replaced with random
  gibberish names** — useless to us. Always rename anything we'll reference.
- Use a consistent prefix convention: `btn` buttons, `txt` text boxes, `cmb`
  combo boxes, `chk` checkboxes, `lbl` labels, `lst` lists.

### 4. The form (window) title

Set the **layer's** label to `Form-<Title>` (e.g. `Form-Vehicle Settings`). The
part after the dash becomes the window/page title.

### 5. Edge anchoring

For controls that should pin to a side (e.g. OK/Cancel bottom-right), add an
`Anchor` attribute via the XML editor: `Anchor="Right, Bottom"`.

## Known rough edges (so you're not surprised)

These are limitations of the converter, not things you did wrong. We expect them
and clean them up on our side — but knowing them helps you give us better files:

- **Captions can get absorbed or dropped.** Free-standing `Label` text sometimes
  merges into a nearby heading, and control captions (a checkbox's text, a
  button's text) can go missing if the text isn't paired with its control the way
  the template does. **Pair caption text with its control inside the same group**,
  mirroring `_TEMPLATE_DialogStarter.svg`.
- **Multi-line / wrapping text** can come out as several separate text blocks at
  stepped positions rather than one flowing paragraph. Keep paragraph copy in a
  single Inkscape text object and expect us to re-flow it.
- **No behavior is conveyed** — only appearance and structure. Buttons have no
  actions, lists have no data. That's expected; we add all of that in MVVM.
- **Exact dimensions are a feature, not a bug.** Even where layout is fixed-pixel,
  the precise sizes/margins/colors/fonts are a useful spec for us to read off.

## A worked example

A "Vehicle Settings" dialog: a heading, a vertical stack of label+input rows, a
checkbox, and Save/Cancel buttons.

**In Inkscape:**

- Layer label: `Form-Vehicle Settings`
- Heading text — ID `lblHeading`, label `Label`
- Group `stackFields`, label `VerticalStackPanel`, containing one group per row:
  - Group `rowName`, label `HorizontalStackPanel`:
    - text ID `lblName`, label `Label` ("Vehicle Name")
    - rect ID `txtVehicleName`, label `TextBox`
  - …rows for `txtAntennaHeight`, `cmbVehicleType`, `chkArticulated`
- Save/Cancel: each a rect labeled `Button` (IDs `btnSave`, `btnCancel`) with
  `Anchor="Right, Bottom"`, paired with its caption text in the same group.

**Produced (excerpt):**

```xml
<StackPanel x:Name="stackFields" Orientation="Vertical" Spacing="10">
  <StackPanel x:Name="rowName" Orientation="Horizontal" Spacing="10">
    <TextBox x:Name="txtVehicleName" Width="250" Height="32"/>
  </StackPanel>
  <StackPanel x:Name="rowType" Orientation="Horizontal" Spacing="10">
    <ComboBox x:Name="cmbVehicleType"/>
  </StackPanel>
  <StackPanel x:Name="rowArticulated" Orientation="Horizontal" Spacing="10">
    <CheckBox x:Name="chkArticulated"/>
  </StackPanel>
</StackPanel>
```

Clean control types, clean names, responsive stacks — a solid draft for us to
build the real thing from.

## Checklist before you hand off an SVG

- [ ] Started from a copy of `Drawings/_TEMPLATE_DialogStarter.svg`.
- [ ] Layer labeled `Form-<Title>`.
- [ ] Every interactive object has a control label (`Button`, `TextBox`, …).
- [ ] Related objects are grouped, with the group labeled for layout
      (`VerticalStackPanel`, etc.).
- [ ] Every object we'll reference has a meaningful ID — no leftover `rect1234`.
- [ ] Caption text is grouped with the control it belongs to.
- [ ] Edge-pinned controls have an `Anchor` attribute.

---

*Tooling note (for devs): the converter is run via
`svgtools /action:ImpliedDesignToAvaloniaXaml /infile:<page>.svg /outfile:<page>.axaml`.
Generated `.axaml` is scratch — do **not** commit it; it is regenerated from the
SVG and overwritten on every run. SvgTools is AGPL-3.0; it is used only as a
dev-time utility and is never linked into or shipped with AgValoniaGPS.*
