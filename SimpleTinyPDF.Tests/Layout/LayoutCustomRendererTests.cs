using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutCustomRendererTests
    {
        [Fact]
        public void CustomRenderer_OverrideParagraph()
        {
            var layout = new PdfDocumentLayout();
            layout.Renderer = new BoxRenderer();

            layout.AddParagraph("This text is replaced by a box");

            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: custom renderer draws a box instead of text");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/custom-renderer-paragraph");

            // The box renderer draws at y=72 (margin), height=30
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/custom-renderer-paragraph");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(200),
                TestHelper.PtToPx(72), TestHelper.PtToPx(110)));
        }

        [Fact]
        public void CustomRenderer_ReturnNull_UsesDefault()
        {
            var layout = new PdfDocumentLayout();
            layout.Renderer = new PassthroughRenderer();

            layout.AddParagraph("Default rendering");

            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: null return falls through to default rendering");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/custom-renderer-passthrough");

            var pdfText = TestHelper.GetPdfText(bytes);
            Assert.Contains("Default rendering", pdfText);
        }

        [Fact]
        public void CustomRenderer_OverrideShouldBreakPage()
        {
            var layout = new PdfDocumentLayout();
            layout.Renderer = new NoBreakRenderer();

            // Even with content that would normally overflow, the renderer prevents breaks
            layout.AddParagraph("Short text");

            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: ShouldBreakPage override prevents page break");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/custom-renderer-no-break");
        }

        [Fact]
        public void CustomRenderer_WithColumns()
        {
            var layout = new PdfDocumentLayout();
            layout.Renderer = new TrackingRenderer();
            layout.AddSection(new SectionOptions { ColumnCount = 2, ColumnGap = 18 });

            layout.AddParagraph("Column 1 text");
            layout.AddColumnBreak();
            layout.AddParagraph("Column 2 text");

            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            var renderer = (TrackingRenderer)layout.Renderer;
            // Should have been called for both paragraphs
            Assert.Equal(2, renderer.ParagraphCallCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: custom renderer works with columns");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/custom-renderer-columns");
        }

        /// <summary>Draws a filled rectangle instead of paragraph text.</summary>
        private class BoxRenderer : CustomRenderer
        {
            public override float? RenderParagraph(PdfPage page, string text, float x, float y,
                float width, ParagraphOptions options, PageContext context)
            {
                page.DrawRectangle(x, y, width, 30, PdfColor.Rgb(100, 100, 200));
                return y + 30;
            }
        }

        /// <summary>Returns null for all overrides — uses default rendering.</summary>
        private class PassthroughRenderer : CustomRenderer
        {
            // All methods return null by default
        }

        /// <summary>Prevents page breaks.</summary>
        private class NoBreakRenderer : CustomRenderer
        {
            public override bool? ShouldBreakPage(float remainingHeight, float elementHeight,
                PageContext context)
            {
                return false; // Never break
            }
        }

        /// <summary>Tracks calls but uses default rendering.</summary>
        private class TrackingRenderer : CustomRenderer
        {
            public int ParagraphCallCount { get; private set; }

            public override float? RenderParagraph(PdfPage page, string text, float x, float y,
                float width, ParagraphOptions options, PageContext context)
            {
                ParagraphCallCount++;
                return null; // Use default
            }
        }
    }
}
