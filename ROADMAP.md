# Roadmap

This is a working list of planned features for JianpuEditor, kept as plain notes rather than GitHub issues for now.

## Now: instrument playback (top priority)

**Status: Phase 1 and Phase 2 implemented.**

Until now, playback always used whatever General MIDI patch 0 (Acoustic Grand Piano) the system's default MIDI device happened to fall back to — there was no instrument selection anywhere in the app (playback or MIDI export), and sound quality/timbre depended entirely on whatever GM device Windows happened to provide.

- **Phase 1 (done):** Per-score instrument selection.
  - `JianpuScore` gained `MelodyInstrument` / `ChordInstrument` (General MIDI program numbers, 0-127), saved/loaded with the score, defaulting to 0 for full backward compatibility with existing files.
  - `Edit → Instruments...` (and a toolbar button next to Play/Stop) opens a dialog with two dropdowns listing all 128 GM instrument names.
  - Both **live playback** (`ScorePlaybackService`) and **MIDI export** (`MidiExportService`) now send a Program Change message for the melody and chord channels before notes play, so exported `.mid` files sound the same as in-app playback.
  - Instrument changes go through the existing undo/redo command stack, like other header edits.
  - `WindowsMidiSynthesizer` was extracted behind a new `IMidiOutput` interface so playback logic is unit-testable without a real MIDI device.
- **Phase 2 (done):** Consistent sound quality via a bundled SoundFont, replacing the OS's default synth for playback.
  - Playback now goes through **BASSMIDI** (`JianpuEditor/Services/BassMidiSynthesizer.cs`, via the `ManagedBass`/`ManagedBass.Midi` NuGet wrappers) loading a bundled SoundFont (`JianpuEditor/Resources/Soundfonts/GeneralUser-GS.sf2`, permissively licensed), so playback sounds the same on every machine instead of depending on whatever GM device Windows happens to ship.
  - Falls back to the old `WindowsMidiSynthesizer` (system MIDI mapper) automatically if BASSMIDI/the SoundFont fails to initialize for any reason.
  - **Original plan was MeltySynth (pure C#, MIT) + NAudio**, but every MeltySynth release targets `netstandard2.1`+, which .NET Framework 4.7.2 (this app's TFM) cannot consume at all — not a licensing issue, a hard compatibility wall. Switched to BASSMIDI (native DLL via P/Invoke, framework-agnostic) instead.
  - **Licensing note:** BASS/BASSMIDI is free only for individual, non-commercial use — see `JianpuEditor/Native/NOTICE.md` before distributing a commercial fork. `bass.dll`/`bassmidi.dll` (x86 + x64) are bundled directly since they can't be fetched via NuGet/package restore.
  - MIDI export is untouched (still raw MIDI file writing, unaffected by which engine renders it).
- **Phase 3 (done, VST2 only):** VSTi hosting via **BASSVST** (`JianpuEditor/Services/BassVstSynthesizer.cs`, via `ManagedBass.Vst`).
  - `Edit → Audio Engine...` lets you point at a VST2 instrument DLL to use for both melody and chords instead of the bundled SoundFont; this is a global app preference (`%LOCALAPPDATA%\JianpuEditor\settings.json`, alongside Dark Mode), not saved per-score — a plugin path is machine-local and would break score portability if saved into the `.jianpu` file the way the GM instrument numbers are. Takes effect after restarting the app (swapping the whole playback engine live isn't supported).
  - `BassVst.ChannelCreate` creates a real audio-generating channel driven by MIDI events (`BassVst.ProcessEvent`, same event-type values as `BassMidi.StreamEvent`) — turned out to need **no audio-engine rewrite at all**, since BASS itself still owns the audio callback loop; `BassVstSynthesizer` implements the same `IMidiOutput` interface as the other two engines. The originally-assumed "needs a from-scratch real-time audio engine" concern only applies to hosting VST without going through BASS.
  - **VST2 only, not VST3** — BASSVST's `ChannelCreate` takes a single DLL file path, matching VST2's architecture; VST3 is a different bundle format/ABI. Also means no GPLv3/VST3-SDK licensing question, since VST2 hosting doesn't need Steinberg's SDK at all.
  - Bundles one more native library, `bass_vst.dll` (x86 + x64), same as `bass.dll`/`bassmidi.dll` — see `JianpuEditor/Native/NOTICE.md`.
  - **Not covered:** VST *effect* plugins (reverb, EQ, etc.) — BASSVST handles those completely differently (`ChannelSetDSP`, attached to an already-existing audio stream) from instrument plugins (`ChannelCreate`, generates audio from nothing). Effects would be a separate feature using that other API.

## Indonesian notasi angka completeness (gap analysis + plan)

**Status: phase 3's bug-fix half, phase 4 (all of it except Glissando), the discrete-levels half
of phase 5 (dynamics), phase 6 (breath marks), and phase 7 (Segno/Coda, repeat bar lines, and
volta brackets -- the visual halves of all three) are done. Bar line types (Single/Double/Final/
RepeatEnd/RepeatStart) and volta brackets were bundled into phase 7 as originally scoped there.
Phase 10 (pickup measure verification) is also done. Phase 2's `AccidentalKind.Natural` +
manual accidental entry piece is also done (see the note under phase 2) -- the accidental slash
convention itself (the one genuinely `NotationStyle`-gated item, and phase 1's `NotationStyle`
foundation it depends on) is still not started. Everything else below is still not
started**, including phases 1-2's remaining slash-convention work -- see the note under phase 2
for why that one turned out to be more involved than it looked. Phase 10 also surfaced a separate,
real gap in MIDI import (pickup measures aren't preserved) -- see the note under phase
10. A pre-existing ornament/octave-dot rendering collision (unrelated to any single phase, found
while visually verifying the work above) is also fixed -- see the note right after phase 10 about
this sandbox's new Mono+libgdiplus visual-verification capability.

Researched Indonesian *notasi angka* (Indonesian numbered/jianpu notation — rules, symbols,
conventions) against what `JianpuEditor` actually implements. Full gap list and phased plan below.

### Hard constraint: never change Chinese/Western jianpu by default

Almost none of the gaps below are actually "Indonesian vs Chinese" style differences — they're
just plain missing notation elements (dynamics, breath marks, repeat/coda navigation, multi-verse
lyrics, natural signs, SATB) that both traditions use identically. Adding them as new, empty-by-
default model fields cannot change how an existing score renders, because a score that never sets
them has nothing to render — this is the same reasoning that already makes `Ornaments`, `Ties`,
and `ChordMarkers` safe additions today.

**Exactly one item is a genuine regional style difference: the accidental symbol.** Indonesian
notasi angka suffixes a slash (`1/` = sharp, `7\` = flat); this renderer currently prefixes `#`/`b`
(Western/Chinese staff-notation style) via `JianpuPitchCodec.GetAccidentalMark`. Switching the
default would visibly change every existing Chinese/Western-style score's accidentals. This one
gets gated behind a persisted `AppTheme` setting, the same pattern `UnderlinesAbove` already uses
for the underline-position difference (Indonesian: above the melody row; Chinese/Western: below,
the existing default). Recommendation: generalize both into one `AppTheme.NotationStyle` enum
(`Chinese` default, `Indonesian`) rather than accumulating independent booleans, migrating the
existing `UnderlinesAbove` setting into it on load (old `true` → `Indonesian`) so nobody's saved
preference is silently reset.

Every phase below must include, as an acceptance check: an existing score with none of the new
fields set, and `NotationStyle = Chinese` (the default), renders pixel-identical to today.

*A second research pass (another agent, cross-checked against the code rather than taken at face
value) confirmed the findings below and added two real items: the tie/slur bug and volta
brackets. Its Kepatihan section (gamelan cipher notation — a different notation system for a
5/7-tone non-Western-tempered orchestra, not general vocal/choral notasi angka) is out of scope
here and intentionally excluded.*

### Gaps identified

| Gap | Current behavior | Indonesian convention | Gated by NotationStyle? |
|---|---|---|---|
| Accidental symbol | `#`/`b` prefix (`JianpuPitchCodec.GetAccidentalMark`) | `/` (kres) and `\` (mol) suffix | **Yes — the only style-gated item** |
| Natural/pugar sign | `AccidentalKind` has only `None`/`Sharp`/`Flat` | Explicit natural sign; modern typesetting increasingly just uses `♮` rather than a slash variant, so this one doesn't need a NotationStyle branch at all — same glyph either way | No (additive) |
| **Tie tool accepts different-pitch notes — latent playback bug, not just a missing feature** | `TieEditorViewModel.TryCompleteTie` validates only that the end note comes after the start note — never that the pitches match. `ScoreMidiSchedule.BuildTieEndSet` then suppresses the end note's own `NoteOn` unconditionally, so a "tie" drawn between two *different* pitches silently drops the second note's actual pitch during playback/export instead of sounding it | A curved line between same-pitch notes is a tie (sustain); the identical-looking curve between *different* pitches is a slur (legato phrasing, both notes still sound) — two distinct marks that only look alike | No (additive/bugfix; the pitch-match validation is a bug fix independent of anything else here) |
| Staccato / Accent / Tenuto / Glissando | `OrnamentType` declares these four values; zero code references them anywhere (checked rendering, editing, playback) | Standard articulation marks, same in both traditions | No (additive) |
| Repeat bar lines, double/final bar lines, volta brackets (1st/2nd endings) | No bar-line-type concept exists on `JianpuMeasure` at all | Single/double/final/repeat bar lines, plus numbered bracket endings for a repeated section's differing last measure(s) | No (additive) |
| D.C. / D.S. / Coda / Segno navigation | `OrnamentType.RepeatStart/RepeatEnd/Segno/Coda` declared, zero implementation | Standard navigation marks | No (additive) |
| Dynamics (p, f, mf, cresc., dim.) | No model field anywhere | Standard dynamics, placed below the melody row | No (additive) |
| Breath marks | Not modeled | Apostrophe-like mark after a note, common in choir/hymn notasi angka | No (additive) |
| Multi-verse lyrics | `JianpuMeasure.LyricText` is a single string | Songbooks stack 2-4 numbered verses under one melody | No (additive) |
| SATB / multi-voice | One `MelodyNotes` list per measure, period | Hymnals (*Kidung Jemaat* etc.) print 4 independent voices sharing one lyric/measure grid | No (additive, but the biggest structural change by far) |
| Pickup measure (birama gantung) | No explicit flag; unverified whether a short first measure "just works" | A deliberately partial first measure | No (additive, needs verification not new modeling) |

### Phased plan

1. **`NotationStyle` setting** (foundation for everything gated). Add the enum to `AppTheme`,
   migrate `UnderlinesAbove`, re-point the existing underline-position branch at it. No visible
   change for anyone currently on the default.
2. **Accidental slash convention — turned out bigger than it looked, not started.** The naive
   plan (branch `JianpuPitchCodec.GetAccidentalMark`/`GetPitchDisplayText` on `NotationStyle`) only
   covers one of *two* separate accidental rendering paths in `JianpuRenderer`. The other,
   `DrawCompactAccidentalMark`, positions the mark using a dedicated layout band
   (`NoteTopAnnotationLayout.AccidentalX/AccidentalY`, computed in
   `NoteTopAnnotationPlanner.PlaceAccidental`) that assumes a compact mark to the upper-left of
   the digit, at a different vertical position than the digit itself, with octave-dot placement
   already computed to dodge it on that side. A true suffix-slash layout needs a second band
   variant (mark to the right, at the digit's own baseline) and re-deriving the octave-dot
   collision math for that case — real layout work. Recommend doing this as its own PR, with a
   real look at the on-screen result before merging, rather than bundled with lower-risk items.
   **Update: this sandbox can now partially self-verify GDI+ output** (see the note at the end of
   this phased plan) via Mono+libgdiplus, which lowers but doesn't eliminate the risk here — still
   worth its own carefully-reviewed PR given the collision-math complexity, but "no way to check at
   all" is no longer accurate. `AccidentalKind.Natural` doesn't touch the suffix-vs-prefix
   positioning question at all, so it's landed separately and first, described below.
   **`AccidentalKind.Natural` + manual sharp/flat/natural entry — done.** Confirmed the previously-
   noted prerequisite gap first: `AccidentalKind.Sharp`/`Flat` had zero manual entry UI anywhere in
   the app (`JianpuPitchCodec.SetAccidentalPitch` was never called outside its own definition and
   tests) -- the only code path that ever produced an accidental note was `MidiImportService`
   reading a chromatic pitch out of an imported file. Fixed both gaps together rather than shipping
   a glyph nothing could ever attach: a new `NoteEditorViewModel.SetAccidental(AccidentalKind)`
   (mirroring `SetOctave`'s exact shape -- toggles the selected note(s) off back to `None` on a
   second click of the same value, or sets the *pending* note's accidental when nothing is selected
   so it carries onto the next digit typed) wired to a new Edit > Accidental menu (Sharp/Flat/
   Natural/None). `SetAccidentalPitch` (previously only handling Sharp/Flat) now also assigns the
   plain integer pitch for `None`/`Natural` so it's a complete "set pitch from degree + accidental"
   function usable from all four call sites (`AddNote`'s selected-note and new-note-entry branches,
   `AppendNote`, and the new `SetAccidental`) -- fixing, as a side effect, a latent pre-existing
   inconsistency where retyping a digit over an already-accidented note left a stale `Accidental`
   flag paired with a mismatched integer `Pitch`.
   **The `♮` glyph itself is hand-drawn as vector strokes, not the Unicode natural-sign character
   (U+266E).** Tried the Unicode character first and caught a real rendering bug with this
   session's Mono+libgdiplus visual-verification harness: libgdiplus's Arial substitute doesn't
   carry a proper glyph for that code point and rendered an unrecognizable fallback mark instead of
   a natural sign. Rather than gamble on whether real Windows GDI+ has better luck, `DrawCompactAccidentalMark`
   special-cases `Natural` and draws two vertical strokes plus two thick diagonal connectors
   directly (`DrawNaturalSignGlyph`) -- guaranteed to look the same on every platform, confirmed
   visually via the same harness (including alongside an octave dot, to confirm no collision with
   `NoteTopAnnotationPlanner`'s existing dodge-the-accidental math). `GetAccidentalMark` still
   returns the Unicode character for the non-compact legacy layout path (`ScoreLayoutOptions.
   Default`), which nothing in the real app actually renders through -- only `Editor`/`PdfExport`
   (both `CompactAccidentalGlyphs = true`) are used on screen or in PDF export, and both go through
   the fixed vector path.
3. **Tie/slur pitch bug: the validation half is done; slur support is not.** Added a same-pitch
   check to `TieEditorViewModel.TryCompleteTie` (rejects with a status message and treats the
   mismatched note as a new start candidate, mirroring the existing "must come after" rejection
   flow) — the tie tool can no longer silently drop a note's pitch during playback. Covered by two
   new regression tests. Real slur support (a `JianpuSlur` list shaped like `JianpuTie`, no pitch
   constraint, rendering-only) is still future work.
4. **Wire up the dead `OrnamentType` articulation values — Mordent, Staccato, Accent, and Tenuto
   done; Glissando not started.** Added the missing Mordent button (ribbon + Edit menu + context
   menu, plus a new `RibbonIcon.Mordent` glyph) — its backend (glyph layout, playback expansion)
   already existed from earlier work, so this was pure UI wiring. Staccato/Accent/Tenuto needed
   the full pattern: a ribbon button + Edit-menu item + context-menu item each (matching the now
   eight-strong Grace/Trill/Turn/Mordent/Fermata/BreathMark/Staccato/Accent/Tenuto set), a new
   placeholder glyph each (`stac`/`acc`/`ten`, following this codebase's existing text-abbreviation
   convention rather than real notation symbols -- see the class doc comment on
   `OrnamentService.GetPlaceholderGlyph`), and a new playback effect each in
   `OrnamentPlaybackService`: staccato shortens the sounding duration by half (leaving a gap before
   the next note, since the next note's start time comes from the *nominal* duration, not the
   shortened one), accent and tenuto boost velocity (accent more than tenuto), each clamped to the
   MIDI max. All three reuse the exact same generic ornament-band positioning Trill/Turn/Mordent
   already use (registered into `NoteTopAnnotationPlanner`'s existing `HasCenterOrnament` stacking
   switch so they correctly clear any accidental/octave-dot glyph on the same note) rather than any
   new anchor-position math, and the three playback effects are applied as one uniform post-process
   over whatever events the note already expanded into (a plain note, or every segment of a
   trill/turn/mordent/grace note) instead of a new branch in the ornament-expansion if/else-if
   chain -- so none of this carries the layout-band risk flagged elsewhere in this phase. Glissando
   (pitch-bend between notes) is the one genuinely harder case -- left for its own separate step.
5. **Dynamics markings — the six discrete levels (pp/p/mp/mf/f/ff) are done; hairpins
   (cresc./dim.) are not.** New `DynamicMarking` model (`Text` + `NoteIndex`/`BeatPosition`,
   mirroring `JianpuOrnament`'s note-index resolution, but with "at most one marking per note"
   toggle/replace semantics instead of ornaments' additive semantics — a note can't be both piano
   and forte). UI is a "select a note, click a level" toolbar/menu/context-menu button set
   (`DynamicsEditorViewModel`), not chord-marker-style click-to-add-at-a-beat/drag/inline-text-edit
   — deliberately simpler than that richer interaction model to avoid new canvas hit-testing code,
   since only a fixed vocabulary of levels is supported (no free-text dynamics like "molto
   espress." in this version).
   **Rendering got its own new row directly below the melody row** (the roadmap's original
   ask), not the ornament band above the note — this needed expanding `StaffBlockHeight` (a
   constant used everywhere: block bounds, hit-testing, PDF pagination content height, block
   stacking) to insert a fourth `DynamicsRowHeight` band between the melody and the existing
   "Secondary" (chord marker) row, touching every place that assumed exactly three rows (both
   copies of the row-label drawing, the main `HitTest` dispatcher's row-band math, and the
   `GetSecondaryRowTop`/`GetLyricRowTop` helper chain). This is the one item in this whole gap
   list that carries the same category of cross-cutting layout risk flagged for the accidental
   slash-convention work (phase 2) — done here only because the user explicitly asked for the
   real row over the lower-risk alternative (reusing the ornament band) after being shown the
   tradeoff. Locked in with a dedicated geometry test
   (`StaffRowLayoutTests.RowTops_StackWithoutOverlapOrGapDrift`) asserting every row boundary
   lines up with no gap/overlap drift, on top of the usual render-to-bitmap smoke test — but the
   actual on-screen appearance still hasn't been visually confirmed on a real Windows machine.
   **Playback**: a velocity-scaling pass in `ScoreMidiSchedule.BuildMelodyNotes` tracks "the
   current dynamic level" across notes and measures (`DynamicMarkingPlaybackService` maps each
   level to a fixed velocity 33-112), replacing the constant `MelodyVelocity` from the marked note
   onward until the next marking or the end of the score. A score with no dynamic markings
   schedules byte-identical output to before this existed. Applies to the melody part only, not
   chord markers, in this version.
   **Hairpins (cresc./dim., a gradual ramp between two points) are a separate, harder follow-up**:
   they need continuous interpolation between two markers rather than this phase's step-function
   level changes, plus a rendering shape (an actual `<`/`>` wedge, not text) — scoping that as its
   own piece of work rather than folding it in here.
6. **Breath marks — done.** Added `OrnamentType.BreathMark`, folded into the existing per-note
   ornament list/UI pattern (ribbon + Edit menu + context menu, same as Mordent). Unlike every
   other ornament here, it's anchored just *after* the note's right edge instead of centered above
   it (`JianpuRenderer.GetOrnamentAnchorX` / `NoteTopAnnotationLayout.GetOrnamentAnchorX`, one new
   branch each) since that's where a breath mark actually sits in notasi angka/jianpu sheet music
   — but it deliberately reuses the existing ornament stacking band for its Y position and doesn't
   participate in `NoteTopAnnotationPlanner`'s accidental/octave-dot collision math at all, so it
   carries none of the layout risk flagged under phase 2. Visual-only, no playback/MIDI-export
   effect (a version that inserts a micro-rest is a possible follow-up, not v1).
7. **Repeat bar lines, volta brackets, and D.C./D.S./Coda/Segno navigation.** Three sub-parts:
   - **Segno/Coda markers — done.** Wired up via the exact same ribbon/Edit-menu/context-menu/
     glyph pattern as Staccato/Accent/Tenuto, using the `OrnamentType.Segno`/`Coda` values that
     already existed. These two are genuinely note/beat-anchored point symbols in real notation
     (unlike a repeat bar line, which decorates the barline itself, spanning the staff height) —
     see the note below on why `RepeatStart`/`RepeatEnd` were deliberately *not* wired the same
     way. Visual-only: no playback/MIDI-export effect, since jumping to a Segno/Coda isn't
     performed (see the third bullet below).
   - **Repeat bar lines (visual) — done; volta brackets still not started.** Added `BarLineType`
     (`Single`/`Double`/`Final`/`RepeatEnd`) and `IsRepeatStart` (bool) to `JianpuMeasure`, exactly
     the field shape called out below rather than reusing `OrnamentType.RepeatStart`/`RepeatEnd`
     (still deliberately unwired, for the same reason: a repeat bar line is a property of the
     measure boundary, not a note-anchored ornament). Rendering is purely additive: the two
     existing unconditional `DrawBarLine` calls in `DrawStaffLineRange` are untouched (so default
     Single/no-repeat scores are pixel-identical to before), and a new `DrawBarLineDecoration`
     draws the extra ink (a second parallel line for Double, a thick line for Final, a thick line
     + two dots for RepeatEnd/RepeatStart) after them, confined to the drawing measure's own
     horizontal footprint so it can never visually collide with a neighboring measure's own
     decoration at the shared boundary. Edit menu > Bar Line submenu wired via
     `MeasureContentViewModel.SetBarLineType`/`ToggleRepeatStart`, each backed by its own
     `INoteEditCommand` (`ModifyBarLineTypeCommand`/`ModifyRepeatStartCommand`) for undo/redo,
     mirroring `ModifyLyricTextCommand`'s exact shape. Visual-only, like Segno/Coda: no
     playback/MIDI-export effect (see the third bullet below).
   - **Volta brackets (1st/2nd endings) — done, visual only.** New score-level `JianpuVolta`
     (`StartMeasureIndex`/`EndMeasureIndex`/`Label`) list on `JianpuScore`, mirroring `JianpuTie`'s
     shape but anchored to whole measures instead of notes. Rendered in the previously-unused gap
     above each staff line (`DrawVoltaBrackets`, called alongside `DrawTiesForLineRange` from both
     the screen and PDF render paths) — the same headroom "Beams Above Notes" mode already reaches
     into for its beam lines, so this needed no new row and doesn't touch `StaffBlockHeight`/hit-
     testing/PDF pagination at all. A bracket whose start/end measures land on different staff
     lines (a line wrap falls inside it) is skipped rather than drawn broken across two systems.
     One real cross-cutting fix was needed: a volta on the very first line collided with the title/
     key/tempo header text, since that line's headroom is the header itself rather than the plain
     `StaffBlockSpacing` gap every later system gets — `GetMarginTop` now adds extra clearance, but
     **only when the score actually has at least one volta**, so scores without any (100% of
     existing scores) keep today's exact margin; confirmed byte-for-byte identical PNG output for
     an unrelated sample score before/after this change. Edit menu > Volta Bracket ("1st Ending" /
     "2nd Ending" / "Remove Volta Bracket") operates on the current contiguous multi-measure
     selection (falling back to just the current measure with no multi-selection), backed by
     `VoltaService`/`VoltaMaintenanceService` (the latter mirrors `TieMaintenanceService`'s
     measure-removal index bookkeeping) and a `ScoreSnapshotEditCommand` for undo/redo. Visual-
     only, like the other bar-line/navigation work above: no playback/MIDI-export effect.
   - **Actually performing the repeat/jump/volta-skip during playback and MIDI export** (higher
     risk): needs real changes to `ScoreMidiSchedule`/`ScorePlaybackService`/`MidiExportService`'s
     scheduling, which today assumes one linear pass through the measures. Worth scoping and
     building separately once the visual half has landed and been used for a while.
8. **Multi-verse lyrics.** Change `LyricText` to a verse list (verse 1 keeps today's exact
   field/behavior for backward compatibility; additional verses are new, optional). Inline lyric
   editor gains a verse stepper; renderer stacks N lyric rows instead of a fixed one — a real but
   contained rendering change, inert for every existing single-verse score.
9. **SATB / multi-voice support.** By far the largest item — this is a core data-model change
   (today's single `MelodyNotes` per measure would need to become one of N independent voices,
   rippling through rendering, playback scheduling, MIDI import/export, undo/redo commands, and
   the selection model). Treat this as its own separately-scoped project, not part of the same
   wave as items 1-8, and prototype with 2 voices before committing to 4 (SATB) — 2 voices proves
   out the whole architecture (each voice's own note list, ties, and undo integration, all
   sharing one chord-marker row/lyric block/measure grid) at half the risk.
10. **Pickup measure — verified, documentation only, done.** Traced every path a manually-entered
    short first measure touches: editing (`MeasureNavigationViewModel.ApplyAddMeasure` appends a
    new measure unconditionally, no check that the previous one is "full"), rendering/beam
    grouping and `ScoreMidiSchedule`/`MidiExportService` (`GetMeasureDurationUnits` sums the
    measure's own notes, with no fixed-beat-count assumption anywhere in either file). All of it
    derives a measure's length purely from its actual note content, so a deliberately short first
    measure already plays, exports, and renders correctly today — confirmed with a new regression
    test (`ScoreMidiScheduleTests.Build_PickupMeasure_SecondMeasureStartsRightAfterShortFirstMeasure`)
    proving the second measure starts right after the short one instead of being padded out.
    No code change needed for the editor itself.

    **Found a real, separate gap while verifying this: MIDI import does not preserve a pickup
    measure.** `MidiImportService` feeds its raw note stream through
    `MeasureNormalizationService.NormalizeMeasures`, which flattens every note across the *entire*
    imported file into one continuous stream and rechunks it into fixed `DefaultMeasureBeats`-size
    measures from scratch — so a real anacrusis in the source MIDI file gets silently absorbed
    into the reflow instead of preserved as a short first measure. Fixing this needs a way to
    detect an intended pickup from the MIDI file itself (e.g. a shorter first bar implied by the
    time-signature meta-event's position, which isn't reliably distinguishable from "the recording
    just started off-beat") — real design work, scoped separately rather than folded into this
    already-done verification.

### A real visual-verification capability, discovered mid-session (updates the caveats above)

Every phase above was built and reviewed blind on rendering output, citing "this sandbox can't
visually verify GDI+ output" — that turned out to be only half true. `libgdiplus` (Mono's
System.Drawing implementation) is installed in this sandbox, so the real `JianpuRenderer` code,
compiled as-is via `mcs` against the actual built `JianpuEditor.exe` + `Newtonsoft.Json.dll`, can
be driven from a small throwaway harness and actually run under `mono` — including calling
`RenderToBitmap` and saving a real PNG, which can then be read and visually inspected directly.
This isn't a substitute for a real Windows/GDI+ screenshot (font metrics and rasterization can
differ slightly between libgdiplus and real GDI+), but it's much stronger evidence than reasoning
about layout math alone, and it already found a real, previously-invisible bug:

**`NoteTopAnnotationLayout.AnnotationLayerClearance` (6f) visually collided an octave dot with the
ornament glyph above it — fixed, done.** This constant is the gap between two stacked annotation
layers' *anchor Y values*, not their actual rendered glyph heights, so a small clearance can still
let a tall glyph's ink overlap the layer below it. Confirmed pre-existing (not caused by any
change this session): rendering Trill/Mordent/Turn — all untouched by this session's work — with
a high-octave-dot note showed the exact same collision, just never previously visible since nobody
had rendered and looked. Fixed by setting the clearance to the actual `Font.Height` of the tallest
stacked ornament font (Microsoft YaHei / Arial Italic, both 11pt) rather than a hand-picked small
number, confirmed visually before and after, and locked in with a real regression test
(`NoteTopAnnotationPlannerTests.AnnotationLayerClearance_CoversTheTallestStackedOrnamentFont`) that
measures those fonts' real height rather than asserting a hardcoded pixel value.

## CLAP support (spike plan only, not started)

Unlike VST2/BASSVST, there's no existing library to build on here — no NuGet package, no .NET binding anywhere, and BASS itself has no CLAP support (CLAP is a separate standard from a different origin, u-he/Bitwig rather than Steinberg/un4seen, released well after BASS). The [CLAP SDK](https://github.com/free-audio/clap) is MIT-licensed but is just a C header — hosting a CLAP plugin means writing the host implementation from scratch.

What that would actually take:
1. **A native host bridge**, in C or C++ (CLAP's ABI is a C header, `clap.h`; hosting from managed code requires a native shim regardless of language, same as BASSMIDI/BASSVST being native DLLs — but here there's no pre-built one to lean on). This means: loading a `.clap` file (itself a shared library exporting a `clap_entry` symbol), calling its plugin factory, implementing the required `clap_host_t` callback table, wiring up audio buffer processing (`clap_plugin_audio_ports`) and note/MIDI event input (`clap_plugin_note_ports` / `clap_event_*`).
2. **A build toolchain that can produce a real Windows DLL.** Not available in a Linux sandbox session like this one — would need either a Windows dev machine, or a verified cross-compilation setup (e.g. MinGW-w64) that's actually been proven to produce a working native Windows binary, plus a way to test it against a real `.clap` plugin (none available here either).
3. **A thin P/Invoke layer** on the C# side once the bridge exists — this part is straightforward and similar to the existing `IMidiOutput` implementations.

Rough effort/risk compared to what shipped so far: BASSMIDI and BASSVST were each "wire up an existing, mature, already-compiled library" — verified end-to-end in a single session. A CLAP host is "design and implement a new native audio plugin host from a spec," untestable in this environment, with correctness bugs (audio glitches, crashes, threading issues) that are hard to diagnose without a real plugin and a real Windows machine. This should go to whoever picks it up as its own dedicated project, built and tested on an actual Windows dev machine, not attempted piecemeal alongside other work.

## Plugin system (generalized from upstream #26)

Upstream issue #26 asked for a narrow "sample plugin registers a custom symbol" proof of concept. Generalizing that into an actual extension mechanism:

- Define a small `IJianpuPlugin` contract (e.g. `Initialize(IPluginHost host)`) and a host-side plugin loader (scan a `plugins/` folder next to the .exe for DLLs implementing it).
- Expose a couple of real extension points through `IPluginHost`: register a custom ornament/symbol, add a toolbar button, contribute a menu item.
- Ship one first-party sample plugin in the repo (`JianpuEditor.SamplePlugin`) as both a working example and a smoke test that the API is usable end-to-end.
- Document how to build/deploy a plugin in the README.

## MIDI → Jianpu import (already exists — promote out of "Spike")

`MidiImportService` already does this (see `File → Import MIDI...`), but it's labeled experimental. To make it a first-class feature:
- Harden multi-track MIDI handling (today it picks one melody track; add a track picker for multi-track files).
- Expand test coverage for real-world MIDI files (different DAWs export slightly differently).
- Drop the "(Spike)" label once it's reliable enough, and document supported/unsupported MIDI features in the README.

## Audio/image → Jianpu (AI-assisted, new)

Genuinely new, higher-risk/experimental territory:
- **Audio → Jianpu:** pitch/onset detection (e.g. via an ML pitch tracker) on a hummed or recorded melody, converting detected notes to a draft score.
- **Image → Jianpu:** OCR of a photographed/scanned Jianpu sheet into an editable score.
- Both should be explicitly marked experimental/best-effort in the UI (accuracy will vary a lot with input quality) and live behind a clearly-labeled menu entry, not the main import path.

## Professional UI (new, needs more definition)

"Professional" is vague on its own — breaking it into concrete, independently-shippable pieces:
- Modernize the WinForms visual style beyond the current flat theme + dark mode (custom-drawn menu/toolbar chrome, consistent spacing/icons instead of text-only toolbar buttons).
- High-DPI correctness pass (test at 150%/200% scaling).
- Resizable/dockable panels instead of the current fixed toolbar + canvas layout.
- Live note preview under the cursor while entering notes.
- User-customizable keyboard shortcuts (today's shortcuts are hardcoded).
- **Rendering engine**: polishing the current GDI+ renderer (shared geometry model for draw/hit-test, zoom/DPI support, generalized drag) and adding [alphaTab](https://github.com/CoderLine/alphaTab) as a second, user-selectable renderer (research, feature-coverage spike, and milestone plan already done — see [`RENDERER_PLAN.md`](RENDERER_PLAN.md)).

## Other suggested features

A few additions worth considering, not asked for explicitly but adjacent to the above:
- **Real-time MIDI keyboard input:** play notes on a connected MIDI keyboard to enter/audition them live, instead of only the number-key entry.
- **Metronome / count-in** during playback and recording.
- **Multi-part scores:** today's model is one melody + chord markers; a genuine multi-instrument/multi-staff score (e.g. piano LH/RH, or vocal + accompaniment as independently playable parts) is a bigger data-model change worth scoping separately.
- **Auto-save & crash recovery** — periodic snapshot of the working score so a crash doesn't lose unsaved edits.
- **Direct printing**, not just PDF export.
