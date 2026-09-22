using System.Drawing;
using JianpuEditor.Models;
using JianpuEditor.Rendering;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Rendering
{
    /// <summary>Covers voice-aware hit-testing: a click on any voice's row (descant/solo above the
    /// melody, or Alto/Tenor/Bass below it) resolves to a real Note/Gap hit against that voice's
    /// own notes, carrying its <see cref="ScoreHitResult.VoiceIndex"/> -- not a generic Measure hit
    /// the way it did before this pass, and not misread against the primary voice's bounds.</summary>
    public sealed class VoiceHitTestingTests
    {
        private static JianpuScore BuildDescantAboveSatbScore()
        {
            var score = new JianpuScore { Title = "Voice hit test" };
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4));
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Descant", IsAbove = true, Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(6), ScoreTestHelper.Note(5), ScoreTestHelper.Note(6) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(5), ScoreTestHelper.Note(5), ScoreTestHelper.Note(5) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Tenor", Notes = { ScoreTestHelper.Note(3), ScoreTestHelper.Note(3), ScoreTestHelper.Note(3), ScoreTestHelper.Note(3) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Bass", Notes = { ScoreTestHelper.Note(1), ScoreTestHelper.Note(1), ScoreTestHelper.Note(1), ScoreTestHelper.Note(1) } });
            score.Measures.Add(measure);
            return score;
        }

        [Fact]
        public void HitTest_ClickOnDescantNote_ResolvesWithItsVoiceIndex()
        {
            var renderer = new JianpuRenderer();
            var score = BuildDescantAboveSatbScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];
            var descantRowTop = layout.GetVoiceRowTop(0);
            layout.FindExtraVoiceLayout(0).GetNoteDrawBounds(1, layout.X, layout.Width, out var noteX, out var noteWidth);

            var hit = renderer.HitTest(score, 900, new Point(noteX + noteWidth / 2, descantRowTop + 20));

            Assert.Equal(ScoreHitType.Note, hit.HitType);
            Assert.Equal(0, hit.VoiceIndex);
            Assert.Equal(1, hit.NoteIndex);
        }

        [Fact]
        public void HitTest_ClickOnAltoNote_ResolvesWithItsVoiceIndex()
        {
            var renderer = new JianpuRenderer();
            var score = BuildDescantAboveSatbScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];
            var altoRowTop = layout.GetVoiceRowTop(1);
            layout.FindExtraVoiceLayout(1).GetNoteDrawBounds(2, layout.X, layout.Width, out var noteX, out var noteWidth);

            var hit = renderer.HitTest(score, 900, new Point(noteX + noteWidth / 2, altoRowTop + 20));

            Assert.Equal(ScoreHitType.Note, hit.HitType);
            Assert.Equal(1, hit.VoiceIndex);
            Assert.Equal(2, hit.NoteIndex);
        }

        [Fact]
        public void HitTest_ClickOnBassNote_ResolvesWithItsVoiceIndex()
        {
            var renderer = new JianpuRenderer();
            var score = BuildDescantAboveSatbScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];
            var bassRowTop = layout.GetVoiceRowTop(3);
            layout.FindExtraVoiceLayout(3).GetNoteDrawBounds(0, layout.X, layout.Width, out var noteX, out var noteWidth);

            var hit = renderer.HitTest(score, 900, new Point(noteX + noteWidth / 2, bassRowTop + 20));

            Assert.Equal(ScoreHitType.Note, hit.HitType);
            Assert.Equal(3, hit.VoiceIndex);
        }

        [Fact]
        public void HitTest_ClickNearAltoNoteEdge_ResolvesAsGapWithItsVoiceIndex()
        {
            var renderer = new JianpuRenderer();
            var score = BuildDescantAboveSatbScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];
            var altoRowTop = layout.GetVoiceRowTop(1);
            layout.FindExtraVoiceLayout(1).GetNoteDrawBounds(0, layout.X, layout.Width, out var noteX, out _);

            var hit = renderer.HitTest(score, 900, new Point(noteX + 2, altoRowTop + 20));

            Assert.Equal(ScoreHitType.Gap, hit.HitType);
            Assert.Equal(1, hit.VoiceIndex);
            Assert.Equal(0, hit.InsertIndex);
        }

        [Fact]
        public void HitTest_ClickOnPrimaryMelodyNote_KeepsPrimaryVoiceIndex()
        {
            var renderer = new JianpuRenderer();
            var score = BuildDescantAboveSatbScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];
            layout.GetNoteDrawBounds(0, out var noteX, out var noteWidth);

            var hit = renderer.HitTest(score, 900, new Point(noteX + noteWidth / 2, layout.BlockTop + 20));

            Assert.Equal(ScoreHitType.Note, hit.HitType);
            Assert.Equal(ScoreNoteRef.PrimaryVoiceIndex, hit.VoiceIndex);
        }

        [Fact]
        public void HitTest_PlainSingleVoiceScore_EveryNoteStillResolvesCorrectly()
        {
            var renderer = new JianpuRenderer();
            var score = new JianpuScore { Title = "Plain" };
            score.Measures.Add(ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));
            var layout = renderer.GetMeasureLayouts(score, 900)[0];

            for (var i = 0; i < 4; i++)
            {
                layout.GetNoteDrawBounds(i, out var noteX, out var noteWidth);
                var hit = renderer.HitTest(score, 900, new Point(noteX + noteWidth / 2, layout.BlockTop + 20));

                Assert.Equal(ScoreHitType.Note, hit.HitType);
                Assert.Equal(i, hit.NoteIndex);
                Assert.Equal(ScoreNoteRef.PrimaryVoiceIndex, hit.VoiceIndex);
            }
        }
    }
}
