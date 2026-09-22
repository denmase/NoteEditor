using JianpuEditor.Models;
using JianpuEditor.Tests.Helpers;
using JianpuEditor.ViewModels;
using Xunit;

namespace JianpuEditor.Tests.ViewModels
{
    /// <summary>Covers full parity of the core note-editing operations (insert, delete, pitch,
    /// octave, duration, accidentals) on an extra voice (SATB's Alto/Tenor/Bass, or an "above"
    /// descant), routed through <see cref="Services.VoiceLayoutService"/> instead of always
    /// targeting <see cref="JianpuMeasure.MelodyNotes"/>. Split/Merge/Copy/Paste and ornaments/
    /// dynamics stay primary-voice-only (see ROADMAP.md) -- the tests here also confirm that a
    /// selection on an extra voice is safely ignored by those, rather than silently corrupting the
    /// primary voice's own notes at the same index.</summary>
    public sealed class VoiceAwareEditingTests
    {
        private static JianpuMeasure BuildMeasureWithAlto()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2));
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(5) } });
            return measure;
        }

        [Fact]
        public void AddNote_OnSelectedAltoNote_ChangesOnlyThatVoice()
        {
            var (document, selection, messenger, _) = ViewModelTestHelper.CreateDocumentWithSelection();
            var editor = ViewModelTestHelper.CreateNoteEditor(document, selection, messenger);
            document.Score.Measures.Add(BuildMeasureWithAlto());
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, NoteIndex = 0, VoiceIndex = 0 });

            var result = editor.AddNote(3);

            Assert.True(result.Changed);
            Assert.Equal(3, document.Score.Measures[0].ExtraVoices[0].Notes[0].Pitch);
            Assert.Equal(1, document.Score.Measures[0].MelodyNotes[0].Pitch);
        }

        [Fact]
        public void SetOctave_OnSelectedAltoNote_ChangesOnlyThatVoice()
        {
            var (document, selection, messenger, _) = ViewModelTestHelper.CreateDocumentWithSelection();
            var editor = ViewModelTestHelper.CreateNoteEditor(document, selection, messenger);
            document.Score.Measures.Add(BuildMeasureWithAlto());
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, NoteIndex = 1, VoiceIndex = 0 });

            editor.SetOctave(1);

            Assert.Equal(1, document.Score.Measures[0].ExtraVoices[0].Notes[1].Octave);
            Assert.Equal(0, document.Score.Measures[0].MelodyNotes[1].Octave);
        }

        [Fact]
        public void AddNote_OnAltoGap_InsertsIntoAltoOnly()
        {
            var (document, selection, messenger, _) = ViewModelTestHelper.CreateDocumentWithSelection();
            var editor = ViewModelTestHelper.CreateNoteEditor(document, selection, messenger);
            document.Score.Measures.Add(BuildMeasureWithAlto());
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, InsertIndex = 2, VoiceIndex = 0 });

            var result = editor.AddNote(6);

            Assert.True(result.Changed);
            Assert.Equal(3, document.Score.Measures[0].ExtraVoices[0].Notes.Count);
            Assert.Equal(6, document.Score.Measures[0].ExtraVoices[0].Notes[2].Pitch);
            Assert.Equal(2, document.Score.Measures[0].MelodyNotes.Count);
            Assert.Equal(0, result.SelectNoteVoiceIndex);
        }

        [Fact]
        public void ModifyMelodyNotesCommand_Undo_OnAltoNote_RestoresOnlyThatVoice()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var editor = ViewModelTestHelper.CreateNoteEditor(document, selection, messenger, history);
            document.Score.Measures.Add(BuildMeasureWithAlto());
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, NoteIndex = 0, VoiceIndex = 0 });

            editor.AddNote(3);
            Assert.Equal(3, document.Score.Measures[0].ExtraVoices[0].Notes[0].Pitch);

            history.Undo();

            Assert.Equal(5, document.Score.Measures[0].ExtraVoices[0].Notes[0].Pitch);
            Assert.Equal(1, document.Score.Measures[0].MelodyNotes[0].Pitch);
        }

        [Fact]
        public void Delete_OnSelectedAltoNote_RemovesOnlyFromThatVoice()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var chordEditor = ViewModelTestHelper.CreateChordEditor(document, selection, navigation, messenger, history: history);
            var scoreEditor = ViewModelTestHelper.CreateScoreEditor(document, selection, navigation, chordEditor, messenger, history);
            document.Score.Measures.Add(BuildMeasureWithAlto());
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, NoteIndex = 1, VoiceIndex = 0 });

            var result = scoreEditor.Delete();

            Assert.True(result.Changed);
            Assert.Single(document.Score.Measures[0].ExtraVoices[0].Notes);
            Assert.Equal(2, document.Score.Measures[0].MelodyNotes.Count);
        }

        [Fact]
        public void SplitSelectedNotes_WithAltoNoteSelected_DoesNotCorruptPrimaryVoice()
        {
            var (document, selection, messenger, _) = ViewModelTestHelper.CreateDocumentWithSelection();
            var editor = ViewModelTestHelper.CreateNoteEditor(document, selection, messenger);
            document.Score.Measures.Add(BuildMeasureWithAlto());
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, NoteIndex = 0, VoiceIndex = 0 });

            editor.SplitSelectedNotes();

            Assert.Equal(2, document.Score.Measures[0].MelodyNotes.Count);
            Assert.Equal(1, document.Score.Measures[0].MelodyNotes[0].Pitch);
            Assert.Equal(2, document.Score.Measures[0].ExtraVoices[0].Notes.Count);
            Assert.Equal(5, document.Score.Measures[0].ExtraVoices[0].Notes[0].Pitch);
        }
    }
}
