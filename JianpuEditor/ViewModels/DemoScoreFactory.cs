using System.Collections.Generic;
using JianpuEditor.Models;
using JianpuEditor.Services;

namespace JianpuEditor.ViewModels
{
    internal static class DemoScoreFactory
    {
        /// <summary>Beethoven's "Ode to Joy" (Hymn to Joy), the real melody and Henry van Dyke's
        /// public-domain 1907 English text, in a standard four-part (SATB) hymn harmonization.
        /// Mirrors <c>sample/Ode to Joy.jianpu</c> exactly, so "Load Demo Score" and opening that
        /// sample show the same piece.</summary>
        public static JianpuScore CreateOdeToJoy()
        {
            var score = new JianpuScore
            {
                Title = "Ode to Joy",
                KeySignature = "1=C",
                Tempo = "Moderato",
                Bpm = 120,
                Composer = "Beethoven",
                PrimaryVoiceLabel = "Soprano",
                Measures = new List<JianpuMeasure>
                {
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 3 },
                            new JianpuNote { Pitch = 4 }, new JianpuNote { Pitch = 5 }
                        },
                        ExtraVoices = new List<JianpuVoice>
                        {
                            new JianpuVoice
                            {
                                Role = "Alto",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 5 },
                                    new JianpuNote { Pitch = 7 }, new JianpuNote { Pitch = 7 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Tenor",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 3 },
                                    new JianpuNote { Pitch = 2 }, new JianpuNote { Pitch = 2 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Bass",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 1 }, new JianpuNote { Pitch = 1 },
                                    new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 5 }
                                }
                            }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "C", BeatPosition = 0 },
                            new ChordMarker { Text = "G", BeatPosition = 2 }
                        },
                        LyricText = "Joy ful, joy ful,"
                    },
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 4 },
                            new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 2 }
                        },
                        ExtraVoices = new List<JianpuVoice>
                        {
                            new JianpuVoice
                            {
                                Role = "Alto",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 7 }, new JianpuNote { Pitch = 7 },
                                    new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 5 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Tenor",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 2 }, new JianpuNote { Pitch = 2 },
                                    new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 3 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Bass",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 5 },
                                    new JianpuNote { Pitch = 1 }, new JianpuNote { Pitch = 1 }
                                }
                            }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "G", BeatPosition = 0 },
                            new ChordMarker { Text = "C", BeatPosition = 2 }
                        },
                        LyricText = "we a dore Thee,"
                    },
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 1 }, new JianpuNote { Pitch = 1 },
                            new JianpuNote { Pitch = 2 }, new JianpuNote { Pitch = 3 }
                        },
                        ExtraVoices = new List<JianpuVoice>
                        {
                            new JianpuVoice
                            {
                                Role = "Alto",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 5 },
                                    new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 5 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Tenor",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 3 },
                                    new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 3 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Bass",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 1 }, new JianpuNote { Pitch = 1 },
                                    new JianpuNote { Pitch = 1 }, new JianpuNote { Pitch = 1 }
                                }
                            }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "C", BeatPosition = 0 }
                        },
                        LyricText = "God of glo ry,"
                    },
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 3, Dotted = true },
                            new JianpuNote { Pitch = 2, Underlines = 1 },
                            new JianpuNote { Pitch = 2, Dashes = 1 }
                        },
                        ExtraVoices = new List<JianpuVoice>
                        {
                            new JianpuVoice
                            {
                                Role = "Alto",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 7, Dotted = true },
                                    new JianpuNote { Pitch = 7, Underlines = 1 },
                                    new JianpuNote { Pitch = 7, Dashes = 1 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Tenor",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 5, Dotted = true },
                                    new JianpuNote { Pitch = 5, Underlines = 1 },
                                    new JianpuNote { Pitch = 5, Dashes = 1 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Bass",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 5, Dotted = true },
                                    new JianpuNote { Pitch = 5, Underlines = 1 },
                                    new JianpuNote { Pitch = 5, Dashes = 1 }
                                }
                            }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "G", BeatPosition = 0 }
                        },
                        LyricText = "Lord of love;"
                    },
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 3 },
                            new JianpuNote { Pitch = 4 }, new JianpuNote { Pitch = 5 }
                        },
                        ExtraVoices = new List<JianpuVoice>
                        {
                            new JianpuVoice
                            {
                                Role = "Alto",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 5 },
                                    new JianpuNote { Pitch = 7 }, new JianpuNote { Pitch = 7 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Tenor",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 3 },
                                    new JianpuNote { Pitch = 2 }, new JianpuNote { Pitch = 2 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Bass",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 1 }, new JianpuNote { Pitch = 1 },
                                    new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 5 }
                                }
                            }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "C", BeatPosition = 0 },
                            new ChordMarker { Text = "G", BeatPosition = 2 }
                        },
                        LyricText = "Hearts un fold like"
                    },
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 4 },
                            new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 2 }
                        },
                        ExtraVoices = new List<JianpuVoice>
                        {
                            new JianpuVoice
                            {
                                Role = "Alto",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 7 }, new JianpuNote { Pitch = 7 },
                                    new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 5 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Tenor",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 2 }, new JianpuNote { Pitch = 2 },
                                    new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 3 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Bass",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 5 },
                                    new JianpuNote { Pitch = 1 }, new JianpuNote { Pitch = 1 }
                                }
                            }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "G", BeatPosition = 0 },
                            new ChordMarker { Text = "C", BeatPosition = 2 }
                        },
                        LyricText = "flow'rs be fore Thee,"
                    },
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 1 }, new JianpuNote { Pitch = 1 },
                            new JianpuNote { Pitch = 2 }, new JianpuNote { Pitch = 3 }
                        },
                        ExtraVoices = new List<JianpuVoice>
                        {
                            new JianpuVoice
                            {
                                Role = "Alto",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 5 },
                                    new JianpuNote { Pitch = 5 }, new JianpuNote { Pitch = 5 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Tenor",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 3 },
                                    new JianpuNote { Pitch = 3 }, new JianpuNote { Pitch = 3 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Bass",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 1 }, new JianpuNote { Pitch = 1 },
                                    new JianpuNote { Pitch = 1 }, new JianpuNote { Pitch = 1 }
                                }
                            }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "C", BeatPosition = 0 }
                        },
                        LyricText = "Op' ning to the"
                    },
                    new JianpuMeasure
                    {
                        MelodyNotes = new List<JianpuNote>
                        {
                            new JianpuNote { Pitch = 2, Dotted = true },
                            new JianpuNote { Pitch = 1, Underlines = 1 },
                            new JianpuNote { Pitch = 1, Dashes = 1 }
                        },
                        ExtraVoices = new List<JianpuVoice>
                        {
                            new JianpuVoice
                            {
                                Role = "Alto",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 7, Dotted = true },
                                    new JianpuNote { Pitch = 7, Underlines = 1 },
                                    new JianpuNote { Pitch = 3, Dashes = 1 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Tenor",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 5, Dotted = true },
                                    new JianpuNote { Pitch = 5, Underlines = 1 },
                                    new JianpuNote { Pitch = 5, Dashes = 1 }
                                }
                            },
                            new JianpuVoice
                            {
                                Role = "Bass",
                                Notes = new List<JianpuNote>
                                {
                                    new JianpuNote { Pitch = 5, Dotted = true },
                                    new JianpuNote { Pitch = 5, Underlines = 1 },
                                    new JianpuNote { Pitch = 1, Dashes = 1 }
                                }
                            }
                        },
                        ChordMarkers = new List<ChordMarker>
                        {
                            new ChordMarker { Text = "G", BeatPosition = 0 },
                            new ChordMarker { Text = "C", BeatPosition = 2 }
                        },
                        LyricText = "sun a bove."
                    }
                }
            };

            ChordMarkerService.NormalizeScore(score);
            return score;
        }
    }
}
