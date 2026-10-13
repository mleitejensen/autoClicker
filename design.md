# Design System — "Deep Black / Neon Accent" UI

A portable design reference extracted from **Audio Game Chat Mixer**. Copy this
file into other projects to reproduce the same dark, high-contrast, data-dense
desktop UI look & feel — regardless of framework (WPF, Avalonia, Electron,
Web, etc.).

---

## 1. Design Philosophy

- **Near-black, almost monochrome base** — the UI reads as black/white with
  small, deliberate bursts of saturated accent color used only to carry
  meaning (group identity, state, status).
- **Flat & borderless by default** — no gradients, no skeuomorphism. Depth
  comes from a single soft drop shadow and subtle 1px borders, never from
  bevels or glows.
- **Information hierarchy via weight & size, not color** — primary content is
  white (`FontWeight: Light/Bold` extremes), secondary content is muted gray.
  Color is reserved for accents/semantics (success, danger, identity).
- **Tiny, bold, letter-spaced uppercase labels** for section/category tags
  (e.g. `GAME`, `CHAT`, `UNASSIGNED`, `HOTKEYS ON`) — mimics a technical/HUD
  aesthetic.
- **Generous rounded corners on cards (8–10px), tighter radii on controls
  (4–6px)** — soft but structured.
- **Everything is flat + hand cursor on interactive elements**; hover/press
  states are communicated by subtle border/background lightening only.

---

## 2. Color Palette

### Base / surface colors (near-black scale)

| Token          | Hex       | Usage                                              |
|----------------|-----------|-----------------------------------------------------|
| `BgDeep`       | `#000000` | Header bar, footer bar (outermost chrome)          |
| `BgBase`       | `#050505` | Window/app background                               |
| `BgPanel`      | `#0D0D0D` | Card/panel background (group panels, dropdown popups, context menus) |
| `BgCard`       | `#141414` | Nested item background (app rows, chips, inputs)   |
| `BgTrack`      | `#1C1C1C` | Slider track / inactive progress fill               |
| `BorderColor`  | `#1E1E1E` | Default 1px borders everywhere                      |

### Text colors

| Token          | Hex       | Usage                                  |
|----------------|-----------|------------------------------------------|
| `TextPrimary`  | `#FFFFFF` | Headlines, primary values                |
| `TextMuted`    | `#666666` | Secondary labels, inactive buttons       |
| `TextDim`      | `#2E2E2E` | Disabled text, empty-state placeholders  |

Additional ad-hoc grays used inline: `#888888` (tertiary text), `#555555`
(muted+disabled combo), `#CCCCCC` (overlay body text), `#333333` / `#444444`
(hover borders/dividers), `#2A2A2A` (default outline buttons).

### Accent / semantic colors

| Token             | Hex       | Meaning                          |
|-------------------|-----------|------------------------------------|
| Green (`Game`)    | `#4ADE80` | Identity accent 1 · "active/on" state |
| Blue (`Chat`)     | `#60A5FA` | Identity accent 2                  |
| Orange (`Media`)  | `#FB923C` | Identity accent 3                  |
| Gray (`Other`)    | `#888888` | Fallback/neutral identity          |
| Red (`Danger`)    | `#C0392B` (base) / `#E74C3C` (hover) / `#A93226` (pressed) | Mute / destructive state |

**Pattern:** each logical "group" or category in the app gets its own accent
color, used consistently for: a top accent stripe on its card, its label
text, its progress bar fill, and its glyph/icon. Pick 3–5 distinct hues from
the Tailwind-ish palette above (`#4ADE80` green, `#60A5FA` blue, `#FB923C`
orange, `#C084FC` purple, `#F87171` red, `#FACC15` yellow) when a new project
needs more than three categories.

### Shadow

```
Color: #000000
Opacity: 0.6
BlurRadius: 24
ShadowDepth: 2
Direction: 270 (straight down)
```
A single soft, large-radius, low-offset black shadow — used sparingly (e.g.
only on floating/overlay surfaces), not on every card.

---

## 3. Typography

- **Font family:** `Segoe UI` (system UI font — substitute the OS-native UI
  font on other platforms, e.g. `-apple-system`/`Inter`/`Roboto`).
- **Scale used:**
  | Size | Weight     | Usage                                   |
  |------|------------|------------------------------------------|
  | 9px  | Bold       | Micro tags/eyebrows (`GAME`, `UNASSIGNED`) — always UPPERCASE |
  | 10px | SemiBold   | Small buttons, status badges            |
  | 11px | Bold/Regular | Body-small, group name labels, muted meta text |
  | 12px | Regular    | Primary list/row text                   |
  | 13px | Bold/Light | Header wordmark (mixed weights for contrast, e.g. "AUDIO" bold + "MIXER" light) |
  | 28px | Light      | Hero numeric readout (e.g. volume %)    |
- **Letter-spacing / case:** tiny labels are always `ALL CAPS` with bold
  weight to read as tags/badges, paired with a thin `1px × 12px` vertical
  divider between wordmark segments.
- **Mixed-weight headers:** combine a `Bold` word with a `Light` word of the
  same size to create a wordmark (e.g. `AUDIO` + `MIXER`), separated by a
  thin vertical rule.

---

## 4. Shape & Spacing

| Element                     | Corner radius |
|------------------------------|----------------|
| Outer group/panel cards      | 10px           |
| Overlay/toast window          | 10px           |
| Item rows, chips, buttons, inputs, dropdown popups | 4–6px |
| Progress bars / slider track  | 2px (pill-like, 3px tall) |
| Status dot badges             | fully round (Ellipse) |

- **Borders:** 1px solid `BorderColor` (`#1E1E1E`) as the default separator
  for every card/control; lighten to `#444444`/`#3A3A3A` on hover/focus.
- **Padding conventions:** cards use `20,18` (outer) / `12,9` (item rows) /
  `16,7` (pill buttons) / `10,5`–`10,6` (inputs, chips).
- **Top accent stripe:** a 2px-tall full-width `Rectangle` at the very top of
  a card, filled with that card's accent color — a simple but effective way
  to color-code a card without tinting its whole background.
- **Dividers:** 1px `BorderColor` line to separate header/content sections
  within a card.

---

## 5. Core Components

### Cards / Panels
Flat `BgPanel` background, 1px `BorderColor` border, 10px corner radius,
`ClipToBounds` so the accent stripe's corners don't bleed. Inner content
margin ~20px. No shadow needed for in-layout cards (reserve shadow for
floating/overlay elements only).

### Buttons (pill / outline style)
- Default: `Background: Transparent`, `BorderBrush: #2A2A2A`, 1px border,
  6px radius, `Foreground: TextMuted`, uppercase SemiBold 11px text,
  `Cursor: Hand`.
- Hover: border → `#444444`, background → slightly lighter (`#141414`).
- Pressed: background → `#1E1E1E`.
- **Active/"on" state** (e.g. Muted, enabled toggle): solid fill with the
  semantic color (`#C0392B` red for mute) instead of outline, text turns
  white, label text itself changes (e.g. `MUTE` → `MUTED`).

### Status badges / pills
Small rounded (`4px`) bordered chip containing a tiny colored dot (`Ellipse`,
5×5px) + uppercase 10px SemiBold label. Off state: gray dot + dim gray text
on transparent background. On state: colored dot + colored text, background
tinted a very dark shade of the same hue (e.g. `#0D1F0D` bg / `#1A3D1A`
border for a green "on" badge) — never a bright fill.

### Sliders
Custom flat track: 3px tall pill track (`BgTrack` background, accent/fore
fill for the "filled" portion), 14×14px plain white circular thumb, no tick
marks, no outline. `OverridesDefaultStyle=True` to strip all native chrome.

### Progress bars (read-only meters)
Same 3px pill shape as sliders but non-interactive; `Opacity: 0.2` when the
represented channel is muted (instead of changing color) to show "present
but silenced."

### Dropdowns / ComboBox
Flat `BgCard` background, 1px `BorderColor`, 6px radius, custom small
triangular arrow glyph (a 3-point `Path` instead of a system arrow). Popup:
`BgPanel` background, 1px border, 6px radius, 4px padding, fade-in animation,
item hover → `#1E1E1E`, item selected → `#252525`.

### Context menus
`BgPanel`-ish `#0D0D0D` background, `#1E1E1E` 1px border, 6px radius, 4px
inner padding. Items: transparent by default, `#1E1E1E` on highlight, 4px
item radius, disabled items drop to `TextDim`.

### Chips / tags (compact item summary)
`BgCard` background, 1px `BorderColor`, 6px radius, horizontal padding
`12,6`–`12,9`, primary label (white, 12px) + secondary meta (muted gray,
11px) packed tightly, `Cursor: Hand`, right-click/context-menu affordance.

### "Hero" numeric readouts
Large (28px) `FontWeight: Light` number paired with a tiny bold colored
label above/beside it. When the represented value is in a "muted/off"
state: dim the number to `#333333` and apply strikethrough, then overlay a
small solid-color (danger red) rounded badge with bold white caps text
(e.g. `MUTED`) centered on top of it.

### Toasts / floating overlays
Small, rounded (10px), semi-transparent-black (`#E6000000`) panel with the
soft `CardShadow`, `AllowsTransparency=True`, no title bar, always-on-top,
click-through-ish HUD style. Body text 11–12px, light gray (`#CCCCCC`).

### Scrollbars
Minimal: 4px wide, no track, just a flat `#2A2A2A` rounded thumb — no
arrows, no buttons.

---

## 6. Interaction Patterns

- **Hover:** border lightens one step (e.g. `#2A2A2A` → `#444444`); never
  invert colors on hover, only on active/toggled state.
- **Active/toggled state:** inverts the pattern — outline button becomes a
  solid filled button in a semantic color, with white text.
- **Disabled/empty state:** drop to `TextDim` (`#2E2E2E`), keep layout
  stable (don't collapse space).
- **Muted/inactive data:** prefer **opacity reduction** (0.2–0.3) over
  hiding, so users see structure persists even when silenced/disabled.
- **Cursor:** `Hand` on every clickable surface, including whole cards/rows,
  not just buttons.

---

## 7. Applying This Elsewhere

1. Define the base neutral tokens first (`BgDeep/Base/Panel/Card/Track`,
   `TextPrimary/Muted/Dim`, `BorderColor`) as reusable style resources.
2. Pick 3–5 saturated accent colors (one per "category"/identity in your
   domain) and apply them *only* to: labels, top stripes, meters/progress,
   and small glyphs — never as large fills.
3. Reserve a single danger color (`#C0392B`/`#E74C3C`/`#A93226` for
   base/hover/pressed) for destructive/mute/error states, used as full solid
   fills (the one exception to "accents are never large fills").
2. Build buttons/badges/chips as outline-first, filled-on-active.
3. Use 10px radius for top-level cards, 4–6px for everything nested inside
   them.
4. Keep typography restricted to one font family, mixing only `Light` /
   `Regular` / `SemiBold` / `Bold` weights and a narrow set of sizes
   (9/10/11/12/13/28px).
5. Use uppercase, bold, tiny (9–10px) labels for any taxonomy/category tag.
6. Add shadow only to floating/overlay surfaces, never to in-flow cards.
