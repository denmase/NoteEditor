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

## Indonesian notasi angka completeness (gap analysis + plan, not started)

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
2. **Accidental slash convention.** Branch `JianpuPitchCodec.GetAccidentalMark`/
   `GetPitchDisplayText` on `NotationStyle`: Indonesian returns a suffix mark (`/` or `\`) instead
   of a prefix; Chinese/Western path is byte-for-byte what exists today. Update whichever
   renderer code lays out glyph width around the accidental mark (it currently assumes a prefix)
   to also handle a suffix. Add `AccidentalKind.Natural` alongside this (rendered as `♮`, no
   NotationStyle branch needed) — turns out simpler than it looks, since the renderer doesn't
   currently carry accidentals through a measure at all (each note's `Accidental` is
   independent), so Natural is just a third glyph state, no new measure-scoped tracking needed.
3. **Fix the tie/slur pitch bug, then add real slur support.** Independent of everything else
   here and arguably the highest-priority item, since it's a correctness bug in scores that exist
   *today*, Chinese/Western included: add a same-pitch check to `TieEditorViewModel.
   TryCompleteTie` (reject or, better, offer to create a slur instead when the picked notes'
   pitches differ) so the tie tool can no longer silently drop a note's pitch during playback.
   Once that guard exists, adding actual slur support is comparatively small: a `JianpuSlur` list
   shaped like `JianpuTie` (no pitch constraint) that only affects rendering (curved line drawn
   over the span) — no playback/MIDI effect needed for a first version, since a slur doesn't
   change what notes sound, only how they're described to play (legato phrasing).
4. **Wire up the four dead `OrnamentType` articulation values** (Staccato, Accent, Tenuto,
   Glissando): ribbon button + Edit-menu item + context-menu item each, matching the existing
   Grace/Trill/Turn/Fermata pattern exactly, plus a glyph and a playback effect (staccato =
   shorten sounding duration; accent = velocity boost; tenuto = slight duration/emphasis;
   glissando = pitch-bend between notes, the one genuinely harder one — may want to split it into
   its own step). While here, also add the missing Mordent button flagged in the last audit —
   same gap, same fix.
5. **Dynamics markings.** New model (a marking anchored to a beat, e.g. `mf`/`cresc.`/`dim.`,
   plus optionally a hairpin start/end pair), a small "add dynamic here" UI mirroring how chord
   markers already attach to a beat, rendering below the melody row, and a playback/MIDI-export
   velocity-scaling pass applied to notes until the next marking.
6. **Breath marks.** Simplest of the remaining additive items — fold into the existing
   `Ornaments` per-note-index list (`OrnamentType.BreathMark`, new value), visual-only glyph, no
   playback effect in v1 (a version that inserts a micro-rest is a possible follow-up, not v1).
7. **Repeat bar lines, volta brackets, and D.C./D.S./Coda/Segno navigation.** Two sub-parts with
   very different risk:
   - **Visual-only** (lower risk): a `BarLineType` field per measure boundary (single/double/
     final/repeat-start/repeat-end), a volta-bracket span (which measures, which ending number)
     for 1st/2nd endings, and `Segno`/`Coda` as measure-anchored markers using the `OrnamentType`
     values that already exist. Playback/MIDI export keep playing straight through once, same as
     today, with a status message noting repeat structure isn't performed.
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
10. **Pickup measure.** Verify first whether a short first measure already plays/exports/renders
    correctly (it may — nothing found in `ScoreMidiSchedule` enforcing an exact per-measure beat
    count). If it does, this is a documentation note, not code. If not, scope it then.

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
