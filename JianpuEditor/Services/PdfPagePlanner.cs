using System;
using System.Collections.Generic;
using JianpuEditor.Rendering;

namespace JianpuEditor.Services
{
    public static class PdfPagePlanner
    {
        public const int CompactHeaderHeight = 44;

        public const int BottomMargin = 48;

        public static int GetPageHeightPixels(int renderWidth)
        {
            return (int)Math.Round(renderWidth * 297.0 / 210.0);
        }

        /// <summary>Plans pages assuming every line is the fixed <see cref="JianpuRenderer.
        /// StaffBlockHeight"/> tall. Kept for callers with only a line count on hand; prefer the
        /// <see cref="PlanPages(IReadOnlyList{int}, int, int)"/> overload (real per-line heights,
        /// from <see cref="JianpuRenderer.GetStaffLineHeights"/>) whenever a score might have lines
        /// taller than that -- a measure with extra voices (SATB etc.) does.</summary>
        public static List<PdfPageSlice> PlanPages(int totalLines, int firstPageHeaderHeight, int pageHeightPixels)
        {
            if (totalLines <= 0)
            {
                return PlanPages((IReadOnlyList<int>)null, firstPageHeaderHeight, pageHeightPixels);
            }

            var uniformHeights = new int[totalLines];
            for (var i = 0; i < totalLines; i++)
            {
                uniformHeights[i] = JianpuRenderer.StaffBlockHeight;
            }

            return PlanPages(uniformHeights, firstPageHeaderHeight, pageHeightPixels);
        }

        /// <summary>Plans pages from each line's real effective height, so a page containing a
        /// taller line (a measure with extra voices) is never assigned more lines than actually fit
        /// -- <see cref="JianpuRenderer.RenderPdfPageToBitmap"/> already sizes its own bitmap from
        /// the real per-line height; this is what decides the line groupings it's given.</summary>
        public static List<PdfPageSlice> PlanPages(IReadOnlyList<int> lineHeights, int firstPageHeaderHeight, int pageHeightPixels)
        {
            var pages = new List<PdfPageSlice>();
            if (lineHeights == null || lineHeights.Count == 0)
            {
                pages.Add(new PdfPageSlice
                {
                    FirstLineIndex = 0,
                    LineCount = 0,
                    PageNumber = 1,
                    TotalPages = 1
                });
                return pages;
            }

            var lineIndex = 0;
            while (lineIndex < lineHeights.Count)
            {
                var isFirstPage = pages.Count == 0;
                var headerHeight = isFirstPage ? firstPageHeaderHeight : CompactHeaderHeight;
                var availableHeight = pageHeightPixels - headerHeight - BottomMargin;
                var lineCount = CountLinesThatFit(lineHeights, lineIndex, availableHeight);
                pages.Add(new PdfPageSlice
                {
                    FirstLineIndex = lineIndex,
                    LineCount = lineCount,
                    PageNumber = pages.Count + 1
                });
                lineIndex += lineCount;
            }

            var totalPages = pages.Count;
            foreach (var page in pages)
            {
                page.TotalPages = totalPages;
            }

            return pages;
        }

        internal static int CountLinesThatFit(IReadOnlyList<int> lineHeights, int startIndex, int availableHeight)
        {
            var maxLines = lineHeights.Count - startIndex;
            if (maxLines <= 0)
            {
                return 0;
            }

            for (var lineCount = maxLines; lineCount >= 1; lineCount--)
            {
                var height = GetLinesHeight(lineHeights, startIndex, lineCount);
                if (height <= availableHeight)
                {
                    return lineCount;
                }
            }

            return 1;
        }

        internal static int GetLinesHeight(IReadOnlyList<int> lineHeights, int startIndex, int lineCount)
        {
            if (lineCount <= 0)
            {
                return 0;
            }

            var total = 0;
            for (var i = 0; i < lineCount; i++)
            {
                total += lineHeights[startIndex + i];
            }

            return total + Math.Max(0, lineCount - 1) * JianpuRenderer.StaffBlockSpacing;
        }
    }
}
