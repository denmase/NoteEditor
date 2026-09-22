using JianpuEditor.Models;
using JianpuEditor.Rendering;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using PdfSharp.Pdf.IO;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class PdfExportServiceTests
    {
        [Fact]
        public void Export_32Measures_ProducesMultiplePages()
        {
            var score = CreateScoreWithMeasures(32, "Multi Page Score");
            var path = Path.Combine(Path.GetTempPath(), "jianpu-pdf-" + Guid.NewGuid() + ".pdf");
            try
            {
                PdfExportService.Export(score, path, ScoreLayoutOptions.PdfRenderWidth);

                Assert.True(File.Exists(path));
                Assert.True(new FileInfo(path).Length > 1024);

                using (var document = PdfReader.Open(path, PdfDocumentOpenMode.Import))
                {
                    Assert.True(document.PageCount >= 2);
                }
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void Export_ShortScore_ProducesSinglePage()
        {
            var score = CreateScoreWithMeasures(4, "Short Score");
            var path = Path.Combine(Path.GetTempPath(), "jianpu-pdf-short-" + Guid.NewGuid() + ".pdf");
            try
            {
                PdfExportService.Export(score, path, ScoreLayoutOptions.PdfRenderWidth);

                using (var document = PdfReader.Open(path, PdfDocumentOpenMode.Import))
                {
                    Assert.Equal(1, document.PageCount);
                }
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void PlanPages_EightLines_SplitsAcrossPages()
        {
            var renderWidth = ScoreLayoutOptions.PdfRenderWidth;
            var pages = PdfPagePlanner.PlanPages(
                8,
                ScoreLayoutOptions.PdfExport.HeaderMarginTop,
                PdfPagePlanner.GetPageHeightPixels(renderWidth));

            Assert.True(pages.Count >= 2);
            Assert.Equal(8, pages.Sum(page => page.LineCount));
            Assert.Equal(1, pages[0].PageNumber);
            Assert.Equal(pages.Count, pages[0].TotalPages);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(7, 2)]
        [InlineData(8, 2)]
        public void PlanPages_LineCount_MapsToExpectedPageCount(int lineCount, int expectedPages)
        {
            var pages = PdfPagePlanner.PlanPages(
                lineCount,
                ScoreLayoutOptions.PdfExport.HeaderMarginTop,
                PdfPagePlanner.GetPageHeightPixels(ScoreLayoutOptions.PdfRenderWidth));

            Assert.Equal(expectedPages, pages.Count);
            Assert.Equal(lineCount, pages.Sum(page => page.LineCount));
        }

        [Fact]
        public void PlanPages_UniformHeightsOverload_MatchesTheIntOverloadExactly()
        {
            var renderWidth = ScoreLayoutOptions.PdfRenderWidth;
            var headerHeight = ScoreLayoutOptions.PdfExport.HeaderMarginTop;
            var pageHeightPixels = PdfPagePlanner.GetPageHeightPixels(renderWidth);

            var intPages = PdfPagePlanner.PlanPages(8, headerHeight, pageHeightPixels);
            var uniformHeights = Enumerable.Repeat(JianpuRenderer.StaffBlockHeight, 8).ToArray();
            var heightPages = PdfPagePlanner.PlanPages(uniformHeights, headerHeight, pageHeightPixels);

            Assert.Equal(intPages.Count, heightPages.Count);
            for (var i = 0; i < intPages.Count; i++)
            {
                Assert.Equal(intPages[i].LineCount, heightPages[i].LineCount);
                Assert.Equal(intPages[i].FirstLineIndex, heightPages[i].FirstLineIndex);
            }
        }

        [Fact]
        public void PlanPages_TallerLines_NeverPacksMorePerPageThanFits()
        {
            var renderWidth = ScoreLayoutOptions.PdfRenderWidth;
            var headerHeight = ScoreLayoutOptions.PdfExport.HeaderMarginTop;
            var pageHeightPixels = PdfPagePlanner.GetPageHeightPixels(renderWidth);

            // A line with 3 extra voice rows (SATB) is taller than the fixed StaffBlockHeight the
            // old (int-count) planning assumed -- so a page's actual summed height must still fit.
            var satbLineHeight = JianpuRenderer.StaffBlockHeight + 3 * (JianpuRenderer.MelodyRowHeight + JianpuRenderer.RowGap);
            var heights = Enumerable.Repeat(satbLineHeight, 8).ToArray();

            var pages = PdfPagePlanner.PlanPages(heights, headerHeight, pageHeightPixels);

            Assert.Equal(8, pages.Sum(page => page.LineCount));
            foreach (var page in pages)
            {
                var isFirst = page.PageNumber == 1;
                var available = pageHeightPixels - (isFirst ? headerHeight : PdfPagePlanner.CompactHeaderHeight) - PdfPagePlanner.BottomMargin;
                var used = page.LineCount * satbLineHeight + Math.Max(0, page.LineCount - 1) * JianpuRenderer.StaffBlockSpacing;
                Assert.True(used <= available, $"page {page.PageNumber} used {used}px but only had {available}px");
            }
        }

        [Fact]
        public void Export_SatbScore_EveryPageBitmapFitsThePhysicalPageHeight()
        {
            var measures = new List<JianpuMeasure>();
            for (var i = 0; i < 20; i++)
            {
                var measure = ScoreTestHelper.Measure(
                    ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4));
                measure.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(5), ScoreTestHelper.Note(5), ScoreTestHelper.Note(5) } });
                measure.ExtraVoices.Add(new JianpuVoice { Role = "Tenor", Notes = { ScoreTestHelper.Note(3), ScoreTestHelper.Note(3), ScoreTestHelper.Note(3), ScoreTestHelper.Note(3) } });
                measure.ExtraVoices.Add(new JianpuVoice { Role = "Bass", Notes = { ScoreTestHelper.Note(1), ScoreTestHelper.Note(1), ScoreTestHelper.Note(1), ScoreTestHelper.Note(1) } });
                measures.Add(measure);
            }

            var score = new JianpuScore { Title = "SATB PDF", KeySignature = "1=C", Measures = measures };
            var renderer = new JianpuRenderer();
            var options = ScoreLayoutOptions.PdfExport;
            var renderWidth = ScoreLayoutOptions.PdfRenderWidth;
            var heights = renderer.GetStaffLineHeights(score, renderWidth, options);
            var headerHeight = options.HeaderMarginTop > 0 ? options.HeaderMarginTop : JianpuRenderer.MarginTop;
            var pageHeightPixels = PdfPagePlanner.GetPageHeightPixels(renderWidth);
            var slices = PdfPagePlanner.PlanPages(heights, headerHeight, pageHeightPixels);

            foreach (var slice in slices)
            {
                using (var bitmap = renderer.RenderPdfPageToBitmap(score, renderWidth, options, slice))
                {
                    Assert.True(bitmap.Height <= pageHeightPixels, $"page {slice.PageNumber} bitmap height {bitmap.Height} exceeds page height {pageHeightPixels}");
                }
            }
        }

        private static JianpuScore CreateScoreWithMeasures(int measureCount, string title)
        {
            var measures = new List<JianpuMeasure>();
            for (var i = 0; i < measureCount; i++)
            {
                measures.Add(ScoreTestHelper.Measure(
                    ScoreTestHelper.Note(1 + (i % 7)),
                    ScoreTestHelper.Note(2 + (i % 6)),
                    ScoreTestHelper.Note(3 + (i % 5)),
                    ScoreTestHelper.Note(4 + (i % 4))));
            }

            return new JianpuScore
            {
                Title = title,
                KeySignature = "1=C",
                Tempo = "Moderato",
                Bpm = 120,
                Composer = "Test",
                Measures = measures
            };
        }
    }
}
