using System.Collections.Generic;
using JianpuEditor.Models;
using JianpuEditor.Services;

namespace JianpuEditor.ViewModels
{
    internal static class DemoScoreFactory
    {
        public static JianpuScore CreateOdeToJoy()
        {
            var score = new JianpuScore
            {
                Title = "Ode to Joy",
                KeySignature = "1=C",
                Tempo = "Moderato",
                Bpm = 120,
                Composer = "Beethoven",
                Measures = new List<JianpuMeasure>
                {
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 3 },
                            new JianpuNote { Pitch = 4 }, new JianpuNote { Pitch = 5 }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "C", BeatPosition = 0 },
                            new ChordMarker { Text = "G", BeatPosition = 2 }
                        },
                        LyricText = "Goddess of joy"
                    },
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 4 },
                            new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 2 }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "G", BeatPosition = 0 },
                            new ChordMarker { Text = "C", BeatPosition = 2 }
                        },
                        LyricText = "pure and beautiful"
                    },
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 1, Dashes = 1 }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "F", BeatPosition = 0 }
                        },
                        LyricText = "Radiant light"
                    },
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 1 }, new JianpuNote { Pitch = 2 },
                            new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 2, Dashes = 1 }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "C", BeatPosition = 0 },
                            new ChordMarker { Text = "G", BeatPosition = 2 }
                        },
                        LyricText = "shines on the earth"
                    },
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 3, Dotted = true, Underlines = 1 },
                            new JianpuNote { Pitch = 3, Underlines = 1 }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "C", BeatPosition = 0 }
                        },
                        LyricText = "we gather joyfully"
                    }
                }
            };

            ChordMarkerService.NormalizeScore(score);
            return score;
        }
    }
}
