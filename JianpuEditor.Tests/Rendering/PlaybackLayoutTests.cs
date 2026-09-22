using JianpuEditor.Models;
using JianpuEditor.Rendering;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Rendering
{
    /// <summary>Covers the playback marker/drag-hit-testing fix for a line with extra voice rows
    /// (SATB etc.): both used to assume every line was the fixed <see cref="JianpuRenderer.
    /// StaffBlockHeight"/> tall, so the marker didn't extend through the extra rows and a
    /// drag-to-seek into that space snapped to the nearest available (wrong) row instead of the
    /// one actually under the pointer.</summary>
    public sealed class PlaybackLayoutTests
    {
        private static JianpuScore BuildSatbScore()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4));
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(5), ScoreTestHelper.Note(5), ScoreTestHelper.Note(5) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Tenor", Notes = { ScoreTestHelper.Note(3), ScoreTestHelper.Note(3), ScoreTestHelper.Note(3), ScoreTestHelper.Note(3) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Bass", Notes = { ScoreTestHelper.Note(1), ScoreTestHelper.Note(1), ScoreTestHelper.Note(1), ScoreTestHelper.Note(1) } });

            var score = new JianpuScore { Title = "SATB playback" };
            score.Measures.Add(measure);
            return score;
        }

        [Fact]
        public void BuildSegments_PlainScore_HeightMatchesStaffBlockHeight()
        {
            var score = new JianpuScore { Title = "Plain" };
            score.Measures.Add(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));

            var segments = PlaybackLayout.BuildSegments(score, 900);

            Assert.Equal(JianpuRenderer.StaffBlockHeight, segments[0].Height);
        }

        [Fact]
        public void BuildSegments_SatbScore_HeightIncludesExtraVoiceRows()
        {
            var segments = PlaybackLayout.BuildSegments(BuildSatbScore(), 900);

            var expected = JianpuRenderer.StaffBlockHeight + 3 * (JianpuRenderer.MelodyRowHeight + JianpuRenderer.RowGap);
            Assert.Equal(expected, segments[0].Height);
        }

        [Fact]
        public void GetMarkerPosition_SatbScore_MarkerExtendsThroughEveryVoiceRow()
        {
            var segments = PlaybackLayout.BuildSegments(BuildSatbScore(), 900);

            var marker = PlaybackLayout.GetMarkerPosition(segments, 2.0);

            Assert.Equal(segments[0].BlockTop + segments[0].Height, marker.Bottom);
        }

        [Fact]
        public void MapXToBeat_YInsideExtraVoiceRow_StillResolvesAgainstThisLine()
        {
            var segments = PlaybackLayout.BuildSegments(BuildSatbScore(), 900);

            // Past where a plain single-voice line's StaffBlockHeight would have ended, but still
            // within this SATB line's real (taller) footprint.
            var yInsideBassRow = segments[0].BlockTop + JianpuRenderer.StaffBlockHeight - 10;
            var beat = PlaybackLayout.MapXToBeat(segments, segments[0].X + segments[0].Width / 2, yInsideBassRow);

            Assert.InRange(beat, 0, 4.5);
        }

        private static JianpuScore BuildDescantScore()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4));
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Descant", IsAbove = true, Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(5), ScoreTestHelper.Note(5), ScoreTestHelper.Note(5) } });

            var score = new JianpuScore { Title = "Descant playback" };
            score.Measures.Add(measure);
            return score;
        }

        [Fact]
        public void BuildSegments_DescantScore_BlockTopStartsAboveTheMelodyRow()
        {
            var renderer = new JianpuRenderer();
            var score = BuildDescantScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];

            var segments = PlaybackLayout.BuildSegments(score, 900);

            // BuildSegments used to report the melody row's own top (layout.BlockTop), which sits
            // BELOW the descant row -- the marker/seek never reached it. It must report the whole
            // block's real top instead.
            Assert.Equal(layout.GetDrawTop(), segments[0].BlockTop);
            Assert.True(segments[0].BlockTop < layout.BlockTop);
        }

        [Fact]
        public void GetMarkerPosition_DescantScore_MarkerTopCoversTheDescantRow()
        {
            var renderer = new JianpuRenderer();
            var score = BuildDescantScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];

            var segments = PlaybackLayout.BuildSegments(score, 900);
            var marker = PlaybackLayout.GetMarkerPosition(segments, 2.0);

            Assert.Equal(layout.GetDrawTop(), marker.Top);
            Assert.True(marker.Top <= layout.BlockTop - JianpuRenderer.MelodyRowHeight);
        }
    }
}
