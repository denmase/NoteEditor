namespace JianpuEditor.Rendering
{
    public sealed class ScoreLayoutOptions
    {
        public static readonly ScoreLayoutOptions Default = new ScoreLayoutOptions();

        public static readonly ScoreLayoutOptions Editor = new ScoreLayoutOptions
        {
            ChordMarkersTextOnly = true,
            ShowChordEditorAffordances = true,
            CompactAccidentalGlyphs = true
        };

        public static readonly ScoreLayoutOptions PdfExport = new ScoreLayoutOptions
        {
            MeasuresPerLine = 4,
            EqualizeMeasureWidths = true,
            NoteWidthScale = 1.0,
            HeaderMarginTop = 96,
            TitleFontSize = 36f,
            MetaFontSize = 18f,
            HeaderMetaLeftAligned = true,
            ChordMarkersTextOnly = true,
            CompactAccidentalGlyphs = true,
            RespectAppTheme = false
        };

        public const int PdfRenderWidth = 1280;

        /// <summary>0 = wrap by page width; otherwise fixed measures per line.</summary>
        public int MeasuresPerLine { get; set; }

        public bool EqualizeMeasureWidths { get; set; }

        public double NoteWidthScale { get; set; } = 1.0;

        /// <summary>0 = use renderer default top margin.</summary>
        public int HeaderMarginTop { get; set; }

        public float TitleFontSize { get; set; } = 22f;

        public float MetaFontSize { get; set; } = 11f;

        public bool HeaderMetaLeftAligned { get; set; }

        /// <summary>For export scenarios like PDF: draws only the chord text, without edit borders or handles.</summary>
        public bool ChordMarkersTextOnly { get; set; }

        /// <summary>Editing UI: shows the beat grid, hints, and selection-state markers.</summary>
        public bool ShowChordEditorAffordances { get; set; }

        /// <summary>When false (e.g. PDF export), always use a light paper background.</summary>
        public bool RespectAppTheme { get; set; } = true;

        /// <summary>Editor and PDF: stacks note annotations above the note in layers (accidentals, octave dots, ornaments stacked vertically).</summary>
        public bool CompactAccidentalGlyphs { get; set; }
    }
}
