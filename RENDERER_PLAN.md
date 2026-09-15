# Renderer plan: polish the current renderer, add alphaTab as a selectable alternative

This is the working plan for two related efforts requested together: (1) make the existing
GDI+ renderer/editor ("Classic") measurably better, and (2) add
[alphaTab](https://github.com/CoderLine/alphaTab) as a second, user-selectable rendering engine
for numbered notation, without removing Classic. It captures the research and spike results
from this session so the reasoning doesn't have to be rediscovered later.

## Why two renderers, not a straight replacement

A full swap to alphaTab is a large, risky, all-or-nothing bet: it requires migrating off
.NET Framework 4.7.2 first (alphaTab's WinForms/WPF packages only target `.net8.0-windows`+),
and alphaTab is a **read-only rendering + playback library** — it has no editing API, so this
app's editing UI (selection, drag, inline text editors, undo) has to be rebuilt on top of it
regardless. That's a lot of risk to take on before knowing whether users even prefer it.

Instead: introduce a renderer abstraction (mirroring the existing `IMidiOutput` pattern that
already lets this app swap `WindowsMidiSynthesizer` / `BassMidiSynthesizer` / `BassVstSynthesizer`
under one interface, user-selectable via `Edit → Audio Engine...`). Classic stays the default.
alphaTab ships as an opt-in, clearly-labeled alternative (`View → Renderer → alphaTab (experimental)`
or similar) until it's proven out, then the decision to switch the default — or keep both — is
made with real data instead of speculation.

## What we already know (research done this session)

- **Current renderer** (`JianpuRenderer.cs`, 2366 lines + `ScoreCanvas.cs`, 1672 lines): raw GDI+,
  no music font, hand-rolled pixel-math layout, no zoom/DPI abstraction. Hit-testing rebuilds the
  full layout per click and re-derives bounds with formulas duplicated from the draw path (risk of
  drift). Ornament/accidental/octave-dot stacking is a small bespoke constraint solver
  (`NoteTopAnnotationPlanner`). Full assessment in the session transcript; summarized in
  "Track A" below.
- **alphaTab** (`@coderline/alphatab`, MPL-2.0, v1.8.4): confirmed via direct source inspection
  (`packages/alphatab/src/rendering/NumberedBarRenderer.ts`, `NumberedNoteHeadGlyph.ts`) — genuine
  *not angka* rendering (`drawnLineCount = 0`, digits via `fillText`, stacked octave dots, dedicated
  dash/tie/slur/key-signature glyph classes), not staff notation with numbers stuck on. Confirmed
  visually via a rendered screenshot the user provided from alphaTab's own docs.
- **Feature-coverage spike** (Node.js + `@coderline/alphatab` + headless SVG render + Playwright
  screenshot, using a translated excerpt of `sample/Ode to Joy.jianpu`):
  - **Visually confirmed working**: digits/no-staff rendering, key signature, chord-marker text
    per beat (`beat.chordId` → `Staff.chords`), lyrics per beat (`beat.lyrics = [...]`), grace
    notes (rendered as a compact merged digit pair, correct jianpu convention).
  - **Structurally present, not visually confirmed in the spike** (API accepted the values without
    error; likely a test-harness SVG-compositing issue, not a product gap — see session transcript):
    ties (`Note.tieOrigin`/`isTieDestination`, dedicated `NumberedTieGlyph.ts`), fermata
    (`Beat.fermata`, correct alphaTex syntax is `{fermata <short|medium|long> <beats>}`), turn/mordent
    (`Note.ornament` enum has `Turn`/`UpperMordent`/`LowerMordent` — **not** `Trill`), trill
    (separate `Note.trillValue`/`trillSpeed` fields). All four have a default-enabled
    `NotationElement` flag (`EffectFermata`, `EffectNoteOrnament`, `EffectTrill`, ...).
  - **One real structural mismatch**: this app's `ChordMarker` can sit at a beat position with no
    underlying melody note (see the empty-text placeholder markers in `sample/Canon in D.jianpu`).
    alphaTab's `chordId` attaches to a `Beat`, so a chord-only position needs an empty/rest beat to
    carry it. Workable, not a blocker, but a real translation-layer detail, not a 1:1 mapping.
  - **Rendering API confirmed usable from .NET**: alphaTab exposes `rendering.BoundsLookup` /
    `NoteBounds` / `BeatBounds` — a real hit-testing API tied to its own render output. This matters
    a lot: it means the alphaTab renderer path does **not** need a hand-rolled geometry/hit-test
    model the way Classic does (see Track B, M4).
  - Spike scripts are throwaway (`/tmp` scratch, not committed) — treat the findings above as the
    durable artifact, not the scripts themselves.

## Track A — polish the Classic (GDI+) renderer

Independent of the alphaTab decision; do this regardless, since it fixes real bugs/debt in code
that stays load-bearing either way (Classic remains the default renderer for the foreseeable
future, and even under alphaTab this app still owns its own hit-testing for things alphaTab has no
concept of — chord markers, this app's specific ornament set, structured lyrics).

- **A1. Shared geometry model — note/gap positioning done.** Found and fixed a real bug while
  doing this: `JianpuRenderer` computed a note's on-screen X/width one way for drawing
  (`GetNoteDrawBounds`, scaled by `MeasureLayout.MelodyScale` with a min-width floor and a
  "stretch the last note to fill the measure" special case) and a *different*, unscaled way for
  hit-testing (`HitTestMelodyRow` walking raw `GetNoteOffset`/`GetNoteWidth`). Because the app's
  normal (non-paginated) layout sizes every measure's box to exactly fit its own content minus a
  fixed 4px margin, `MelodyScale` ends up marginally below 1.0 for **any** non-empty measure — so
  this wasn't a rare dense-measure edge case, it affected click accuracy on ordinary measures all
  the time, worst near the last note in a measure (the stretch case). Fixed by moving the scaled
  formula onto `MeasureLayout` itself (`GetNoteDrawBounds`/`GetNoteBounds`, now the single source
  of truth) and routing both drawing and `HitTestMelodyRow` through it; also found and collapsed a
  third independent copy of the same formula in `ChordMarkerLayout.cs`; made `GetInsertOffset`
  (gap/caret positioning) scale-consistent the same way. Ties (`TryGetTieGeometry`) were already
  correctly shared between draw and hit-test — no change needed there. 5 new regression tests in
  `NoteHitTestingTests.cs`.
  **Still open**: stop rebuilding the *entire* layout on every mouse click/hover (a perf concern,
  not a correctness one — deferred, candidate to fold into A3 since drag/hover is where it'd
  actually matter).
- **A2. Zoom/DPI.** Done. Added `CanvasZoom` (`JianpuEditor/Controls/CanvasZoom.cs`), a small,
  independently unit-tested (`CanvasZoomTests.cs`) class holding the scale factor (0.5x-2.5x,
  0.25 step) and the `ToScreen`/`ToLogical` conversions. `JianpuRenderer`/`MeasureLayout`/
  `PlaybackLayout`/`ChordMarkerLayout` were deliberately left untouched — they keep operating in
  unscaled "logical" pixels exactly as A1 established. All the change is at `ScoreCanvas`'s
  render/input boundary: `OnContentPaint` wraps the (still logically-rendered) score bitmap blit
  and playback-head draw in a `Graphics.ScaleTransform`; every mouse handler converts its
  screen-space point to logical via `_zoom.ToLogical` before hit-testing/dragging; every method
  that positions a WinForms overlay controls (`_inlineEditor`/`_headerEditor`/`_chordInlineEditor`
  bounds, `GetPlaybackHeadBounds`'s `Invalidate` rect) converts its logical bounds to screen via
  `_zoom.ToScreen` before handing them to WinForms. `ZoomIn`/`ZoomOut`/`ResetZoom` are exposed as
  public `ScoreCanvas` methods, wired to `View → Zoom In/Out/Reset Zoom` (Ctrl+/Ctrl-/Ctrl+0) in
  `MainForm`, plus Ctrl+MouseWheel on the canvas itself. **Needs manual interactive verification**
  before merge (click accuracy, drag, chord/header inline-editor positioning, and scrolling at
  non-default zoom) — this is WinForms mouse/paint behavior that unit tests can't cover and CI only
  proves compiles/existing tests still pass, not interactive correctness.
- **A3. Generalize drag — done.** Chord-marker repositioning and playback-head seek used to be two
  independent bespoke `MouseDown/Move/Up` state machines in `ScoreCanvas.cs`, each with its own
  mouse-capture bookkeeping. Replaced with one reusable mechanism: `OnContentMouseDown` registers a
  small `DragHandler` (a `Move`/`Release` delegate pair tagged with a `DragKind`) for whichever
  gesture started, and `OnContentMouseMove`/`OnContentMouseUp` dispatch through it generically
  instead of each duplicating the same capture/branch logic. `DragKind` exists only so code outside
  the gesture (`HidePlaybackHead`) can still tell which drag, if any, is active, without reaching
  into gesture-specific state. Also folded in A1's flagged perf item: chord-marker dragging used to
  rebuild the *entire* score's measure layout on every mouse-move tick just to find the one measure
  it needed; layout doesn't change mid-drag (moving a chord marker's beat doesn't affect measure
  geometry), so it's now captured once when the drag begins. Behavior is unchanged byte-for-byte
  (verified by compiling the full `ScoreCanvas`/`JianpuRenderer`/`Models` graph and diffing the
  generalized dispatch against the original two state machines) -- this was a mechanical
  refactor, not a features change. New drag gestures (e.g. note pitch/duration drag, matching
  `ROADMAP.md`'s "live note preview under the cursor" item) now only need a new `BeginDrag(...)`
  call site, not a new state machine.
- **A4. Vertical-stacking generalization.** `NoteTopAnnotationPlanner`'s band-based overlap
  avoidance for accidentals/octave-dots/ornaments works but is tuned by hand; make it a proper
  "avoid collision" pass rather than fixed Y-bands, informed by how alphaTab's own v1.8 changelog
  described reworking the identical problem for its numbered-notation dot/overflow calculations.

**Milestones**: A1 → A3 → A2 → A4, in that order (A1 unblocks A3's reuse story; A2 and A4 are
independently schedulable after A1).

## Option: SkiaSharp as the Classic renderer's drawing backend

A third path, distinct from both "polish GDI+ in place" (Track A) and "adopt alphaTab" (Track B).
No literal drop-in replacement for GDI+ exists, but SkiaSharp is the closest realistic one: it
targets `net472` (via netstandard2.0, no forced .NET 8 migration the way alphaTab requires),
integrates into WinForms as a custom control, and keeps this app's own layout/geometry model and
data model entirely intact — unlike Track B, there's no score-model translation and no "read-only
renderer, rebuild the editing UI from scratch" tradeoff. It's a backend swap under the code Track A
already owns, not a replacement for Track A's work.

**What it would fix**: real anti-aliased vector rendering, better text shaping (matters for CJK
lyrics and any future custom Jianpu glyph font), GPU-accelerated redraws, and — the most concretely
valuable piece — a real vector PDF export path. `RenderPdfPageToBitmap` currently rasterizes each
page to a `Bitmap` before embedding it in the PDF; SkiaSharp can draw directly to a PDF canvas,
which would mean crisper printed output, smaller file sizes, and real selectable/searchable text
in exported PDFs instead of a rasterized image of text.

**What it would *not* fix**: A2's zoom/DPI problem. That's not a rendering-backend limitation —
GDI+ already supports scaling via `Graphics.ScaleTransform`. The actual problem is that
`MeasureLayout`'s note widths/offsets and `JianpuRenderer`'s magic-number pixel offsets are
hardcoded `int`s with no scale factor threaded through, including in hit-testing (which takes raw
mouse pixels). That refactor (A2) is required either way, so it should happen before, not after, a
SkiaSharp port — porting ~2300 lines of `Graphics.DrawXxx` calls to `SKCanvas` equivalents while
the layout math is simultaneously being made scale-aware would mean fighting two changes at once
instead of one.

**Cost**: porting every `Graphics.DrawString`/`DrawLine`/`FillEllipse`/`DrawBezier`/etc. call in
`JianpuRenderer.cs` to `SKCanvas` equivalents — a real, nontrivial, but self-contained effort (no
new data model, no framework migration, no editing-UI rebuild).

**Sequencing recommendation**: A2 (zoom/DPI plumbing) first, on GDI+, since that work is needed
regardless of backend and touches the same layout code a SkiaSharp port would touch. A SkiaSharp
port is then a reasonable "Track A5" — or its own separately-scoped effort — once the layout math
is scale-aware, so the port only has to change *how* pixels get drawn, not *where*.

## Track B — alphaTab as a selectable renderer

- **B0. Renderer abstraction.** Introduce an interface (name TBD, e.g. `IScoreRenderer`) that
  `ScoreCanvas`/`MainForm` render through, with `JianpuClassicRenderer` (today's `JianpuRenderer`,
  adapted to the interface) as the default implementation. This is the only piece Track A and
  Track B both touch — land it first, behind no visible behavior change, before either track's
  further work.
- **B1. .NET 8 migration.** Retarget `JianpuEditor.csproj` from `net472` to `net8.0-windows` with
  `UseWindowsForms=true` (the same compatibility path already used for `JianpuEditor.Tests` — see
  the earlier CI fix in this repo's history). Verify every current dependency
  (`ManagedBass`/`ManagedBass.Midi`/`ManagedBass.Vst`, `PDFsharp`, `Newtonsoft.Json`,
  `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`) on net8.0-windows. Update
  `installer/JianpuEditor.iss`, `scripts/build-portable.ps1`, and both GitHub Actions workflows
  for the new TFM and runtime deployment story (self-contained vs. framework-dependent — .NET 8
  isn't preinstalled on Windows the way .NET Framework is, so the installer/portable builds need
  to decide how the runtime gets onto the user's machine). This is mechanical but not small —
  budget real time for dependency verification and installer rework, not just the TFM edit.
- **B2. alphaTab data-model translator.** `JianpuScore` → alphaTab `Score`/`Track`/`Staff`/`Bar`/
  `Voice`/`Beat`/`Note`, covering: pitch/octave (already validated), chords (including the
  empty-beat workaround for chord-only positions), lyrics, ties, grace notes, and — pending the
  fermata/turn/trill wiring being nailed down properly (unlike the spike's rushed attempt) —
  ornaments. Scope explicitly: this app's `OrnamentType.Glissando/Staccato/Accent/Tenuto/RepeatStart/
  RepeatEnd/Segno/Coda/Custom` have no confirmed alphaTab equivalent yet and may need to render as
  a text annotation overlay rather than a native alphaTab glyph — verify per-type, don't assume.
- **B3. WinForms embedding.** Host alphaTab's native `net8.0-windows` WinForms control
  (`AlphaTabApi`/`AlphaTabApiBase`) inside a panel, feed it the translated score, re-render on
  every edit (the same "live preview" pattern used by comparable tools like `jpeditor`'s
  CodeMirror+SVG-preview, researched earlier this session).
- **B4. Hit-testing via `BoundsLookup`.** Wire selection clicks through alphaTab's own
  `NoteBounds`/`BeatBounds` API instead of hand-rolled geometry — this is the concrete payoff for
  not building a Classic-style hit-test layer for this renderer.
- **B5. Editing UI on top.** Selection highlight overlay, inline editors (title/lyrics/chords)
  repositioned against alphaTab's bounds, and — the open-ended part — drag interactions. Likely the
  single biggest remaining unknown in the whole plan; alphaTab gives no help here since it's
  display-only.
- **B6. Renderer picker + parity pass.** `View → Renderer → Classic / alphaTab (experimental)`
  (mirrors `Edit → Audio Engine...`'s UX). Run the full `sample/` library and a manual pass over
  this app's own feature set (ties, chord markers, all ornament types, structured lyrics,
  multi-note chords, PDF/MIDI export unaffected either way) through both renderers side by side
  before considering alphaTab anything more than experimental.

**Milestones**: B0 → B1 → B2 → B3 → B4 → B5 → B6. B1 is the one hard gate (nothing alphaTab-shaped
can land before it); B2's per-ornament-type verification is the other place likely to expand scope.

## Open questions to resolve before starting Track B in earnest

1. Self-contained vs. framework-dependent .NET 8 deployment for the installer/portable builds —
   affects download size and whether end users need anything preinstalled.
2. Exact alphaTex/object-model wiring for fermata/turn/trill (the spike's harness issue needs a
   clean re-run, not just a source-level "it should work").
3. How much of B5 (editing UI) is worth building before even knowing if users prefer alphaTab's
   output — consider a read-only "preview only" milestone (render + click-to-seek during playback,
   no editing) as a cheap way to get real feedback before committing to the full B5 rebuild.

## Explicit non-goals for now

- Deleting Classic. It stays the default and Track A's investment isn't wasted if Track B never
  ships.
- Full parity with alphaTab's guitar-specific features (tab, bends, palm-mute, etc.) — irrelevant
  to this app.
- Deciding the eventual default renderer before B6's parity pass produces real evidence.
