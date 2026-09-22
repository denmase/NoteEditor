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

**Status: phases 1 and 2 (`NotationStyle` foundation and the accidental slash convention -- the
one genuinely regional-style-gated item in this whole list) are done, along with phase 3's bug-fix
half, phase 4 (all of it, including Glissando now), phase 5 in full (both the discrete levels and
the crescendo/diminuendo hairpins), phase 6 (breath marks), and phase 7 (Segno/Coda, repeat bar
lines, volta brackets, and D.C./D.S./Fine/Coda navigation -- both the visual halves of all three
original items and the playback/MIDI-export scheduling that was originally left as a follow-up).
Bar line types (Single/Double/Final/RepeatEnd/RepeatStart) and volta brackets were bundled into
phase 7 as originally scoped there. Phase 10 (pickup measure verification, and the MIDI-import
pickup-preservation gap it surfaced) is also done. Everything else
below is still not started.** Phase 10 also surfaced a separate,
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

1. **`NotationStyle` setting — done.** Added `Models.NotationStyle` (`Chinese` default,
   `Indonesian`) and `AppTheme.NotationStyle` as the persisted source of truth. `UnderlinesAbove`
   is now a computed pass-through (`NotationStyle == Indonesian`) rather than its own independent
   flag, so every existing call site reading it (the beam-position branches in `JianpuRenderer`)
   keeps working completely unchanged -- only the *setter* path changed. A settings file saved by
   a build from before this existed has only the old `UnderlinesAbove` bool; `AppTheme.Load`
   migrates `true` there to `Indonesian` (`ResolveNotationStyle`) so nobody's saved preference is
   silently reset back to Chinese. The View menu's old single "Beams Above Notes (Indonesian
   Jianpu style)" checkbox is now a proper "Notation Style" submenu (Chinese/Western default vs.
   Indonesian), reflecting that this one setting now also gates the accidental convention below,
   not just beam position.
2. **Accidental slash convention — done.** Landed in two pieces:
   - `AccidentalKind.Natural` + manual sharp/flat/natural entry landed first (see the note right
     below this list) since it doesn't touch the suffix-vs-prefix positioning question at all.
   - **The kres/mol stroke-through layout itself.** The naive plan (branch
     `JianpuPitchCodec.GetAccidentalMark`/`GetPitchDisplayText` on `NotationStyle`, drawing a
     separate `/`/`\` character next to the digit) turned out visually wrong on the first real
     rendered look, caught after comparing against a real notasi angka sheet music example: kres
     and mol aren't a separate character positioned next to the digit at all -- they're a diagonal
     stroke drawn *through* the digit itself (kres bottom-left to top-right, mol top-left to
     bottom-right), the way `#`/`b` sit as a prefix rather than a same-size neighboring glyph.
     `DrawCompactAccidentalMark` (the real production path -- the other, `NoteTopAnnotationLayout.
     AccidentalX/AccidentalY`-based upper-left band computed in `NoteTopAnnotationPlanner.
     PlaceAccidental`, is what `#`/`b`/`♮` still use) now measures the digit's actual rendered box
     at draw time (`g.MeasureString`, since font metrics vary per platform) and draws the stroke
     corner-to-corner across it with `Graphics.DrawLine` instead of drawing separate glyph text. A
     new `NoteTopAnnotationLayout.AccidentalIsSuffix` flag (true only for Sharp/Flat under
     Indonesian style -- Natural stays a prefix under both styles, since this renderer doesn't
     carry accidentals through a measure the way real key-signature-aware notation does, so a
     natural sign here is always a standalone cancel-mark rather than something that needs to
     visually match a stroke-through accidental within a phrase) tells `PlaceOctaveDots`/
     `PlaceOrnamentBands` to skip their accidental-dodge math for it (nothing occupies the
     upper-left band to dodge anymore, since the stroke sits on top of the digit instead), so
     octave dots and ornaments center normally instead of being pushed aside for no reason. `#`/
     `b`/`♮` and their upper-left positioning are completely untouched for Chinese style --
     confirmed with byte-for-byte identical PNG output for the built-in "Ode to Joy" sample before/
     after this change, and confirmed visually via this session's Mono+libgdiplus harness (compared
     directly against a real Indonesian notasi angka sheet music image) that kres/mol read clearly
     as a stroke through the digit and don't collide with an octave dot or a stacked ornament (e.g.
     a grace note) on the same note.
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
4. **Wire up the dead `OrnamentType` articulation values — Mordent, Staccato, Accent, Tenuto, and
   now Glissando too — all done.** Added the missing Mordent button (ribbon + Edit menu + context
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
   chain -- so none of this carries the layout-band risk flagged elsewhere in this phase.
   **Glissando — done.** Wired the same "full pattern" (ribbon + Edit menu + context menu, plus a
   `gliss` placeholder glyph via `OrnamentService.GetPlaceholderGlyph`, registered into
   `NoteTopAnnotationPlanner`'s `HasCenterOrnament` stacking switch exactly like Staccato/Accent/
   Tenuto) needed no new rendering infrastructure at all, since `JianpuRenderer`'s ornament-band
   drawing already calls `GetPlaceholderGlyph` generically for any `OrnamentType`. The interesting
   part was playback: a real MIDI pitch-bend message would need new event-type plumbing the
   scheduler doesn't have anywhere else, so instead `OrnamentPlaybackService` treats it the same
   way Trill/Turn/Mordent already treat their pitch decorations -- as a rapid run of ordinary
   note-on events -- rather than adding a first pitch-bend code path. `BuildGlissando` steps
   chromatically, one semitone per segment, from the note's own MIDI pitch toward (but
   deliberately never reaching) the *next* melody slot's pitch, filling the note's whole duration;
   the actual next note's own separately-scheduled event supplies the true arrival, so the run
   never double-triggers that pitch. Scoped to same-measure adjacent notes only (matching how a
   glissando is drawn in real notation, between two adjacent written notes) -- a Glissando on the
   last note of a measure, or with no explicit next note to slide toward, simply has no playback
   effect, degrading gracefully to a plain note rather than reaching across a measure/repeat
   boundary. `ScheduleMelodyNote` gained an optional `nextNote` parameter (defaults to `null`, so
   every existing call site is unaffected) that only `ScoreMidiSchedule.BuildMelodyNotes`'s single-
   note branch populates, via a one-slot lookahead in the already-iterated `notes` list.
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
   **Hairpins (cresc./dim.) — done.** New score-level `JianpuHairpin`
   (`StartMeasureIndex`/`StartNoteIndex`/`EndMeasureIndex`/`EndNoteIndex`/`IsCrescendo`), mirroring
   `JianpuTie`'s shape rather than `DynamicMarking`'s single-point one, since a hairpin commonly
   spans across measure boundaries the way a discrete level marking never needs to.
   `HairpinService` (`TryAddHairpin`/`TryRemoveHairpinCovering`) and `HairpinMaintenanceService`
   (`OnNoteRemoved`/`OnMeasureRemoved`/`OnMelodyNoteCountChanged`) mirror `VoltaService`/
   `TieMaintenanceService` exactly, wired into the same note/measure-removal call sites
   `TieMaintenanceService` already sits at in `NoteSplitMergeService` and `ScoreEditorViewModel`.
   **UI** reuses the existing multi-note selection (`ScoreSelectionViewModel.SelectedNotes`)
   instead of a new two-click "pick start, then pick end" mode: `DynamicsEditorViewModel.
   AddHairpin(bool isCrescendo)` spans from the earliest to the latest selected note (mirroring how
   `ScoreEditorViewModel.AddVolta` already uses the current selection's range rather than its own
   separate picking mode), with `RemoveHairpin()` removing whichever hairpin covers the currently
   selected note. Wired through the same three places every other dynamics/ornament control uses
   (ribbon, Edit menu, context menu), plus two new `RibbonIcon.Crescendo`/`Diminuendo` vector wedge
   glyphs (simple enough to draw directly, unlike the D.C./D.S./Fine text-label icons).
   **Rendering** draws the wedge directly in the existing `DynamicsRowHeight` band discrete
   `DynamicMarking` text already uses (`JianpuRenderer.DrawHairpins`/`TryGetHairpinGeometry`,
   called from both the screen and PDF paths alongside `DrawVoltaBrackets`) -- a hairpin is
   fundamentally a dynamics-row shape, not an ornament-band one. Note-position-to-X lookup mirrors
   `TryGetTieGeometry` exactly; like `DrawVoltaBrackets`, a hairpin whose start/end land on
   different staff lines is skipped rather than drawn broken across two systems.
   **Playback** needed real continuous interpolation, not just another step-function level:
   `ScoreMidiSchedule.BuildHairpinVelocityOverrides` resolves every hairpin, up front, into a per-
   note velocity override keyed by score *position* (measure+note index) rather than elapsed
   playback time -- like `DynamicMarking`, a hairpin's effect is a property of where a note sits in
   the score, so it reapplies identically on every pass through a repeated section instead of only
   affecting whichever pass reaches it first. Interpolation is by note ordinal within the span
   (not by elapsed quarter-time), which keeps the pre-pass purely structural: the same span always
   contains the same notes regardless of how repeats later revisit it. The start level is whatever
   the discrete step-function would already be at that position; the end level is an explicit
   `DynamicMarking` at the end note if one exists, otherwise a nominal ~16-velocity nudge
   (`DynamicMarkingPlaybackService.NominalHairpinVelocityDelta`, matching the spacing between
   adjacent pp..ff levels) in the hairpin's direction. A hairpin's resolved level persists past its
   own end the same way an explicit marking would (a crescendo with nothing marked after it holds
   at the level it reached rather than snapping back). A score with no hairpins schedules byte-
   identical velocity output to before this existed.
6. **Breath marks — done.** Added `OrnamentType.BreathMark`, folded into the existing per-note
   ornament list/UI pattern (ribbon + Edit menu + context menu, same as Mordent). Unlike every
   other ornament here, it's anchored just *after* the note's right edge instead of centered above
   it (`JianpuRenderer.GetOrnamentAnchorX` / `NoteTopAnnotationLayout.GetOrnamentAnchorX`, one new
   branch each) since that's where a breath mark actually sits in notasi angka/jianpu sheet music
   — but it deliberately reuses the existing ornament stacking band for its Y position and doesn't
   participate in `NoteTopAnnotationPlanner`'s accidental/octave-dot collision math at all, so it
   carries none of the layout risk flagged under phase 2. Visual-only, no playback/MIDI-export
   effect (a version that inserts a micro-rest is a possible follow-up, not v1).
7. **Repeat bar lines, volta brackets, and D.C./D.S./Coda/Segno navigation — done, including
   playback/MIDI export.** Four sub-parts:
   - **Segno/Coda markers — done.** Wired up via the exact same ribbon/Edit-menu/context-menu/
     glyph pattern as Staccato/Accent/Tenuto, using the `OrnamentType.Segno`/`Coda` values that
     already existed. These two are genuinely note/beat-anchored point symbols in real notation
     (unlike a repeat bar line, which decorates the barline itself, spanning the staff height) —
     see the note below on why `RepeatStart`/`RepeatEnd` were deliberately *not* wired the same
     way.
   - **Repeat bar lines (visual) — done.** Added `BarLineType` (`Single`/`Double`/`Final`/
     `RepeatEnd`) and `IsRepeatStart` (bool) to `JianpuMeasure`, exactly the field shape called out
     below rather than reusing `OrnamentType.RepeatStart`/`RepeatEnd` (still deliberately unwired,
     for the same reason: a repeat bar line is a property of the measure boundary, not a
     note-anchored ornament). Rendering is purely additive: the two existing unconditional
     `DrawBarLine` calls in `DrawStaffLineRange` are untouched (so default Single/no-repeat scores
     are pixel-identical to before), and a new `DrawBarLineDecoration` draws the extra ink (a
     second parallel line for Double, a thick line for Final, a thick line + two dots for
     RepeatEnd/RepeatStart) after them, confined to the drawing measure's own horizontal footprint
     so it can never visually collide with a neighboring measure's own decoration at the shared
     boundary. Edit menu > Bar Line submenu wired via `MeasureContentViewModel.SetBarLineType`/
     `ToggleRepeatStart`, each backed by its own `INoteEditCommand`
     (`ModifyBarLineTypeCommand`/`ModifyRepeatStartCommand`) for undo/redo, mirroring
     `ModifyLyricTextCommand`'s exact shape.
   - **Volta brackets (1st/2nd endings) — done.** New score-level `JianpuVolta`
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
     measure-removal index bookkeeping) and a `ScoreSnapshotEditCommand` for undo/redo.
   - **Actually performing the repeat/jump/volta-skip during playback and MIDI export — done.**
     `ScoreMidiSchedule`/`ScorePlaybackService`/`MidiExportService` previously assumed one linear
     pass through `Measures`; all three now go through a shared expanded play order (measure
     indices in actual playback order, a repeated measure's index appearing more than once) instead
     of `0..Measures.Count-1`, computed once in `ScoreMidiSchedule.Build` and used for scheduling,
     total-length (progress bar), and MIDI export alike, since all three already funneled through
     `Build`/`ComputeTotalQuarterLength`.
     - **Repeats and voltas**: new `RepeatPlaybackExpander.Expand` resolves `IsRepeatStart`/
       `BarLineType.RepeatEnd` (a `RepeatEnd` jumps back once to the nearest preceding
       `IsRepeatStart`, or the beginning if none is marked — the standard "repeat from the top"
       shorthand) and `JianpuVolta` (a measure covered by a volta only plays on the pass matching
       its label's leading digit — the only labels the UI itself ever creates are `"1."`/`"2."` —
       and is skipped on every other pass). Each independent repeated section gets its own fresh
       1st-ending pass; nested repeats aren't standard notation and aren't specially handled.
       Bounded-iteration guard against a pathological/malformed structure (falls back to a
       straight linear play rather than hanging).
     - **Segno/Coda navigation needed a real, separate model gap filled first**: `Segno`/`Coda`
       were pure point markers with no jump *trigger* anywhere in the model — real notation needs
       an explicit "D.C. al Fine"/"D.S. al Coda"-style instruction, which didn't exist at all.
       Added three new `OrnamentType` values (`DaCapo`, `DalSegno`, `Fine`), wired through the
       exact same ribbon/Edit-menu/context-menu pattern as every other ornament (their ribbon
       icons reuse the existing `DrawDynamicLabel` text-label helper rather than new vector
       glyphs, same as the dynamics-level icons). New `SegnoCodaPlaybackExpander.ApplyNavigation`
       runs after the repeat/volta expansion: once the main pass reaches the measure carrying a
       `DaCapo`/`DalSegno` marker, it jumps back to the beginning or the `Segno` mark respectively,
       then plays straight through *without* re-applying repeat/volta expansion a second time (the
       common real-world simplification) until either a `Fine` marker stops the piece there, a
       pair of `Coda` markers jumps from the first to the second (the standard "two coda symbols"
       convention: one marks where to exit early, the other marks the coda section itself), or
       — with neither — it just plays to the end. All navigation resolves at measure granularity,
       matching how repeats/voltas already work; if both a stray `DaCapo` and `DalSegno` exist
       (malformed input), whichever is actually reached later in the main pass wins.
     - Verified end-to-end against the real built assembly via the Mono/libgdiplus harness (since
       `dotnet test` can't execute in this sandbox): a `5`-measure D.C. al Fine score and an
       `8`-measure D.S. al Coda score both produced the exact expected note sequence and total
       playback length, plus a render-smoke-test confirming the three new ornament glyphs draw
       without throwing and visibly differ from an undecorated measure.
8. **Multi-verse lyrics.** Change `LyricText` to a verse list (verse 1 keeps today's exact
   field/behavior for backward compatibility; additional verses are new, optional). Inline lyric
   editor gains a verse stepper; renderer stacks N lyric rows instead of a fixed one — a real but
   contained rendering change, inert for every existing single-verse score.
9. **SATB / multi-voice support — prototype phase done, first real implementation pass shipped.** By far
   the largest item — a core data-model change (today's single `MelodyNotes` per measure becomes
   one of N independent voices), rippling through rendering, playback scheduling, MIDI import/
   export, undo/redo commands, and the selection model. A multi-round scratchpad prototype
   (throwaway code, never part of this repo, always reverted before any commit) fully validated
   the architecture before any production code changed — full findings below, since they directly
   define what the real implementation had to get right from the start.

   The original plan here was "prototype with 2 voices before committing to 4 (SATB)," on the
   assumption that 2 voices proves out the architecture at half the risk. Skipped straight to 4
   per explicit direction, since the real risk items (independent rhythm per voice, whether
   undo/clone/serialization need any changes, whether ties stay voice-scoped) don't actually get
   cheaper to prove at 2 voices than at 4 — the same code paths are exercised either way. Later
   extended to 5 (Descant above SATB) specifically to validate voice *ordering* as a distinct
   concern from voice *count*.

   **Round 1 — data model, undo/clone, per-voice scheduling, tie-scoping.** Built a throwaway
   `SatbMeasure`/`SatbScore` (real `JianpuNote`/duration logic reused via a direct reference to
   the built assembly, not a reinvented toy model) with a 2-measure chorale phrase, one measure
   deliberately giving Bass a different rhythm (four eighth notes) than the other three voices (a
   half note each) — the actual hard case, since real hymnal SATB occasionally has a passing tone
   in one voice while the others hold.
   - **Undo/redo and file save/load need zero changes.** `ScoreSnapshotEditCommand`'s undo and
     `ScoreFileService`'s save/load both already go through `ScoreCloneService.Clone`, which is a
     plain Newtonsoft.Json serialize/deserialize round-trip over the whole `JianpuScore` object
     graph — not field-specific diffing. Round-tripping the prototype's 4-voice structure through
     the exact same settings worked with no special-casing at all, the same "additive field, JSON
     round-trips for free" property every other score-level addition this session (Ties/
     Ornaments/Voltas/Hairpins) already relied on.
   - **Independent per-voice rhythm schedules cleanly** using the real `JianpuRenderer.
     GetDurationUnits`/`ScoreMidiSchedule.ToMelodyMidiNote` — Bass's 8 differently-shaped events
     and the other three voices' 5 events each land on correct, independent timelines with no
     shared-state bugs between voices.
   - **A real cross-voice invariant surfaced that the real feature needs to actively check, not
     just assume**: every voice's *total* duration within a measure must agree even when slot
     counts differ (Bass's 4×0.5 = the others' 1×2.0 in the stress-test measure) — nothing
     enforces this in a naive per-voice note list, and a mismatched voice silently miscalibrates
     that voice's own per-measure scale factor instead of erroring (confirmed for real later, see
     below — a hand-entered descant measure that was actually 5 beats against the other voices'
     6 drifted every note after the first, silently, until measured).
   - **Ties stay correctly voice-scoped** with a `VoicePart` discriminator added to a `JianpuTie`-
     shaped record: an identical-looking note in a different voice at the same measure/note
     position is untouched by another voice's tie.

   **Round 2 — full-fidelity rendering (real `JianpuRenderer` glyphs, not a toy renderer) exposed
   three separate, real layout bugs**, found and fixed in this order, each verified by patching
   `JianpuRenderer.cs` locally, re-rendering, measuring actual glyph pixel positions (not
   eyeballing), then reverting before commit:
   - **Bug 1 — shared measure width.** `MeasureLayout.Width` is derived independently per voice
     from that voice's own note content. Two voices with the same total beat duration but very
     different note granularity (Bass's 16 sixteenth-notes vs. the others' one whole note) can
     compute *different* widths, because `MinNoteWidth` (28px) floors any note narrower than
     that — 16×28=448px vs. the proportional 16×24=384px. Measured drift: up to 160px in the
     stress case. Fix: compute each measure's width as `max` across all its voices, then stretch
     every narrower voice to that shared width — `MeasureLayout.ApplyMelodyScale` gained a second
     `stretchToFill` parameter (`false` preserves today's exact shrink-only behavior).
   - **Bug 2 — dash marks aren't on the beat grid.** `DrawNoteDottedAndDashes` spaced a held
     note's trailing dashes by evenly dividing the *leftover space after the glyph*
     (`extensionWidth / (Dashes + 1)`), a purely cosmetic convention invisible in single-voice
     rendering (nothing else ever needed to line up against it) that visibly drifted once a
     second voice's independently-positioned notes/dashes were compared at the same beat.
     Fix: one dash per true beat-cell, `beatCellWidth = noteWidth / (Dashes + 1)`.
   - **Bug 3 — notes and dashes disagreed on where "centered" means.** After bug 2's fix, glyphs
     still looked wrong together: `DrawCenteredNoteText` visually centers a note glyph *within*
     its own beat-cell (`cellStart + beatCellWidth/2`), but the bug-2 fix placed each dash's
     center *at* the cell boundary, not centered within its own cell — a systematic half-cell
     offset between two individually-grid-correct conventions. A note in one voice and a dash in
     another voice at the exact same beat, both mathematically on-grid, still rendered visibly
     half a cell apart. Fix: `dashCenterX = x + beatCellWidth × (i + 1.5)`, matching exactly where
     a note glyph would center if it occupied that same beat.
   - **Both fixes' regression risk, confirmed and resolved by gating, not by changing the default
     path.** Naively making `ApplyMelodyScale` unconditionally bidirectional, or switching
     `DrawNoteDottedAndDashes` unconditionally to the grid formula, each visibly changes ordinary
     *single-voice* scores that exist today (a short measure that's correctly padded with blank
     space now stretches to fill it instead; every dashed note's dashes shift position) — confirmed
     with real before/after pixel diffs, including one round where the "no difference" first
     result turned out to be a stale-build testing mistake on the AI's part, caught by re-deriving
     the numbers by hand and not matching, then corrected by rebuilding after every single edit
     going forward. **Final, verified-safe design: both fixes gate behind the same condition
     (a measure has more than one active voice), so a score with no `ExtraVoices` renders through
     the exact unchanged code path.** Re-verified after the fix: a single-voice regression render
     pixel-diffs as byte-identical to a freshly-rebuilt true original (only each test image's own
     title text differs); the multi-voice case's bar-line and within-cell glyph positions now
     agree within 1-4px across every voice, everywhere measured.
   - **Voice *ordering* validated separately, at 5 voices (Descant above SATB).** Confirms the
     render loop needs zero special-casing for "which voice is the descant" — it's purely a
     position in an ordered list (`[Descant, Soprano, Alto, Tenor, Bass]` renders top-to-bottom).
     The descant was deliberately given fully independent rhythm (its own 8th-note run, a
     whole-measure rest while Bass is busy, its own cadence) to stress-test the general N-voice
     case, not just the 4-voice SATB one.
   - **One unrelated, real bug found and already fixed while visually comparing octave dots
     across the multi-voice render**: `NoteTopAnnotationPlanner` computed a high-octave dot's X
     as `HeadCenterX - 3f`, and the renderer's draw call subtracted 3 again — a double subtraction
     that left every high-octave dot 3px left of true center (low-octave dots, drawn via a
     different code path, were unaffected). Shipped as its own standalone fix, independent of
     the multi-voice work — see the git history for the dedicated commit; two existing tests had
     pinned the buggy offset as "expected" and were corrected alongside it.

   **Proposed real data model** (what's now being implemented, see below): `JianpuMeasure.
   MelodyNotes` stays exactly as-is (zero migration, the primary/Soprano-equivalent voice). New
   `List<JianpuVoice> ExtraVoices` (`JianpuVoice { string Role; List<JianpuNote> Notes; bool
   IsAbove; }`), empty by default. Render order resolves to: every `IsAbove` voice first (in list
   order), then `MelodyNotes`, then the rest of `ExtraVoices` in list order — this is exactly the
   ordering rule the 5-voice prototype validated needs no special-casing beyond list position.
   Ties/Ornaments/DynamicMarking/Hairpins each gain a voice discriminator. UI exposes a small
   fixed preset set (Single / SATB / SATB+Solo) that populate/clear `ExtraVoices`, rather than
   free-form add-any-voice — the data model itself stays fully general for a later "custom" option.

   **First implementation pass — shipped.** All of the following is real, committed production
   code (not scratchpad), each piece verified against the actual built assembly via the same
   Mono/libgdiplus render-and-measure harness the prototype used, since `dotnet test` can't run in
   this sandbox:
   - **Data model**: `JianpuMeasure.ExtraVoices: List<JianpuVoice>` (`JianpuVoice { string Role;
     List<JianpuNote> Notes; bool IsAbove; }`), empty by default — zero migration, an existing
     score round-trips (clone/save/load) unchanged. `VoiceLayoutService.GetRenderOrder` resolves a
     measure's voices into the validated order (every `IsAbove` voice first, then `MelodyNotes`,
     then the rest of `ExtraVoices` in list order); `HasMultipleVoices` is the single gate every
     piece below uses.
   - **Both verified rendering fixes, ported into production `JianpuRenderer`**, gated on
     `VoiceLayoutService.HasMultipleVoices(measure)` instead of the prototype's test-only
     `EqualizeMeasureWidths` reuse: `CalculateMeasureWidth` takes the max natural width across
     every voice in the measure; `MeasureLayout.ApplyMelodyScale` gained the `stretchToFill`
     parameter; `DrawNoteDottedAndDashes` switches to the beat-grid-precise dash formula only for
     a multi-voice measure. Regression-verified byte-identical (matching SHA-256 of the rendered
     bitmap, not just "looks the same") for a plain score with no `ExtraVoices`.
   - **N-voice row stacking.** Each measure's "below" voices (Alto/Tenor/Bass) render in their own
     row directly under the melody, via a new `MeasureLayout.ExtraVoiceLayout` (mirrors the
     primary voice's own note-layout/draw-bounds math, scaled to the same shared width) and
     `DrawExtraVoiceRows`/`DrawExtraVoiceNote` — notes, octave dots, and dotted-note/dash marks
     only; ties, ornaments, chords, and beat-group underlines aren't part of `JianpuVoice` yet, so
     they're not drawn for extra voices (kept additive to `DrawMelodyRow` rather than generalizing
     it). `StaffBlockHeight` (previously a fixed constant used everywhere: hit-testing, PDF content
     height, block/line stacking) is now `MeasureLayout.GetEffectiveHeight()`/`StaffLineLayout.
     GetEffectiveHeight()`, growing by `MelodyRowHeight + RowGap` per below-voice row; Dynamics/
     Secondary/Lyrics rows, hit-testing zones, row labels, bar-line height, and selection highlight
     all shift down through the same two helpers, so they never drifted out of sync with what's
     actually drawn. A click on a below-voice row resolves as a generic Measure hit in this initial
     pass (per-note editing there landed in the follow-up pass below) rather than being misread
     against the primary voice's note bounds. Verified via `RenderToBitmap` (ink actually present on
     each extra row, at the right Y) and `HitTest` (below-voice clicks vs. melody clicks resolve
     correctly) — not just the layout math. "Above" voices (descant/solo) were in the data model and
     render-order rule from this pass, but didn't render as their own row yet — see the follow-up
     pass below.
   - **Per-voice MIDI/playback scheduling.** `ScoreMidiSchedule.BuildExtraVoiceNotes` schedules
     every `ExtraVoices` slot (both "below" and "above" — audio doesn't depend on the row being
     drawn) on its own channel (`ExtraVoiceChannelBase` = 2, one channel per slot), deliberately
     much simpler than the primary voice's `BuildMelodyNotes`: plain notes/rests/continuation-dots
     only, no ties/ornaments/dynamics/hairpins (not on `JianpuVoice` yet). Each measure resyncs to
     the shared per-measure clock regardless of how a voice's own notes added up, so a duration
     mismatch (the real risk the prototype's descant flagged) can't cascade into drift on later
     measures — it only ever affects that one measure's internal timing, and is actively logged via
     `AppLog.Info` rather than silently miscalibrating. Verified including the resync case directly
     (a voice missing from one measure in the middle of a score, confirming the next measure's notes
     land back on the correct beat).
   - **Minimal reachability: Edit → Voices → Single Voice / SATB.** `VoiceModeService.ApplySatb`
     populates every measure with rest-filled Alto/Tenor/Bass voices (each rest's dash count sized
     to that measure's own beat count, so it never trips the mismatch check above) unless that
     measure already has extra voices — re-choosing "SATB" never clobbers hand-entered voice
     content. `ApplySingle` clears them. Wired through `ScoreEditorViewModel.SetVoiceMode` as an
     ordinary `ScoreSnapshotEditCommand` (whole-score undo/redo, same as Clear Score/Add Volta).
     Verified end-to-end: apply SATB, hand-edit a rest into a real note (simulating what a user
     would type next), render to a bitmap, confirm the extra rows/taller layout/pushed-down
     Dynamics all appear together, then switch back to Single Voice and confirm the score collapses
     back to exactly its original single-voice layout.

   **Known gaps from this pass, all closed by the follow-up pass below** (real, not hypothetical —
   each was a fixed constant that assumed uniform per-line height, same root cause, different call
   site): `PdfPagePlanner.GetLinesHeight` estimated how many lines fit on a PDF page using the fixed
   `StaffBlockHeight` alone, so a page containing an SATB line could be planned as if it were
   shorter than it actually rendered; `PlaybackLayout`'s vertical playback marker and Y-based
   drag-to-seek row-matching both used the fixed `StaffBlockHeight` for a line's vertical extent, so
   on a line with extra voice rows the marker didn't extend through them and a seek-drag into that
   space snapped to the nearest available row instead of that exact one.

   **Follow-up pass — non-uniform line heights, "above" voice rendering, voice-aware hit-testing,
   and full-parity note editing for extra voices — done.** Closes every gap flagged above plus the
   two items the original assessment deliberately deferred (per-note editing/selection of extra
   voices, and "above"-voice rendering as its own row).
   - **Non-uniform line heights (PDF pagination + playback marker/seek).** New
     `JianpuRenderer.GetStaffLineHeights` returns each line's real `GetEffectiveHeight()`, threaded
     into a new `PdfPagePlanner.PlanPages(IReadOnlyList<int> lineHeights, ...)` overload (the old
     `PlanPages(int totalLines, ...)` is kept, now just delegating to the new one with a uniform-
     height array, so every existing caller is unaffected). `PlaybackMeasureSegment` gained a real
     `Height` property (from `MeasureLayout.GetEffectiveHeight()`), and `PlaybackLayout.
     GetMarkerPosition`/`FindNearestRowBlockTop` use it instead of the fixed `StaffBlockHeight`
     constant, so the marker and drag-to-seek now reach correctly through every SATB row. Verified
     with a full page-count regression test (a taller SATB line never lets more content get packed
     onto a page than actually fits) and new `PlaybackLayoutTests` covering marker/seek through an
     extra-voice row.
   - **"Above" voices render as their own row.** Two-pass layout: the existing per-measure placement
     loop leaves `MeasureLayout.BlockTop` meaning exactly what every pre-existing consumer already
     assumes (the melody row's own top — ties, hairpins, gap carets, Dynamics/Secondary/Lyrics
     anchors all still read it unchanged), then a new second pass (`ApplyAboveVoiceHeadroom`) shifts
     each line's `BlockTop` down by the cumulative height every earlier line's own above-voice rows
     need, reserving headroom without disturbing any of those existing consumers. New `GetDrawTop()`/
     `GetDrawHeight()` accessors mean "the whole visual block, above-voice rows included" and are
     used only where that's genuinely what's needed: bar lines, hit-testing bounds, and the selection
     highlight rectangle. Verified by rendering a descant-above-SATB score to a bitmap and confirming
     ink actually lands in the reserved row, plus a hit-test regression confirming a click above the
     melody row still resolves as a generic Measure hit when there's no above-voice content there.
   - **Voice-aware hit-testing.** `ScoreNoteRef` (now a struct) and `ScoreHitResult` both carry a
     `VoiceIndex` (`ScoreNoteRef.PrimaryVoiceIndex = -1` for the melody, otherwise an index into
     `JianpuMeasure.ExtraVoices`). `JianpuRenderer.HitTest` resolves a click in any above- or
     below-voice row (note, gap, or dash) against that specific voice's own note list and draw
     bounds via new `HitTestVoiceRow`/`CreateVoiceGapHit`, rather than only ever resolving against
     the primary voice. Verified with dedicated hit-testing tests for a descant, an alto, and a bass
     row, plus a full plain-score (no extra voices) regression sweep confirming every existing
     hit-testing path is byte-for-byte unaffected.
   - **Full-parity note editing for extra voices.** The single largest piece: insert, delete,
     pitch/degree change, duration (dashes/dots/underlines), rests, accidentals, and octave-shift now
     work identically whether the current selection is on the primary voice or any extra voice —
     confirmed by the user as the intended scope for this pass ("full parity with primary voice
     editing"), explicitly *excluding* Split/Merge/Copy-Paste-as-a-structural-op-into-any-voice,
     multi-note drag-range-select across voices, and keyboard Tab/arrow navigation across voices,
     which all stay primary-voice-only (with active guards — see below — rather than being left as
     an unverified assumption). Ties/ornaments/chords/dynamics remain entirely out of scope, since
     none of those are part of the `JianpuVoice` data model yet.
     - New `VoiceLayoutService.GetNotesList(measure, voiceIndex)`/`InsertNote`/`RemoveNote` are the
       single resolution point every editing call site now goes through instead of ever hardcoding
       `.MelodyNotes` — `InsertNote`/`RemoveNote` delegate to `MelodyChordService.InsertSlot`/
       `RemoveSlot` for the primary voice (keeping the chord-slot shadow list in sync, unchanged
       behavior), and do a plain list insert/remove for an extra voice (which has no chord/tie/
       ornament/dynamics support to keep in sync).
     - Because `JianpuNote` is a mutable reference type, most in-place edits (octave, accidental,
       dotted, duration step, transpose) became voice-aware for free once note *selection*
       resolution was fixed to go through `VoiceLayoutService.GetNotesList` — only the
       list-structure-changing operations (insert, delete) needed explicit voice-index threading
       through `ModifyMelodyNotesCommand`/`InsertMelodyNoteCommand`, `NoteEditorViewModel`,
       `ScoreEditorViewModel`, and `ScoreCanvas`'s selection/hit-click handling.
     - Selection rendering (the yellow highlight box/gap caret) is voice-aware too: `JianpuRenderer.
       Draw`/`DrawExtraVoiceRows`/`DrawVoiceRow` take the selected voice index and only highlight the
       note/gap in that specific voice's row, never the primary voice's note at the same index.
     - **Found and fixed a real, pre-existing architectural landmine while wiring this up**: three
       separate `ViewModel` classes (`ScoreEditorViewModel`, `OrnamentEditorViewModel`,
       `DynamicsEditorViewModel`) each had their *own*, independent `GetSelectedNoteRefs()` — not
       shared with `NoteEditorViewModel`'s — that constructed a `ScoreNoteRef` without any voice
       index, silently defaulting to the primary voice. `ScoreEditorViewModel`'s was made properly
       voice-aware (and its `Delete` regrouped by `(MeasureIndex, VoiceIndex)` instead of
       `MeasureIndex` alone, so an extra-voice deletion can never fall into the primary-voice-only
       tie/hairpin/ornament/dynamics maintenance path). `OrnamentEditorViewModel`'s and
       `DynamicsEditorViewModel`'s were fixed the other way — since ornaments/dynamics aren't part of
       `JianpuVoice`, a selection on an extra voice is now explicitly filtered out there, so it's
       safely ignored instead of silently mutating whatever primary-voice note happens to share that
       note index. This was only caught by testing the actual `ViewModel` call chain end-to-end
       (delete an extra-voice note, confirm only that voice lost a note) rather than only the
       lower-level services in isolation.
     - Verified with a comprehensive end-to-end pass (insert/delete/octave/undo on an Alto voice, all
       confirmed to leave the primary voice untouched; a Split attempted on an Alto selection
       confirmed to no-op rather than corrupt either voice) plus a rigorous bitmap-diff test for the
       selection-highlight rendering (render with/without a selection, diff specific pixel regions
       byte-for-byte, confirming the highlighted region changes only where expected and stays
       pixel-identical everywhere else — more reliable under `libgdiplus` than an absolute-color
       heuristic, which produced false positives from font-antialiasing).

   **Third pass — found by actually testing a real Windows build of the above, not by code review:
   beat-to-pixel width consistency, a real `beatToX`, missing voice-row labels, and the primary
   voice's own label — done.** The user built and ran the app (screenshots of "Canon" and "Ode to
   Joy" in SATB mode) and reported two concrete things looking wrong that code review alone hadn't
   caught: bar widths not looking content-consistent across a line, and no row labels at all next to
   the Alto/Tenor/Bass rows. Both turned out real:
   - **Beat-to-pixel width consistency.** Two measures with the *same total beat length* could
     previously render at different widths purely from how finely their notes happened to be
     subdivided: `GetNoteWidth`'s per-note floor (`MinNoteWidth` = 28px) binds more often for many
     short notes than for a few long ones covering the same duration (16 sixteenth-notes floors to
     16×28=448px; the same 4 beats as one whole note is a clean 384px) — the exact cross-voice bug
     from the first pass above (`CalculateMeasureWidth`'s own doc comment), just across *different
     measures* instead of different voices in one measure, and never addressed for that direction.
     Fixed the same way: `JianpuRenderer.BuildLayout` now runs a pre-pass (`ComputeBeatGroupWidths`)
     grouping every measure in the score by its total beat length (`ScoreMidiSchedule.
     GetMeasureDurationUnits`) and taking the widest natural requirement in each group; every
     narrower member of that group stretches up to it via the existing `ApplyMelodyScale`
     `stretchToFill` mechanism (now also triggered when a measure's assigned width exceeds its own
     natural width, not just when it has multiple voices). A measure that's the sole member of its
     beat-length group is completely unaffected — its group width is just its own natural width, so
     `ApplyMelodyScale` computes a no-op 1.0 scale exactly as before, confirmed with a dedicated test
     and by re-running every prior regression check (SHA-diffing a real rendered score with no
     duration-sharing measures) with byte-identical results. Only the `PdfExport` equal-width-per-line
     grid path (`MeasuresPerLine`) is untouched, since it already forces a stronger equalization
     unrelated to beat length.
   - **A real `beatToX`/`XToBeat`.** `MeasureLayout.BeatToX(beatOffset)`/`XToBeat(x)` map a beat
     position within a measure to (and from) the exact X coordinate the renderer actually draws
     there, reusing the same `GetNoteDrawBounds` source of truth as drawing and hit-testing —
     correctly non-linear whenever a note's width was floored or stretched, unlike the playback
     marker/seek's previous assumption that beats are spaced evenly across a measure's pixel width
     (mathematically equivalent for a measure with no floor/stretch distortion, visibly wrong for one
     that has it). `PlaybackMeasureSegment` now carries its source `MeasureLayout`, and `PlaybackLayout.
     GetMarkerPosition`/`MapXToBeat` use `BeatToX`/`XToBeat` when it's available, falling back to the
     old even-spacing approximation only for a segment nothing built this way (e.g. a hand-constructed
     test fixture). Verified with a round-trip test (`XToBeat(BeatToX(b)) == b` across a measure) and a
     direct comparison against `GetNoteDrawBounds` at a note boundary in a mixed long/short-note
     measure.
   - **Extra voice rows had no label at all.** `JianpuVoice.Role` was stored and used for the SATB
     preset's internal bookkeeping but never actually drawn anywhere — an Alto/Tenor/Bass/descant row
     was visually indistinguishable from an empty gap once scrolled past the always-labeled first
     line's melody row. Fixed with `DrawVoiceRowLabels`, mirroring the existing fixed "Melody"/
     "Dynamics"/"Secondary"/"Lyrics" row-label pattern: each above/below voice row is labeled with the
     first measure-on-that-line's own `Role` at that row position. Verified visually (a rendered PNG
     showing "Melody"/"Alto"/"Tenor"/"Bass" cleanly stacked) and with a permanent ink-presence
     regression test.
   - **The primary voice's own row was always labeled "Melody", even in SATB mode.** Raised by the
     user directly: a real SATB score should read "Soprano/Alto/Tenor/Bass", not "Melody/Alto/Tenor/
     Bass", and a descant above SATB needs its own label independent of whichever name the primary
     row uses. Added `JianpuScore.PrimaryVoiceLabel` (null/empty falls back to "Melody"): `VoiceModeService.
     ApplySatb` sets it to "Soprano" *only* when it's still at the default (never clobbers a name
     picked by hand, mirroring how `ApplySatb` already never clobbers hand-entered `ExtraVoices`
     content), and `ApplySingle` unconditionally resets it back to null, since "Soprano" only makes
     sense alongside Alto/Tenor/Bass. `JsonConvert`-based clone/save/load picks up the new field for
     free (no manual (de)serialization code to update). Verified end to end: applying SATB sets the
     label, re-applying it after a hand-picked rename doesn't clobber that rename, switching back to
     Single Voice resets it, and a descant-above-SATB score renders "Descant / Soprano / Alto / Tenor
     / Bass" top-to-bottom exactly as expected.

   **Fourth pass — a manual forced line break, and real-content SATB/descant sample files — done.**
   Prompted by the user actually opening the shipped `sample/Ode to Joy.jianpu` and asking how to
   force a line break, since they'd assumed `sample/Canon in D.jianpu` had one.
   - **`JianpuMeasure.ForcesLineBreak`.** There was no way to break a line anywhere other than
     where automatic width-based wrapping (`JianpuRenderer.BuildLayout`'s `needNewLine`) would put
     it — Canon in D's own line breaks are exactly that same automatic mechanism, not an explicit
     indicator. Added the field, a `ModifyLineBreakCommand` (undoable, mirrors the existing
     `ModifyRepeatStartCommand` exactly), `MeasureContentViewModel.ToggleLineBreak()`, and an Edit
     > Bar Line > "Toggle Line Break After This Measure" menu item. `BuildLayout` checks the
     *previous* measure's flag on every iteration (above and beyond its two existing wrap
     conditions) and forces a wrap, unless already at the start of a fresh line. Verified a forced
     break wraps right after the flagged measure without ever producing a trailing empty line when
     it's set on the score's last measure.
   - **`sample/Ode to Joy.jianpu` was real Beethoven, but with genuinely wrong rhythm data** — measures
     didn't actually total 4 beats each despite the declared 4/4 (2, 5, and 1.25 beats respectively
     for three of the five measures), which is what was really behind the uneven bar widths the user
     first noticed (not a rendering bug — measures with truly different durations correctly render at
     different widths; see the beat-width-consistency fix above). Rather than patch the rhythm bug in
     place, replaced it entirely with a proper 8-measure, four-part (SATB) hymn arrangement of the
     real tune and Henry van Dyke's real 1907 public-domain English text ("Joyful, Joyful, We Adore
     Thee"), harmonized in root-position block chords against the piece's own chord symbols (verified
     every voice totals exactly 4 beats in every measure). `DemoScoreFactory.CreateOdeToJoy` (the
     in-memory "Load Demo Score" path, separate from the sample file) was updated to match exactly,
     so both paths show the same, correct piece.
   - **New `sample/Amazing Grace (Descant).jianpu`** — a from-scratch second sample demonstrating a
     genuine 5-voice score (Descant above SATB), the real "New Britain" tune and John Newton's real
     1779 public-domain text, in its actual 3/4 meter, with the tune's famous low-register dip
     (`Octave: -1`) at "I once was lost... but now am found" and the descant's high register
     (`Octave: 1`) throughout, resolving to a high tonic on the final "I see." Verified every voice
     totals exactly 3 beats in every measure and the render shows all five labeled rows in the
     correct order (Descant / Soprano / Alto / Tenor / Bass).

   **Fifth pass — extra voices played as bare piano notes indistinguishable from the melody, and
   the playback marker didn't cover a descant row — done.** Both found by the user actually
   listening to and watching playback of the new real-content SATB samples above.
   - **Extra voices had no instrument of their own.** `ScorePlaybackService.LoadTimeline` and
     `MidiExportService.BuildTrack` only ever sent a `ProgramChange` for the melody and chord
     channels — every extra-voice channel (`ExtraVoiceChannelBase` and up) was left on whatever a
     never-explicitly-set MIDI channel happens to default to, which is typically the *same* plain
     piano patch as the melody. The notes were genuinely being scheduled and played correctly (not
     silent), but on an identical timbre in a similar register with no octave separation, four-part
     block harmony can easily just sound like "one voice" rather than four -- exactly what was
     reported. New `ScoreMidiSchedule.DefaultExtraVoiceInstrument` (General MIDI program 52,
     "Choir Aahs") is now sent for every extra-voice channel a score actually uses, in both the live
     playback path and MIDI export, via a new `ScoreMidiSchedule.ExtraVoiceChannelCount` (the same
     "how many extra-voice slots does this score need" count `BuildExtraVoiceNotes` already computed
     internally, now shared instead of duplicated). Verified with a `FakeMidiOutput`-based test that
     every extra-voice channel receives the `ProgramChange` before playback starts, a MIDI-export
     test that the written file contains the same `ProgramChange` bytes on those channels, and
     end-to-end against the real "Ode to Joy" SATB sample file (Alto/Tenor/Bass channels 2-4 all
     receive it).
   - **The playback marker/seek didn't reach an above voice.** `BuildPlaybackSegments` built each
     segment's `BlockTop`/`Height` from `MeasureLayout.BlockTop`/`GetEffectiveHeight()` -- exactly
     the melody-row-and-below span, which is *below* where a descant/solo row actually draws (see
     the "above" voice rendering pass). The marker's own top (and the seekable region) started at
     the melody row, never covering the descant above it, matching the user's report precisely.
     Fixed by switching to `GetDrawTop()`/`GetDrawHeight()` -- the same "whole visual block,
     above-voice rows included" accessors hit-testing, bar lines, and the selection highlight
     already use, so this was a real gap specific to `BuildPlaybackSegments` alone, not a new
     concept. A plain score or a SATB score with only below voices sees `GetDrawTop() == BlockTop`
     and `GetDrawHeight() == GetEffectiveHeight()` exactly (no above-voice headroom to add), so
     this is a strict generalization with no behavior change for either case -- confirmed by the
     full existing `PlaybackLayoutTests` suite passing unchanged. New tests cover the descant case
     directly: the segment's `BlockTop` sits above the melody row's own `BlockTop`, and the marker's
     `Top` reaches at least a full melody-row-height above it.
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

    **Found a real, separate gap while verifying this: MIDI import did not preserve a pickup
    measure — now fixed.** `MidiImportService` feeds its raw note stream through
    `MeasureNormalizationService.NormalizeMeasures`, which flattens every note across the *entire*
    imported file into one continuous stream and rechunks it into fixed `DefaultMeasureBeats`-size
    measures from scratch — so a real anacrusis in the source MIDI file got silently absorbed
    into the reflow instead of preserved as a short first measure.

    The real difficulty this "real design work" note originally flagged: raw note *timing* alone
    can't tell "this file has a pickup measure" apart from "the piece just doesn't fill its last
    measure" — a first attempt at this fix guessed a pickup from the leftover remainder of
    total-length-mod-measure-length, and that guess turned out to misfire on exactly that ordinary
    case (caught by a regression test modeled on the existing `Import_NormalizesMeasuresToFourBeats`
    test, which has a 7-beat piece — 4 + 3 — that the remainder guess reinterpreted as "a 3-beat
    pickup plus a 4-beat measure"). The fix instead trusts only an explicit, standard MIDI signal:
    real notation software (Finale, Sibelius, MuseScore, Logic...) that exports a piece with a
    pickup measure uses a narrower time-signature meta-event covering just the first measure,
    immediately followed by the piece's real time signature from measure 2 onward — a mechanism
    `MidiImportService` was already parsing every instance of (`TimeSignatureChanges`) but only
    ever reading the first entry from. `MidiImportService.DetectPickupBeats` checks for exactly
    that two-event pattern (first at tick 0, second beginning precisely where the implied short
    first measure ends) and reports no pickup for anything else, including a file with no time-
    signature changes at all or changes anywhere else in the piece. `MeasureNormalizationService.
    NormalizeMeasures` gained an optional `firstMeasureBeats` parameter that caps only the still-
    unflushed first rebuilt measure's capacity — every measure after it, and every other existing
    caller that doesn't pass it, is completely unaffected. Verified via a raw-MIDI-bytes test
    building the explicit two-time-signature-event pattern, plus the regression test above pinning
    down that an ordinary short *final* measure is never reinterpreted as a pickup.

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

## Upstream bug-fix sync (loootte/JianpuEditor)

This fork shares lineage with [loootte/JianpuEditor](https://github.com/loootte/JianpuEditor).
Checked its recent commits for bug fixes this fork should carry too:

- **Beat-group underline breaking at a shorter note — done, ported.** Upstream's fix (`fix(render):
  break duration underlines at shorter notes`): when a note with fewer underlines (e.g. an eighth
  note) sits between two notes with more (e.g. sixteenths) in the same beat group, the deeper
  underline level must break at the shorter note instead of drawing one continuous line across it.
  This fork had the exact same bug -- `DrawBeatGroupUnderlines` computed a single span per
  underline level from the first to the last matching note in the group, ignoring any gap in
  between. Fixed the same way upstream did: extracted the grouping/span logic out of
  `JianpuRenderer` into a new testable `BeatGroupUnderlinePlanner` (mirroring upstream's class
  name), whose `CollectSpans` now returns every contiguous run of notes reaching a given underline
  level rather than just the first/last. Confirmed visually via this session's Mono+libgdiplus
  render harness (both "Beams Above Notes" and the default below-mode render the break correctly)
  and confirmed byte-for-byte identical output for scores with no such gap (an unaffected sample,
  and a synthetic four-consecutive-sixteenths case).
- **MIDI import crash on B major (`1=B`) — already fixed independently, no action needed.**
  Upstream's fix delegates key-signature parsing to `KeySignatureService.FormatKeySignature`/
  `TryParseTonicPitchClass` instead of manually treating a leading "B" as a flat-accidental prefix.
  Checked `ScoreMidiSchedule.ParseTonicMidi` and `MidiImportService` here: both already call
  through to `KeySignatureService` (this must have landed independently at some earlier point in
  this fork's history) -- `MidiImportService.TonicNames` is dead code left over from before that,
  harmless but unused (matches a pre-existing compiler warning already present in this fork).

## Beat continuation slot / held-note beaming (done)

Cross-checking the accidental slash convention (see phase 2 above) against real notasi angka sheet
music ("Indonesia Pusaka" and "Gugur Bunga", both Ismail Marzuki; "Pertolongan-Mu", Citra
Scholastika) surfaced a genuine, separate rendering gap: a held note that continues into a later
beat position is drawn there as its own continuation mark (printed as `.` in these sources),
occupying its own slot in the beat grid -- and that slot beams together with an adjacent note
exactly like two real notes would (e.g. `3 . 1 5`: `3` stands alone as an unbeamed quarter note,
then `.` and `1` share one beam as the held-through eighth position plus the next eighth note,
then a new beam starts at `5`). A beam in this notation always ties together two beat-grid
positions -- a continuation slot counts as one of them exactly like a real note.

**This app has no equivalent of that continuation slot.** `JianpuNote.Dashes` extends a note's own
duration by widening *that note's own cell* (rendered as small dash marks trailing its digit, see
`DrawNoteDottedAndDashes`) -- it isn't a separate position in the beat grid that could sit next to,
and beam with, a following note. So today there's no way to enter or render the `3 . 1 5` pattern
above the way this reference does it: our model can only produce a wide "3" cell followed
immediately by "1", never a beam connecting a held-position slot to the next note.

**The exact duration rules for the dot** (per a reference guide the user supplied, translated from
Indonesian notasi angka teaching material), confirmed against the sheet music above:
- **Not under any beam:** a dot is worth a full beat, same as an un-underlined note. `5 . 3 4` in
  4/4 is beats 1-2-3-4: `5` sustains through beat 2 via the dot, `3` starts beat 3, `4` starts beat
  4. Two consecutive dots add two beats (`1 . . 2`: `1` sustains 3 beats, `2` takes beat 4); three
  fill a whole 4/4 measure by themselves. **This maps exactly onto `Dashes`** ("each dash adds +1
  beat," per the comment on `JianpuRenderer.GetDurationUnits`) -- `5 . 3 4` is precisely `Dashes=1`
  on the `5`, rhythmically identical to today's model. Only the *rendering* differs: this reference
  draws each dash as its own separate "." token in its own cell, this app draws dash marks trailing
  the same note's digit in one wider cell.
- **Under a beam:** here's the part our model genuinely can't represent. The dot's value isn't
  fixed by the preceding note's *own* underline count -- it's fixed by *whatever beam depth the dot
  itself is drawn under*, which the sheet music examples show can differ from the preceding note's
  depth. Worked example from the reference (`5 . 4` with two stacked beam levels: a shallow one
  spanning all three positions, a second deeper one spanning just `.` and `4`): `5` sits under only
  the shallow beam = 1/2 beat; `.` and `4` share the deeper beam = 1/4 beat each; total 1 beat. So
  in that example the note and its own continuation dot are at *different* effective underline
  depths (`5` at depth 1, its dot at depth 2) -- the dot is beamed with the *following* note at the
  dot's own depth, not simply "half of the note before it" in a fixed recursive sense (that framing
  in the reference guide is a simplification that happens to hold for its own worked examples, but
  the sheet-music cross-check shows the real mechanism is "the dot's duration comes from its own
  beam depth," matching how a real note's duration comes from its own `Underlines`).
- **Consequence:** representing this needs each continuation unit to carry its *own* effective
  underline depth, independent of the note it continues -- `JianpuNote.Dashes` (a bare count) can't
  carry that. This is a real, additional data-model requirement discovered by working through the
  reference's own examples, not just a rendering gap.

**Implementation, once built, turned out simpler than the scope above predicted.** Rather than a
whole new slot/entry type, it's `JianpuNote.Type = NoteType.Rest` plus one new bool,
`IsContinuation` -- because a continuation dot genuinely *is* rest-like for every purpose except
display glyph and playback duration: it's not a playable pitch, can't be tied, doesn't get a new
lyric syllable, doesn't take part in chord/harmony suggestion, can't be split/merged -- and
`Type == NoteType.Rest` was already the exclusion check used almost everywhere for exactly that
("not a real note") meaning, across services, harmony/chord/lyric/tie code. Riding that existing
exclusion instead of introducing a third `NoteType` meant only two real call sites needed new
behavior, not a sweep of every `NoteType` switch in the codebase:
- `JianpuPitchCodec.GetPitchDisplayText`: renders `.` instead of `0` when `IsContinuation` is set
  (both `JianpuRenderer` call sites that used to hardcode `"0"` for a rest now just call this,
  which was already correct for the plain-rest case and free for the new one).
- `ScoreMidiSchedule.BuildMelodyNotes`: a new `soundingEventIndices` tracker records whichever
  event(s) are currently sounding; hitting a continuation-dot slot extends their
  `DurationQuarter` by this slot's own `GetDurationUnits` instead of scheduling a new note-on
  (mirrors how tie extension already reaches back to the tie's start event, just without needing
  an explicit `JianpuTie` object). Correctly keeps extending the *original* note-on across a
  suppressed tied-to slot, and safely no-ops if a continuation dot follows a true rest (nothing to
  hold).
- `BeatGroupUnderlinePlanner.GroupNotesByQuarterBeat`/`CollectSpans` needed **zero changes** --
  confirmed by both a scratchpad prototype (built before touching production code, per the user's
  explicit request to visually validate the beam/dot rendering against real reference sheets first)
  and by `BeatGroupUnderlinePlannerTests` -- it already operates purely on each slot's own
  `Underlines`/`GetDurationUnits`, with no `NoteType` branching at all.
- Manual `JianpuNote` clone/copy sites (copy-paste, undo/redo capture, measure clone, split/merge,
  chord sync -- about eight call sites) needed `IsContinuation` added to their property lists so it
  round-trips through those paths; JSON save/load needed nothing extra (plain reflection-based
  property serialization, and old files simply default the new bool to `false`).
- Editor UI: a "." button next to the existing note/rest digit row (`MainForm.CreateContinuationDotButton`),
  a matching context-menu item, and `NoteEditorViewModel.AddContinuationDot`/`AppendContinuationDot`
  mirroring `AddRest`/`AppendRest` exactly except for the flag.
- Not gated by `NotationStyle`: the continuation-dot mechanism itself is notation-style-agnostic
  (it only cares about `Underlines`/beat position), and automatically renders with beams
  above/below via the existing `AppTheme.UnderlinesAbove` branch -- Indonesian sources are simply
  what surfaced the gap, not a restriction on where the fix applies.
- Checked, not touched: the octave-dot/beam vertical spacing that a scratchpad prototype needed a
  workaround for turned out to be a false alarm in the real renderer -- `DrawNoteOctaveDots`'s
  above-mode dot Y (`rowTop + 4`) and `DrawBeatGroupUnderlines`'s above-mode beam Y
  (`rowTop - 8` or higher) already sit a fixed ~12px apart regardless of grouping, so no collision
  exists there to fix.

Existing `Dashes`-based scores are untouched -- this is purely additive for the new
beamed-continuation case; the unbeamed case (`Dashes`, rendered as trailing marks on the previous
note's own cell) keeps rendering exactly as it did before.
Scoping it as its own dedicated, carefully-reviewed piece of work rather than folding it into the
beam-fix or accidental-convention PRs.

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
- **Per-track mute/solo during playback** — melody, chords, and each extra voice (SATB/descant)
  already play on their own MIDI channel (see `ScoreMidiSchedule`), so this is mostly UI: a
  mute/solo toggle per track in the playback controls, wired to skip that channel's events when
  building the playback timeline (and, separately, MIDI export). Explicitly requested by the user
  while discussing chord-track playback below -- distinct from that request (which was about *how*
  the chord track sounds, not about isolating/muting tracks) and not yet implemented.

## Harmony suggestion: enhanced (Markov) engine

A second, opt-in chord-suggestion backend alongside the original rule-based `HarmonySuggestionService`
("Legacy"). Adapted from a design another agent proposed (written for a WPF app with no DI
container; this app is WinForms + `Microsoft.Extensions.DependencyInjection`), reusing its
knowledge base and algorithm shape but reworked to fit this codebase rather than ported verbatim
-- see the specifics below.

- **`HarmonyEngine`/`ProgressionEngine`** (new, internal): combine melody-fit weighting
  (downbeat/final-note/longer-note bias), voice-leading scoring between chords, a cadence bonus,
  and an optional Markov chord-transition model, replacing the original suggester's flat "first
  1-3 diatonic matches" lookup. `MarkovHarmonySuggestionService` adapts the result back into the
  same `HarmonySuggestion`/`HarmonyProgressionSuggestion` DTOs the UI already renders (both
  sealed with no numeric `Score` field, so the score is folded into `Reason`).
- **`MarkovChordModel`**: order-1/order-2 Markov model over chord-transition strings, trained once
  at startup from `Data/chord_corpus.txt` (a hand-written corpus of real progressions) and cached
  to `Data/markov_chord_model.txt` next to it (retrained automatically if the corpus is ever
  edited and is newer than the cache). Training/loading failure just means the Markov engine falls
  back to melody-fit + voice-leading scoring only -- the model is optional everywhere it's used.
- **`SelectableHarmonySuggestionService`**: the DI-registered `IHarmonySuggestionService`
  singleton, wrapping both backends and delegating to whichever `ActiveKind` is selected
  (**defaults to Legacy**, so existing behaviour is provably unchanged until a user opts in --
  verified with a runtime test comparing its default output byte-for-byte against calling the old
  `HarmonySuggestionServiceAdapter` directly). A new capability interface,
  `IHarmonySuggestionEngineOptions`, exposes the switch; `ChordEditorViewModel` casts to it
  (`SupportsHarmonyEngineSelection`/`HarmonyEngineKind`) rather than taking a new constructor
  dependency, so it degrades to "no picker" gracefully for any test or future caller that injects
  a plain single-backend service. Both `HarmonySuggestionDialog` and
  `HarmonyProgressionSuggestionDialog` grew an "Engine:" combo box (only shown when the injected
  service supports it) that re-queries and updates the list live when switched, without closing
  the dialog.
- **Adapted, not copied, from the source design**:
  - It targeted WPF (`App.xaml.cs`/`StartupEventArgs`, XAML `ComboBox` bindings); this app is
    WinForms with a DI container, so the Markov-model bootstrap moved into
    `AppBootstrapper.CreateHarmonySuggestionService`/`LoadOrTrainMarkovModel` (mirroring how
    `SampleLibraryService`/`AppTheme` already resolve resources off
    `AppDomain.CurrentDomain.BaseDirectory`), and the engine picker became a cast-based optional
    capability instead of a second parallel `IHarmonySuggestionStrategy` interface duplicating the
    app's existing `IHarmonySuggestionService`.
  - **Fixed a real bug in the source design's adapter before porting it**: its single-note
    `JianpuNoteAdapter` discarded every rest wholesale, continuation-dot rests included. In this
    app's model a continuation-dot rest (`JianpuNote.IsContinuation`) isn't silence -- it extends
    the *previous* note's duration (see `JianpuNote.IsContinuation`'s doc comment, and
    `ScoreMidiSchedule`'s identical handling for playback) -- so dropping it would have silently
    shrunk a held note's weight in the melody-fit scoring. `JianpuNoteAdapter.ToHarmonyMelodyNotes`
    is sequence-aware instead: a continuation-dot rest extends the previous `HarmonyMelodyNote`'s
    `Duration` (via the same `JianpuRenderer.GetDurationUnits` every other duration calculation in
    this codebase already uses) rather than being dropped or treated as a new, pitchless note.
    Covered by `JianpuNoteAdapterTests` (a bare rest is skipped and extends nothing; a
    continuation dot with nothing before it is ignored; multiple consecutive continuation dots all
    extend the same note).
  - **Fixed a real bug found while porting `RomanNumeral.ToString()`**: the source design both
    lower-cased the numeral for a minor-family quality (roman-numeral convention) *and* appended
    that quality's full suffix (`"m"`/`"m7"`/...), double-encoding minor-ness -- e.g. `vi` came
    out as `"vim"`, `ii7` as `"iim7"`, neither matching the plain `"vi"`/`"ii7"` the corpus and the
    rest of this codebase expect. `RomanNumeral`'s private `RomanSuffix` now strips the redundant
    leading `"m"` for a minor-family quality (the case already signals it), while
    `HarmonyEngine.ComputeChordSymbol` still uses the untouched `ChordQualityExtensions.GetSuffix`
    for plain chord-symbol text (`"Dm7"`, `"F#dim"`) that has no letter case to lean on. Caught by
    a scratch verification harness before it ever reached a checked-in test (see below).
  - Dropped from the ported design as unreachable/unused given this app's actual, stateless
    per-call `IHarmonySuggestionService` interface (a single-measure suggestion call never sees
    neighboring measures, so there is no session to carry "previous chord" context across calls):
    the `HarmonicContext` session object and its user-preference learning, `RomanNumeral.Inversion`
    and the inversion-aware chord-symbol logic that used it (nothing in the vocabulary ever set
    it), `ChordQuality.Major6`/`Minor6`/`IsSeventh` (never referenced by the ported algorithm), and
    `HarmonyMelodyNote.Octave`/`BeatPosition` (computed but never read).
- **Verification**: the pure-algorithm code (`ChordVocabulary`, `VoiceLeadingScorer`,
  `MarkovChordModel`, `ChordTransitionTrainer`, `HarmonyEngine`, `ProgressionEngine`,
  `RomanNumeral`) has no WinForms dependency at all, so it was compiled and run directly with
  `mcs`/`mono` as a standalone harness (independent of the xunit test files, which need the
  net8.0-windows / WindowsDesktop runtime this sandbox doesn't have) -- this is where the two bugs
  above were actually caught, before ever being committed. The WinForms-touching pieces
  (`JianpuNoteAdapter`, `MarkovHarmonySuggestionService`, `SelectableHarmonySuggestionService`)
  were verified by compiling a second harness against the real built `JianpuEditor.exe` (named to
  match the project's `InternalsVisibleTo` grant so it can see the same `internal` types the real
  test project sees). `AppBootstrapper.CreateHarmonySuggestionService`/`LoadOrTrainMarkovModel`
  were verified end-to-end via reflection, run from inside the real net472 build output directory:
  confirms the corpus is actually found and trained through `AppDomain.CurrentDomain.BaseDirectory`
  exactly as the shipped app will resolve it, that the resulting model gets cached to
  `Data/markov_chord_model.txt`, and that a second run correctly loads the cache instead of
  retraining. All new/changed files also passed `dotnet build` (zero errors) and
  `dotnet format --verify-no-changes` (after the usual CRLF round-trip this sandbox needs -- see
  the SATB passes above for why).

**Follow-up fix, found while answering "can chords be suggested for a whole song?" -- done.**
`ProgressionEngine.SuggestProgression`'s template library only goes up to 4 measures; for a longer
range (the common case for "whole song") it fell back to `Templates[4].Select(t => t.Take(count))`
-- `Take` on a 4-element array with `count > 4` just returns all 4 elements unchanged, so the
"Enhanced (Markov)" engine silently suggested chords for only the first 4 measures of any longer
selection and left the rest blank on Apply, with no error. The original Legacy engine
(`HarmonyProgressionService.ExpandTemplate`) never had this problem -- it already pads a base
template out to any length. Fixed the Markov engine's `ProgressionEngine` with a new
`ExtendTemplate` that does the same "alternating IV/V filler, forced V-I cadence at the end" pad
as `ExpandTemplate`, so both backends now behave predictably and completely past 4 measures.
Covered by new `HarmonyEngineTests.ProgressionEngine_LongerThanFourMeasures_CoversEveryMeasureInsteadOfTruncating`
and `SelectableHarmonySuggestionServiceTests.MarkovHarmonySuggestionService_SuggestForMeasureRange_WholeSong_CoversEveryMeasure`.

## Chord playback style ("play the chord track like in real music") — done

User's follow-up question after the harmony-engine work above: chords were already playing
automatically alongside the melody on every Play (not muted/absent), but as one flat, fully
sustained block chord per chord marker -- struck once, held for the whole span, no rhythm. That's
what didn't sound "like in real music": a real backing part re-articulates the chord in some
pattern instead of just holding it. Explicitly *not* about mute/solo (muting the melody to get a
"minus one" backing track) -- that's a separate, not-yet-implemented request, tracked above under
"Other suggested features".

- **New `ChordPlaybackStyle` enum** (`Models/ChordPlaybackStyle.cs`): `Block` (the original,
  default, only-ever behavior -- an existing score with nothing set plays identically to before),
  `Comping`, `Arpeggio`, `Strum`. A new `JianpuScore.ChordPlaybackStyle` property stores the
  choice per score/song (mirroring how `MelodyInstrument`/`ChordInstrument` already work), exposed
  via a new "Chord style" combo box in the existing Instruments dialog and applied through a new
  undoable `ModifyChordPlaybackStyleCommand`.
- **`ScoreMidiSchedule.BuildChordNotes`** dispatches each chord marker's span to one of four new
  note-generation methods based on the style, all built from the same
  `ChordParser.ToBlockChordMidiNotes` note set the original code already used:
  - `Comping`: re-strikes the chord on every beat within its span, each hit gated to 85% of the
    beat so it reads as a detached rhythmic re-articulation rather than one long sustained note --
    a simple piano/guitar backing pattern.
  - `Arpeggio`: breaks the chord into an up-down broken-chord sequence (e.g. a triad becomes root,
    third, fifth, third, root, ...) played one note at a time on 8th-note (0.5-beat) steps, instead
    of struck together.
  - `Strum`: same notes as Block, but each note's onset is staggered by a small, tempo-relative
    fraction of a beat (low to high), mimicking a real strum across strings, with each note's
    duration shortened to match so nothing hangs past the chord's actual span.
  - `ChordParser.ToBlockChordMidiNotes` doesn't return notes in pitch order (a slash-chord bass
    note is appended last and can be lower than the root), so `Arpeggio`/`Strum` sort ascending
    first rather than assuming the raw return order is already low-to-high.
  - Since both live playback (`ScorePlaybackService`) and MIDI export (`MidiExportService`)
    already just iterate whatever `ScoreMidiSchedule.Build` produces, changing only the shared
    `BuildChordNotes` method makes every style apply identically to both without touching either
    of those files -- the same "single source of truth" pattern the extra-voice-instrument fix
    used earlier in this session.
- **Verified**: `ScoreMidiScheduleTests` covers each style's event count/timing/gating directly
  (e.g. Comping produces `chordToneCount * 4` events for a 4-beat chord at 4 distinct beat starts;
  Arpeggio produces exactly 8 single-note events, never a stacked chord, for the same span; Strum's
  onsets strictly increase and its pitches sort ascending). `ScoreDocumentViewModelTests` covers
  the new edit command's apply/undo/no-op-when-unchanged. Also verified end-to-end against the
  real built assembly under Mono (same harness pattern as the harmony-engine work above): all four
  styles produce the expected event shapes, and `ChordPlaybackStyle` survives a real
  `ScoreFileService.Save`/`Load` JSON round-trip.

### Follow-up: the four styles above weren't "real" enough yet -- done

User's reaction after trying Comping/Arpeggio/Strum: the *rhythm* was right but it still didn't
sound like real music. Asked what specifically was still off; the answer was everything at once --
flat/robotic dynamics, bare chord voicing, patterns that don't adapt to the song's meter, and (a
separate axis entirely) the instrument/timbre itself. Addressed the first three in code; the
fourth isn't a pattern-generation problem -- see the note at the end of this section.

- **Dedicated bass note + voice leading.** Every chord now gets an explicit bass note one octave
  below its lowest tone (`BassOctaveDrop`), added to whatever the style's pattern already does --
  real backing has a distinct, louder bass, not just a cluster of upper-voice tones. The upper
  voicing itself is now voice-led: `ApplyVoiceLeading` tests shifting each new chord by whichever
  of {-1, 0, +1} octaves keeps its average pitch closest to the *previous* chord's, recomputed from
  the untransposed notes each time (so it can't drift arbitrarily far over a long progression) --
  the same "stay in a settled register, move as little as possible" instinct a real accompanist
  has, instead of every new chord symbol resetting to the same fixed root-position octave.
- **Humanized, non-flat dynamics.** `GetHitVelocity` replaces the old flat `ChordVelocity` constant
  with an accent/secondary/bass/jitter model: an attack (a chord's first strike, or the start of
  each Arpeggio up-down cycle) is louder than a re-strike within the same harmony; the bass note is
  louder still; and a small random +/-4 jitter is applied to every hit so identical chords don't
  sound mechanically identical. `ChordVelocity` (now unused everywhere) was removed rather than
  left as dead public API.
- **Meter-aware Comping.** The old Comping just re-struck the *whole* chord every beat, undifferentiated -- the single least "real" of the four patterns despite being the most commonly
  requested one (piano/guitar comping). It's now a genuine bass/chord alternation: "boom-chick" in
  a duple meter (bass, chord, bass, chord...) or, detected from `score.TimeSignature` via the
  existing `TimeSignatureService`, "oom-pah-pah" in a triple meter (bass, chord, chord...) -- the
  single most recognizable difference between a generic backing pattern and one that actually fits
  a waltz. Arpeggio and Strum needed no pattern-shape changes for this: both already start from the
  lowest voiced note (now the new bass note) via their existing ascending sort, so they picked up
  the richer voicing for free.
- **Instrument/timbre is a separate axis, not fixed here.** No amount of pattern-generation code
  changes how the underlying GM soundfont patch itself sounds -- that's a completely different
  lever. The existing Instruments dialog (Edit > Instruments) already lets a user pick a different
  GM instrument for the chord channel (an idiomatic backing timbre like Acoustic Guitar (nylon) or
  Vibraphone often reads as more "real" than generic Piano for this purpose), and the app already
  supports a custom SoundFont (`AppTheme.CustomSoundFontPath`) or a VST2 chord plugin
  (`AppTheme.VstChordPluginPath`, see `AppBootstrapper.CreateMidiOutput`) for anyone who wants
  genuinely better-sampled instruments. Not something this pass touches.
- **Verified**: `ScoreMidiScheduleTests` rewritten for the new behavior -- bass note presence,
  register, and louder-than-the-rest velocity (proven as a guaranteed inequality given the boost
  and jitter constants, not a flaky random assertion); voice leading keeping two chords with a
  known-11-semitone *raw* gap (C then B, both from the same fixed root octave) under 6 semitones
  once voice-led; Comping's exact beat-by-beat bass/chord split in both 4/4 and 3/4; Arpeggio's
  first-step-vs-second-step accent ordering (also a guaranteed inequality: worst-case accented+bass
  velocity still beats best-case secondary velocity). Re-ran the same real-assembly Mono harness
  used for the previous pass, extended with all of the above plus a printed velocity sequence for
  manual inspection -- all pass.
