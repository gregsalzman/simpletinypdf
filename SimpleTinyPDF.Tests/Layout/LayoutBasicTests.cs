using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutBasicTests
    {
        [Fact]
        public void EmptyLayout_ProducesValidDocument()
        {
            var layout = new PdfDocumentLayout();
            var doc = layout.Generate();

            Assert.NotNull(doc);
            Assert.Equal(0, doc.PageCount);
        }

        [Fact]
        public void SingleParagraph_RendersText()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Hello, World!");
            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: single paragraph 'Hello, World!' at top-left");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/basic-single-paragraph");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/basic-single-paragraph");

            // Text should appear in the content area (after 72pt margin)
            int contentY = TestHelper.PtToPx(72 + 6); // margin + approx text baseline
            int contentX = TestHelper.PtToPx(72 + 10);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(200),
                TestHelper.PtToPx(72), TestHelper.PtToPx(90)));
        }

        [Fact]
        public void MultipleParagraphs_RenderInOrder()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("First paragraph");
            layout.AddParagraph("Second paragraph");
            layout.AddParagraph("Third paragraph");
            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: three paragraphs stacked vertically");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/basic-multiple-paragraphs");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/basic-multiple-paragraphs");

            // Check that text appears in the top content area
            float lineHeight = 12f * 1.2f; // default fontSize * lineSpacing
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(250),
                TestHelper.PtToPx(72), TestHelper.PtToPx(72 + lineHeight * 3 + 5)));
        }

        [Fact]
        public void DefaultParagraphOptions_AreApplied()
        {
            var layout = new PdfDocumentLayout();
            layout.DefaultParagraphOptions = new ParagraphOptions
            {
                FontSize = 20,
                Font = PdfFont.TimesBold,
                SpaceAfter = 10
            };
            layout.AddParagraph("Using defaults");
            layout.AddParagraph("Also defaults");
            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: large TimesBold text with spacing");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/basic-default-options");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/basic-default-options");

            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(300),
                TestHelper.PtToPx(72), TestHelper.PtToPx(130)));
        }

        [Fact]
        public void ExplicitOptions_OverrideDefaults()
        {
            var layout = new PdfDocumentLayout();
            layout.DefaultParagraphOptions = new ParagraphOptions
            {
                FontSize = 10,
                Color = PdfColor.Blue
            };
            layout.AddParagraph("Big red text", new ParagraphOptions
            {
                FontSize = 24,
                Color = PdfColor.Red
            });
            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: large red text overriding blue default");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/basic-explicit-options");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/basic-explicit-options");

            // Large text at 24pt should produce more dark pixels than small text
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(300),
                TestHelper.PtToPx(72), TestHelper.PtToPx(100)));
        }

        [Fact]
        public void WrapExistingDocument_AppendsPages()
        {
            var doc = new PdfDocument();
            var existingPage = doc.AddPage();
            existingPage.DrawText("Existing content", 50, 50);

            var layout = new PdfDocumentLayout(doc);
            layout.AddParagraph("Layout content");
            layout.Generate();

            Assert.Equal(2, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: existing page + layout page");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/basic-wrap-existing");
        }

        [Fact]
        public void RichTextParagraph_RendersMixedStyles()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph(new[]
            {
                new TextSpan("Bold text ", PdfFont.HelveticaBold, 14),
                new TextSpan("and normal ", PdfFont.Helvetica, 12),
                new TextSpan("and red", PdfFont.Helvetica, 12, PdfColor.Red)
            });
            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: mixed bold/normal/red text on one line");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/basic-rich-text");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/basic-rich-text");

            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(400),
                TestHelper.PtToPx(72), TestHelper.PtToPx(95)));
        }

        [Fact]
        public void CenterAlignment_CentersText()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Centered", new ParagraphOptions
            {
                Alignment = TextAlignment.Center,
                FontSize = 16
            });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: text is horizontally centered");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/basic-center-alignment");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/basic-center-alignment");

            // Centered text on A4 (595pt wide, 72pt margins = 451pt content)
            // "Centered" at 16pt ≈ 65pt wide, so starts at ~72 + (451-65)/2 ≈ 265pt
            int midX = TestHelper.PtToPx(595f / 2f);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                midX - 50, midX + 50,
                TestHelper.PtToPx(72), TestHelper.PtToPx(95)));
        }

        [Fact]
        public void RightAlignment_RightAlignsText()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Right", new ParagraphOptions
            {
                Alignment = TextAlignment.Right,
                FontSize = 16
            });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: text is right-aligned");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/basic-right-alignment");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/basic-right-alignment");

            // Right-aligned should have dark pixels near the right margin
            int rightEdge = TestHelper.PtToPx(595 - 72);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                rightEdge - 80, rightEdge,
                TestHelper.PtToPx(72), TestHelper.PtToPx(95)));
        }
    }
}
