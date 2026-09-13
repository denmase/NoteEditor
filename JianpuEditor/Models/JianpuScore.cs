using System.Collections.Generic;

namespace JianpuEditor.Models
{
    public class JianpuScore
    {
        public string Title { get; set; } = "Untitled Score";

        public string KeySignature { get; set; } = "1=C";

        public string Tempo { get; set; } = "Moderato";

        /// <summary>Beats per minute, used for MIDI export.</summary>
        public int Bpm { get; set; } = 120;

        public string Composer { get; set; } = string.Empty;

        public List<JianpuMeasure> Measures { get; set; } = new List<JianpuMeasure>();

        public List<JianpuTie> Ties { get; set; } = new List<JianpuTie>();
    }
}
