namespace JianpuEditor.Models
{
    /// <summary>A numbered bracket ending (1st/2nd ending) spanning a contiguous range of
    /// measures, rendered above the staff. Unlike <see cref="JianpuTie"/>, which anchors to
    /// specific notes, a volta bracket anchors to whole measures.</summary>
    public sealed class JianpuVolta
    {
        public int StartMeasureIndex { get; set; }

        public int EndMeasureIndex { get; set; }

        public string Label { get; set; } = "1.";
    }
}
