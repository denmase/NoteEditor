using System.Collections.Generic;

namespace JianpuEditor.Models
{
    public sealed class JianpuOrnament
    {
        public OrnamentType Type { get; set; } = OrnamentType.Unknown;

        /// <summary>Index of the melody note this ornament is anchored to (0 = first note). -1 means anchored by beat position only.</summary>
        public int NoteIndex { get; set; } = -1;

        /// <summary>Quarter-beat position within the measure (0 = beat 1). Synced by the normalization logic when NoteIndex is valid.</summary>
        public double BeatPosition { get; set; }

        /// <summary>Extra parameters, such as grace note pitch, slide direction, repeat count, etc.</summary>
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }
}
