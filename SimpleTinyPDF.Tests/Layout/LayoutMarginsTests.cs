using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutMarginsTests
    {
        [Fact]
        public void DefaultMargins_ContentStartsAtOneInch()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Content at default margins");
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: content starts 72pt (1in) from edges");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/margins-default");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/margins-default");

            // Content should appear at ~72pt from top and left
            int marginPx = TestHelper.PtToPx(72);
            // Should NOT have text in the margin area (left of 72pt) -
            // but description text is at 10pt so skip that
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                marginPx, marginPx + 200,
                marginPx, marginPx + 20));
        }

        [Fact]
        public void CustomMargins_PositionContent()
        {
            var layout = new PdfDocumentLayout();
            layout.Margins = new PdfMargins(144, 36, 144, 36); // 2in top/bottom, 0.5in left/right
            layout.AddParagraph("Narrow side margins, tall top/bottom margins");
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: large top margin (2in), narrow side margins (0.5in)");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/margins-custom");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/margins-custom");

            // Content at y=144pt, x=36pt
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(36), TestHelper.PtToPx(200),
                TestHelper.PtToPx(144), TestHelper.PtToPx(165)));

            // No content above 144pt (except description label)
            // Content should not be at y=72 (the default margin area)
            Assert.False(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(36), TestHelper.PtToPx(200),
                TestHelper.PtToPx(80), TestHelper.PtToPx(130)));
        }

        [Fact]
        public void ZeroMargins_ContentAtEdges()
        {
            var layout = new PdfDocumentLayout();
            layout.Margins = new PdfMargins(0);
            layout.AddParagraph("Edge to edge content");
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: content starts at very top-left corner");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/margins-zero");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/margins-zero");

            // Content should start near (0,0)
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                0, TestHelper.PtToPx(100),
                0, TestHelper.PtToPx(18)));
        }

        [Fact]
        public void AsymmetricMargins_WorkCorrectly()
        {
            var layout = new PdfDocumentLayout();
            layout.Margins = new PdfMargins(50, 100, 50, 200); // big left margin
            layout.AddParagraph("Large left margin shifts content right");
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: content indented 200pt from left edge");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/margins-asymmetric");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/margins-asymmetric");

            // Content should start at x=200pt
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(200), TestHelper.PtToPx(400),
                TestHelper.PtToPx(50), TestHelper.PtToPx(70)));
        }

        [Fact]
        public void MarginConstructors_WorkCorrectly()
        {
            // Single value
            var m1 = new PdfMargins(50);
            Assert.Equal(50, m1.Top);
            Assert.Equal(50, m1.Right);
            Assert.Equal(50, m1.Bottom);
            Assert.Equal(50, m1.Left);

            // Two values
            var m2 = new PdfMargins(30, 60);
            Assert.Equal(30, m2.Top);
            Assert.Equal(60, m2.Right);
            Assert.Equal(30, m2.Bottom);
            Assert.Equal(60, m2.Left);

            // Four values
            var m3 = new PdfMargins(10, 20, 30, 40);
            Assert.Equal(10, m3.Top);
            Assert.Equal(20, m3.Right);
            Assert.Equal(30, m3.Bottom);
            Assert.Equal(40, m3.Left);
        }
    }
}
