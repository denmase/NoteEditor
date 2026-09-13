# MIDI Import Spike (v1.3) — Issue #51 / #52

## Goal

Generate an editable `.jianpu` score from a MIDI file, lowering the cost of building the sample library and transcribing scores (relates to #21 / #22).

## Approach

| Module | Description |
|------|------|
| `MidiImportService` | In-house MIDI parsing (mirrors `MidiExportService`; no third-party library) |
| `MeasureNormalizationService` | After import, normalizes measures to 4/4: splits overly long notes, pads the end with rests, and merges very short trailing measures |
| `JianpuPitchCodec` | Semitone `.5` encoding with `#` / `b` display, and MIDI pitch conversion in both directions |
| Melody track selection | Format 0 is used directly; Format 1 picks the non-drum track with the most notes |
| Duration quantization | Sixteenth-note grid (0.25 beat) |
| Pitch mapping | In-key notes 1–7; semitones → `Pitch` decimal + `Accidental` (`#1` / `b3`) |
| Measure splitting | Defaults to 4/4 (4 beats per measure), gaps auto-filled with rests |
| UI | **File → Import MIDI... (Spike)** |

## Acceptance criteria (Spike + #52)

- [x] Supports single-melody / simple multi-track MIDI (melody channel 0)
- [x] Automatically converts to Jianpu digits 1–7 plus duration and octave dots
- [x] Semitones display as `#` / `b`, with correct pitch on playback and MIDI export
- [x] After import, measures are predominantly 4 beats (normalized)
- [x] Output can be edited, played back, and exported in the editor
- [x] Export → import round-trip test (`MidiImportServiceTests`)

## Command-line trial

```powershell
# First export a MIDI file
.\JianpuEditor\bin\Debug\net472\JianpuEditor.exe

# Or via script: convert MIDI to .jianpu JSON
.\scripts\import-midi.ps1 -InputMidi "path\to\file.mid" -OutputJianpu "out.jianpu"
```

## Known limitations

- **Not imported**: slurs/ties, ornaments, lyrics, chords (only the main melody channel)
- **Quantization**: only discrete durations such as 1/16, 1/8, dotted, 1/4–whole note are supported; complex tuplets may be approximated
- **Accidental entry**: the toolbar does not yet support manually entering `#` / `b` (only produced by MIDI import)
- **Format**: SMPTE timecode is not supported; multi-track merging does not separate voices

## Next steps

1. Remove the "(Spike)" label from the menu and unify with the open/save flow
2. Let the import dialog choose key signature, quantization precision, and beats per measure
3. Support accidental entry in the toolbar
4. Import a second track as chord annotations (#22)
5. Extend the round-trip export comparison test to the Ode to Joy sample
