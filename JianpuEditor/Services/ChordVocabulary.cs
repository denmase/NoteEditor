using System.Collections.Generic;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    /// <summary>
    /// Melody degree -> viable chords, plus colour chords (borrowed / secondary dominants), for
    /// the enhanced (Markov) harmony engine. INVARIANT: every candidate listed under degree D
    /// contains degree D (verified by ChordVocabularyTests).
    /// </summary>
    internal static class ChordVocabulary
    {
        public static readonly IReadOnlyDictionary<int, IReadOnlyList<RomanNumeral>> DegreeToCandidates =
            new Dictionary<int, IReadOnlyList<RomanNumeral>>
            {
                [1] = new List<RomanNumeral>
                {
                    new RomanNumeral { Degree = 1, Quality = ChordQuality.Major },
                    new RomanNumeral { Degree = 6, Quality = ChordQuality.Minor },
                    new RomanNumeral { Degree = 4, Quality = ChordQuality.Major },
                    new RomanNumeral { Degree = 1, Quality = ChordQuality.Major7 },
                    new RomanNumeral { Degree = 2, Quality = ChordQuality.Minor7 }
                }.AsReadOnly(),

                [2] = new List<RomanNumeral>
                {
                    new RomanNumeral { Degree = 5, Quality = ChordQuality.Major },
                    new RomanNumeral { Degree = 2, Quality = ChordQuality.Minor },
                    new RomanNumeral { Degree = 7, Quality = ChordQuality.Diminished },
                    new RomanNumeral { Degree = 2, Quality = ChordQuality.Minor7 },
                    new RomanNumeral { Degree = 5, Quality = ChordQuality.Dominant7 }
                }.AsReadOnly(),

                [3] = new List<RomanNumeral>
                {
                    new RomanNumeral { Degree = 1, Quality = ChordQuality.Major },
                    new RomanNumeral { Degree = 3, Quality = ChordQuality.Minor },
                    new RomanNumeral { Degree = 6, Quality = ChordQuality.Minor },
                    new RomanNumeral { Degree = 1, Quality = ChordQuality.Major7 }
                }.AsReadOnly(),

                [4] = new List<RomanNumeral>
                {
                    new RomanNumeral { Degree = 4, Quality = ChordQuality.Major },
                    new RomanNumeral { Degree = 2, Quality = ChordQuality.Minor },
                    new RomanNumeral { Degree = 2, Quality = ChordQuality.Minor7 },
                    new RomanNumeral { Degree = 4, Quality = ChordQuality.Major7 },
                    new RomanNumeral { Degree = 5, Quality = ChordQuality.Dominant7 },
                    new RomanNumeral { Degree = 7, Quality = ChordQuality.Diminished }
                }.AsReadOnly(),

                [5] = new List<RomanNumeral>
                {
                    new RomanNumeral { Degree = 5, Quality = ChordQuality.Major },
                    new RomanNumeral { Degree = 1, Quality = ChordQuality.Major },
                    new RomanNumeral { Degree = 3, Quality = ChordQuality.Minor },
                    new RomanNumeral { Degree = 5, Quality = ChordQuality.Dominant7 },
                    new RomanNumeral { Degree = 1, Quality = ChordQuality.Major7 }
                }.AsReadOnly(),

                [6] = new List<RomanNumeral>
                {
                    new RomanNumeral { Degree = 6, Quality = ChordQuality.Minor },
                    new RomanNumeral { Degree = 4, Quality = ChordQuality.Major },
                    new RomanNumeral { Degree = 2, Quality = ChordQuality.Minor },
                    new RomanNumeral { Degree = 6, Quality = ChordQuality.Minor7 },
                    new RomanNumeral { Degree = 2, Quality = ChordQuality.Minor7 }
                }.AsReadOnly(),

                [7] = new List<RomanNumeral>
                {
                    new RomanNumeral { Degree = 5, Quality = ChordQuality.Major },
                    new RomanNumeral { Degree = 7, Quality = ChordQuality.Diminished },
                    new RomanNumeral { Degree = 3, Quality = ChordQuality.Minor },
                    new RomanNumeral { Degree = 5, Quality = ChordQuality.Dominant7 }
                }.AsReadOnly()
            };

        public static readonly IReadOnlyList<RomanNumeral> BorrowedChords = new List<RomanNumeral>
        {
            new RomanNumeral { Degree = 6, Accidental = -1, Quality = ChordQuality.Major }, // bVI
            new RomanNumeral { Degree = 7, Accidental = -1, Quality = ChordQuality.Major }, // bVII
            new RomanNumeral { Degree = 4, Quality = ChordQuality.Minor },                  // iv
            new RomanNumeral { Degree = 3, Accidental = -1, Quality = ChordQuality.Major }  // bIII
        }.AsReadOnly();

        public static readonly IReadOnlyList<RomanNumeral> SecondaryDominants = new List<RomanNumeral>
        {
            new RomanNumeral
            {
                Degree = 5, Quality = ChordQuality.Dominant7,
                SecondaryOf = new RomanNumeral { Degree = 5, Quality = ChordQuality.Major }
            },
            new RomanNumeral
            {
                Degree = 5, Quality = ChordQuality.Dominant7,
                SecondaryOf = new RomanNumeral { Degree = 6, Quality = ChordQuality.Minor }
            },
            new RomanNumeral
            {
                Degree = 5, Quality = ChordQuality.Dominant7,
                SecondaryOf = new RomanNumeral { Degree = 2, Quality = ChordQuality.Minor }
            },
            new RomanNumeral
            {
                Degree = 5, Quality = ChordQuality.Dominant7,
                SecondaryOf = new RomanNumeral { Degree = 4, Quality = ChordQuality.Major }
            }
        }.AsReadOnly();

        public static readonly int[] MajorScaleSemitones = { 0, 2, 4, 5, 7, 9, 11 };

        public static readonly string[] NoteNames =
        {
            "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
        };
    }
}
