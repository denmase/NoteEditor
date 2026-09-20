# Jianpu Editor (JianpuEditor)

A Jianpu (numbered musical notation) editing tool built on C# WinForms, supporting main melody editing, ornaments, chord markers, lyrics, ties, as well as JSON saving, PDF/MIDI export, and score playback.

![Jianpu Editor interface screenshot](Screen%20Sample.png)

## Features

- **Main melody editing**: Enter notes 1–7, rest 0; click between notes to insert
- **Simultaneous notes / chord notes (v1.3 #57)**
  - A single beat position can contain multiple melody notes (e.g., chord notes imported from MIDI)
  - Displayed stacked vertically on the canvas; sounds simultaneously during playback and MIDI export
  - Stored in JSON as `Chords[]`; older scores containing only `MelodyNotes` are automatically migrated on open
- **Multi-selecting notes**
  - **Ctrl + Left click**: Add/remove a single note from the selection
  - **Shift + Left click**: Batch add/remove the range from the anchor to the current note (can span measures)
  - When multi-selecting across measures, the toolbar's "From / To" measure range automatically syncs the highlight
  - With multiple selection, toolbar ornament/delete operations etc. apply in batch
- **Cut / Copy / Paste**
  - **Ctrl+X** / **Ctrl+C** / **Ctrl+V** (also on the Edit menu and the right-click menu) cut, copy, or paste the selected note(s)
  - Paste inserts after the last selected note, or at the selected gap, or at the end of the current measure; works across tabs and across open scores
  - An in-app clipboard (not the OS clipboard), separate from the tie/chord/lyric/ornament data attached to the copied notes -- those are not carried over
- **Right-click context menu**
  - Context-aware: shows different commands depending on what's under the cursor -- a note (Cut/Copy/Delete, Shorten/Extend, octave/transpose, Split/Merge, Tie, Ornaments), an empty gap (Insert Note/Rest, Paste), a tie (Remove Tie), a chord marker (Add/Delete), lyric text (Align Lyrics), or empty space (Add Measure, Duplicate Measure(s), Paste, and the existing "Move playback marker here")
  - Right-clicking a note or chord marker that isn't already selected selects it first, so the menu always acts on what you clicked
- **Duration modification**
  - **Increase (+) / Decrease (-) duration**: cycles through six levels: 1/16 → 1/8 → 1/4 → extend 1 beat → extend 2 beats → extend 3 beats
  - High octave dot / low octave dot / dot (dotted note)
- **Pitch adjustment**
  - **Key up / Key down**: raise or lower the selected notes by scale degree within the current key (1–7 and octave dots)
- **Split / Merge**
  - **Split**: A 1/4 note splits into two 1/8 notes; a note longer than 1/4 splits into equal 1/4 segments; 1/16 notes cannot be split
  - **Merge**: Within a measure, notes are paired by index `(0,1), (2,3), …` and merged; requires them to be adjacent on the score with a duration ratio ≤ 2; the pitch of the first note is kept; an unpaired leftover note is left unchanged
- **Undo / Redo**
  - Menu **Edit → Undo** (**Ctrl+Z**) / **Redo** (**Ctrl+Y**) steps backward or forward through score edits
  - Covers note editing, deletion, measures, transposition, header/lyric/chord inline editing, ties, ornaments, bulk lyric editing, etc.; the undo/redo stack is cleared after creating, opening, or loading a score
- **Ornaments**
  - Toolbar "Ornaments" section: **Grace note** / **Trill** / **Turn** / **Mordent** / **Fermata** / **Breath Mark** / **Staccato** / **Accent** / **Tenuto** / **Segno** / **Coda**; the menu **Edit → Ornaments** provides the same options
  - Select one or more notes first, then click an ornament button; with multiple selection, ornaments are added in batch
  - Clicking the same button again removes that type of ornament from the note (other types are kept)
  - Delete / "Delete" removes ornaments on the selected note first
  - The canvas and PDF export draw placeholder symbols above the note (grace / tr / turn / fermata / stac / acc / ten / segno / coda); a breath mark draws just after the note instead, matching where it's placed in notasi angka/jianpu sheet music
  - Score playback and MIDI export expand grace notes, trills, turns, mordents, and fermata durations; staccato shortens the sounding duration, accent and tenuto boost velocity (accent more than tenuto); a breath mark, Segno, and Coda are visual-only and don't affect playback/export -- Segno/Coda mark where a D.S./D.C. jump would go, but the jump itself isn't performed during playback yet
- **Dynamics**
  - Toolbar "Dynamics" section: **pp** / **p** / **mp** / **mf** / **f** / **ff**; the menu **Edit → Dynamics** provides the same options
  - Select a note first, then click a dynamic level; clicking the same level again removes it, clicking a different level replaces it (a note can only be at one dynamic level at a time, unlike ornaments)
  - Dynamics render in their own row directly below the melody row, in italic bold type
  - The dynamic level applies to that note and every note after it -- across measures -- until the next dynamic marking or the end of the score, both during playback and MIDI export (each level maps to a fixed velocity; a score with no dynamics plays exactly as before)
  - Currently applies to the melody part only, not chord markers; there's no click-to-add-at-a-beat or drag-to-move interaction yet (select a note, then use the toolbar/menu)
- **Ties**
  - Click "Tie" → select the start note → select the end note; Esc to cancel
  - The end note must be the same pitch as the start note (a tie sustains one pitch); picking a
    different pitch keeps tie mode active and treats that note as a new start candidate instead
  - Click the arc to select it (highlighted in blue)
  - Delete / "Delete" removes the selected tie
  - Deleting the tie's start/end note automatically clears the tie
- **Chord markers (secondary melody row)**
  - Up to 4 chord markers per measure, aligned to quarter-beat positions
  - Click an empty beat position to add one; click a marker to edit it inline
  - Drag `::` to change the beat position; dragging onto another marker's beat position swaps their order
  - Click `x` or press Delete to remove; the toolbar's "Chord" box can edit the selected item in sync
  - Only text recognized as a chord symbol participates in playback and MIDI export
- **Header editing**: Click the score title, key signature, tempo, BPM, or composer to edit inline directly (the toolbar has been simplified)
- **Chord transpose**
  - Menu "Edit → Chord Transpose..."
  - After entering the target key signature, all chord symbols in the secondary melody are automatically transposed (e.g., `C` → `G`, `Bm7` → `F#m7`, `G/D` → `D/A`)

- **Chord suggestion (v2.0 #31)**
  - Menu "Edit → Chord Suggestion..."
  - Single measure: recommends 1–3 chords based on the key signature and melody pitch (e.g., I / V / vi in C major)
  - Consecutive measures: combines the melody's bass notes and harmonic direction to recommend 1–3 chord progressions (e.g., I–IV–V–I), writable to each measure with one click
  - Purely local rule-based logic, no external LLM
  - Updates the score's key signature field in sync; the main melody's numbered Jianpu digits are not transposed
  - Supports key signature formats such as `C`, `1=G`, `F#`, `Bb`, `D major`, etc.
- **Lyrics**
  - Click the lyric row to edit the entire `LyricText` line inline
  - **Bulk edit lyrics**: Menu **Edit → Bulk Edit Lyrics...** lists each row's lyrics by measure range, allowing multiple measures to be modified at once
  - Optional **Re-align current range**: maps lyrics to main melody notes by syllable (skipping rests and tie-continuation notes), writing to `LyricSyllables`
  - After alignment, the canvas displays each syllable under its corresponding note; when not aligned, the full lyric line is still shown
  - Bulk changes are merged into a single undo record (**Ctrl+Z** restores it in one step)
- **Three-row layout**: main melody, chord markers, lyrics
- **Measure operations**
  - Create a new measure; **Edit → Add Measure (with placeholders)** (**Ctrl+Shift+N**) pre-fills 4 quarter-note placeholders
  - **View → Fill placeholders by default for new measures** makes a regular "Add Measure" include placeholders too
  - Set the "From / To" measure numbers in the toolbar, then copy the measure range
- **File**: Save/open in JSON format
- **PDF export**: A4 portrait, 4 measures per row; long scores are automatically paginated across multiple pages (the first page has the full header, subsequent pages show the title and page number); accidentals, octave dots, and ornaments are laid out in layers (to avoid overlap on narrow notes); chords are output as plain text with no editing border
- **MIDI export**
  - The main melody is exported according to Jianpu durations and the key signature; ornaments are expanded into extra MIDI notes or extended durations
  - Chords are exported as block chords according to their beat start/end times (e.g., `D`, `Bm7`, `G/D`)
  - Tempo is controlled by BPM (independent of the score's "Tempo" text)
- **MIDI import (Spike)**
  - **File → Import MIDI... (Spike)**: Generates an editable Jianpu score from a `.mid` file
  - Automatically detects the key signature (including `1=B` etc.), quantizes to sixteenth notes, and normalizes to 4/4 measures (splitting overly long measures, filling in rests)
  - Multiple notes at the same instant are merged into a simultaneous-note slot, so chord notes are no longer lost
  - Chromatic pitches are represented with `.5` (e.g., `1.5` = `#1`, `2.5` = `b3`), kept consistent across the canvas, playback, and MIDI export
- **Score playback**
  - Toolbar "Play / Stop" plays the main melody and chords in real time according to BPM
  - The blue progress bar can be dragged to seek; playback logic shares scheduling with MIDI export (including ornament expansion)
  - Rendered with a bundled SoundFont (BASSMIDI + [GeneralUser GS](JianpuEditor/Resources/Soundfonts/LICENSE.txt)), so playback sounds the same on every machine rather than depending on whatever GM device Windows happens to provide; falls back to the system MIDI device automatically if the bundled synth fails to initialize
- **Instrument selection**
  - `Edit → Instruments...` (or the toolbar "Instruments..." button) picks a General MIDI instrument for the melody and for chords independently
  - Applies to both live playback and MIDI export, so the exported file sounds the same as in-app playback; saved with the score (`MelodyInstrument`/`ChordInstrument`, default Acoustic Grand Piano)
- **Custom instrument file (optional)**
  - `Edit → Audio Engine...` can point playback at a custom SoundFont (`.sf2`) or SFZ (`.sfz`) instrument file instead of the bundled SoundFont, for higher-quality piano/guitar/cello sounds than the default General MIDI set — both formats load through the same BASSMIDI engine, so instrument selection, multi-timbral melody/chord playback, and MIDI export all keep working unchanged
  - This is a machine-local app preference (not saved in the score file, since a file path isn't portable between machines) and takes effect after restarting the app
  - The toolbar shows an **"Engine: ..."** indicator (next to "Instruments...") naming whichever engine is actually active — click it to open `Edit → Audio Engine...`, which also lists the active engine at the top. If a configured instrument file fails to load, playback silently falls back to the bundled SoundFont; the indicator and dialog reflect that fallback rather than the (non-functional) configured path

## Requirements

- Windows
- [.NET Framework 4.7.2](https://dotnet.microsoft.com/download/dotnet-framework/net472) or higher
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (used for unit tests; the version is specified by `global.json` in the repository root)
- Running tests in Rider requires the **.NET 8 x86 runtime** (the 32-bit ReSharper Test Runner uses `Program Files (x86)\dotnet`); after upgrading the TFM, please **Build → Rebuild Solution** and clear the old `bin/Debug/net6.0` cache
- Playback uses a bundled SoundFont synthesizer (BASSMIDI), so no external MIDI synthesizer setup is needed; if it fails to initialize for any reason, playback falls back to whatever GM device Windows provides (such as Microsoft GS Wavetable Synth)
- **BASS/BASSMIDI licensing note:** free for individual, non-commercial use; a commercial fork/distribution needs its own license from [un4seen.com](https://www.un4seen.com/bass.html) — see `JianpuEditor/Native/NOTICE.md`

## Build and Run

```powershell
dotnet build JianpuEditor.sln -c Debug
dotnet test JianpuEditor.sln -c Debug   # Requires .NET 8 SDK
.\scripts\check-format.ps1             # Code format check (same as CI)
dotnet format JianpuEditor/JianpuEditor.csproj   # Auto-fix formatting

The build automatically runs the Roslyn analyzers (`Microsoft.CodeAnalysis.NetAnalyzers`, Recommended rule set); rules are defined in `Directory.Build.props` and `.editorconfig` in the repository root.
.\JianpuEditor\bin\Debug\net472\JianpuEditor.exe
```

Release build:

```powershell
dotnet build JianpuEditor.sln -c Release
.\JianpuEditor\bin\Release\net472\JianpuEditor.exe
```

## Installer

Build a Windows installer using Inno Setup:

```powershell
.\scripts\build-installer.ps1
```

Output file: `installer/output/JianpuEditor-Setup-1.2.0.exe`

## Portable build

No installation, no admin rights, no registry writes — just unzip and run `JianpuEditor.exe` from anywhere (a USB drive, a shared folder, etc.). It's the same Release build output as the installer (`JianpuEditor/bin/Release/net472/`), plus a `portable.txt` marker file that tells the app to keep `settings.json` next to the exe instead of `%LOCALAPPDATA%` — so the whole app, its settings, and (if you use one) your chosen VST plugin path all travel together. Delete `portable.txt` to fall back to per-user settings.

The `Release` GitHub Actions workflow (see below) builds this automatically as `JianpuEditor-Portable-<version>.zip`, alongside the installer. To build one locally:

```powershell
.\scripts\build-portable.ps1
```

Output file: `installer/output/JianpuEditor-Portable.zip`

## CI/CD

GitHub Actions workflows are located at `.github/workflows/`:

| Workflow | Trigger | Description |
|--------|------|------|
| **CI** | Push / PR to the `main` branch | `dotnet format` check + Roslyn analyzers + Release build + unit tests + MIDI smoke test (with NuGet / .NET caching) |
| **Release** | Pushing a `v*` tag or manual run | Builds the installer package (`.exe` + `.zip`) and a portable `.zip`; automatically creates a GitHub Release with all three assets when triggered by a tag |

### Publishing a New Version

```powershell
git tag v1.2.0
git push origin v1.2.0
```

You can also manually specify a version number in GitHub under **Actions → Release → Run workflow**, which only generates the installer artifact (without creating a Release).

## Basic Operations

| Action | Description |
|------|------|
| Click a note | Select it and modify duration, octave, dot, etc. |
| Ctrl / Shift + click a note | Multi-select notes; syncs the highlighted measure range when spanning measures |
| Click between notes | Insert a new note at that position |
| Click a header field | Edit the title, key signature, tempo, BPM, or composer inline |
| Click a chord marker | Select and edit it inline; the toolbar's "Chord" box stays in sync |
| Click an empty beat position in the secondary melody row | Add a chord marker at that beat position |
| Click a lyric row | Edit the entire lyric line inline |
| Bulk edit lyrics | **Edit → Bulk Edit Lyrics...**; you can check "re-align", after which each syllable is shown under its note |
| Increase (+) / Decrease (-) duration | Cycles through six duration levels (1/16 to extend 3 beats) |
| Key up / Key down | Raises or lowers the pitch of selected notes within the current key |
| Split / Merge | Splits or merges the duration of selected notes |
| Toolbar "From / To" + Copy Measures | Copies measures within the specified range |
| Tie | Click "Tie" → select the start/end note; click the arc to select it, Delete to remove |
| Ornaments | Select a note, then click "Grace Note / Trill / Turn / Mordent / Fermata / Breath Mark / Staccato / Accent / Tenuto / Segno / Coda" in the toolbar; click the same button again to remove it |
| Dynamics | Select a note, then click "pp / p / mp / mf / f / ff" in the toolbar; click the same level again to remove it |
| Undo / Redo | **Edit → Undo / Redo** or **Ctrl+Z** / **Ctrl+Y** |
| Play / Stop | Plays the score according to BPM; drag the blue progress bar to seek |
| Transpose | Menu "Edit → Chord Transpose..."; transposes chord markers only |
| Export PDF / MIDI | Export from the "File" menu |
| Import MIDI | **File → Import MIDI... (Spike)** |
| Dark mode | Menu **View → Dark Mode** (the setting is saved locally; PDF export still uses a light paper background) |

On startup, the "Ode to Joy" sample score is loaded automatically. The `sample/` directory provides more examples (such as "Canon"), which can be loaded via **File → Sample Library** or the toolbar's **Library** button.

## Chord Markers

Up to 4 markers per measure, each containing free text and a beat position (`BeatPosition`, where 0 is beat 1).

Example editing UI (two chords in measure 1):

```
Beat: 0      2
      C      G
```

During playback and MIDI export, only valid chord symbols are parsed; non-chord text is still displayed but produces no sound.

Supported chord types: major, minor, `7`, `maj7`, `m7`, inversions (e.g., `G/D`), and other common notations.

### Chord Transpose

When transposing from the current key signature (e.g., `1=C`) to a target key signature (e.g., `G` or `1=G`), each measure's `ChordMarkers` is traversed, and only text recognized as a chord symbol by `ChordParser` is transposed by semitone; free text (such as "Interlude") is left unchanged. After transposing, the key signature field is updated to the `1=<target note name>` format.

The main melody's Jianpu digits (1–7) are not transposed; to transpose the melody, edit it manually.

## Lyrics and Alignment

Each measure's lyrics have two representations:

- **`LyricText`**: The full line of text; click the lyric row to edit it directly
- **`LyricSyllables`**: A list of syllables, each bound to a main melody note index

**Re-alignment** rules:

- Chinese text is split by character and English text by spaces, then mapped in order to alignable melody notes
- Rests and tie-continuation notes (tie endpoints) do not participate in alignment
- When the number of lyric characters doesn't match the number of notes, as many syllables as possible are aligned, and the status shows the excess/remainder

The **Bulk Edit Lyrics** dialog supports specifying "from measure N to measure M"; the initial range is taken from the currently selected measures. After editing, you can optionally re-align, and all changes are undoable as a single operation.

## Score File Format

Scores are saved as JSON (`.json` / `.jianpu`), with the main fields:

- `Title`, `KeySignature`, `Tempo`, `Bpm`, `Composer`
- `Measures[]`: Each measure contains `MelodyNotes`, `Chords`, `ChordMarkers`, `LyricText`, `LyricSyllables`, `Ornaments`
- `Ties[]`: Ties (start/end measure and note indices)

Measure field descriptions:

| Field | Description |
|------|------|
| `MelodyNotes[]` | Main melody slots (one primary-note view per beat); `Pitch` is `1`–`7` or a chromatic value such as `1.5` / `2.5` (paired with `Accidental`) |
| `Chords[]` | Simultaneous-note slots: `{ "BeatPosition": 0, "Notes": [ ... ], "Text": "" }`; `Notes` can contain multiple notes sounding at once |
| `ChordMarkers[]` | `{ "Text": "C", "BeatPosition": 0 }` (chord symbols in the secondary melody row, distinct from `Chords`) |
| `LyricText` | The full lyric line text |
| `LyricSyllables[]` | Lyrics by syllable, `{ "Text": "Hi", "NoteIndex": 0, "BeatPosition": 0 }`; when present, the canvas draws each syllable under its note |
| `Ornaments[]` | Ornaments, `{ "Type": "Trill", "NoteIndex": 0, "BeatPosition": 0 }`; `Type` is an enum name (e.g., `GraceNote`, `Trill`, `Turn`, `Fermata`) |
| `Pitch` / `Accidental` | Natural pitch `Pitch: 3`; chromatic pitches such as `Pitch: 1.5, Accidental: "Sharp"` (displayed as `#1`) or `Pitch: 2.5, Accidental: "Flat"` (displayed as `b3`) |

When opening an older score that has only `LyricText` and no `LyricSyllables`, it is still displayed as a full line; performing re-alignment or bulk editing with alignment checked generates syllable data. Older files without `Ornaments` or `Chords` fields open normally; on load, `MelodyChordService` automatically migrates `MelodyNotes` into `Chords`.

`Tempo` is the display term shown on the score (e.g., "Moderato"), while `Bpm` is the beats-per-minute value used for playback and MIDI (default 120).

## Logs

Runtime logs are written to:

```
%LocalAppData%\JianpuEditor\logs\jianpu-editor.log
```

When a playback- or MIDI-related error occurs, the error dialog will point to the log path above.

## Architecture (MVVM)

This project uses **CommunityToolkit.Mvvm** + **Microsoft.Extensions.DependencyInjection** to separate the WinForms UI from the editing logic:

| Layer | Directory | Responsibility |
|------|------|------|
| **View** | `MainForm.cs`, `Views/`, `Controls/` | Menus, toolbar, dialogs; `ILayoutService` / `WinFormsLayoutService` manages layout and DPI; the thin View layer only handles WinForms and file selection |
| **Glue** | `Glue/` | `MainFormViewBinder` (two-way control ↔ ViewModel binding), `ScoreCanvasGlue` (canvas refresh and selection sync), `ScoreSelectionMapper` |
| **ViewModel** | `ViewModels/` | Edit commands, score state, selection coordination; publishes messages such as `ScoreEditedMessage` via `IAppMessenger` |
| **Model** | `Models/` | Pure POCOs: `JianpuScore`, measures, notes, chord markers |
| **Core** | `Core/Abstractions/`, `Core/Messaging/` | Service interfaces (`IScoreFileService`, `IScoreUndoService`, `IPdfExportService`, etc.) and the message bus |
| **Services** | `Services/` | Static business logic + DI adapters; includes `MelodyChordService` (simultaneous-note slots and migration), `EditCommandHistory` (undo/redo), `OrnamentService`, `OrnamentPlaybackService`, `LyricAlignmentService`, `BulkLyricEditService`, `NoteSplitMergeService`, `JianpuPitchService`, etc. |
| **Rendering** | `Rendering/` | Layout, drawing, `AppTheme` (light/dark theme) |

**Data flow**: User action → `MainForm` calls a ViewModel method → returns `ScoreEditResult` → `ScoreCanvasGlue` updates the canvas → `MainFormViewBinder` syncs the controls.

**Dependency injection** (`AppBootstrapper.cs`): All ViewModels and Service interfaces are registered as Singleton, and `MainForm` is Transient.

## Project Structure

```
JianpuEditor/
  Program.cs               # Startup, DI container, theme loading
  AppBootstrapper.cs       # Service and ViewModel registration
  MainForm.cs              # Main UI (thin View layer)
  Views/                   # ILayoutService, layout context
  Glue/                    # View ↔ ViewModel glue layer
  ViewModels/              # MVVM ViewModels (10)
  Core/                    # Interface abstractions and messaging
  Controls/ScoreCanvas.cs  # Canvas, selection, playback progress bar, chord inline editing
  Models/                  # Score, measures, notes, ties, chord markers
  Rendering/               # Layout, drawing, AppTheme
  Services/                # JSON/PDF/MIDI, playback, chord parsing/transpose, tie maintenance
  installer/               # Inno Setup installer scripts
  scripts/                 # Build and test scripts
  sample/                  # Sample library (.jianpu / .json)
JianpuEditor.Tests/        # xUnit unit tests (248; services, ViewModels, Glue, rendering)
```

## Dependencies

- [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm) 8.4.0
- [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection) 9.0.0
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) 13.0.3
- [PDFsharp](https://www.nuget.org/packages/PDFsharp) 6.2.0
- [ManagedBass](https://www.nuget.org/packages/ManagedBass) / ManagedBass.Midi / ManagedBass.Vst 3.1.1 — .NET bindings for the bundled native [BASS](https://www.un4seen.com/bass.html)/BASSMIDI/BASSVST audio libraries (`JianpuEditor/Native/`, x86 and x64). **Free for individual, non-commercial use only**; a commercial fork/distribution needs its own license from un4seen.com — see `JianpuEditor/Native/NOTICE.md`
- [Microsoft.ML.OnnxRuntime](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime) 1.30.0
- [GeneralUser GS](JianpuEditor/Resources/Soundfonts/LICENSE.txt) v2.0.3 (bundled SoundFont for the default playback engine), License v2.0 — free for personal and commercial use; see the linked license for sample-origin caveats

MIDI export and score playback are implemented in-house, with no third-party MIDI library.

### Audio-to-MIDI import (Spike)

"Import from Audio" transcribes a single-instrument (STEM) recording using one of two
bundled ONNX models, run locally via Microsoft.ML.OnnxRuntime (no Python at runtime):

- **Instrument**: [basic-pitch](https://github.com/spotify/basic-pitch) (Spotify), Apache License 2.0. Handles monophonic and polyphonic (chord) instrument audio.
- **Vocal**: [GAME](https://github.com/openvpi/GAME) (openvpi), code MIT-licensed; **the bundled pretrained model weights are CC BY-NC-SA 4.0 (non-commercial)**. Vocal-only (monophonic singing voice), but produces cleaner results than basic-pitch on real singing audio.

## Contributing

Contributions via Issues and Pull Requests are welcome. Before submitting code, please read the [Contributor License Agreement (CLA)](CLA.md) and confirm your agreement in your first PR.

## License

This project is licensed under the [Apache License 2.0](LICENSE).
