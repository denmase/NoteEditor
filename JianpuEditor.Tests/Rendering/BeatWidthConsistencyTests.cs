using System.Collections.Generic;
using JianpuEditor.Models;
using JianpuEditor.Rendering;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Rendering
{
    /// <summary>Covers the beats-to-pixels consistency fix: two measures with the same total beat
    /// length previously could render at different widths purely because of how finely their notes
    /// happened to be subdivided (many short notes hit <see cref="JianpuRenderer.MinNoteWidth"/>'s
    /// per-note floor more than a few long ones covering the same duration -- exactly the
    /// cross-voice version of this bug documented on <see cref="JianpuRenderer.MeasureLayout.
    /// ApplyMelodyScale"/>, just across different measures instead of different voices in one
    /// measure). Also covers <see cref="JianpuRenderer.MeasureLayout.BeatToX"/>/<c>XToBeat</c>, the
    /// real glyph-accurate beat-position mapping the playback marker/seek now use instead of
    /// assuming beats are spaced evenly across a measure's pixel width.</summary>
    public sealed class BeatWidthConsistencyTests
    {
        private static JianpuNote Note(int pitch, int dashes = 0, int underlines = 0) =>
            new JianpuNote { Type = NoteType.Note, Pitch = pitch, Dashes = dashes, Underlines = underlines };

        [Fact]
        public void GetMeasureLayouts_EqualDurationMeasuresWithDifferentGranularity_RenderAtTheSameWidth()
        {
            // All three total 4 beats: one whole note, four quarter notes, sixteen sixteenth notes.
            var wholeNote = new JianpuMeasure { MelodyNotes = new List<JianpuNote> { Note(1, dashes: 3) } };
            var quarterNotes = ScoreTestHelper.Measure(Note(1), Note(2), Note(3), Note(4));
            var sixteenths = new List<JianpuNote>();
            for (var i = 0; i < 16; i++)
            {
                sixteenths.Add(Note((i % 7) + 1, underlines: 2));
            }

            var score = new JianpuScore { Title = "Beat width" };
            score.Measures.Add(wholeNote);
            score.Measures.Add(quarterNotes);
            score.Measures.Add(new JianpuMeasure { MelodyNotes = sixteenths });

            var renderer = new JianpuRenderer();
            var layouts = renderer.GetMeasureLayouts(score, 2000, ScoreLayoutOptions.Editor);

            Assert.Equal(layouts[0].Width, layouts[1].Width);
            Assert.Equal(layouts[1].Width, layouts[2].Width);
        }

        [Fact]
        public void GetMeasureLayouts_DifferentDurationMeasures_AreNotForcedToTheSameWidth()
        {
            var fourBeats = new JianpuMeasure { MelodyNotes = new List<JianpuNote> { Note(1, dashes: 3) } };
            var twoBeats = ScoreTestHelper.Measure(Note(1), Note(2));

            var score = new JianpuScore { Title = "Beat width mismatch" };
            score.Measures.Add(fourBeats);
            score.Measures.Add(twoBeats);

            var renderer = new JianpuRenderer();
            var layouts = renderer.GetMeasureLayouts(score, 2000, ScoreLayoutOptions.Editor);

            Assert.True(layouts[1].Width < layouts[0].Width);
        }

        [Fact]
        public void GetMeasureLayouts_MeasureAloneInItsBeatGroup_KeepsItsOwnNaturalWidth()
        {
            // A 2-beat measure with nothing else in the score sharing that beat length must render
            // exactly as it always did -- this consistency pass is a no-op when there's no other
            // measure to be consistent with.
            var soloMeasure = ScoreTestHelper.Measure(Note(1), Note(2));
            var soloScore = new JianpuScore { Title = "Solo" };
            soloScore.Measures.Add(soloMeasure);

            var groupedMeasure = ScoreTestHelper.Measure(Note(1), Note(2));
            var fourBeats = new JianpuMeasure { MelodyNotes = new List<JianpuNote> { Note(1, dashes: 3) } };
            var groupedScore = new JianpuScore { Title = "Grouped" };
            groupedScore.Measures.Add(fourBeats);
            groupedScore.Measures.Add(groupedMeasure);

            var renderer = new JianpuRenderer();
            var soloWidth = renderer.GetMeasureLayouts(soloScore, 2000, ScoreLayoutOptions.Editor)[0].Width;
            var groupedWidth = renderer.GetMeasureLayouts(groupedScore, 2000, ScoreLayoutOptions.Editor)[1].Width;

            Assert.Equal(soloWidth, groupedWidth);
        }

        [Fact]
        public void BeatToX_AtMeasureBoundaries_MatchesTheMeasuresOwnXAndRightEdge()
        {
            var measure = new JianpuMeasure { MelodyNotes = new List<JianpuNote> { Note(1, dashes: 3) } };
            var score = new JianpuScore { Title = "BeatToX" };
            score.Measures.Add(measure);

            var renderer = new JianpuRenderer();
            var layout = renderer.GetMeasureLayouts(score, 2000, ScoreLayoutOptions.Editor)[0];

            Assert.Equal(layout.X, layout.BeatToX(0));
            Assert.Equal(layout.X + layout.Width, layout.BeatToX(layout.TotalBeats));
        }

        [Fact]
        public void BeatToX_AtANoteBoundary_MatchesTheRealRenderedNoteX()
        {
            // A whole note followed by two floor-affected sixteenth notes -- the exact case where a
            // naive "beats spaced evenly across the measure" approximation would disagree with where
            // the second note is actually drawn.
            var notes = new List<JianpuNote> { Note(1, dashes: 3), Note(2, underlines: 2), Note(3, underlines: 2) };
            var score = new JianpuScore { Title = "Mixed" };
            score.Measures.Add(new JianpuMeasure { MelodyNotes = notes });

            var renderer = new JianpuRenderer();
            var layout = renderer.GetMeasureLayouts(score, 2000, ScoreLayoutOptions.Editor)[0];

            layout.GetNoteDrawBounds(1, out var note1X, out _);
            var xAtNote1Start = layout.BeatToX(4.0);

            Assert.True(System.Math.Abs(note1X - xAtNote1Start) <= 1);
        }

        [Fact]
        public void XToBeat_IsTheInverseOfBeatToX()
        {
            var measure = new JianpuMeasure { MelodyNotes = new List<JianpuNote> { Note(1, dashes: 3) } };
            var score = new JianpuScore { Title = "RoundTrip" };
            score.Measures.Add(measure);

            var renderer = new JianpuRenderer();
            var layout = renderer.GetMeasureLayouts(score, 2000, ScoreLayoutOptions.Editor)[0];

            for (var beat = 0.0; beat <= 4.0; beat += 0.5)
            {
                var x = layout.BeatToX(beat);
                var roundTrip = layout.XToBeat(x);
                Assert.True(System.Math.Abs(roundTrip - beat) < 0.05, $"beat={beat} -> x={x} -> beat={roundTrip}");
            }
        }
    }
}
