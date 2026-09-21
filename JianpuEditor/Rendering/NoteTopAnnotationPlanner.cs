using System;
using System.Collections.Generic;
using JianpuEditor.Models;
using JianpuEditor.Services;

namespace JianpuEditor.Rendering
{
    public static class NoteTopAnnotationPlanner
    {
        private const float AccidentalLeftPadding = 2f;

        private const float AccidentalToOctaveGap = 4f;

        public static NoteTopAnnotationLayout Plan(
            JianpuNote note,
            int noteX,
            int headWidth,
            IReadOnlyList<JianpuOrnament> ornaments,
            bool compactAccidentals)
        {
            var headCenterX = noteX + headWidth / 2f;
            var layout = new NoteTopAnnotationLayout
            {
                HeadCenterX = headCenterX,
                OctaveDotCenterX = headCenterX - 3f,
                OrnamentY = NoteTopAnnotationLayout.OrnamentBandYWithoutLowerLayers,
                FermataY = NoteTopAnnotationLayout.OrnamentBandYWithoutLowerLayers
            };

            if (note == null || note.Type == NoteType.Rest)
            {
                return layout;
            }

            AnalyzeOrnaments(ornaments, layout);
            PlaceAccidental(note, noteX, compactAccidentals, layout);
            PlaceOctaveDots(note, layout);
            PlaceOrnamentBands(layout);
            return layout;
        }

        public static IReadOnlyList<JianpuOrnament> GetOrnamentsForNote(JianpuMeasure measure, int noteIndex)
        {
            if (measure?.Ornaments == null || measure.Ornaments.Count == 0)
            {
                return new List<JianpuOrnament>();
            }

            OrnamentService.NormalizeMeasure(measure);
            var ornaments = new List<JianpuOrnament>();
            foreach (var ornament in measure.Ornaments)
            {
                if (ornament == null || ornament.Type == OrnamentType.Unknown)
                {
                    continue;
                }

                if (OrnamentService.ResolveNoteIndex(measure, ornament) == noteIndex)
                {
                    ornaments.Add(ornament);
                }
            }

            return ornaments;
        }

        private static void AnalyzeOrnaments(IReadOnlyList<JianpuOrnament> ornaments, NoteTopAnnotationLayout layout)
        {
            if (ornaments == null)
            {
                return;
            }

            foreach (var ornament in ornaments)
            {
                if (ornament == null)
                {
                    continue;
                }

                switch (ornament.Type)
                {
                    case OrnamentType.GraceNote:
                        layout.HasGraceOrnament = true;
                        break;
                    case OrnamentType.Fermata:
                        layout.HasFermata = true;
                        break;
                    case OrnamentType.Trill:
                    case OrnamentType.Turn:
                    case OrnamentType.Mordent:
                    case OrnamentType.Staccato:
                    case OrnamentType.Accent:
                    case OrnamentType.Tenuto:
                    case OrnamentType.Segno:
                    case OrnamentType.Coda:
                        layout.HasCenterOrnament = true;
                        break;
                }
            }
        }

        private static void PlaceAccidental(
            JianpuNote note,
            int noteX,
            bool compactAccidentals,
            NoteTopAnnotationLayout layout)
        {
            if (!compactAccidentals || note.Accidental == AccidentalKind.None)
            {
                return;
            }

            layout.HasAccidental = true;
            layout.AccidentalKind = note.Accidental;
            layout.AccidentalIsSuffix = JianpuPitchCodec.IsSuffixAccidental(note);
            layout.AccidentalX = noteX + AccidentalLeftPadding;
            layout.AccidentalY = NoteTopAnnotationLayout.AccidentalBandY;
        }

        private static void PlaceOctaveDots(JianpuNote note, NoteTopAnnotationLayout layout)
        {
            if (note.Octave <= 0)
            {
                return;
            }

            layout.HasHighOctaveDots = true;
            var accidentalOccupiesUpperLeft = layout.HasAccidental && !layout.AccidentalIsSuffix;
            layout.OctaveDotBaseY = accidentalOccupiesUpperLeft
                ? NoteTopAnnotationLayout.OctaveDotBandY
                : NoteTopAnnotationLayout.OctaveDotBandYWithoutAccidental;

            if (accidentalOccupiesUpperLeft)
            {
                var minCenterX = layout.AccidentalX
                    + NoteTopAnnotationLayout.AccidentalMarkWidth
                    + AccidentalToOctaveGap
                    + NoteTopAnnotationLayout.OctaveDotDiameter / 2f;
                layout.OctaveDotCenterX = Math.Max(layout.HeadCenterX - 3f, minCenterX);
            }
            else
            {
                layout.OctaveDotCenterX = layout.HeadCenterX - 3f;
            }
        }

        /// <summary>
        /// Stacks the outer annotation layers (center/grace ornament, then fermata) outward from
        /// whatever occupies the space closer to the note, instead of picking a Y from a fixed
        /// table of hand-covered combinations. Each present layer claims
        /// <see cref="NoteTopAnnotationLayout.AnnotationLayerClearance"/> above the boundary set by
        /// the layer(s) below it, so a combination the old table never accounted for -- e.g. a
        /// fermata over a trill -- gets real clearance instead of landing almost on top of it.
        /// </summary>
        private static void PlaceOrnamentBands(NoteTopAnnotationLayout layout)
        {
            var boundaryY = NoteTopAnnotationLayout.DigitTextY;
            if (layout.HasAccidental && !layout.AccidentalIsSuffix)
            {
                boundaryY = Math.Min(boundaryY, layout.AccidentalY);
            }

            if (layout.HasHighOctaveDots)
            {
                boundaryY = Math.Min(boundaryY, layout.OctaveDotBaseY);
            }

            var hasUpperOrnament = layout.HasGraceOrnament || layout.HasCenterOrnament;
            if (hasUpperOrnament)
            {
                layout.OrnamentY = boundaryY - NoteTopAnnotationLayout.AnnotationLayerClearance;
                boundaryY = layout.OrnamentY;
            }

            if (layout.HasFermata)
            {
                layout.FermataY = boundaryY - NoteTopAnnotationLayout.AnnotationLayerClearance;
            }
        }
    }
}
