using System;
using JianpuEditor.Models;
using JianpuEditor.Rendering;

namespace JianpuEditor.Services
{
    public static class JianpuPitchCodec
    {
        public const double AccidentalFraction = 0.5;
        private const double PitchEpsilon = 0.001;

        private static readonly int[] MajorScaleOffsets = { 0, 2, 4, 5, 7, 9, 11 };

        public static bool IsNatural(double pitch)
        {
            return Math.Abs(pitch - Math.Round(pitch)) < PitchEpsilon;
        }

        public static bool HasAccidental(JianpuNote note)
        {
            return note != null
                && note.Type == NoteType.Note
                && note.Accidental != AccidentalKind.None;
        }

        public static int GetDisplayDegree(JianpuNote note)
        {
            if (note == null || note.Type == NoteType.Rest)
            {
                return 0;
            }

            if (note.Accidental == AccidentalKind.Sharp)
            {
                return (int)Math.Floor(note.Pitch + PitchEpsilon);
            }

            if (note.Accidental == AccidentalKind.Flat)
            {
                return (int)Math.Ceiling(note.Pitch - PitchEpsilon);
            }

            return (int)Math.Round(note.Pitch);
        }

        public static string GetPitchDisplayText(JianpuNote note)
        {
            if (note == null || note.Type == NoteType.Rest)
            {
                return "0";
            }

            var degree = GetDisplayDegree(note);
            var mark = GetAccidentalMark(note);
            if (mark == null)
            {
                return degree.ToString();
            }

            return IsSuffixAccidental(note) ? degree + mark : mark + degree;
        }

        /// <summary>True for a Sharp/Flat accidental when the active <see cref="AppTheme.NotationStyle"/>
        /// is <see cref="NotationStyle.Indonesian"/> -- notasi angka suffixes kres (`/`, sharp) and
        /// mol (`\`, flat) after the digit instead of the Chinese/Western `#`/`b` prefix. A Natural
        /// sign stays a prefix under both styles: this renderer doesn't carry accidentals through a
        /// measure the way real key-signature-aware notation does, so a natural sign here is always
        /// a standalone cancel-mark on one note rather than something that needs to visually match
        /// a preceding suffixed accidental within a phrase, and Indonesian sources don't establish a
        /// suffix convention for it the way they do for kres/mol.</summary>
        public static bool IsSuffixAccidental(JianpuNote note)
        {
            return note != null
                && AppTheme.NotationStyle == NotationStyle.Indonesian
                && (note.Accidental == AccidentalKind.Sharp || note.Accidental == AccidentalKind.Flat);
        }

        public static string GetAccidentalMark(JianpuNote note)
        {
            if (note == null || note.Type == NoteType.Rest)
            {
                return null;
            }

            if (note.Accidental == AccidentalKind.Sharp)
            {
                return AppTheme.NotationStyle == NotationStyle.Indonesian ? "/" : "#";
            }

            if (note.Accidental == AccidentalKind.Flat)
            {
                return AppTheme.NotationStyle == NotationStyle.Indonesian ? "\\" : "b";
            }

            if (note.Accidental == AccidentalKind.Natural)
            {
                return "♮";
            }

            return null;
        }

        public static bool IsValidMelodyPitch(JianpuNote note)
        {
            if (note == null || note.Type == NoteType.Rest)
            {
                return false;
            }

            if (note.Accidental == AccidentalKind.Sharp || note.Accidental == AccidentalKind.Flat)
            {
                return Math.Abs(note.Pitch - Math.Floor(note.Pitch) - AccidentalFraction) < PitchEpsilon
                    || Math.Abs(note.Pitch - Math.Ceiling(note.Pitch) - AccidentalFraction) < PitchEpsilon;
            }

            return note.Pitch >= 1 - PitchEpsilon && note.Pitch <= 7 + PitchEpsilon && IsNatural(note.Pitch);
        }

        public static int ToMelodyMidiNote(JianpuNote note, int tonicMidi)
        {
            if (note == null || note.Type == NoteType.Rest || !IsValidMelodyPitch(note))
            {
                return tonicMidi;
            }

            var degree = GetDiatonicDegree(note);
            if (degree < 1 || degree > 7)
            {
                return tonicMidi;
            }

            var midi = tonicMidi + MajorScaleOffsets[degree - 1] + note.Octave * 12;
            if (note.Accidental == AccidentalKind.Sharp)
            {
                midi += 1;
            }
            else if (note.Accidental == AccidentalKind.Flat)
            {
                midi -= 1;
            }

            return Math.Max(0, Math.Min(127, midi));
        }

        public static void SetNaturalPitch(JianpuNote note, int pitch)
        {
            if (note == null)
            {
                return;
            }

            note.Pitch = pitch;
            note.Accidental = AccidentalKind.None;
        }

        public static void SetAccidentalPitch(JianpuNote note, AccidentalKind accidental, int degree)
        {
            if (note == null)
            {
                return;
            }

            note.Accidental = accidental;
            if (accidental == AccidentalKind.Sharp)
            {
                note.Pitch = degree + AccidentalFraction;
                return;
            }

            if (accidental == AccidentalKind.Flat)
            {
                note.Pitch = (degree - 1) + AccidentalFraction;
                return;
            }

            // None or Natural: both display as a plain integer degree, the only difference being
            // whether GetAccidentalMark draws a natural sign (see the doc comment there).
            note.Pitch = degree;
        }

        private static int GetDiatonicDegree(JianpuNote note)
        {
            if (note.Accidental == AccidentalKind.Sharp)
            {
                return (int)Math.Floor(note.Pitch + PitchEpsilon);
            }

            if (note.Accidental == AccidentalKind.Flat)
            {
                return (int)Math.Ceiling(note.Pitch - PitchEpsilon);
            }

            return (int)Math.Round(note.Pitch);
        }
    }
}
