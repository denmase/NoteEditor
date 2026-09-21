using System;
using System.Drawing;
using JianpuEditor.Models;
using JianpuEditor.Rendering;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Rendering
{
    public sealed class NoteTopAnnotationPlannerTests
    {
        /// <summary>Regression guard for a real bug: AnnotationLayerClearance is a gap between two
        /// layers' anchor Y values, not their rendered glyph heights, so it must be at least as
        /// tall as the ornament font actually drawn there or the glyphs visually collide (confirmed
        /// by rendering real output and inspecting it -- the original 6f let an octave dot overlap
        /// the ornament text above it). Checks both font families/styles the stacked ornament band
        /// actually uses (JianpuRenderer's _ornamentStackedFont/_ornamentLatinStackedFont).</summary>
        [Fact]
        public void AnnotationLayerClearance_CoversTheTallestStackedOrnamentFont()
        {
            using (var yaHei = new Font("Microsoft YaHei", 11f, FontStyle.Regular))
            using (var arialItalic = new Font("Arial", 11f, FontStyle.Italic))
            {
                var tallestFontHeight = Math.Max(yaHei.Height, arialItalic.Height);

                Assert.True(
                    NoteTopAnnotationLayout.AnnotationLayerClearance >= tallestFontHeight,
                    "AnnotationLayerClearance (" + NoteTopAnnotationLayout.AnnotationLayerClearance
                        + ") must be at least the stacked ornament font's line height ("
                        + tallestFontHeight + ") or stacked glyphs visually overlap.");
            }
        }

        [Fact]
        public void Plan_GraceAndSharp_PlacesAccidentalClosestToNoteAndGraceAbove()
        {
            var note = new JianpuNote
            {
                Type = NoteType.Note,
                Pitch = 1.5,
                Accidental = AccidentalKind.Sharp,
                Octave = 1
            };
            var ornaments = new List<JianpuOrnament>
            {
                new JianpuOrnament { Type = OrnamentType.GraceNote, NoteIndex = 0 }
            };

            var layout = NoteTopAnnotationPlanner.Plan(note, 100, 28, ornaments, compactAccidentals: true);

            Assert.True(layout.HasAccidental);
            Assert.Equal(NoteTopAnnotationLayout.AccidentalBandY, layout.AccidentalY);
            Assert.True(layout.AccidentalX < layout.HeadCenterX);
            Assert.True(layout.HasGraceOrnament);
            Assert.Equal(layout.HeadCenterX, layout.GetOrnamentAnchorX(OrnamentType.GraceNote, 100, 28), 1);
            Assert.True(layout.OctaveDotBaseY < layout.AccidentalY);
            Assert.True(layout.GetOrnamentY(OrnamentType.GraceNote) < layout.OctaveDotBaseY);
        }

        [Fact]
        public void Plan_TrillHighOctaveAndFlat_StacksMarkersFromNoteUpward()
        {
            var note = new JianpuNote
            {
                Type = NoteType.Note,
                Pitch = 2.5,
                Accidental = AccidentalKind.Flat,
                Octave = 1
            };
            var ornaments = new List<JianpuOrnament>
            {
                new JianpuOrnament { Type = OrnamentType.Trill, NoteIndex = 0 }
            };

            var layout = NoteTopAnnotationPlanner.Plan(note, 100, 28, ornaments, compactAccidentals: true);

            Assert.True(layout.HasCenterOrnament);
            Assert.True(layout.HasAccidental);
            Assert.Equal(NoteTopAnnotationLayout.AccidentalBandY, layout.AccidentalY);
            Assert.True(layout.AccidentalX < layout.HeadCenterX);
            Assert.True(layout.OctaveDotCenterX > layout.AccidentalX);
            Assert.Equal(NoteTopAnnotationLayout.OctaveDotBandY, layout.OctaveDotBaseY);
            Assert.Equal(
                layout.OctaveDotBaseY - NoteTopAnnotationLayout.AnnotationLayerClearance,
                layout.GetOrnamentY(OrnamentType.Trill));
            Assert.True(layout.AccidentalY > layout.OctaveDotBaseY);
            Assert.True(layout.OctaveDotBaseY > layout.GetOrnamentY(OrnamentType.Trill));
        }

        [Fact]
        public void Plan_FermataAndSharp_PlacesFermataTopmostAndAccidentalClosestToNote()
        {
            var note = new JianpuNote
            {
                Type = NoteType.Note,
                Pitch = 1.5,
                Accidental = AccidentalKind.Sharp,
                Octave = 1
            };
            var ornaments = new List<JianpuOrnament>
            {
                new JianpuOrnament { Type = OrnamentType.Fermata, NoteIndex = 0 }
            };

            var layout = NoteTopAnnotationPlanner.Plan(note, 100, 28, ornaments, compactAccidentals: true);

            Assert.Equal(layout.HeadCenterX, layout.GetOrnamentAnchorX(OrnamentType.Fermata, 100, 28), 1);
            Assert.Equal(
                layout.OctaveDotBaseY - NoteTopAnnotationLayout.AnnotationLayerClearance,
                layout.GetOrnamentY(OrnamentType.Fermata));
            Assert.Equal(NoteTopAnnotationLayout.AccidentalBandY, layout.AccidentalY);
            Assert.True(layout.AccidentalX < layout.OctaveDotCenterX);
            Assert.True(layout.GetOrnamentY(OrnamentType.Fermata) < layout.OctaveDotBaseY);
        }

        [Fact]
        public void Plan_FermataAndTrillTogether_StackWithRealClearanceInsteadOfNearlyOverlapping()
        {
            // Regression test for the bug the old fixed-band table couldn't represent: a fermata
            // and a center ornament (trill/turn/mordent) can both be attached to the same note, but
            // the old table always placed fermata at a fixed Y=0 and any center ornament sharing a
            // note with one at a fixed Y=2 -- only 2px apart regardless of glyph size. The new
            // stacking model gives each layer its own real clearance instead.
            var note = new JianpuNote { Type = NoteType.Note, Pitch = 1 };
            var ornaments = new List<JianpuOrnament>
            {
                new JianpuOrnament { Type = OrnamentType.Trill, NoteIndex = 0 },
                new JianpuOrnament { Type = OrnamentType.Fermata, NoteIndex = 0 }
            };

            var layout = NoteTopAnnotationPlanner.Plan(note, 100, 28, ornaments, compactAccidentals: true);

            Assert.True(layout.HasCenterOrnament);
            Assert.True(layout.HasFermata);
            var gap = layout.GetOrnamentY(OrnamentType.Trill) - layout.GetOrnamentY(OrnamentType.Fermata);
            Assert.True(layout.GetOrnamentY(OrnamentType.Fermata) < layout.GetOrnamentY(OrnamentType.Trill));
            Assert.Equal(NoteTopAnnotationLayout.AnnotationLayerClearance, gap);
        }

        [Fact]
        public void Plan_OctaveOnly_PlacesDotsClosestToNote()
        {
            var note = new JianpuNote
            {
                Type = NoteType.Note,
                Pitch = 1,
                Octave = 1
            };

            var layout = NoteTopAnnotationPlanner.Plan(note, 100, 28, new List<JianpuOrnament>(), compactAccidentals: true);

            Assert.Equal(NoteTopAnnotationLayout.OctaveDotBandYWithoutAccidental, layout.OctaveDotBaseY);
            Assert.Equal(layout.HeadCenterX - 3f, layout.OctaveDotCenterX, 1);
        }

        [Fact]
        public void GetOrnamentAnchorX_BreathMark_AnchorsAfterNoteInsteadOfAboveIt()
        {
            var layout = NoteTopAnnotationPlanner.Plan(
                new JianpuNote { Type = NoteType.Note, Pitch = 1 },
                100,
                28,
                new List<JianpuOrnament>(),
                compactAccidentals: true);

            var breathAnchor = layout.GetOrnamentAnchorX(OrnamentType.BreathMark, 100, 28);
            var trillAnchor = layout.GetOrnamentAnchorX(OrnamentType.Trill, 100, 28);

            Assert.True(breathAnchor > 100 + 28);
            Assert.Equal(layout.HeadCenterX, trillAnchor, 1);
            Assert.NotEqual(trillAnchor, breathAnchor);
        }

        [Fact]
        public void Plan_IndonesianSharp_AccidentalIsSuffixAndOctaveDotDoesNotDodge()
        {
            using (new NotationStyleScope(NotationStyle.Indonesian))
            {
                var note = new JianpuNote
                {
                    Type = NoteType.Note,
                    Pitch = 1.5,
                    Accidental = AccidentalKind.Sharp,
                    Octave = 1
                };

                var layout = NoteTopAnnotationPlanner.Plan(note, 100, 28, new List<JianpuOrnament>(), compactAccidentals: true);

                Assert.True(layout.HasAccidental);
                Assert.True(layout.AccidentalIsSuffix);
                // Same as the no-accidental case (Plan_OctaveOnly_PlacesDotsClosestToNote above) --
                // a suffix accidental sits to the right of the digit, so the octave dot doesn't need
                // to dodge it the way a prefix accidental (Chinese style) requires.
                Assert.Equal(NoteTopAnnotationLayout.OctaveDotBandYWithoutAccidental, layout.OctaveDotBaseY);
                Assert.Equal(layout.HeadCenterX - 3f, layout.OctaveDotCenterX, 1);
            }
        }

        [Fact]
        public void Plan_IndonesianNatural_AccidentalStaysPrefixedNotSuffix()
        {
            using (new NotationStyleScope(NotationStyle.Indonesian))
            {
                var note = new JianpuNote
                {
                    Type = NoteType.Note,
                    Pitch = 4,
                    Accidental = AccidentalKind.Natural,
                    Octave = 1
                };

                var layout = NoteTopAnnotationPlanner.Plan(note, 100, 28, new List<JianpuOrnament>(), compactAccidentals: true);

                Assert.True(layout.HasAccidental);
                Assert.False(layout.AccidentalIsSuffix);
                Assert.Equal(NoteTopAnnotationLayout.OctaveDotBandY, layout.OctaveDotBaseY);
            }
        }

        [Fact]
        public void GetOrnamentsForNote_ReturnsOnlyMatchingOrnaments()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2));
            OrnamentService.TryAddOrnament(measure, 0, OrnamentType.GraceNote);
            OrnamentService.TryAddOrnament(measure, 1, OrnamentType.Trill);

            var ornaments = NoteTopAnnotationPlanner.GetOrnamentsForNote(measure, 0);

            Assert.Single(ornaments);
            Assert.Equal(OrnamentType.GraceNote, ornaments[0].Type);
        }
    }
}
