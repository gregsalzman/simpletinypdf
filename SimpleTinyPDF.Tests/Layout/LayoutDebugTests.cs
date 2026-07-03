using System.Collections.Generic;
using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutDebugTests
    {
        [Fact]
        public void ShowMargins_DrawsDashedGuides()
        {
            var layout = new PdfDocumentLayout();
            layout.Debug = new DebugOptions { ShowMargins = true };
            layout.AddParagraph("Content with margin guides");
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: dashed magenta lines at all four margin boundaries");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/debug-margins");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/debug-margins");

            // Left margin vertical guide (x = 72), sampled below the text
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(70), TestHelper.PtToPx(74),
                TestHelper.PtToPx(300), TestHelper.PtToPx(400)));
            // Bottom margin horizontal guide (y = 770)
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(200), TestHelper.PtToPx(400),
                TestHelper.PtToPx(768), TestHelper.PtToPx(772)));
        }

        [Fact]
        public void NoDebug_NoGuides()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Content without margin guides");
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: no debug guides anywhere");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/debug-none");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/debug-none");

            Assert.False(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(70), TestHelper.PtToPx(74),
                TestHelper.PtToPx(300), TestHelper.PtToPx(400)));
        }

        [Fact]
        public void ShowElementBounds_DrawsRectangles()
        {
            var layout = new PdfDocumentLayout();
            layout.Debug = new DebugOptions { ShowElementBounds = true };
            layout.AddParagraph("Short");
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: magenta rectangle around the paragraph's bounds");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/debug-bounds");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/debug-bounds");

            // The bounds rect spans the full content width, so its right edge
            // (x = 523) is far beyond the short text.
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(521), TestHelper.PtToPx(525),
                TestHelper.PtToPx(72), TestHelper.PtToPx(88)));
        }

        [Fact]
        public void ShowColumns_DrawsColumnGuides()
        {
            var layout = new PdfDocumentLayout();
            layout.Debug = new DebugOptions { ShowColumns = true };
            layout.AddSection(new SectionOptions { ColumnCount = 2, ColumnGap = 18 });
            layout.AddParagraph("Column one text");
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: dashed vertical guide between the two columns");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/debug-columns");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/debug-columns");

            // Column boundary guide at x = 72 + 216.5 + 18 - 9 = 297.5
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(295), TestHelper.PtToPx(300),
                TestHelper.PtToPx(400), TestHelper.PtToPx(500)));
        }

        [Fact]
        public void OnLayoutWarning_FiresForOversizedImage()
        {
            var warnings = new List<string>();
            var layout = new PdfDocumentLayout();
            layout.Debug = new DebugOptions { OnLayoutWarning = warnings.Add };
            var image = PdfImage.FromBytes(TestHelper.CreateTestJpeg());
            layout.AddImage(image, new ImageOptions { Width = 1000 });
            var doc = layout.Generate();

            Assert.Contains(warnings, w => w.Contains("exceeds"));

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: oversized image scaled down to content width");
            TestHelper.SavePdf(doc.ToArray(), "Layout/debug-warning-image");
        }

        [Fact]
        public void OnLayoutWarning_FiresForEmptyParagraph()
        {
            var warnings = new List<string>();
            var layout = new PdfDocumentLayout();
            layout.Debug = new DebugOptions { OnLayoutWarning = warnings.Add };
            layout.AddParagraph("");
            layout.AddParagraph("Real content");
            layout.Generate();

            Assert.Contains(warnings, w => w.Contains("Empty paragraph"));
        }
    }
}
