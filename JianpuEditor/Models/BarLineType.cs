namespace JianpuEditor.Models
{
    /// <summary>The bar line drawn at the end (right edge) of a measure. Repeat-start is
    /// deliberately not a value here -- see <see cref="JianpuMeasure.IsRepeatStart"/>, which
    /// decorates a measure's own left edge instead, since that's a visually and semantically
    /// separate thing from how its previous neighbor's measure ends.</summary>
    public enum BarLineType
    {
        Single = 0,
        Double = 1,
        Final = 2,
        RepeatEnd = 3
    }
}
