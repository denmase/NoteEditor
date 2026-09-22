using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using JianpuEditor.Models;
using JianpuEditor.Services;

namespace JianpuEditor.Rendering
{
    public sealed class JianpuRenderer : IDisposable
    {
        public const int NoteCellWidth = 96;
        public const int MinMeasureWidth = 120;
        public const int MarginLeft = 72;
        public const int MarginTop = 96;
        public const int MelodyRowHeight = 88;
        public const int DynamicsRowHeight = 24;
        public const int SecondaryRowHeight = 40;
        public const int TextRowHeight = SecondaryRowHeight;
        public const int RowGap = 6;
        public const int StaffBlockHeight = MelodyRowHeight + RowGap + DynamicsRowHeight + RowGap + SecondaryRowHeight + RowGap + TextRowHeight;
        public const int StaffBlockSpacing = 48;
        public const int GapEdgeWidth = 10;
        public const int BarHitWidth = 10;
        public const int GapCaretWidth = 6;
        public const int MinNoteWidth = 28;
        public const float LyricBaseFontSize = 20f;

        private readonly Font _rowLabelFont = new Font("Microsoft YaHei", 9f, FontStyle.Regular);
        private readonly Font _ornamentFont = new Font("Microsoft YaHei", 10f, FontStyle.Regular);
        private readonly Font _ornamentLatinFont = new Font("Arial", 10f, FontStyle.Italic);
        private readonly Font _ornamentStackedFont = new Font("Microsoft YaHei", 11f, FontStyle.Regular);
        private readonly Font _ornamentLatinStackedFont = new Font("Arial", 11f, FontStyle.Italic);
        private readonly Font _noteFont = new Font("Arial", 26f, FontStyle.Bold);
        private readonly Font _secondaryFont = new Font("Arial", 20f, FontStyle.Bold);
        private readonly Font _dynamicsFont = new Font("Times New Roman", 14f, FontStyle.Bold | FontStyle.Italic);
        private readonly Font _voltaFont = new Font("Arial", 9f, FontStyle.Bold);
        private bool _disposed;
        private ScoreLayoutOptions _activeLayoutOptions;

        private Color InkColor
        {
            get
            {
                return _activeLayoutOptions != null
                       && _activeLayoutOptions.RespectAppTheme
                       && AppTheme.IsDarkMode
                    ? AppTheme.PrimaryText
                    : Color.Black;
            }
        }

        private Brush CreateInkBrush()
        {
            return new SolidBrush(InkColor);
        }

        private Pen CreateInkPen(float width)
        {
            return new Pen(InkColor, width);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _rowLabelFont.Dispose();
            _ornamentFont.Dispose();
            _ornamentLatinFont.Dispose();
            _ornamentStackedFont.Dispose();
            _ornamentLatinStackedFont.Dispose();
            _noteFont.Dispose();
            _secondaryFont.Dispose();
            _dynamicsFont.Dispose();
            _voltaFont.Dispose();
            _disposed = true;
        }

        public IReadOnlyList<PlaybackMeasureSegment> BuildPlaybackSegments(JianpuScore score, int width)
        {
            var segments = new List<PlaybackMeasureSegment>();
            if (score?.Measures == null || score.Measures.Count == 0)
            {
                return segments;
            }

            var layout = BuildLayout(score, width, ScoreLayoutOptions.Default);
            var beat = 0.0;
            foreach (var measure in layout.Measures)
            {
                if (measure.MeasureIndex < 0 || measure.MeasureIndex >= score.Measures.Count)
                {
                    continue;
                }

                var duration = ScoreMidiSchedule.GetMeasureDurationUnits(score.Measures[measure.MeasureIndex]);
                segments.Add(new PlaybackMeasureSegment
                {
                    StartBeat = beat,
                    DurationBeat = duration,
                    X = measure.X,
                    Width = measure.Width,
                    BlockTop = measure.BlockTop,
                    Height = measure.GetEffectiveHeight()
                });
                beat += duration;
            }

            return segments;
        }

        public Size MeasureScore(JianpuScore score, int maxWidth, ScoreLayoutOptions options = null)
        {
            options = options ?? ScoreLayoutOptions.Default;
            var layout = BuildLayout(score, maxWidth, options);
            var marginTop = GetMarginTop(options, score);
            // The last line's own (post-above-voice-headroom-shift) BlockTop already reflects every
            // line's downward push, including its own -- see ApplyAboveVoiceHeadroom -- so its
            // bottom edge is directly the content's overall bottom, no separate above-voice sum
            // needed here.
            var height = marginTop + 48;
            if (layout.Lines.Count > 0)
            {
                var lastLine = layout.Lines[layout.Lines.Count - 1];
                height = lastLine.BlockTop + lastLine.GetEffectiveHeight() + 48;
            }
            return new Size(Math.Max(maxWidth, layout.TotalWidth + MarginLeft), Math.Max(320, height));
        }

        public IReadOnlyList<MeasureLayout> GetMeasureLayouts(JianpuScore score, int width, ScoreLayoutOptions layoutOptions = null)
        {
            layoutOptions = layoutOptions ?? ScoreLayoutOptions.Default;
            return BuildLayout(score, width, layoutOptions).Measures;
        }

        public void Draw(
            Graphics graphics,
            JianpuScore score,
            int width,
            int selectedMeasureIndex = -1,
            int selectedNoteIndex = -1,
            int selectedInsertIndex = -1,
            IReadOnlyList<int> selectedMeasureIndices = null,
            int selectedTieIndex = -1,
            int selectedChordMeasureIndex = -1,
            int selectedChordMarkerIndex = -1,
            IReadOnlyList<ScoreNoteRef> selectedNotes = null,
            ScoreLayoutOptions layoutOptions = null,
            int selectedVoiceIndex = ScoreNoteRef.PrimaryVoiceIndex)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            layoutOptions = layoutOptions ?? ScoreLayoutOptions.Default;
            _activeLayoutOptions = layoutOptions;
            graphics.Clear(AppTheme.GetScoreBackground(layoutOptions.RespectAppTheme));
            var layout = BuildLayout(score, width, layoutOptions);
            DrawHeader(graphics, score, width, layoutOptions);
            DrawRowLabels(graphics, layout);
            DrawStaff(
                graphics,
                score,
                layout,
                selectedMeasureIndex,
                selectedNoteIndex,
                selectedInsertIndex,
                selectedMeasureIndices,
                selectedTieIndex,
                selectedChordMeasureIndex,
                selectedChordMarkerIndex,
                selectedNotes,
                layoutOptions,
                selectedVoiceIndex);
            _activeLayoutOptions = null;
        }

        public static double GetDurationUnits(JianpuNote note)
        {
            if (note == null)
            {
                return 1;
            }

            // Jianpu duration-extension dashes: 0 dashes=quarter(1 beat), 1 dash=half(2 beats), 3 dashes=whole(4 beats) - durations add up linearly rather than as 2^n
            var lengthUnits = 1.0 + note.Dashes;
            var divisor = Math.Pow(2, note.Underlines);
            var duration = lengthUnits / divisor;
            if (note.Dotted)
            {
                duration *= 1.5;
            }

            return duration;
        }

        public static int GetNoteWidth(JianpuNote note, double scale = 1.0)
        {
            if (scale <= 0)
            {
                scale = 1.0;
            }

            var width = (int)Math.Round(NoteCellWidth * scale * GetDurationUnits(note));
            return Math.Max(Math.Max(12, (int)Math.Round(MinNoteWidth * scale)), width);
        }

        private static int GetGapEdgeWidth(int noteWidth)
        {
            return Math.Min(GapEdgeWidth, Math.Max(4, noteWidth / 5));
        }

        public ScoreHitResult HitTest(JianpuScore score, int width, Point point)
        {
            var headerHit = HitTestHeader(score, width, point);
            if (headerHit != null)
            {
                return headerHit;
            }

            var layout = BuildLayout(score, width, null);

            var barHit = HitTestBarLineGap(score, layout, point);
            if (barHit != null)
            {
                return barHit;
            }

            var tieHit = HitTestTies(score, layout, point);
            if (tieHit != null)
            {
                return tieHit;
            }

            var chordHit = HitTestChordMarkers(score, layout, point);
            if (chordHit != null)
            {
                return chordHit;
            }

            foreach (var measure in layout.Measures)
            {
                var blockBounds = new Rectangle(measure.X, measure.GetDrawTop(), measure.Width, measure.GetDrawHeight());
                if (!blockBounds.Contains(point))
                {
                    continue;
                }

                // Above-voice rows (descant/solo) sit above the melody row itself (point.Y <
                // BlockTop). AboveVoices[0] is the topmost (furthest from the melody) -- see its
                // own doc comment -- matching what DrawExtraVoiceRows draws.
                if (point.Y < measure.BlockTop)
                {
                    for (var i = 0; i < measure.AboveVoices.Count; i++)
                    {
                        var rowTop = measure.BlockTop - (measure.AboveVoices.Count - i) * (MelodyRowHeight + RowGap);
                        if (point.Y >= rowTop && point.Y < rowTop + MelodyRowHeight)
                        {
                            return HitTestVoiceRow(measure, measure.AboveVoices[i], rowTop, point);
                        }
                    }

                    return new ScoreHitResult
                    {
                        HitType = ScoreHitType.Measure,
                        MeasureIndex = measure.MeasureIndex
                    };
                }

                var melodyBottom = measure.BlockTop + MelodyRowHeight;
                var dynamicsTop = GetDynamicsRowTop(measure);
                var secondaryTop = GetSecondaryRowTop(measure);
                var secondaryBottom = secondaryTop + SecondaryRowHeight;
                var lyricTop = GetLyricRowTop(measure);
                var lyricBottom = lyricTop + TextRowHeight;

                if (point.Y < melodyBottom)
                {
                    return HitTestMelodyRow(score, measure, point);
                }

                if (point.Y < dynamicsTop)
                {
                    // Below-voice rows (SATB's Alto/Tenor/Bass) sit between the melody row and
                    // Dynamics. A click on the RowGap between rows (or when there are none) falls
                    // through to a generic Measure hit, same as clicking Dynamics does.
                    for (var i = 0; i < measure.BelowVoices.Count; i++)
                    {
                        var rowTop = measure.BlockTop + (i + 1) * (MelodyRowHeight + RowGap);
                        if (point.Y >= rowTop && point.Y < rowTop + MelodyRowHeight)
                        {
                            return HitTestVoiceRow(measure, measure.BelowVoices[i], rowTop, point);
                        }
                    }

                    return new ScoreHitResult
                    {
                        HitType = ScoreHitType.Measure,
                        MeasureIndex = measure.MeasureIndex
                    };
                }

                if (point.Y < dynamicsTop + DynamicsRowHeight)
                {
                    return new ScoreHitResult
                    {
                        HitType = ScoreHitType.Measure,
                        MeasureIndex = measure.MeasureIndex
                    };
                }

                if (point.Y < secondaryBottom)
                {
                    return HitTestSecondaryRow(score, measure, point, secondaryTop);
                }

                if (point.Y < lyricBottom)
                {
                    return new ScoreHitResult
                    {
                        HitType = ScoreHitType.LyricText,
                        MeasureIndex = measure.MeasureIndex,
                        Bounds = GetTextCellBounds(measure, lyricTop, TextRowHeight).ToIntRect()
                    };
                }

                return new ScoreHitResult
                {
                    HitType = ScoreHitType.Measure,
                    MeasureIndex = measure.MeasureIndex
                };
            }

            return new ScoreHitResult();
        }

        private ScoreHitResult HitTestMelodyRow(JianpuScore score, MeasureLayout measure, Point point)
        {
            var notes = score.Measures[measure.MeasureIndex].MelodyNotes;
            var relX = point.X - measure.X;
            var noteCount = notes.Count;

            if (noteCount == 0)
            {
                return CreateGapHit(measure, 0);
            }

            for (var i = 0; i < noteCount; i++)
            {
                measure.GetNoteDrawBounds(i, out var noteAbsX, out var noteWidth);
                var cellStart = noteAbsX - measure.X;
                var cellEnd = cellStart + noteWidth;
                if (relX < cellStart || relX >= cellEnd)
                {
                    continue;
                }

                var offset = relX - cellStart;
                var edgeWidth = GetGapEdgeWidth(noteWidth);
                if (offset < edgeWidth)
                {
                    return CreateGapHit(measure, i);
                }

                if (offset > noteWidth - edgeWidth)
                {
                    return CreateGapHit(measure, i + 1);
                }

                return new ScoreHitResult
                {
                    HitType = ScoreHitType.Note,
                    MeasureIndex = measure.MeasureIndex,
                    NoteIndex = i,
                    Bounds = measure.GetNoteBounds(i).ToIntRect()
                };
            }

            if (relX >= 0 && relX < measure.Width)
            {
                return CreateGapHit(measure, noteCount);
            }

            return new ScoreHitResult
            {
                HitType = ScoreHitType.Measure,
                MeasureIndex = measure.MeasureIndex
            };
        }

        /// <summary>Same note/gap hit-testing as <see cref="HitTestMelodyRow"/>, against an extra
        /// voice's own notes and draw bounds instead of the primary voice's -- the result carries
        /// <paramref name="voice"/>'s <see cref="MeasureLayout.ExtraVoiceLayout.ExtraVoiceIndex"/>
        /// so a click on Alto/Tenor/Bass (or a descant) resolves to that voice, not the primary
        /// one.</summary>
        private ScoreHitResult HitTestVoiceRow(MeasureLayout measure, MeasureLayout.ExtraVoiceLayout voice, int rowTop, Point point)
        {
            var relX = point.X - measure.X;
            var noteCount = voice.NoteCount;

            if (noteCount == 0)
            {
                return CreateVoiceGapHit(measure, voice, rowTop, 0);
            }

            for (var i = 0; i < noteCount; i++)
            {
                voice.GetNoteDrawBounds(i, measure.X, measure.Width, out var noteAbsX, out var noteWidth);
                var cellStart = noteAbsX - measure.X;
                var cellEnd = cellStart + noteWidth;
                if (relX < cellStart || relX >= cellEnd)
                {
                    continue;
                }

                var offset = relX - cellStart;
                var edgeWidth = GetGapEdgeWidth(noteWidth);
                if (offset < edgeWidth)
                {
                    return CreateVoiceGapHit(measure, voice, rowTop, i);
                }

                if (offset > noteWidth - edgeWidth)
                {
                    return CreateVoiceGapHit(measure, voice, rowTop, i + 1);
                }

                return new ScoreHitResult
                {
                    HitType = ScoreHitType.Note,
                    MeasureIndex = measure.MeasureIndex,
                    VoiceIndex = voice.ExtraVoiceIndex,
                    NoteIndex = i,
                    Bounds = new Rectangle(noteAbsX, rowTop, noteWidth, MelodyRowHeight).ToIntRect()
                };
            }

            if (relX >= 0 && relX < measure.Width)
            {
                return CreateVoiceGapHit(measure, voice, rowTop, noteCount);
            }

            return new ScoreHitResult
            {
                HitType = ScoreHitType.Measure,
                MeasureIndex = measure.MeasureIndex
            };
        }

        private static ScoreHitResult CreateVoiceGapHit(MeasureLayout measure, MeasureLayout.ExtraVoiceLayout voice, int rowTop, int insertIndex)
        {
            return new ScoreHitResult
            {
                HitType = ScoreHitType.Gap,
                MeasureIndex = measure.MeasureIndex,
                VoiceIndex = voice.ExtraVoiceIndex,
                InsertIndex = insertIndex,
                Bounds = GetVoiceGapBounds(measure, voice, rowTop, insertIndex).ToIntRect()
            };
        }

        public static Rectangle GetVoiceGapBounds(MeasureLayout measure, MeasureLayout.ExtraVoiceLayout voice, int rowTop, int insertIndex)
        {
            var x = measure.X + voice.GetInsertOffset(insertIndex) - GapCaretWidth / 2;
            return new Rectangle(x, rowTop + 6, GapCaretWidth, MelodyRowHeight - 12);
        }

        private ScoreHitResult HitTestBarLineGap(JianpuScore score, ScoreLayout layout, Point point)
        {
            foreach (var measure in layout.Measures)
            {
                var melodyTop = measure.BlockTop;
                var melodyBottom = melodyTop + MelodyRowHeight;
                if (point.Y < melodyTop || point.Y >= melodyBottom)
                {
                    continue;
                }

                if (Math.Abs(point.X - measure.X) <= BarHitWidth)
                {
                    if (measure.MeasureIndex > 0)
                    {
                        var prevLayout = FindMeasureLayout(layout, measure.MeasureIndex - 1);
                        if (prevLayout != null && prevLayout.BlockTop == measure.BlockTop && point.X < measure.X)
                        {
                            var prevCount = score.Measures[prevLayout.MeasureIndex].MelodyNotes.Count;
                            return CreateGapHit(prevLayout, prevCount);
                        }
                    }

                    return CreateGapHit(measure, 0);
                }

                if (Math.Abs(point.X - measure.BarLineX) <= BarHitWidth)
                {
                    var noteCount = score.Measures[measure.MeasureIndex].MelodyNotes.Count;
                    var hasNext = measure.MeasureIndex < score.Measures.Count - 1;
                    if (hasNext && point.X > measure.BarLineX)
                    {
                        var nextMeasure = FindMeasureLayout(layout, measure.MeasureIndex + 1);
                        if (nextMeasure != null && nextMeasure.BlockTop == measure.BlockTop)
                        {
                            return CreateGapHit(nextMeasure, 0);
                        }
                    }

                    return CreateGapHit(measure, noteCount);
                }
            }

            return null;
        }

        private static MeasureLayout FindMeasureLayout(ScoreLayout layout, int measureIndex)
        {
            foreach (var measure in layout.Measures)
            {
                if (measure.MeasureIndex == measureIndex)
                {
                    return measure;
                }
            }

            return null;
        }

        private static ScoreHitResult CreateGapHit(MeasureLayout measure, int insertIndex)
        {
            return new ScoreHitResult
            {
                HitType = ScoreHitType.Gap,
                MeasureIndex = measure.MeasureIndex,
                InsertIndex = insertIndex,
                Bounds = GetGapBounds(measure, insertIndex).ToIntRect()
            };
        }

        public static Rectangle GetGapBounds(MeasureLayout measure, int insertIndex)
        {
            var x = measure.X + measure.GetInsertOffset(insertIndex) - GapCaretWidth / 2;
            return new Rectangle(x, measure.BlockTop + 6, GapCaretWidth, MelodyRowHeight - 12);
        }

        public Bitmap RenderToBitmap(JianpuScore score, int width, ScoreLayoutOptions options = null)
        {
            options = options ?? ScoreLayoutOptions.Default;
            var size = MeasureScore(score, width, options);
            var bitmap = new Bitmap(size.Width, size.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                Draw(g, score, width, -1, -1, -1, null, -1, -1, -1, null, options);
            }

            return bitmap;
        }

        public int GetStaffLineCount(JianpuScore score, int width, ScoreLayoutOptions options = null)
        {
            options = options ?? ScoreLayoutOptions.Default;
            return BuildLayout(score, width, options).Lines.Count;
        }

        /// <summary>Each staff line's real vertical footprint (<see cref="StaffLineLayout.
        /// GetEffectiveHeight"/>), in the same order <see cref="GetStaffLineCount"/> counts them.
        /// PDF page planning needs this instead of the fixed <see cref="StaffBlockHeight"/> so a
        /// page containing a taller line (extra voice rows) isn't assigned more lines than it
        /// actually has room for.</summary>
        public IReadOnlyList<int> GetStaffLineHeights(JianpuScore score, int width, ScoreLayoutOptions options = null)
        {
            options = options ?? ScoreLayoutOptions.Default;
            var layout = BuildLayout(score, width, options);
            var heights = new List<int>(layout.Lines.Count);
            foreach (var line in layout.Lines)
            {
                heights.Add(line.GetEffectiveHeight());
            }

            return heights;
        }

        public Bitmap RenderPdfPageToBitmap(
            JianpuScore score,
            int width,
            ScoreLayoutOptions options,
            PdfPageSlice slice)
        {
            options = options ?? ScoreLayoutOptions.PdfExport;
            slice = slice ?? new PdfPageSlice { PageNumber = 1, TotalPages = 1 };
            var layout = BuildLayout(score, width, options);
            if (layout.Lines.Count == 0 || slice.LineCount <= 0)
            {
                var emptyHeight = slice.PageNumber == 1
                    ? GetMarginTop(options, score)
                    : PdfPagePlanner.CompactHeaderHeight;
                emptyHeight += PdfPagePlanner.BottomMargin;
                var emptyBitmap = new Bitmap(Math.Max(1, width), Math.Max(1, emptyHeight));
                using (var g = Graphics.FromImage(emptyBitmap))
                {
                    g.Clear(AppTheme.GetScoreBackground(false));
                    if (slice.PageNumber == 1)
                    {
                        DrawHeader(g, score, width, options);
                    }
                    else
                    {
                        DrawPdfContinuationHeader(g, score, width, slice);
                    }
                }

                return emptyBitmap;
            }

            var start = Math.Max(0, slice.FirstLineIndex);
            var count = Math.Min(slice.LineCount, layout.Lines.Count - start);
            var firstLine = layout.Lines[start];
            var lastLine = layout.Lines[start + count - 1];
            // firstLine's own above-voice headroom (if any) draws above its BlockTop, so the
            // content region starts at its real draw-top (BlockTop - above-headroom), not BlockTop
            // itself -- otherwise a continuation page whose first line has above voices would clip
            // them against the compact header above.
            var contentHeight = lastLine.BlockTop + lastLine.GetEffectiveHeight()
                - (firstLine.BlockTop - firstLine.GetAboveVoicesHeight());
            var headerHeight = slice.PageNumber == 1
                ? GetMarginTop(options, score)
                : PdfPagePlanner.CompactHeaderHeight;
            var bitmapHeight = headerHeight + contentHeight + PdfPagePlanner.BottomMargin;
            var bitmap = new Bitmap(width, Math.Max(1, bitmapHeight));
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                _activeLayoutOptions = options;
                g.Clear(AppTheme.GetScoreBackground(false));
                if (slice.PageNumber == 1)
                {
                    DrawHeader(g, score, width, options);
                }
                else
                {
                    DrawPdfContinuationHeader(g, score, width, slice);
                }

                g.TranslateTransform(0, headerHeight - (firstLine.BlockTop - firstLine.GetAboveVoicesHeight()));
                DrawRowLabelsForBlock(g, firstLine.BlockTop, firstLine.GetMaxBelowVoiceRows());
                DrawStaffLineRange(g, score, layout, start, count, options);
                DrawTiesForLineRange(g, score, layout, start, count, -1);
                DrawVoltaBrackets(g, score, layout, start, count);
                DrawHairpins(g, score, layout, start, count);
                _activeLayoutOptions = null;
            }

            return bitmap;
        }

        public static bool TryGetSyllableAnchorX(
            MeasureLayout layout,
            JianpuMeasure measure,
            LyricSyllable syllable,
            out float centerX)
        {
            centerX = 0;
            if (layout == null || measure == null || syllable == null || string.IsNullOrEmpty(syllable.Text))
            {
                return false;
            }

            var noteIndex = LyricSyllableService.ResolveNoteIndex(measure, syllable);
            var noteCount = measure.MelodyNotes?.Count ?? 0;
            if (noteIndex < 0 || noteIndex >= noteCount)
            {
                return false;
            }

            layout.GetNoteDrawBounds(noteIndex, out var noteX, out var noteWidth);
            centerX = GetNoteHeadCenterX(noteX, noteWidth);
            return true;
        }

        public static bool TryGetOrnamentAnchorX(
            MeasureLayout layout,
            JianpuMeasure measure,
            JianpuOrnament ornament,
            out float anchorX)
        {
            anchorX = 0;
            if (layout == null || measure == null || ornament == null || ornament.Type == OrnamentType.Unknown)
            {
                return false;
            }

            var noteIndex = OrnamentService.ResolveNoteIndex(measure, ornament);
            var noteCount = measure.MelodyNotes?.Count ?? 0;
            if (noteIndex < 0 || noteIndex >= noteCount)
            {
                return false;
            }

            layout.GetNoteDrawBounds(noteIndex, out var noteX, out var noteWidth);
            anchorX = GetOrnamentAnchorX(ornament.Type, noteX, noteWidth);
            return true;
        }

        public static Rectangle GetTextCellBounds(MeasureLayout measure, int rowTop, int rowHeight)
        {
            return new Rectangle(measure.X + 4, rowTop + 2, measure.Width - 8, rowHeight - 4);
        }

        public static int GetDynamicsRowTop(MeasureLayout measure)
        {
            // (1 + BelowVoiceRowCount) rows -- the primary melody row plus every below-voice row
            // (SATB's Alto/Tenor/Bass) -- sit above Dynamics. Equals the original
            // MelodyRowHeight + RowGap when there are no extra voices.
            return measure.BlockTop + (1 + measure.BelowVoiceRowCount) * (MelodyRowHeight + RowGap);
        }

        public static int GetSecondaryRowTop(MeasureLayout measure)
        {
            return GetDynamicsRowTop(measure) + DynamicsRowHeight + RowGap;
        }

        public static int GetLyricRowTop(MeasureLayout measure)
        {
            return GetSecondaryRowTop(measure) + SecondaryRowHeight + RowGap;
        }

        private void DrawPdfContinuationHeader(Graphics g, JianpuScore score, int width, PdfPageSlice slice)
        {
            var title = string.IsNullOrWhiteSpace(score.Title) ? "Untitled Score" : score.Title.Trim();
            var pageText = title + "    Page " + slice.PageNumber + " / " + slice.TotalPages;
            using (var font = new Font("Microsoft YaHei", 14f, FontStyle.Regular))
            using (var ink = CreateInkBrush())
            {
                g.DrawString(pageText, font, ink, MarginLeft, 12f);
            }
        }

        private void DrawRowLabelsForBlock(Graphics g, int blockTop, int belowVoiceRows = 0)
        {
            var dynamicsOffset = (1 + belowVoiceRows) * (MelodyRowHeight + RowGap);
            DrawRowLabel(g, "Melody", blockTop + 28);
            DrawRowLabel(g, "Dynamics", blockTop + dynamicsOffset + 6);
            DrawRowLabel(g, "Secondary", blockTop + dynamicsOffset + DynamicsRowHeight + RowGap + 10);
            DrawRowLabel(g, "Lyrics", blockTop + dynamicsOffset + DynamicsRowHeight + RowGap + SecondaryRowHeight + RowGap + 8);
        }

        private void DrawHeader(Graphics g, JianpuScore score, int width, ScoreLayoutOptions options)
        {
            var title = score.Title ?? string.Empty;
            using (var titleFont = new Font("Microsoft YaHei", options.TitleFontSize, FontStyle.Bold))
            using (var metaFont = new Font("Microsoft YaHei", options.MetaFontSize, FontStyle.Regular))
            {
                var titleTop = 20f;
                var titleSize = g.MeasureString(title, titleFont);
                using (var ink = CreateInkBrush())
                {
                    g.DrawString(title, titleFont, ink, (width - titleSize.Width) / 2f, titleTop);
                }

                var bpm = score.Bpm > 0 ? score.Bpm : 120;
                var meta = string.Format(
                    "{0}    {1}    {2}    BPM {3}",
                    score.KeySignature ?? "1=C",
                    string.IsNullOrWhiteSpace(score.TimeSignature) ? "4/4" : score.TimeSignature,
                    score.Tempo ?? string.Empty,
                    bpm);
                if (!string.IsNullOrWhiteSpace(score.Composer))
                {
                    meta += "    Composer: " + score.Composer;
                }

                var metaTop = titleTop + titleFont.Size + 10f;
                var metaX = options.HeaderMetaLeftAligned
                    ? (float)MarginLeft
                    : (width - g.MeasureString(meta, metaFont).Width) / 2f;
                using (var metaBrush = new SolidBrush(AppTheme.SecondaryText))
                {
                    g.DrawString(meta, metaFont, metaBrush, metaX, metaTop);
                }
            }
        }

        private ScoreHitResult HitTestHeader(JianpuScore score, int width, Point point, ScoreLayoutOptions options = null)
        {
            options = options ?? ScoreLayoutOptions.Default;
            var marginTop = GetMarginTop(options, score);
            if (point.Y < 0 || point.Y >= marginTop)
            {
                return null;
            }

            using (var bitmap = new Bitmap(1, 1))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var headerLayout = BuildHeaderLayout(graphics, score, width, options);
                if (headerLayout.TitleBounds.Contains(point))
                {
                    return CreateHeaderHit(ScoreHeaderField.Title, headerLayout.TitleBounds);
                }

                if (headerLayout.KeyBounds.Contains(point))
                {
                    return CreateHeaderHit(ScoreHeaderField.KeySignature, headerLayout.KeyBounds);
                }

                if (headerLayout.TimeSignatureBounds.Contains(point))
                {
                    return CreateHeaderHit(ScoreHeaderField.TimeSignature, headerLayout.TimeSignatureBounds);
                }

                if (headerLayout.TempoBounds.Contains(point))
                {
                    return CreateHeaderHit(ScoreHeaderField.Tempo, headerLayout.TempoBounds);
                }

                if (headerLayout.BpmBounds.Contains(point))
                {
                    return CreateHeaderHit(ScoreHeaderField.Bpm, headerLayout.BpmBounds);
                }

                if (headerLayout.ComposerBounds.Contains(point))
                {
                    return CreateHeaderHit(ScoreHeaderField.Composer, headerLayout.ComposerBounds);
                }
            }

            return null;
        }

        private static ScoreHitResult CreateHeaderHit(ScoreHeaderField field, Rectangle bounds)
        {
            return new ScoreHitResult
            {
                HitType = ScoreHitType.ScoreHeader,
                HeaderField = field,
                Bounds = bounds.ToIntRect()
            };
        }

        private sealed class ScoreHeaderLayout
        {
            public Rectangle TitleBounds { get; set; }

            public Rectangle KeyBounds { get; set; }

            public Rectangle TimeSignatureBounds { get; set; }

            public Rectangle TempoBounds { get; set; }

            public Rectangle BpmBounds { get; set; }

            public Rectangle ComposerBounds { get; set; }
        }

        private ScoreHeaderLayout BuildHeaderLayout(Graphics g, JianpuScore score, int width, ScoreLayoutOptions options)
        {
            var layout = new ScoreHeaderLayout();
            using (var titleFont = new Font("Microsoft YaHei", options.TitleFontSize, FontStyle.Bold))
            using (var metaFont = new Font("Microsoft YaHei", options.MetaFontSize, FontStyle.Regular))
            {
                const float titleTop = 20f;
                const float metaGap = 10f;
                var metaTop = titleTop + titleFont.Size + metaGap;
                var rowHeight = Math.Max(titleFont.Height, metaFont.Height) + 8f;
                var titleText = string.IsNullOrWhiteSpace(score.Title) ? "Click to enter title" : score.Title;
                var titleWidth = g.MeasureString(titleText, titleFont).Width;
                layout.TitleBounds = Rectangle.Round(new RectangleF(
                    Math.Max(8f, (width - titleWidth) / 2f - 12f),
                    titleTop - 4f,
                    Math.Min(width - 16f, titleWidth + 24f),
                    rowHeight));

                var keyText = score.KeySignature ?? "1=C";
                var timeSignatureText = string.IsNullOrWhiteSpace(score.TimeSignature) ? "4/4" : score.TimeSignature;
                var tempoText = score.Tempo ?? string.Empty;
                var bpmValue = score.Bpm > 0 ? score.Bpm : 120;
                var bpmText = bpmValue.ToString();
                var composerText = score.Composer ?? string.Empty;
                var gapText = "    ";
                var gapWidth = g.MeasureString(gapText, metaFont).Width;

                var keyWidth = g.MeasureString(keyText, metaFont).Width;
                var timeSignatureWidth = Math.Max(g.MeasureString(timeSignatureText, metaFont).Width, 24f);
                var tempoWidth = Math.Max(g.MeasureString(tempoText, metaFont).Width, 36f);
                var bpmSegment = "BPM " + bpmText;
                var bpmWidth = g.MeasureString(bpmSegment, metaFont).Width;
                var composerSegment = "Composer: " + composerText;
                var composerWidth = Math.Max(g.MeasureString(composerSegment, metaFont).Width, 56f);

                var metaTotalWidth = keyWidth + gapWidth + timeSignatureWidth + gapWidth + tempoWidth + gapWidth + bpmWidth + gapWidth + composerWidth;

                var metaX = options.HeaderMetaLeftAligned
                    ? (float)MarginLeft
                    : Math.Max(8f, (width - metaTotalWidth) / 2f);
                var metaY = metaTop - 4f;
                var metaRowHeight = metaFont.Height + 8f;

                layout.KeyBounds = Rectangle.Round(new RectangleF(metaX, metaY, keyWidth + 12f, metaRowHeight));
                metaX += keyWidth + gapWidth;
                layout.TimeSignatureBounds = Rectangle.Round(new RectangleF(metaX - 6f, metaY, timeSignatureWidth + 12f, metaRowHeight));
                metaX += timeSignatureWidth + gapWidth;
                layout.TempoBounds = Rectangle.Round(new RectangleF(metaX - 6f, metaY, tempoWidth + 12f, metaRowHeight));
                metaX += tempoWidth + gapWidth;
                layout.BpmBounds = Rectangle.Round(new RectangleF(metaX - 6f, metaY, bpmWidth + 12f, metaRowHeight));
                metaX += bpmWidth + gapWidth;
                layout.ComposerBounds = Rectangle.Round(new RectangleF(
                    metaX - 6f,
                    metaY,
                    composerWidth + 12f,
                    metaRowHeight));
            }

            return layout;
        }

        public string GetHeaderFieldText(JianpuScore score, ScoreHeaderField field)
        {
            switch (field)
            {
                case ScoreHeaderField.Title:
                    return score.Title ?? string.Empty;
                case ScoreHeaderField.KeySignature:
                    return score.KeySignature ?? string.Empty;
                case ScoreHeaderField.TimeSignature:
                    return score.TimeSignature ?? string.Empty;
                case ScoreHeaderField.Tempo:
                    return score.Tempo ?? string.Empty;
                case ScoreHeaderField.Bpm:
                    return (score.Bpm > 0 ? score.Bpm : 120).ToString();
                case ScoreHeaderField.Composer:
                    return score.Composer ?? string.Empty;
                default:
                    return string.Empty;
            }
        }

        private void DrawRowLabels(Graphics g, ScoreLayout layout)
        {
            if (layout.Lines.Count == 0)
            {
                return;
            }

            var firstLine = layout.Lines[0];
            var blockTop = firstLine.BlockTop;
            var belowVoiceRows = firstLine.GetMaxBelowVoiceRows();
            var dynamicsOffset = (1 + belowVoiceRows) * (MelodyRowHeight + RowGap);
            DrawRowLabel(g, "Melody", blockTop + 28);
            DrawRowLabel(g, "Dynamics", blockTop + dynamicsOffset + 6);
            DrawRowLabel(g, "Secondary", blockTop + dynamicsOffset + DynamicsRowHeight + RowGap + 10);
            DrawRowLabel(g, "Lyrics", blockTop + dynamicsOffset + DynamicsRowHeight + RowGap + SecondaryRowHeight + RowGap + 8);
        }

        private void DrawRowLabel(Graphics g, string text, float y)
        {
            g.DrawString(text, _rowLabelFont, Brushes.DimGray, 8, y);
        }

        private void DrawStaff(
            Graphics g,
            JianpuScore score,
            ScoreLayout layout,
            int selectedMeasureIndex,
            int selectedNoteIndex,
            int selectedInsertIndex,
            IReadOnlyList<int> selectedMeasureIndices,
            int selectedTieIndex,
            int selectedChordMeasureIndex,
            int selectedChordMarkerIndex,
            IReadOnlyList<ScoreNoteRef> selectedNotes,
            ScoreLayoutOptions layoutOptions,
            int selectedVoiceIndex)
        {
            DrawStaffLineRange(
                g,
                score,
                layout,
                0,
                layout.Lines.Count,
                selectedMeasureIndex,
                selectedNoteIndex,
                selectedInsertIndex,
                selectedMeasureIndices,
                selectedChordMeasureIndex,
                selectedChordMarkerIndex,
                selectedNotes,
                layoutOptions,
                selectedVoiceIndex);
            DrawTiesForLineRange(g, score, layout, 0, layout.Lines.Count, selectedTieIndex);
            DrawVoltaBrackets(g, score, layout, 0, layout.Lines.Count);
            DrawHairpins(g, score, layout, 0, layout.Lines.Count);
        }

        private void DrawStaffLineRange(
            Graphics g,
            JianpuScore score,
            ScoreLayout layout,
            int firstLineIndex,
            int lineCount,
            ScoreLayoutOptions layoutOptions)
        {
            DrawStaffLineRange(
                g,
                score,
                layout,
                firstLineIndex,
                lineCount,
                -1,
                -1,
                -1,
                null,
                -1,
                -1,
                null,
                layoutOptions,
                ScoreNoteRef.PrimaryVoiceIndex);
        }

        private void DrawStaffLineRange(
            Graphics g,
            JianpuScore score,
            ScoreLayout layout,
            int firstLineIndex,
            int lineCount,
            int selectedMeasureIndex,
            int selectedNoteIndex,
            int selectedInsertIndex,
            IReadOnlyList<int> selectedMeasureIndices,
            int selectedChordMeasureIndex,
            int selectedChordMarkerIndex,
            IReadOnlyList<ScoreNoteRef> selectedNotes,
            ScoreLayoutOptions layoutOptions,
            int selectedVoiceIndex)
        {
            layoutOptions = layoutOptions ?? ScoreLayoutOptions.Default;
            var visibleMeasures = BuildVisibleMeasures(layout, firstLineIndex, lineCount);
            foreach (var measure in layout.Measures)
            {
                if (!visibleMeasures.Contains(measure))
                {
                    continue;
                }

                var measureData = score.Measures[measure.MeasureIndex];
                var isInSelection = IsMeasureSelected(measure.MeasureIndex, selectedMeasureIndices, selectedMeasureIndex);
                var hasSelectedNotes = selectedNotes != null && selectedNotes.Count > 0;
                var isSelectedMeasure = isInSelection && selectedInsertIndex < 0 && !hasSelectedNotes && selectedNoteIndex < 0;

                if (isInSelection)
                {
                    var alpha = measure.MeasureIndex == selectedMeasureIndex ? 42 : 28;
                    using (var brush = new SolidBrush(Color.FromArgb(alpha, 66, 133, 244)))
                    {
                        g.FillRectangle(brush, measure.X, measure.GetDrawTop(), measure.Width, measure.GetDrawHeight());
                    }

                    if (measure.MeasureIndex == selectedMeasureIndex && selectedMeasureIndices != null && selectedMeasureIndices.Count > 1)
                    {
                        using (var pen = new Pen(Color.FromArgb(180, 41, 98, 255), 2f))
                        {
                            g.DrawRectangle(pen, measure.X + 1, measure.GetDrawTop() + 1, measure.Width - 2, measure.GetDrawHeight() - 2);
                        }
                    }
                }

                DrawMelodyRow(g, measureData, measure, selectedMeasureIndex, selectedNoteIndex, selectedInsertIndex, selectedNotes, selectedVoiceIndex);
                DrawExtraVoiceRows(g, measureData, measure, selectedMeasureIndex, selectedNoteIndex, selectedInsertIndex, selectedNotes, selectedVoiceIndex);
                DrawDynamicsRow(g, measureData, measure);
                DrawChordMarkersRow(
                    g,
                    measureData,
                    measure,
                    isSelectedMeasure,
                    selectedMeasureIndex,
                    selectedChordMeasureIndex,
                    selectedChordMarkerIndex,
                    layoutOptions);
                var hasStructuredLyrics = LyricSyllableService.HasStructuredLyrics(measureData);
                DrawLyricRow(
                    g,
                    measureData,
                    measure,
                    isSelectedMeasure,
                    !hasStructuredLyrics && string.IsNullOrWhiteSpace(measureData.LyricText));
                DrawBarLine(g, measure.X, measure.GetDrawTop(), measure.GetDrawHeight());
                DrawBarLine(g, measure.BarLineX, measure.GetDrawTop(), measure.GetDrawHeight());
                DrawBarLineDecoration(g, measure, measureData);
            }
        }

        private static HashSet<MeasureLayout> BuildVisibleMeasures(ScoreLayout layout, int firstLineIndex, int lineCount)
        {
            var visibleMeasures = new HashSet<MeasureLayout>();
            if (layout?.Lines == null || lineCount <= 0)
            {
                return visibleMeasures;
            }

            var endLine = Math.Min(layout.Lines.Count, firstLineIndex + lineCount);
            for (var lineIndex = Math.Max(0, firstLineIndex); lineIndex < endLine; lineIndex++)
            {
                var line = layout.Lines[lineIndex];
                if (line.Measures == null)
                {
                    continue;
                }

                foreach (var measure in line.Measures)
                {
                    visibleMeasures.Add(measure);
                }
            }

            return visibleMeasures;
        }

        private void DrawTiesForLineRange(
            Graphics g,
            JianpuScore score,
            ScoreLayout layout,
            int firstLineIndex,
            int lineCount,
            int selectedTieIndex)
        {
            if (score.Ties == null || score.Ties.Count == 0)
            {
                return;
            }

            var visibleMeasures = BuildVisibleMeasures(layout, firstLineIndex, lineCount);
            for (var i = 0; i < score.Ties.Count; i++)
            {
                var tie = score.Ties[i];
                var startLayout = FindMeasureLayout(layout, tie.StartMeasureIndex);
                var endLayout = FindMeasureLayout(layout, tie.EndMeasureIndex);
                if (startLayout == null
                    || endLayout == null
                    || !visibleMeasures.Contains(startLayout)
                    || !visibleMeasures.Contains(endLayout))
                {
                    continue;
                }

                if (!TryGetTieGeometry(score, layout, tie, out var geometry))
                {
                    continue;
                }

                var isSelected = i == selectedTieIndex;
                if (isSelected)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(48, 66, 133, 244)))
                    using (var path = new GraphicsPath())
                    {
                        path.AddBezier(
                            geometry.X1,
                            geometry.BaseY,
                            geometry.X1,
                            geometry.ArchTop,
                            geometry.X2,
                            geometry.ArchTop,
                            geometry.X2,
                            geometry.BaseY);
                        g.FillPath(brush, path);
                    }
                }

                var color = isSelected ? AppTheme.TieActive : AppTheme.TieInactive;
                var width = isSelected ? 3f : 2f;
                using (var pen = new Pen(color, width))
                {
                    if (isSelected)
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                    }

                    g.DrawBezier(
                        pen,
                        geometry.X1,
                        geometry.BaseY,
                        geometry.X1,
                        geometry.ArchTop,
                        geometry.X2,
                        geometry.ArchTop,
                        geometry.X2,
                        geometry.BaseY);
                }
            }
        }

        /// <summary>Draws numbered ending brackets in the gap above each staff line (the same
        /// headroom "Beams Above Notes" mode already reaches into for its beam lines), so it needs
        /// no new row and can't affect StaffBlockHeight/hit-testing/PDF pagination. A bracket whose
        /// start and end measures land on different staff lines (a line wrap falls inside it) is
        /// skipped rather than drawn broken across two systems -- narrow scores rarely need more
        /// than a handful of measures per ending, so this is expected to be rare in practice.</summary>
        private void DrawVoltaBrackets(Graphics g, JianpuScore score, ScoreLayout layout, int firstLineIndex, int lineCount)
        {
            if (score?.Voltas == null || score.Voltas.Count == 0)
            {
                return;
            }

            var visibleMeasures = BuildVisibleMeasures(layout, firstLineIndex, lineCount);
            foreach (var volta in score.Voltas)
            {
                var startLayout = FindMeasureLayout(layout, volta.StartMeasureIndex);
                var endLayout = FindMeasureLayout(layout, volta.EndMeasureIndex);
                if (startLayout == null
                    || endLayout == null
                    || !visibleMeasures.Contains(startLayout)
                    || !visibleMeasures.Contains(endLayout)
                    || startLayout.BlockTop != endLayout.BlockTop)
                {
                    continue;
                }

                var lineY = startLayout.BlockTop - 24;
                var tickBottomY = startLayout.BlockTop - 16;
                var x1 = startLayout.X;
                var x2 = endLayout.BarLineX;

                using (var pen = CreateInkPen(1.5f))
                {
                    g.DrawLine(pen, x1, lineY, x2, lineY);
                    g.DrawLine(pen, x1, lineY, x1, tickBottomY);
                    g.DrawLine(pen, x2, lineY, x2, tickBottomY);
                }

                using (var brush = CreateInkBrush())
                {
                    g.DrawString(volta.Label, _voltaFont, brush, x1 + 4, lineY - 13);
                }
            }
        }

        /// <summary>Draws a crescendo/diminuendo wedge in the same <see cref="DynamicsRowHeight"/>
        /// band the discrete <see cref="DynamicMarking"/> text labels use (see
        /// <see cref="DrawDynamicsRow"/>) -- a hairpin is fundamentally a dynamics-row shape, not
        /// an ornament-band one. Mirrors <see cref="DrawVoltaBrackets"/>'s same-staff-line
        /// requirement: a hairpin whose start/end land on different lines (a line wrap falls
        /// inside it) is skipped rather than drawn broken across two systems.</summary>
        private void DrawHairpins(Graphics g, JianpuScore score, ScoreLayout layout, int firstLineIndex, int lineCount)
        {
            if (score?.Hairpins == null || score.Hairpins.Count == 0)
            {
                return;
            }

            var visibleMeasures = BuildVisibleMeasures(layout, firstLineIndex, lineCount);
            foreach (var hairpin in score.Hairpins)
            {
                if (!TryGetHairpinGeometry(score, layout, hairpin, out var geometry))
                {
                    continue;
                }

                if (!visibleMeasures.Contains(geometry.StartLayout) || !visibleMeasures.Contains(geometry.EndLayout))
                {
                    continue;
                }

                using (var pen = CreateInkPen(1.5f))
                {
                    if (hairpin.IsCrescendo)
                    {
                        g.DrawLine(pen, geometry.X1, geometry.MidY, geometry.X2, geometry.Top);
                        g.DrawLine(pen, geometry.X1, geometry.MidY, geometry.X2, geometry.Bottom);
                    }
                    else
                    {
                        g.DrawLine(pen, geometry.X1, geometry.Top, geometry.X2, geometry.MidY);
                        g.DrawLine(pen, geometry.X1, geometry.Bottom, geometry.X2, geometry.MidY);
                    }
                }
            }
        }

        private static bool TryGetHairpinGeometry(
            JianpuScore score,
            ScoreLayout layout,
            JianpuHairpin hairpin,
            out HairpinGeometry geometry)
        {
            geometry = default;
            var startLayout = FindMeasureLayout(layout, hairpin.StartMeasureIndex);
            var endLayout = FindMeasureLayout(layout, hairpin.EndMeasureIndex);
            if (startLayout == null || endLayout == null || startLayout.BlockTop != endLayout.BlockTop)
            {
                return false;
            }

            if (hairpin.StartMeasureIndex < 0 || hairpin.StartMeasureIndex >= score.Measures.Count
                || hairpin.EndMeasureIndex < 0 || hairpin.EndMeasureIndex >= score.Measures.Count)
            {
                return false;
            }

            var startMeasure = score.Measures[hairpin.StartMeasureIndex];
            var endMeasure = score.Measures[hairpin.EndMeasureIndex];
            if (startMeasure.MelodyNotes == null || endMeasure.MelodyNotes == null
                || hairpin.StartNoteIndex < 0 || hairpin.StartNoteIndex >= startMeasure.MelodyNotes.Count
                || hairpin.EndNoteIndex < 0 || hairpin.EndNoteIndex >= endMeasure.MelodyNotes.Count)
            {
                return false;
            }

            startLayout.GetNoteDrawBounds(hairpin.StartNoteIndex, out var startX, out var startWidth);
            endLayout.GetNoteDrawBounds(hairpin.EndNoteIndex, out var endX, out var endWidth);

            var x1 = GetNoteHeadCenterX(startX, startWidth);
            var x2 = GetNoteHeadCenterX(endX, endWidth);
            if (x2 <= x1)
            {
                return false;
            }

            var rowTop = GetDynamicsRowTop(startLayout);
            geometry = new HairpinGeometry
            {
                X1 = x1,
                X2 = x2,
                StartLayout = startLayout,
                EndLayout = endLayout,
                Top = rowTop + 4f,
                Bottom = rowTop + DynamicsRowHeight - 4f,
                MidY = rowTop + DynamicsRowHeight / 2f
            };
            return true;
        }

        private struct HairpinGeometry
        {
            public float X1;
            public float X2;
            public float Top;
            public float Bottom;
            public float MidY;
            public MeasureLayout StartLayout;
            public MeasureLayout EndLayout;
        }

        private ScoreHitResult HitTestTies(JianpuScore score, ScoreLayout layout, Point point)
        {
            if (score?.Ties == null || score.Ties.Count == 0)
            {
                return null;
            }

            const float hitThreshold = 8f;
            for (var i = score.Ties.Count - 1; i >= 0; i--)
            {
                if (!TryGetTieGeometry(score, layout, score.Ties[i], out var geometry))
                {
                    continue;
                }

                if (!IsPointNearTie(point, geometry, hitThreshold))
                {
                    continue;
                }

                var bounds = Rectangle.FromLTRB(
                    (int)Math.Floor(geometry.X1) - 4,
                    (int)Math.Floor(geometry.ArchTop) - 4,
                    (int)Math.Ceiling(geometry.X2) + 4,
                    (int)Math.Ceiling(geometry.BaseY) + 4);
                return new ScoreHitResult
                {
                    HitType = ScoreHitType.Tie,
                    TieIndex = i,
                    MeasureIndex = score.Ties[i].StartMeasureIndex,
                    Bounds = bounds.ToIntRect()
                };
            }

            return null;
        }

        private void DrawTies(Graphics g, JianpuScore score, ScoreLayout layout, int selectedTieIndex)
        {
            if (score.Ties == null || score.Ties.Count == 0)
            {
                return;
            }

            for (var i = 0; i < score.Ties.Count; i++)
            {
                if (!TryGetTieGeometry(score, layout, score.Ties[i], out var geometry))
                {
                    continue;
                }

                var isSelected = i == selectedTieIndex;
                if (isSelected)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(48, 66, 133, 244)))
                    using (var path = new GraphicsPath())
                    {
                        path.AddBezier(
                            geometry.X1,
                            geometry.BaseY,
                            geometry.X1,
                            geometry.ArchTop,
                            geometry.X2,
                            geometry.ArchTop,
                            geometry.X2,
                            geometry.BaseY);
                        g.FillPath(brush, path);
                    }
                }

                var color = isSelected ? AppTheme.TieActive : AppTheme.TieInactive;
                var width = isSelected ? 3f : 2f;
                using (var pen = new Pen(color, width))
                {
                    if (isSelected)
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                    }

                    g.DrawBezier(
                        pen,
                        geometry.X1,
                        geometry.BaseY,
                        geometry.X1,
                        geometry.ArchTop,
                        geometry.X2,
                        geometry.ArchTop,
                        geometry.X2,
                        geometry.BaseY);
                }
            }
        }

        private static bool TryGetTieGeometry(JianpuScore score, ScoreLayout layout, JianpuTie tie, out TieGeometry geometry)
        {
            geometry = default;
            var startLayout = FindMeasureLayout(layout, tie.StartMeasureIndex);
            var endLayout = FindMeasureLayout(layout, tie.EndMeasureIndex);
            if (startLayout == null || endLayout == null)
            {
                return false;
            }

            if (tie.StartMeasureIndex < 0 || tie.StartMeasureIndex >= score.Measures.Count
                || tie.EndMeasureIndex < 0 || tie.EndMeasureIndex >= score.Measures.Count)
            {
                return false;
            }

            var startMeasure = score.Measures[tie.StartMeasureIndex];
            var endMeasure = score.Measures[tie.EndMeasureIndex];
            if (startMeasure.MelodyNotes == null || endMeasure.MelodyNotes == null
                || tie.StartNoteIndex < 0 || tie.StartNoteIndex >= startMeasure.MelodyNotes.Count
                || tie.EndNoteIndex < 0 || tie.EndNoteIndex >= endMeasure.MelodyNotes.Count)
            {
                return false;
            }

            startLayout.GetNoteDrawBounds(tie.StartNoteIndex, out var startX, out var startWidth);
            endLayout.GetNoteDrawBounds(tie.EndNoteIndex, out var endX, out var endWidth);

            var x1 = GetNoteHeadCenterX(startX, startWidth);
            var x2 = GetNoteHeadCenterX(endX, endWidth);
            if (x2 <= x1)
            {
                return false;
            }

            geometry = new TieGeometry
            {
                X1 = x1,
                X2 = x2,
                BaseY = startLayout.BlockTop + 8f,
                ArchTop = startLayout.BlockTop - 6f
            };
            return true;
        }

        private static bool IsPointNearTie(Point point, TieGeometry geometry, float threshold)
        {
            if (point.X < geometry.X1 - threshold || point.X > geometry.X2 + threshold
                || point.Y < geometry.ArchTop - threshold || point.Y > geometry.BaseY + threshold)
            {
                return false;
            }

            const int steps = 24;
            var thresholdSq = threshold * threshold;
            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var curvePoint = EvaluateCubicBezier(
                    t,
                    geometry.X1,
                    geometry.BaseY,
                    geometry.X1,
                    geometry.ArchTop,
                    geometry.X2,
                    geometry.ArchTop,
                    geometry.X2,
                    geometry.BaseY);
                var dx = point.X - curvePoint.X;
                var dy = point.Y - curvePoint.Y;
                if (dx * dx + dy * dy <= thresholdSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static PointF EvaluateCubicBezier(float t, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3)
        {
            var u = 1f - t;
            var tt = t * t;
            var uu = u * u;
            var uuu = uu * u;
            var ttt = tt * t;
            var x = uuu * x0 + 3f * uu * t * x1 + 3f * u * tt * x2 + ttt * x3;
            var y = uuu * y0 + 3f * uu * t * y1 + 3f * u * tt * y2 + ttt * y3;
            return new PointF(x, y);
        }

        private struct TieGeometry
        {
            public float X1;
            public float X2;
            public float BaseY;
            public float ArchTop;
        }

        private void DrawMelodyRow(
            Graphics g,
            JianpuMeasure measure,
            MeasureLayout layout,
            int selectedMeasureIndex,
            int selectedNoteIndex,
            int selectedInsertIndex,
            IReadOnlyList<ScoreNoteRef> selectedNotes,
            int selectedVoiceIndex)
        {
            if (layout.MeasureIndex == selectedMeasureIndex && selectedInsertIndex >= 0 && selectedVoiceIndex == VoiceLayoutService.PrimaryVoiceIndex)
            {
                DrawGapCaret(g, layout, selectedInsertIndex);
            }

            MelodyChordService.NormalizeMeasure(measure);
            var noteCount = measure.MelodyNotes.Count;
            for (var i = 0; i < noteCount; i++)
            {
                var isSelected = selectedNotes != null && selectedNotes.Count > 0
                    ? selectedNotes.Any(note => note.MeasureIndex == layout.MeasureIndex && note.NoteIndex == i && note.VoiceIndex == VoiceLayoutService.PrimaryVoiceIndex)
                    : layout.MeasureIndex == selectedMeasureIndex && i == selectedNoteIndex && selectedVoiceIndex == VoiceLayoutService.PrimaryVoiceIndex;
                layout.GetNoteDrawBounds(i, out var noteX, out var noteWidth);
                int nextNoteX;
                int nextNoteWidth;
                if (i + 1 < noteCount)
                {
                    layout.GetNoteDrawBounds(i + 1, out nextNoteX, out nextNoteWidth);
                }
                else
                {
                    nextNoteX = noteX + noteWidth;
                    nextNoteWidth = noteWidth;
                }
                var nextHeadCenterX = nextNoteX + Math.Min(NoteCellWidth, nextNoteWidth) / 2f;

                var slotNotes = MelodyChordService.GetNotesAtSlot(measure, i);
                var melodyNotes = slotNotes
                    .Where(note => note.Type == NoteType.Note)
                    .OrderByDescending(note => note.Octave * 7 + note.Pitch)
                    .ToList();
                if (melodyNotes.Count > 1)
                {
                    DrawSimultaneousNotes(
                        g,
                        measure,
                        i,
                        melodyNotes,
                        measure.MelodyNotes[i],
                        noteX,
                        layout.BlockTop,
                        noteWidth,
                        nextHeadCenterX,
                        isSelected);
                }
                else
                {
                    DrawNote(
                        g,
                        measure,
                        i,
                        measure.MelodyNotes[i],
                        noteX,
                        layout.BlockTop,
                        noteWidth,
                        nextHeadCenterX,
                        isSelected);
                }
            }

            DrawBeatGroupUnderlines(g, measure, layout);
            DrawOrnaments(g, measure, layout);
        }

        /// <summary>Draws every "below" voice's own row (SATB's Alto/Tenor/Bass), directly under
        /// the primary melody row -- notes, octave dots, and dotted-note/dash marks only. Ties,
        /// ornaments, chords, and beat-group underlines aren't part of the <see cref="JianpuVoice"/>
        /// data model yet (see ROADMAP.md), so they're not drawn here; that keeps this additive to
        /// <see cref="DrawMelodyRow"/> rather than needing it generalized.</summary>
        private void DrawExtraVoiceRows(
            Graphics g,
            JianpuMeasure measureData,
            MeasureLayout layout,
            int selectedMeasureIndex,
            int selectedNoteIndex,
            int selectedInsertIndex,
            IReadOnlyList<ScoreNoteRef> selectedNotes,
            int selectedVoiceIndex)
        {
            var aboveVoices = layout.AboveVoices;
            for (var voiceIndex = 0; voiceIndex < aboveVoices.Count; voiceIndex++)
            {
                // Index 0 is the topmost row (furthest from the melody) -- see AboveVoices' own
                // doc comment -- so it needs the most negative offset from BlockTop.
                var rowTop = layout.BlockTop - (aboveVoices.Count - voiceIndex) * (MelodyRowHeight + RowGap);
                DrawVoiceRow(g, measureData, layout, aboveVoices[voiceIndex], rowTop, selectedMeasureIndex, selectedNoteIndex, selectedInsertIndex, selectedNotes, selectedVoiceIndex);
            }

            var belowVoices = layout.BelowVoices;
            for (var voiceIndex = 0; voiceIndex < belowVoices.Count; voiceIndex++)
            {
                var rowTop = layout.BlockTop + (voiceIndex + 1) * (MelodyRowHeight + RowGap);
                DrawVoiceRow(g, measureData, layout, belowVoices[voiceIndex], rowTop, selectedMeasureIndex, selectedNoteIndex, selectedInsertIndex, selectedNotes, selectedVoiceIndex);
            }
        }

        private void DrawVoiceRow(
            Graphics g,
            JianpuMeasure measureData,
            MeasureLayout layout,
            MeasureLayout.ExtraVoiceLayout voice,
            int rowTop,
            int selectedMeasureIndex,
            int selectedNoteIndex,
            int selectedInsertIndex,
            IReadOnlyList<ScoreNoteRef> selectedNotes,
            int selectedVoiceIndex)
        {
            if (layout.MeasureIndex == selectedMeasureIndex && selectedInsertIndex >= 0 && selectedVoiceIndex == voice.ExtraVoiceIndex)
            {
                DrawVoiceGapCaret(g, layout, voice, rowTop, selectedInsertIndex);
            }

            var noteCount = voice.NoteCount;
            for (var i = 0; i < noteCount; i++)
            {
                var isSelected = selectedNotes != null && selectedNotes.Count > 0
                    ? selectedNotes.Any(note => note.MeasureIndex == layout.MeasureIndex && note.NoteIndex == i && note.VoiceIndex == voice.ExtraVoiceIndex)
                    : layout.MeasureIndex == selectedMeasureIndex && i == selectedNoteIndex && selectedVoiceIndex == voice.ExtraVoiceIndex;

                voice.GetNoteDrawBounds(i, layout.X, layout.Width, out var noteX, out var noteWidth);
                int nextNoteX;
                int nextNoteWidth;
                if (i + 1 < noteCount)
                {
                    voice.GetNoteDrawBounds(i + 1, layout.X, layout.Width, out nextNoteX, out nextNoteWidth);
                }
                else
                {
                    nextNoteX = noteX + noteWidth;
                    nextNoteWidth = noteWidth;
                }

                var nextHeadCenterX = nextNoteX + Math.Min(NoteCellWidth, nextNoteWidth) / 2f;
                DrawExtraVoiceNote(g, measureData, voice.Notes[i], noteX, rowTop, noteWidth, nextHeadCenterX, isSelected);
            }
        }

        private void DrawExtraVoiceNote(
            Graphics g,
            JianpuMeasure measureData,
            JianpuNote note,
            int x,
            int y,
            int noteWidth,
            float nextHeadCenterX,
            bool isSelected)
        {
            if (isSelected)
            {
                using (var brush = new SolidBrush(Color.FromArgb(90, 255, 214, 102)))
                {
                    g.FillRectangle(brush, x + 2, y + 2, noteWidth - 4, MelodyRowHeight - 4);
                }

                using (var pen = new Pen(Color.FromArgb(220, 180, 60), 2f))
                {
                    g.DrawRectangle(pen, x + 2, y + 2, noteWidth - 4, MelodyRowHeight - 4);
                }
            }

            var headWidth = Math.Min(NoteCellWidth, noteWidth);
            using (var ink = CreateInkBrush())
            using (var inkPen = CreateInkPen(2f))
            {
                var headCenterX = x + headWidth / 2f;
                DrawCenteredNoteText(g, JianpuPitchCodec.GetPitchDisplayText(note), x, y, headWidth, _noteFont, ink);
                DrawNoteOctaveDots(g, note, x, y, headCenterX, ink);
                DrawNoteDottedAndDashes(g, measureData, note, x, y, noteWidth, headWidth, headCenterX, nextHeadCenterX, ink, inkPen);
            }
        }

        private void DrawOrnaments(
            Graphics g,
            JianpuMeasure measure,
            MeasureLayout layout)
        {
            OrnamentService.NormalizeMeasure(measure);
            if (measure.Ornaments == null || measure.Ornaments.Count == 0)
            {
                return;
            }

            var noteCount = measure.MelodyNotes?.Count ?? 0;
            if (noteCount == 0)
            {
                return;
            }

            var grouped = measure.Ornaments
                .GroupBy(item => OrnamentService.ResolveNoteIndex(measure, item))
                .Where(group => group.Key >= 0 && group.Key < noteCount)
                .OrderBy(group => group.Key);
            foreach (var group in grouped)
            {
                layout.GetNoteDrawBounds(group.Key, out var noteX, out var noteWidth);
                var noteOrnaments = NoteTopAnnotationPlanner.GetOrnamentsForNote(measure, group.Key);
                NoteTopAnnotationLayout topLayout = null;
                if (_activeLayoutOptions != null && _activeLayoutOptions.CompactAccidentalGlyphs)
                {
                    topLayout = NoteTopAnnotationPlanner.Plan(
                        measure.MelodyNotes[group.Key],
                        noteX,
                        Math.Min(NoteCellWidth, noteWidth),
                        noteOrnaments,
                        true);
                }

                var stackIndex = 0;
                foreach (var ornament in group)
                {
                    DrawOrnamentGlyph(
                        g,
                        ornament,
                        noteX,
                        noteWidth,
                        layout.BlockTop,
                        stackIndex,
                        group.Count(),
                        topLayout);
                    stackIndex++;
                }
            }
        }

        private void DrawOrnamentGlyph(
            Graphics g,
            JianpuOrnament ornament,
            int noteX,
            int noteWidth,
            int rowTop,
            int stackIndex,
            int stackCount,
            NoteTopAnnotationLayout topLayout = null)
        {
            var glyph = OrnamentService.GetPlaceholderGlyph(ornament.Type);
            if (string.IsNullOrEmpty(glyph))
            {
                return;
            }

            var headWidth = Math.Min(NoteCellWidth, noteWidth);
            var useStackedOrnamentFont = topLayout != null && ornament.Type != OrnamentType.Fermata;
            var font = UsesLatinOrnamentFont(ornament.Type)
                ? useStackedOrnamentFont ? _ornamentLatinStackedFont : _ornamentLatinFont
                : useStackedOrnamentFont ? _ornamentStackedFont : _ornamentFont;

            var size = g.MeasureString(glyph, font);
            float anchorX;
            float drawY;
            if (topLayout != null)
            {
                anchorX = topLayout.GetOrnamentAnchorX(ornament.Type, noteX, headWidth);
                drawY = rowTop + topLayout.GetOrnamentY(ornament.Type);
            }
            else
            {
                anchorX = GetOrnamentAnchorX(ornament.Type, noteX, noteWidth);
                drawY = rowTop + GetOrnamentTopOffset(ornament.Type);
            }

            var totalWidth = stackCount * size.Width + Math.Max(0, stackCount - 1) * 2f;
            var drawX = anchorX - totalWidth / 2f + stackIndex * (size.Width + 2f);
            using (var ink = CreateInkBrush())
            {
                g.DrawString(glyph, font, ink, drawX, drawY);
            }
        }

        private static bool UsesLatinOrnamentFont(OrnamentType type)
        {
            return type == OrnamentType.Trill
                || type == OrnamentType.Mordent
                || type == OrnamentType.BreathMark
                || type == OrnamentType.Staccato
                || type == OrnamentType.Accent
                || type == OrnamentType.Tenuto
                || type == OrnamentType.Segno
                || type == OrnamentType.Coda;
        }

        private static float GetOrnamentAnchorX(OrnamentType type, int noteX, int noteWidth)
        {
            var headWidth = Math.Min(NoteCellWidth, noteWidth);
            if (type == OrnamentType.GraceNote)
            {
                return noteX + Math.Min(14f, headWidth * 0.25f);
            }

            if (type == OrnamentType.BreathMark)
            {
                return noteX + headWidth + BreathMarkGap;
            }

            return GetNoteHeadCenterX(noteX, noteWidth);
        }

        private static float GetOrnamentTopOffset(OrnamentType type)
        {
            return type == OrnamentType.Fermata ? 0f : 2f;
        }

        private static float GetNoteHeadCenterX(int noteX, int noteWidth)
        {
            var headWidth = Math.Min(NoteCellWidth, noteWidth);
            return noteX + headWidth / 2f;
        }

        private void DrawBeatGroupUnderlines(
            Graphics g,
            JianpuMeasure measure,
            MeasureLayout layout)
        {
            var notes = measure.MelodyNotes;
            if (notes == null || notes.Count == 0)
            {
                return;
            }

            var groups = BeatGroupUnderlinePlanner.GroupNotesByQuarterBeat(notes);
            var rowTop = layout.BlockTop;

            foreach (var group in groups)
            {
                if (group.Count == 0)
                {
                    continue;
                }

                var maxUnderlines = 0;
                foreach (var noteIndex in group)
                {
                    maxUnderlines = Math.Max(maxUnderlines, notes[noteIndex].Underlines);
                }

                if (maxUnderlines == 0)
                {
                    continue;
                }

                for (var underlineIndex = 0; underlineIndex < maxUnderlines; underlineIndex++)
                {
                    // A shorter note (fewer underlines) interrupting the group -- e.g. an eighth
                    // note sandwiched between two sixteenths -- must break the deeper underline
                    // level rather than let it span across the shorter note's own region, so this
                    // collects every contiguous run of notes that reach this underline level
                    // instead of just the first/last matching note in the whole group.
                    foreach (var span in BeatGroupUnderlinePlanner.CollectSpans(group, notes, underlineIndex))
                    {
                        var spanStart = span.Start;
                        var spanEnd = span.End;
                        layout.GetNoteDrawBounds(spanStart, out var startX, out var spanStartWidth);
                        var spanStartHeadWidth = Math.Min(NoteCellWidth, spanStartWidth);
                        var spanStartHeadCenterX = startX + spanStartHeadWidth / 2f;
                        if (AppTheme.UnderlinesAbove)
                        {
                            // A note's slot is sized proportionally to its duration (so a dotted-eighth
                            // like "1" gets a much wider slot than a following 16th like "6"), but the
                            // digit itself is centered within that slot -- using the slot's raw left edge
                            // as the beam's start leaves a visible gap of dead space before the digit for
                            // any note whose slot is wider than its rendered text. This exists in below
                            // mode too, but that mode is the original/default Jianpu rendering and must
                            // stay exactly as it was -- Indonesian style is an explicit opt-in via the
                            // View menu toggle, not a replacement, so this fix is scoped to above mode
                            // only. Measure the actual glyph and start there instead, matching
                            // DrawCenteredNoteText's own centering math exactly.
                            var spanStartNote = notes[spanStart];
                            var spanStartText = JianpuPitchCodec.GetPitchDisplayText(spanStartNote);
                            var spanStartTextWidth = g.MeasureString(spanStartText, _noteFont).Width;
                            startX = (int)(startX + (spanStartHeadWidth - spanStartTextWidth) / 2f);
                        }
                        // The barline-crossing beam gap fix (using natural, un-stretched bounds for the
                        // beam's end) is also scoped to above mode only, for the same reason as the
                        // start-edge fix above -- below mode is the original renderer and must not change.
                        int endX;
                        if (AppTheme.UnderlinesAbove)
                        {
                            layout.GetNoteNaturalDrawBounds(spanEnd, out var endNoteX, out var endNoteWidth);
                            endX = endNoteX + endNoteWidth;
                        }
                        else
                        {
                            layout.GetNoteDrawBounds(spanEnd, out var endNoteX, out var endNoteWidth);
                            endX = endNoteX + endNoteWidth;
                        }

                        // A dotted note "borrows" part of the next beat subdivision via its augmentation
                        // dot, so a shorter (higher-index) beam that starts right after a dotted note
                        // should extend back to cover that dot, not start at the following note's own
                        // left edge -- otherwise the beam looks disconnected from the rhythm it's
                        // describing. Only above mode repositions the dot near the beam (see
                        // DrawNoteDottedAndDashes); below mode's dot sits far enough from its own beam
                        // that this doesn't apply.
                        if (AppTheme.UnderlinesAbove && underlineIndex > 0 && spanStart > 0
                            && group.Contains(spanStart - 1) && notes[spanStart - 1].Dotted)
                        {
                            layout.GetNoteDrawBounds(spanStart - 1, out var prevX, out var prevWidth);
                            var prevHeadCenterX = prevX + Math.Min(NoteCellWidth, prevWidth) / 2f;
                            // Match DrawNoteDottedAndDashes' above-mode dot position exactly (glyph
                            // center to glyph center, not slot edges), so the beam's start lines up with
                            // (a hair before) the dot instead of the following note's own edge.
                            startX = (int)((prevHeadCenterX + spanStartHeadCenterX) / 2f - 2.5f);
                        }
                        // Both modes use a fixed mapping from underlineIndex to height -- NOT the
                        // per-group maxUnderlines -- so that every group's underlineIndex-0 (full-span)
                        // beam lands at the same height across the whole row, regardless of whether a
                        // neighboring group happens to have a deeper (16th-note) subdivision. Note.
                        // Underlines is capped at 2 app-wide (see NoteEditorViewModel.GetDurationTier),
                        // so underlineIndex only ever reaches 0 or 1.
                        // Below mode stacks the full-span (underlineIndex 0) beam closest to the row,
                        // with the shorter partial-span (underlineIndex 1, 16th-note level) beam farther
                        // out, starting just past the negative-octave-dot zone (y+52+).
                        // Above mode uses the opposite stacking order -- shorter beam closest to the
                        // row, full-span beam farthest -- matching the Indonesian Jianpu convention (see
                        // reference), stacked upward clear of the positive-octave-dot zone (dots only
                        // ever grow downward from y+4, so this can't collide with them regardless of a
                        // note's octave), using the StaffBlockSpacing gap between systems as headroom.
                        const int maxUnderlineIndex = 1;
                        var lineY = AppTheme.UnderlinesAbove
                            ? rowTop - 8 - (maxUnderlineIndex - underlineIndex) * 6
                            : rowTop + 62 + underlineIndex * 6;
                        g.DrawLine(Pens.Black, startX, lineY, endX, lineY);
                    }
                }
            }
        }

        private void DrawGapCaret(Graphics g, MeasureLayout layout, int insertIndex)
        {
            var x = layout.X + layout.GetInsertOffset(insertIndex);
            var top = layout.BlockTop + 8;
            var height = MelodyRowHeight - 16;
            DrawGapCaretAt(g, x, top, height);
        }

        private void DrawVoiceGapCaret(Graphics g, MeasureLayout layout, MeasureLayout.ExtraVoiceLayout voice, int rowTop, int insertIndex)
        {
            var x = layout.X + voice.GetInsertOffset(insertIndex);
            var top = rowTop + 8;
            var height = MelodyRowHeight - 16;
            DrawGapCaretAt(g, x, top, height);
        }

        private void DrawGapCaretAt(Graphics g, int x, int top, int height)
        {
            using (var brush = new SolidBrush(Color.FromArgb(80, 76, 175, 80)))
            {
                g.FillRectangle(brush, x - GapCaretWidth / 2, top, GapCaretWidth, height);
            }

            using (var pen = new Pen(Color.FromArgb(220, 46, 125, 50), 2f))
            {
                g.DrawLine(pen, x, top, x, top + height);
            }
        }

        private ScoreHitResult HitTestChordMarkers(JianpuScore score, ScoreLayout layout, Point point)
        {
            var boundsList = ChordMarkerLayout.BuildBounds(score, layout.Measures);
            for (var i = boundsList.Count - 1; i >= 0; i--)
            {
                var bounds = boundsList[i];
                if (bounds.DeleteBounds.Contains(point))
                {
                    return new ScoreHitResult
                    {
                        HitType = ScoreHitType.ChordDelete,
                        MeasureIndex = bounds.MeasureIndex,
                        ChordMarkerIndex = bounds.MarkerIndex,
                        Bounds = bounds.DeleteBounds.ToIntRect()
                    };
                }

                if (bounds.DragHandleBounds.Contains(point))
                {
                    return new ScoreHitResult
                    {
                        HitType = ScoreHitType.ChordDragHandle,
                        MeasureIndex = bounds.MeasureIndex,
                        ChordMarkerIndex = bounds.MarkerIndex,
                        Bounds = bounds.DragHandleBounds.ToIntRect()
                    };
                }

                if (bounds.TextBoxBounds.Contains(point))
                {
                    return new ScoreHitResult
                    {
                        HitType = ScoreHitType.ChordMarker,
                        MeasureIndex = bounds.MeasureIndex,
                        ChordMarkerIndex = bounds.MarkerIndex,
                        Bounds = bounds.TextBoxBounds.ToIntRect()
                    };
                }
            }

            return null;
        }

        private ScoreHitResult HitTestSecondaryRow(JianpuScore score, MeasureLayout measure, Point point, int secondaryTop)
        {
            var measureData = score.Measures[measure.MeasureIndex];
            ChordMarkerService.NormalizeMeasure(measureData);
            var bounds = ChordMarkerLayout.GetSecondaryRowBounds(measure);
            if (!bounds.Contains(point))
            {
                return new ScoreHitResult
                {
                    HitType = ScoreHitType.ChordRow,
                    MeasureIndex = measure.MeasureIndex,
                    Bounds = bounds.ToIntRect()
                };
            }

            if (measureData.ChordMarkers.Count < JianpuMeasure.MaxChordMarkers)
            {
                var duration = ScoreMidiSchedule.GetMeasureDurationUnits(measureData);
                var beat = ChordMarkerService.MapXToBeat(measure.X, measure.Width, point.X, duration);
                var occupied = measureData.ChordMarkers.Any(marker => Math.Abs(marker.BeatPosition - beat) < 0.001);
                if (!occupied)
                {
                    return new ScoreHitResult
                    {
                        HitType = ScoreHitType.ChordAddSlot,
                        MeasureIndex = measure.MeasureIndex,
                        Bounds = bounds.ToIntRect()
                    };
                }
            }

            return new ScoreHitResult
            {
                HitType = ScoreHitType.ChordRow,
                MeasureIndex = measure.MeasureIndex,
                Bounds = bounds.ToIntRect()
            };
        }

        private void DrawDynamicsRow(Graphics g, JianpuMeasure measureData, MeasureLayout measure)
        {
            DynamicMarkingService.NormalizeMeasure(measureData);
            if (measureData.Dynamics.Count == 0)
            {
                return;
            }

            var rowBounds = DynamicMarkingLayout.GetRowBounds(measure);
            using (var ink = CreateInkBrush())
            {
                foreach (var marking in measureData.Dynamics)
                {
                    if (string.IsNullOrWhiteSpace(marking.Text))
                    {
                        continue;
                    }

                    var anchorX = ChordMarkerLayout.GetBeatAnchorX(measure, measureData, marking.BeatPosition);
                    var y = rowBounds.Top + (rowBounds.Height - _dynamicsFont.Height) / 2f;
                    g.DrawString(marking.Text, _dynamicsFont, ink, anchorX, y);
                }
            }
        }

        private void DrawChordMarkersRow(
            Graphics g,
            JianpuMeasure measureData,
            MeasureLayout measure,
            bool isSelectedMeasure,
            int selectedMeasureIndex,
            int selectedChordMeasureIndex,
            int selectedChordMarkerIndex,
            ScoreLayoutOptions layoutOptions)
        {
            ChordMarkerService.NormalizeMeasure(measureData);
            var rowBounds = ChordMarkerLayout.GetSecondaryRowBounds(measure);
            var textOnly = layoutOptions.ChordMarkersTextOnly;
            var showAffordances = layoutOptions.ShowChordEditorAffordances;

            if (showAffordances && isSelectedMeasure && measureData.ChordMarkers.Count == 0)
            {
                using (var brush = new SolidBrush(Color.FromArgb(28, 120, 144, 156)))
                {
                    g.FillRectangle(brush, rowBounds);
                }
            }

            if (showAffordances)
            {
                var duration = ScoreMidiSchedule.GetMeasureDurationUnits(measureData);
                var beatCount = (int)Math.Max(1, Math.Round(duration));
                using (var gridPen = new Pen(Color.FromArgb(36, 120, 144, 156), 1f))
                {
                    for (var beat = 0; beat <= beatCount; beat++)
                    {
                        var x = ChordMarkerLayout.GetBeatAnchorX(measure, measureData, beat);
                        g.DrawLine(gridPen, x, rowBounds.Top + 2, x, rowBounds.Bottom - 2);
                    }
                }
            }

            for (var i = 0; i < measureData.ChordMarkers.Count; i++)
            {
                var marker = measureData.ChordMarkers[i];
                var markerBounds = ChordMarkerLayout.GetMarkerBounds(measure, measureData, measure.MeasureIndex, i, marker);
                if (textOnly)
                {
                    var isSelected = measure.MeasureIndex == selectedChordMeasureIndex && i == selectedChordMarkerIndex;
                    DrawChordMarkerTextOnly(g, markerBounds, marker.Text, rowBounds, isSelected, showAffordances);
                }
                else
                {
                    var isSelected = measure.MeasureIndex == selectedChordMeasureIndex && i == selectedChordMarkerIndex;
                    DrawChordMarkerChrome(g, markerBounds, marker.Text, isSelected);
                }
            }

            if (showAffordances
                && isSelectedMeasure
                && measure.MeasureIndex == selectedMeasureIndex
                && measureData.ChordMarkers.Count < JianpuMeasure.MaxChordMarkers)
            {
                using (var font = new Font("Microsoft YaHei", 8f, FontStyle.Regular))
                {
                    var hint = "+ Click an empty beat to add a chord";
                    g.DrawString(hint, font, Brushes.DimGray, rowBounds.Left + 4, rowBounds.Bottom - 14);
                }
            }
        }

        private void DrawChordMarkerTextOnly(
            Graphics g,
            ChordMarkerBounds bounds,
            string text,
            Rectangle rowBounds,
            bool isSelected,
            bool showAffordances)
        {
            var y = rowBounds.Top + (rowBounds.Height - _secondaryFont.Height) / 2f;
            if (showAffordances && isSelected)
            {
                var chromeBounds = Rectangle.Union(
                    bounds.TextBoxBounds,
                    Rectangle.Union(bounds.DeleteBounds, bounds.DragHandleBounds));
                using (var brush = new SolidBrush(Color.FromArgb(255, 255, 240)))
                using (var pen = new Pen(Color.FromArgb(220, 41, 98, 255), 2f))
                {
                    g.FillRectangle(brush, chromeBounds);
                    g.DrawRectangle(pen, chromeBounds);
                }

                using (var handleFont = new Font("Arial", 8f, FontStyle.Bold))
                using (var deleteFont = new Font("Arial", 10f, FontStyle.Bold))
                {
                    g.DrawString("::", handleFont, Brushes.DimGray, bounds.DragHandleBounds.Left + 1, bounds.DragHandleBounds.Top + 4);
                    g.DrawString("x", deleteFont, Brushes.IndianRed, bounds.DeleteBounds.Left + 4, bounds.DeleteBounds.Top + 2);
                }
            }

            var displayText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            if (string.IsNullOrEmpty(displayText))
            {
                return;
            }

            using (var brush = new SolidBrush(AppTheme.PrimaryText))
            {
                g.DrawString(displayText, _secondaryFont, brush, bounds.AnchorX, y);
            }
        }

        private void DrawChordMarkerChrome(Graphics g, ChordMarkerBounds bounds, string text, bool isSelected)
        {
            var backColor = isSelected ? AppTheme.ChordSelectedBackground : AppTheme.ChordBackground;
            var borderColor = isSelected ? AppTheme.ChordSelectedBorder : AppTheme.ChordBorder;

            using (var backBrush = new SolidBrush(backColor))
            using (var borderPen = new Pen(borderColor, isSelected ? 2f : 1f))
            {
                g.FillRectangle(backBrush, bounds.TextBoxBounds);
                g.DrawRectangle(borderPen, bounds.TextBoxBounds);
                g.FillRectangle(backBrush, bounds.DeleteBounds);
                g.DrawRectangle(borderPen, bounds.DeleteBounds);
                g.FillRectangle(backBrush, bounds.DragHandleBounds);
                g.DrawRectangle(borderPen, bounds.DragHandleBounds);
            }

            using (var handleFont = new Font("Arial", 8f, FontStyle.Bold))
            using (var deleteFont = new Font("Arial", 10f, FontStyle.Bold))
            using (var textFont = _secondaryFont)
            {
                using (var handleBrush = new SolidBrush(AppTheme.SecondaryText))
                using (var deleteBrush = new SolidBrush(Color.IndianRed))
                using (var textBrush = new SolidBrush(AppTheme.PrimaryText))
                {
                    g.DrawString("::", handleFont, handleBrush, bounds.DragHandleBounds.Left + 1, bounds.DragHandleBounds.Top + 4);
                    g.DrawString("x", deleteFont, deleteBrush, bounds.DeleteBounds.Left + 4, bounds.DeleteBounds.Top + 2);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var textRect = new RectangleF(
                            bounds.TextBoxBounds.Left + 4,
                            bounds.TextBoxBounds.Top,
                            bounds.TextBoxBounds.Width - 8,
                            bounds.TextBoxBounds.Height);
                        using (var format = new StringFormat
                        {
                            Alignment = StringAlignment.Near,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter,
                            FormatFlags = StringFormatFlags.NoWrap
                        })
                        {
                            g.DrawString(text.Trim(), textFont, textBrush, textRect, format);
                        }
                    }
                }
            }
        }

        private void DrawLyricRow(
            Graphics g,
            JianpuMeasure measureData,
            MeasureLayout layout,
            bool isSelectedMeasure,
            bool isEmpty)
        {
            var rowTop = GetLyricRowTop(layout);
            var bounds = GetTextCellBounds(layout, rowTop, TextRowHeight);
            var fontSize = Math.Max(8f, LyricBaseFontSize * (float)layout.MelodyScale);
            using (var font = new Font("Arial", fontSize, FontStyle.Bold))
            {
                if (LyricSyllableService.HasStructuredLyrics(measureData))
                {
                    DrawStructuredLyricRow(g, measureData, layout, bounds, rowTop, isSelectedMeasure, isEmpty, font);
                    return;
                }

                var text = measureData.LyricText ?? string.Empty;
                DrawTextRowCore(g, text, bounds, isSelectedMeasure, isEmpty, font, StringAlignment.Near);
            }
        }

        private void DrawStructuredLyricRow(
            Graphics g,
            JianpuMeasure measureData,
            MeasureLayout layout,
            Rectangle bounds,
            int rowTop,
            bool isSelectedMeasure,
            bool isEmpty,
            Font font)
        {
            if (isSelectedMeasure && isEmpty)
            {
                using (var pen = new Pen(Color.FromArgb(180, 180, 180)) { DashStyle = DashStyle.Dot })
                {
                    g.DrawRectangle(pen, bounds);
                }
            }

            var noteCount = measureData.MelodyNotes?.Count ?? 0;
            foreach (var syllable in measureData.LyricSyllables)
            {
                if (string.IsNullOrEmpty(syllable?.Text))
                {
                    continue;
                }

                var noteIndex = LyricSyllableService.ResolveNoteIndex(measureData, syllable);
                if (noteIndex < 0 || noteIndex >= noteCount)
                {
                    continue;
                }

                layout.GetNoteDrawBounds(noteIndex, out var noteX, out var noteWidth);
                var centerX = GetNoteHeadCenterX(noteX, noteWidth);
                var textWidth = (int)Math.Ceiling(g.MeasureString(syllable.Text, font).Width) + 4;
                var syllableWidth = Math.Max(12, Math.Min(noteWidth, textWidth));
                var syllableLeft = (int)Math.Round(centerX - syllableWidth / 2f);
                var syllableBounds = new Rectangle(
                    syllableLeft,
                    rowTop + 2,
                    syllableWidth,
                    TextRowHeight - 4);

                using (var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                })
                using (var ink = CreateInkBrush())
                {
                    var rect = new RectangleF(
                        syllableBounds.X,
                        syllableBounds.Y,
                        syllableBounds.Width,
                        syllableBounds.Height);
                    g.DrawString(syllable.Text, font, ink, rect, format);
                }
            }
        }

        private void DrawTextRowCore(
            Graphics g,
            string text,
            Rectangle bounds,
            bool isSelectedMeasure,
            bool isEmpty,
            Font font,
            StringAlignment alignment)
        {
            if (isSelectedMeasure && isEmpty)
            {
                using (var pen = new Pen(Color.FromArgb(180, 180, 180)) { DashStyle = DashStyle.Dot })
                {
                    g.DrawRectangle(pen, bounds);
                }
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var rect = new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            using (var format = new StringFormat
            {
                Alignment = alignment,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                using (var ink = CreateInkBrush())
                {
                    g.DrawString(text, font, ink, rect, format);
                }
            }
        }

        private const float CompactAccidentalFontSize = 10f;

        /// <summary>Gap between a note's right edge and a breath mark anchored just after it --
        /// breath marks sit in the space between notes, unlike every other ornament here, which is
        /// centered above the note itself.</summary>
        private const float BreathMarkGap = 3f;

        private void DrawSimultaneousNotes(
            Graphics g,
            JianpuMeasure measure,
            int noteIndex,
            IReadOnlyList<JianpuNote> melodyNotes,
            JianpuNote durationNote,
            int x,
            int y,
            int noteWidth,
            float nextHeadCenterX,
            bool isSelected)
        {
            var headWidth = Math.Min(NoteCellWidth, noteWidth);
            if (isSelected)
            {
                using (var brush = new SolidBrush(Color.FromArgb(90, 255, 214, 102)))
                {
                    g.FillRectangle(brush, x + 2, y + 2, noteWidth - 4, MelodyRowHeight - 4);
                }

                using (var pen = new Pen(Color.FromArgb(220, 180, 60), 2f))
                {
                    g.DrawRectangle(pen, x + 2, y + 2, noteWidth - 4, MelodyRowHeight - 4);
                }
            }

            using (var ink = CreateInkBrush())
            using (var inkPen = CreateInkPen(2f))
            using (var chordFont = new Font(_noteFont.FontFamily, Math.Max(8f, _noteFont.Size * 0.72f), _noteFont.Style))
            {
                var count = melodyNotes.Count;
                var lineHeight = 17f;
                var blockHeight = count * lineHeight;
                var startY = y + 18f + Math.Max(0f, (36f - blockHeight) / 2f);
                var headCenterX = x + headWidth / 2f;

                for (var i = 0; i < count; i++)
                {
                    var note = melodyNotes[i];
                    var text = JianpuPitchCodec.GetPitchDisplayText(note);
                    var textSize = g.MeasureString(text, chordFont);
                    var textX = x + (headWidth - textSize.Width) / 2f;
                    var textY = startY + i * lineHeight;
                    g.DrawString(text, chordFont, ink, textX, textY);

                    if (note.Octave > 0)
                    {
                        for (var dot = 0; dot < note.Octave; dot++)
                        {
                            g.FillEllipse(ink, textX + textSize.Width + 2, textY - 2 + dot * 6, 4, 4);
                        }
                    }
                    else if (note.Octave < 0)
                    {
                        for (var dot = 0; dot < Math.Abs(note.Octave); dot++)
                        {
                            g.FillEllipse(ink, textX + textSize.Width + 2, textY + 10 + dot * 6, 4, 4);
                        }
                    }
                }

                DrawNoteDottedAndDashes(g, measure, durationNote, x, y, noteWidth, headWidth, headCenterX, nextHeadCenterX, ink, inkPen);
            }
        }

        private void DrawNote(
            Graphics g,
            JianpuMeasure measure,
            int noteIndex,
            JianpuNote note,
            int x,
            int y,
            int noteWidth,
            float nextHeadCenterX,
            bool isSelected)
        {
            var headWidth = Math.Min(NoteCellWidth, noteWidth);
            var useTopLayout = _activeLayoutOptions != null && _activeLayoutOptions.CompactAccidentalGlyphs;
            NoteTopAnnotationLayout topLayout = null;
            if (useTopLayout && note != null && note.Type == NoteType.Note)
            {
                var ornaments = measure == null
                    ? new List<JianpuOrnament>()
                    : NoteTopAnnotationPlanner.GetOrnamentsForNote(measure, noteIndex);
                topLayout = NoteTopAnnotationPlanner.Plan(note, x, headWidth, ornaments, true);
            }

            if (isSelected)
            {
                using (var brush = new SolidBrush(Color.FromArgb(90, 255, 214, 102)))
                {
                    g.FillRectangle(brush, x + 2, y + 2, noteWidth - 4, MelodyRowHeight - 4);
                }

                using (var pen = new Pen(Color.FromArgb(220, 180, 60), 2f))
                {
                    g.DrawRectangle(pen, x + 2, y + 2, noteWidth - 4, MelodyRowHeight - 4);
                }
            }

            using (var ink = CreateInkBrush())
            using (var inkPen = CreateInkPen(2f))
            {
                var headCenterX = x + headWidth / 2f;
                if (note.Type == NoteType.Rest)
                {
                    DrawCenteredNoteText(g, JianpuPitchCodec.GetPitchDisplayText(note), x, y, headWidth, _noteFont, ink);
                }
                else if (topLayout != null)
                {
                    DrawCenteredNoteText(g, JianpuPitchCodec.GetDisplayDegree(note).ToString(), x, y, headWidth, _noteFont, ink);
                    DrawCompactAccidentalMark(g, note, y, topLayout, ink);
                }
                else
                {
                    DrawCenteredNoteText(
                        g,
                        JianpuPitchCodec.GetPitchDisplayText(note),
                        x,
                        y,
                        headWidth,
                        _noteFont,
                        ink);
                }

                if (topLayout != null)
                {
                    DrawNoteOctaveDots(g, note, y, topLayout, ink);
                }
                else
                {
                    DrawNoteOctaveDots(g, note, x, y, headCenterX, ink);
                }
                DrawNoteDottedAndDashes(g, measure, note, x, y, noteWidth, headWidth, headCenterX, nextHeadCenterX, ink, inkPen);
            }
        }

        private void DrawCenteredNoteText(
            Graphics g,
            string text,
            int x,
            int y,
            int headWidth,
            Font font,
            Brush ink)
        {
            var textSize = g.MeasureString(text, font);
            var textX = x + (headWidth - textSize.Width) / 2f;
            var textY = y + 18f;
            g.DrawString(text, font, ink, textX, textY);
        }

        private void DrawCompactAccidentalMark(
            Graphics g,
            JianpuNote note,
            int y,
            NoteTopAnnotationLayout topLayout,
            Brush ink)
        {
            if (topLayout == null || !topLayout.HasAccidental)
            {
                return;
            }

            if (note.Accidental == AccidentalKind.Natural)
            {
                // Drawn as vector strokes rather than the Unicode natural-sign character (U+266E):
                // that glyph isn't reliably present in every font this app might run under (found
                // via this sandbox's Mono+libgdiplus render harness -- Arial there substitutes a
                // fallback glyph that doesn't read as a natural sign at all), so this avoids the
                // font-coverage risk entirely rather than gambling on a specific font/platform.
                DrawNaturalSignGlyph(g, topLayout.AccidentalX, y + topLayout.AccidentalY);
                return;
            }

            if (topLayout.AccidentalIsSuffix)
            {
                // Indonesian kres/mol isn't a separate character next to the digit -- it's a
                // diagonal stroke drawn through the digit itself (kres `/` sharp bottom-left to
                // top-right, mol `\` flat top-left to bottom-right), the way it appears in real
                // notasi angka sheet music. Measure the digit's actual rendered box (font metrics
                // vary per platform, so a fixed size would drift) and draw the stroke corner to
                // corner across it.
                var degreeText = JianpuPitchCodec.GetDisplayDegree(note).ToString();
                var digitSize = g.MeasureString(degreeText, _noteFont);
                var left = topLayout.HeadCenterX - digitSize.Width / 2f;
                var right = topLayout.HeadCenterX + digitSize.Width / 2f;
                var top = y + NoteTopAnnotationLayout.DigitTextY;
                var bottom = top + digitSize.Height;
                using (var pen = CreateInkPen(2.5f))
                {
                    if (note.Accidental == AccidentalKind.Sharp)
                    {
                        g.DrawLine(pen, left, bottom, right, top);
                    }
                    else
                    {
                        g.DrawLine(pen, left, top, right, bottom);
                    }
                }

                return;
            }

            var mark = JianpuPitchCodec.GetAccidentalMark(note);
            if (string.IsNullOrEmpty(mark))
            {
                return;
            }

            using (var accidentalFont = new Font("Arial", CompactAccidentalFontSize, FontStyle.Bold))
            {
                g.DrawString(mark, accidentalFont, ink, topLayout.AccidentalX, y + topLayout.AccidentalY);
            }
        }

        private void DrawNaturalSignGlyph(Graphics g, float x, float y)
        {
            var leftX = x + 1f;
            var rightX = x + 6f;
            using (var thinPen = CreateInkPen(1.3f))
            using (var thickPen = CreateInkPen(2.4f))
            {
                g.DrawLine(thinPen, leftX, y + 4f, leftX, y + 16f);
                g.DrawLine(thinPen, rightX, y, rightX, y + 12f);
                g.DrawLine(thickPen, leftX, y + 6.5f, rightX, y + 2.5f);
                g.DrawLine(thickPen, leftX, y + 13.5f, rightX, y + 9.5f);
            }
        }

        private void DrawNoteOctaveDots(Graphics g, JianpuNote note, int y, NoteTopAnnotationLayout topLayout, Brush ink)
        {
            if (note.Octave > 0)
            {
                for (var i = 0; i < note.Octave; i++)
                {
                    g.FillEllipse(
                        ink,
                        topLayout.OctaveDotCenterX - 3,
                        y + topLayout.OctaveDotBaseY + i * NoteTopAnnotationLayout.OctaveDotStackSpacing,
                        (int)NoteTopAnnotationLayout.OctaveDotDiameter,
                        (int)NoteTopAnnotationLayout.OctaveDotDiameter);
                }
            }
            else if (note.Octave < 0)
            {
                for (var i = 0; i < Math.Abs(note.Octave); i++)
                {
                    g.FillEllipse(ink, topLayout.HeadCenterX - 3, y + 52 + i * 10, 6, 6);
                }
            }
        }

        private void DrawNoteOctaveDots(Graphics g, JianpuNote note, int x, int y, float headCenterX, Brush ink)
        {
            if (note.Octave > 0)
            {
                for (var i = 0; i < note.Octave; i++)
                {
                    g.FillEllipse(ink, headCenterX - 3, y + 4 + i * 10, 6, 6);
                }
            }
            else if (note.Octave < 0)
            {
                for (var i = 0; i < Math.Abs(note.Octave); i++)
                {
                    g.FillEllipse(ink, headCenterX - 3, y + 52 + i * 10, 6, 6);
                }
            }
        }

        private void DrawNoteDottedAndDashes(
            Graphics g,
            JianpuMeasure measure,
            JianpuNote note,
            int x,
            int y,
            int noteWidth,
            int headWidth,
            float headCenterX,
            float nextHeadCenterX,
            Brush ink,
            Pen inkPen)
        {
            if (note.Dotted)
            {
                // Below mode keeps the dot tucked close to the note's own glyph. Above mode moves it
                // to the true midpoint between this note's glyph center and the next note's glyph
                // center (not slot edges -- those don't track the visual character position and left
                // the dot sitting almost on top of the next note instead of centered between the two).
                var dotX = AppTheme.UnderlinesAbove
                    ? (headCenterX + nextHeadCenterX) / 2f - 2.5f
                    : Math.Min(x + headWidth - 8, headCenterX + 12);
                var dotY = AppTheme.UnderlinesAbove ? y + 30 : y + 42;
                g.FillEllipse(ink, dotX, dotY, 5, 5);
            }

            var extensionWidth = noteWidth - headWidth;
            if (extensionWidth > 0 && note.Dashes > 0)
            {
                // A measure sharing its width across multiple voices (SATB etc.) needs each dash
                // on the note's true beat grid so it lines up with the other voices' beats, not
                // spaced by the old cosmetic "fill the leftover space evenly" formula below. The
                // note occupies (Dashes + 1) beat cells (one for the head, one per dash); a note
                // glyph is drawn centered *within* its own cell (see DrawCenteredNoteText), so a
                // dash must be centered within its cell too -- placing it at the cell boundary
                // (i.e. dropping the "+ 0.5") looked grid-aligned but was actually a systematic
                // half-cell offset from where the note glyphs themselves sit.
                if (VoiceLayoutService.HasMultipleVoices(measure))
                {
                    var beatCellWidth = (float)noteWidth / (note.Dashes + 1);
                    for (var i = 0; i < note.Dashes; i++)
                    {
                        var dashCenterX = x + beatCellWidth * (i + 1.5f);
                        var dashX = (int)Math.Round(dashCenterX) - 4;
                        g.DrawLine(inkPen, dashX, y + 36, dashX + 8, y + 36);
                    }
                }
                else
                {
                    for (var i = 0; i < note.Dashes; i++)
                    {
                        var dashX = x + headWidth + (extensionWidth * (i + 1)) / (note.Dashes + 1) - 4;
                        g.DrawLine(inkPen, dashX, y + 36, dashX + 8, y + 36);
                    }
                }
            }
            else
            {
                for (var i = 0; i < note.Dashes; i++)
                {
                    var dashX = x + headWidth - 8 + i * 12;
                    g.DrawLine(inkPen, dashX, y + 36, dashX + 8, y + 36);
                }
            }
        }

        private void DrawBarLine(Graphics g, int x, int top, int height)
        {
            using (var pen = CreateInkPen(2f))
            {
                g.DrawLine(pen, x, top + 4, x, top + height - 4);
            }
        }

        /// <summary>Draws Double/Final/RepeatEnd/RepeatStart bar line decoration as extra ink
        /// added after the measure's own two ordinary <see cref="DrawBarLine"/> calls, which stay
        /// untouched so default (Single, no repeat) rendering is pixel-identical to before. Every
        /// extra line/dot stays within this measure's own horizontal footprint (never crossing
        /// into a neighboring measure), so there's no "which measure owns the shared boundary"
        /// conflict at the line shared with an adjacent measure.</summary>
        private void DrawBarLineDecoration(Graphics g, MeasureLayout measure, JianpuMeasure measureData)
        {
            var top = measure.GetDrawTop();
            var height = measure.GetDrawHeight();
            switch (measureData.BarLineType)
            {
                case BarLineType.Double:
                    DrawBarLine(g, measure.BarLineX - 5, top, height);
                    break;
                case BarLineType.Final:
                    DrawThickBarLine(g, measure.BarLineX - 6, top, height);
                    break;
                case BarLineType.RepeatEnd:
                    DrawThickBarLine(g, measure.BarLineX - 6, top, height);
                    DrawRepeatDots(g, measure.BarLineX - 14, measure.BlockTop);
                    break;
            }

            if (measureData.IsRepeatStart)
            {
                DrawThickBarLine(g, measure.X + 5, top, height);
                DrawRepeatDots(g, measure.X + 13, measure.BlockTop);
            }
        }

        private void DrawThickBarLine(Graphics g, int x, int top, int height)
        {
            using (var pen = CreateInkPen(5f))
            {
                g.DrawLine(pen, x, top + 4, x, top + height - 4);
            }
        }

        private void DrawRepeatDots(Graphics g, int x, int top)
        {
            const int radius = 4;
            var centerY = top + MelodyRowHeight / 2;
            using (var brush = CreateInkBrush())
            {
                g.FillEllipse(brush, x - radius, centerY - radius - 6, radius * 2, radius * 2);
                g.FillEllipse(brush, x - radius, centerY + radius - 2, radius * 2, radius * 2);
            }
        }

        private static int GetMarginTop(ScoreLayoutOptions options, JianpuScore score = null)
        {
            var baseMargin = options.HeaderMarginTop > 0 ? options.HeaderMarginTop : MarginTop;
            // Above-mode beam lines extend upward from the first system's melody row into this
            // margin; without extra headroom here they collide with the header (title/key/tempo)
            // text that occupies the same space. Later systems already clear the gap via
            // StaffBlockSpacing, so this only needs to cover the first row.
            if (AppTheme.UnderlinesAbove)
            {
                baseMargin += 20;
            }

            // A volta bracket also draws into this same margin (see DrawVoltaBrackets), and a
            // score's first volta can land on the very first line, which is the one system whose
            // headroom is the header itself rather than the plain StaffBlockSpacing gap every
            // later system gets. Only reserved when the score actually has a volta, so scores
            // without one keep today's exact margin.
            if (score?.Voltas != null && score.Voltas.Count > 0)
            {
                baseMargin += 24;
            }

            return baseMargin;
        }

        private ScoreLayout BuildLayout(JianpuScore score, int maxWidth, ScoreLayoutOptions options)
        {
            options = options ?? ScoreLayoutOptions.Default;
            var layout = new ScoreLayout();
            if (score.Measures == null || score.Measures.Count == 0)
            {
                return layout;
            }

            var marginTop = GetMarginTop(options, score);
            var usableWidth = Math.Max(
                options.MeasuresPerLine > 0 ? 200 : 500,
                maxWidth - MarginLeft - 16);
            var slotWidth = options.MeasuresPerLine > 0 && options.EqualizeMeasureWidths
                ? usableWidth / options.MeasuresPerLine
                : 0;
            var x = MarginLeft;
            var blockTop = marginTop;
            var line = new StaffLineLayout { BlockTop = blockTop };
            layout.Lines.Add(line);
            var maxRight = x;

            for (var index = 0; index < score.Measures.Count; index++)
            {
                var measureWidth = slotWidth > 0
                    ? slotWidth
                    : CalculateMeasureWidth(score.Measures[index], options.NoteWidthScale);

                var needNewLine = false;
                if (options.MeasuresPerLine > 0)
                {
                    needNewLine = line.Measures.Count >= options.MeasuresPerLine;
                }
                else if (x > MarginLeft && x + measureWidth > MarginLeft + usableWidth)
                {
                    needNewLine = true;
                }

                if (needNewLine)
                {
                    blockTop += line.GetEffectiveHeight() + StaffBlockSpacing;
                    line = new StaffLineLayout { BlockTop = blockTop };
                    layout.Lines.Add(line);
                    x = MarginLeft;
                }

                var measureLayout = new MeasureLayout
                {
                    MeasureIndex = index,
                    X = x,
                    Width = measureWidth,
                    BlockTop = blockTop,
                    BarLineX = x + measureWidth
                };
                measureLayout.ComputeNoteLayout(score.Measures[index], options.NoteWidthScale);
                measureLayout.ApplyMelodyScale(measureWidth, VoiceLayoutService.HasMultipleVoices(score.Measures[index]));
                measureLayout.ComputeExtraVoiceLayouts(score.Measures[index], options.NoteWidthScale, measureWidth);
                layout.Measures.Add(measureLayout);
                line.Measures.Add(measureLayout);

                x += measureWidth;
                maxRight = Math.Max(maxRight, x);
            }

            ApplyAboveVoiceHeadroom(layout);

            layout.TotalWidth = options.MeasuresPerLine > 0 && options.EqualizeMeasureWidths
                ? MarginLeft + usableWidth + 24
                : maxRight + 24;
            return layout;
        }

        /// <summary>Second pass: pushes every line's (and its measures') <see
        /// cref="MeasureLayout.BlockTop"/> down to make room for above-voice rows (descant/solo),
        /// which draw upward from BlockTop. A line's own above-voice headroom, plus every earlier
        /// line's, accumulates into its shift -- so BlockTop keeps meaning exactly what it always
        /// has ("the melody row's top") for every existing consumer (ties, hairpins, gap carets,
        /// Dynamics row), while the extra space those consumers never needed to know about is
        /// reserved above it. A score where no measure has an above voice shifts nothing (zero
        /// regression for the ordinary and below-voice-only cases).</summary>
        private static void ApplyAboveVoiceHeadroom(ScoreLayout layout)
        {
            var cumulativeShift = 0;
            foreach (var line in layout.Lines)
            {
                cumulativeShift += line.GetAboveVoicesHeight();
                if (cumulativeShift == 0)
                {
                    continue;
                }

                line.BlockTop += cumulativeShift;
                foreach (var measure in line.Measures)
                {
                    measure.BlockTop += cumulativeShift;
                }
            }
        }

        private static bool IsMeasureSelected(int measureIndex, IReadOnlyList<int> selectedMeasureIndices, int selectedMeasureIndex)
        {
            if (selectedMeasureIndices != null && selectedMeasureIndices.Count > 0)
            {
                foreach (var index in selectedMeasureIndices)
                {
                    if (index == measureIndex)
                    {
                        return true;
                    }
                }

                return false;
            }

            return measureIndex == selectedMeasureIndex;
        }

        private int CalculateMeasureWidth(JianpuMeasure measure, double noteWidthScale = 1.0)
        {
            var maxWidth = CalculateVoiceContentWidth(measure.MelodyNotes, noteWidthScale);

            // A measure with extra voices (SATB, descant) shares one width across every voice --
            // notes and dashes only line up vertically between voices when they do. Sized to the
            // widest voice's own natural content, exactly like the single-voice case did before
            // (which is why an ordinary measure with no ExtraVoices computes the same width as
            // today, unchanged).
            if (VoiceLayoutService.HasMultipleVoices(measure))
            {
                foreach (var voice in measure.ExtraVoices)
                {
                    maxWidth = Math.Max(maxWidth, CalculateVoiceContentWidth(voice?.Notes, noteWidthScale));
                }
            }

            return Math.Max(MinMeasureWidth, maxWidth);
        }

        private int CalculateVoiceContentWidth(List<JianpuNote> notes, double noteWidthScale)
        {
            if (notes == null || notes.Count == 0)
            {
                return NoteCellWidth;
            }

            var width = 0;
            foreach (var note in notes)
            {
                width += GetNoteWidth(note, noteWidthScale);
            }

            return width;
        }

        private sealed class ScoreLayout
        {
            public List<StaffLineLayout> Lines { get; set; } = new List<StaffLineLayout>();
            public List<MeasureLayout> Measures { get; set; } = new List<MeasureLayout>();
            public int TotalWidth { get; set; }
        }

        private sealed class StaffLineLayout
        {
            public int BlockTop { get; set; }
            public List<MeasureLayout> Measures { get; set; } = new List<MeasureLayout>();

            /// <summary>Vertical space this system actually needs, including any measure's extra
            /// "below" voice rows (SATB etc.) -- the whole line reserves the tallest requirement
            /// among its measures, since they all share one <see cref="BlockTop"/>. Equals <see
            /// cref="StaffBlockHeight"/> when no measure on the line has extra voices, so an
            /// ordinary score's line spacing is unchanged.</summary>
            public int GetEffectiveHeight()
            {
                return StaffBlockHeight + GetMaxBelowVoiceRows() * (MelodyRowHeight + RowGap);
            }

            public int GetMaxBelowVoiceRows()
            {
                var maxBelowRows = 0;
                foreach (var measure in Measures)
                {
                    maxBelowRows = Math.Max(maxBelowRows, measure.BelowVoiceRowCount);
                }

                return maxBelowRows;
            }

            /// <summary>Extra headroom this line needs above its (shared) <see cref="BlockTop"/>
            /// for the tallest above-voice stack among its measures.</summary>
            public int GetAboveVoicesHeight()
            {
                var maxAboveRows = 0;
                foreach (var measure in Measures)
                {
                    maxAboveRows = Math.Max(maxAboveRows, measure.AboveVoiceRowCount);
                }

                return maxAboveRows * (MelodyRowHeight + RowGap);
            }
        }

        public sealed class MeasureLayout
        {
            private int[] _noteWidths = Array.Empty<int>();
            private int[] _noteOffsets = Array.Empty<int>();
            private int _melodyContentWidth;

            public double MelodyScale { get; private set; } = 1.0;

            /// <summary>Voices rendered below the primary <see cref="JianpuMeasure.MelodyNotes"/>
            /// row (SATB's Alto/Tenor/Bass), in the order <see
            /// cref="VoiceLayoutService.GetRenderOrder"/> returns them. Empty for a measure with no
            /// <see cref="JianpuMeasure.ExtraVoices"/> or only "above" ones.</summary>
            public IReadOnlyList<ExtraVoiceLayout> BelowVoices { get; private set; } = Array.Empty<ExtraVoiceLayout>();

            /// <summary>Voices rendered above the primary row (descant/solo), in <see
            /// cref="JianpuMeasure.ExtraVoices"/> list order -- index 0 is the topmost row (furthest
            /// from the melody), matching <see cref="VoiceLayoutService.GetRenderOrder"/>. Unlike
            /// <see cref="BelowVoices"/>, these draw at Y &lt; <see cref="BlockTop"/>, so every
            /// consumer that means "the whole block" (bar lines, hit-testing, selection) must use
            /// <see cref="GetDrawTop"/>/<see cref="GetDrawHeight"/> instead of <see cref="BlockTop"/>/
            /// <see cref="GetEffectiveHeight"/> directly -- everything keyed to the melody row itself
            /// (ties, hairpins, gap carets, Dynamics/Secondary/Lyrics rows) stays anchored to <see
            /// cref="BlockTop"/> exactly as before, unaffected by above voices.</summary>
            public IReadOnlyList<ExtraVoiceLayout> AboveVoices { get; private set; } = Array.Empty<ExtraVoiceLayout>();

            public int BelowVoiceRowCount => BelowVoices.Count;

            public int AboveVoiceRowCount => AboveVoices.Count;

            public int MeasureIndex { get; set; }
            public int X { get; set; }
            public int Width { get; set; }
            public int BlockTop { get; set; }
            public int BarLineX { get; set; }

            public void ComputeNoteLayout(JianpuMeasure measure, double noteWidthScale = 1.0)
            {
                var notes = measure.MelodyNotes ?? new List<JianpuNote>();
                _noteWidths = new int[notes.Count];
                _noteOffsets = new int[notes.Count];
                var offset = 0;
                for (var i = 0; i < notes.Count; i++)
                {
                    _noteOffsets[i] = offset;
                    _noteWidths[i] = JianpuRenderer.GetNoteWidth(notes[i], noteWidthScale);
                    offset += _noteWidths[i];
                }

                _melodyContentWidth = offset;
                MelodyScale = 1.0;
            }

            /// <param name="stretchToFill">When true, also scales up (not just down) so the
            /// voice's content exactly fills <paramref name="displayWidth"/>. Only set for a
            /// measure with more than one active voice, where every voice must share the same
            /// width for notes/dashes to line up vertically; the plain single-voice case keeps
            /// today's shrink-only behavior (a short measure pads with blank space) unchanged.</param>
            public void ApplyMelodyScale(int displayWidth, bool stretchToFill = false)
            {
                var availableWidth = Math.Max(12, displayWidth - 4);
                if (_melodyContentWidth > 0 && (_melodyContentWidth > availableWidth || stretchToFill))
                {
                    MelodyScale = (double)availableWidth / _melodyContentWidth;
                }
                else
                {
                    MelodyScale = 1.0;
                }
            }

            /// <summary>Computes each "below" voice's own note layout against the same shared
            /// <paramref name="displayWidth"/> the primary voice was scaled to (see <see
            /// cref="ApplyMelodyScale"/>), so every voice's notes/dashes land on the same
            /// horizontal grid. A measure with no extra voices leaves <see cref="BelowVoices"/>
            /// empty, matching its pre-SATB behavior exactly.</summary>
            public void ComputeExtraVoiceLayouts(JianpuMeasure measure, double noteWidthScale, int displayWidth)
            {
                var below = new List<ExtraVoiceLayout>();
                var above = new List<ExtraVoiceLayout>();
                var extraVoices = measure?.ExtraVoices;
                if (extraVoices != null)
                {
                    for (var i = 0; i < extraVoices.Count; i++)
                    {
                        var voice = extraVoices[i];
                        if (voice == null)
                        {
                            continue;
                        }

                        var voiceLayout = new ExtraVoiceLayout(voice.Role, voice.Notes, noteWidthScale, displayWidth, i);
                        if (voice.IsAbove)
                        {
                            above.Add(voiceLayout);
                        }
                        else
                        {
                            below.Add(voiceLayout);
                        }
                    }
                }

                BelowVoices = below;
                AboveVoices = above;
            }

            /// <summary>Finds the layout for <see cref="JianpuMeasure.ExtraVoices"/>[<paramref
            /// name="extraVoiceIndex"/>], searching both <see cref="AboveVoices"/> and <see
            /// cref="BelowVoices"/> since either can hold it. Null if out of range.</summary>
            public ExtraVoiceLayout FindExtraVoiceLayout(int extraVoiceIndex)
            {
                foreach (var voice in AboveVoices)
                {
                    if (voice.ExtraVoiceIndex == extraVoiceIndex)
                    {
                        return voice;
                    }
                }

                foreach (var voice in BelowVoices)
                {
                    if (voice.ExtraVoiceIndex == extraVoiceIndex)
                    {
                        return voice;
                    }
                }

                return null;
            }

            /// <summary>The Y this voice's row draws at. <see
            /// cref="VoiceLayoutService.PrimaryVoiceIndex"/> is <see cref="BlockTop"/> itself;
            /// otherwise the matching row in <see cref="AboveVoices"/> or <see
            /// cref="BelowVoices"/>, in the same position <see cref="DrawExtraVoiceRows"/> in <see
            /// cref="JianpuRenderer"/> draws it at. Falls back to <see cref="BlockTop"/> for an
            /// unknown voice index rather than throwing -- callers only reach this from a hit or
            /// selection that was itself built from a real voice index.</summary>
            public int GetVoiceRowTop(int voiceIndex)
            {
                if (voiceIndex == VoiceLayoutService.PrimaryVoiceIndex)
                {
                    return BlockTop;
                }

                for (var i = 0; i < AboveVoices.Count; i++)
                {
                    if (AboveVoices[i].ExtraVoiceIndex == voiceIndex)
                    {
                        return BlockTop - (AboveVoices.Count - i) * (MelodyRowHeight + RowGap);
                    }
                }

                for (var i = 0; i < BelowVoices.Count; i++)
                {
                    if (BelowVoices[i].ExtraVoiceIndex == voiceIndex)
                    {
                        return BlockTop + (i + 1) * (MelodyRowHeight + RowGap);
                    }
                }

                return BlockTop;
            }

            /// <summary>This measure's vertical footprint from <see cref="BlockTop"/> downward,
            /// including its below-voice rows. Equals <see cref="JianpuRenderer.StaffBlockHeight"/>
            /// when it has none. Does NOT include above-voice rows -- see <see
            /// cref="GetDrawHeight"/> for the whole block's footprint.</summary>
            public int GetEffectiveHeight()
            {
                return StaffBlockHeight + BelowVoiceRowCount * (MelodyRowHeight + RowGap);
            }

            /// <summary>Extra headroom this measure's above-voice rows need, reaching upward from
            /// <see cref="BlockTop"/>. Zero when it has none.</summary>
            public int GetAboveVoicesHeight()
            {
                return AboveVoiceRowCount * (MelodyRowHeight + RowGap);
            }

            /// <summary>The real topmost Y this measure draws at -- <see cref="BlockTop"/> itself
            /// unless it has above-voice rows, which draw higher. Use this (with <see
            /// cref="GetDrawHeight"/>) for anything meaning "the whole block" (bar lines,
            /// hit-testing, selection highlight); use <see cref="BlockTop"/> alone for anything
            /// meaning specifically the melody row (ties, hairpins, gap carets, Dynamics row).</summary>
            public int GetDrawTop()
            {
                return BlockTop - GetAboveVoicesHeight();
            }

            /// <summary>The whole block's vertical extent, from <see cref="GetDrawTop"/> down
            /// through every below-voice row and the Dynamics/Secondary/Lyrics rows.</summary>
            public int GetDrawHeight()
            {
                return GetAboveVoicesHeight() + GetEffectiveHeight();
            }

            public int GetNoteWidth(int noteIndex)
            {
                if (noteIndex < 0 || noteIndex >= _noteWidths.Length)
                {
                    return NoteCellWidth;
                }

                return _noteWidths[noteIndex];
            }

            public int GetNoteOffset(int noteIndex)
            {
                if (noteIndex < 0 || noteIndex >= _noteOffsets.Length)
                {
                    return 0;
                }

                return _noteOffsets[noteIndex];
            }

            public int GetInsertOffset(int insertIndex)
            {
                if (insertIndex <= 0)
                {
                    return 0;
                }

                if (_noteOffsets.Length == 0)
                {
                    return 0;
                }

                if (insertIndex >= _noteOffsets.Length)
                {
                    return MelodyScale < 0.999 ? Width : _melodyContentWidth;
                }

                return MelodyScale < 0.999
                    ? (int)Math.Round(_noteOffsets[insertIndex] * MelodyScale)
                    : _noteOffsets[insertIndex];
            }

            /// <summary>
            /// The single source of truth for where a melody note is actually drawn, including the
            /// squeeze applied by <see cref="MelodyScale"/> when a measure's natural content doesn't
            /// fit its allotted width. Hit-testing must go through this too (not just drawing) so a
            /// click lands on the note the user actually sees, not on its unscaled position.
            /// </summary>
            public void GetNoteDrawBounds(int noteIndex, out int noteX, out int noteWidth)
            {
                var minDrawWidth = GetMinDrawWidth();
                noteWidth = Math.Max(minDrawWidth, (int)Math.Round(GetNoteWidth(noteIndex) * MelodyScale));
                noteX = X + (int)Math.Round(GetNoteOffset(noteIndex) * MelodyScale);
                if (MelodyScale < 0.999 && noteIndex == _noteWidths.Length - 1)
                {
                    noteWidth = Math.Max(minDrawWidth, X + Width - noteX);
                }
            }

            /// <summary>
            /// Same as <see cref="GetNoteDrawBounds"/> but without the last-note-in-measure stretch
            /// (which pads the final note's clickable/fillable width out to the barline so a squeezed
            /// measure doesn't leave an ugly visual gap). A beam's endpoint should follow the note's
            /// actual glyph width, not that padding -- otherwise a beam ending on the measure's last
            /// note runs straight through the barline into the next measure's beam with no visible
            /// gap between them.
            /// </summary>
            public void GetNoteNaturalDrawBounds(int noteIndex, out int noteX, out int noteWidth)
            {
                var minDrawWidth = GetMinDrawWidth();
                noteWidth = Math.Max(minDrawWidth, (int)Math.Round(GetNoteWidth(noteIndex) * MelodyScale));
                noteX = X + (int)Math.Round(GetNoteOffset(noteIndex) * MelodyScale);
            }

            public Rectangle GetNoteBounds(int noteIndex)
            {
                GetNoteDrawBounds(noteIndex, out var noteX, out var noteWidth);
                return new Rectangle(noteX, BlockTop, noteWidth, JianpuRenderer.MelodyRowHeight);
            }

            private int GetMinDrawWidth()
            {
                return MelodyScale < 0.999
                    ? Math.Max(6, (int)Math.Round(JianpuRenderer.MinNoteWidth * MelodyScale))
                    : JianpuRenderer.MinNoteWidth;
            }

            /// <summary>One "below" voice's (Alto/Tenor/Bass) own note layout within a measure
            /// shared with the primary voice. Mirrors <see cref="MeasureLayout"/>'s own
            /// GetNoteDrawBounds/GetMinDrawWidth logic (same clamp-to-min-width and
            /// stretch-last-note-to-barline rules) but scaled against its own content width so its
            /// notes/dashes still land on the shared grid even when its rhythm differs from the
            /// primary voice's.</summary>
            public sealed class ExtraVoiceLayout
            {
                private readonly int[] _noteWidths;
                private readonly int[] _noteOffsets;
                private readonly int _contentWidth;

                internal ExtraVoiceLayout(string role, IReadOnlyList<JianpuNote> notes, double noteWidthScale, int displayWidth, int extraVoiceIndex)
                {
                    Role = role ?? string.Empty;
                    Notes = notes ?? new List<JianpuNote>();
                    ExtraVoiceIndex = extraVoiceIndex;
                    _noteWidths = new int[Notes.Count];
                    _noteOffsets = new int[Notes.Count];
                    var offset = 0;
                    for (var i = 0; i < Notes.Count; i++)
                    {
                        _noteOffsets[i] = offset;
                        _noteWidths[i] = JianpuRenderer.GetNoteWidth(Notes[i], noteWidthScale);
                        offset += _noteWidths[i];
                    }

                    _contentWidth = offset;
                    var availableWidth = Math.Max(12, displayWidth - 4);
                    Scale = _contentWidth > 0 ? (double)availableWidth / _contentWidth : 1.0;
                }

                public string Role { get; }
                public IReadOnlyList<JianpuNote> Notes { get; }
                public double Scale { get; }
                public int NoteCount => Notes.Count;

                /// <summary>Index into <see cref="JianpuMeasure.ExtraVoices"/> -- matches <see
                /// cref="ScoreNoteRef.VoiceIndex"/>/<see cref="ScoreHitResult.VoiceIndex"/> for a
                /// hit/selection on this voice.</summary>
                public int ExtraVoiceIndex { get; }

                /// <summary>Mirrors <see cref="MeasureLayout.GetInsertOffset"/> for this voice's own
                /// notes/scale -- the gap-caret X position for inserting at <paramref
                /// name="insertIndex"/>.</summary>
                public int GetInsertOffset(int insertIndex)
                {
                    if (insertIndex <= 0 || _noteOffsets.Length == 0)
                    {
                        return 0;
                    }

                    if (insertIndex >= _noteOffsets.Length)
                    {
                        return (int)Math.Round(_contentWidth * Scale);
                    }

                    return (int)Math.Round(_noteOffsets[insertIndex] * Scale);
                }

                public void GetNoteDrawBounds(int noteIndex, int measureX, int measureWidth, out int noteX, out int noteWidth)
                {
                    var minDrawWidth = GetMinDrawWidth();
                    var rawWidth = noteIndex >= 0 && noteIndex < _noteWidths.Length ? _noteWidths[noteIndex] : JianpuRenderer.NoteCellWidth;
                    var rawOffset = noteIndex >= 0 && noteIndex < _noteOffsets.Length ? _noteOffsets[noteIndex] : 0;
                    noteWidth = Math.Max(minDrawWidth, (int)Math.Round(rawWidth * Scale));
                    noteX = measureX + (int)Math.Round(rawOffset * Scale);
                    if (Scale < 0.999 && noteIndex == _noteWidths.Length - 1)
                    {
                        noteWidth = Math.Max(minDrawWidth, measureX + measureWidth - noteX);
                    }
                }

                private int GetMinDrawWidth()
                {
                    return Scale < 0.999
                        ? Math.Max(6, (int)Math.Round(JianpuRenderer.MinNoteWidth * Scale))
                        : JianpuRenderer.MinNoteWidth;
                }
            }
        }
    }
}
