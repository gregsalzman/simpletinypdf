using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutHorizontalRuleTests
    {
        [Fact]
        public void DefaultRule_DrawsLineAcrossContentWidth()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Above the rule");
            layout.AddHorizontalRule();
            layout.AddParagraph("Below the rule");
            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: thin rule between the two paragraphs, full content width");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/rule-default");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/rule-default");

            // Rule at y = 72 + 14.4 + 6 ≈ 92.4; text above ends well before x=300
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(300), TestHelper.PtToPx(450),
                TestHelper.PtToPx(90), TestHelper.PtToPx(96)));
            // Paragraph below the rule (starts at y ≈ 98.9)
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(200),
                TestHelper.PtToPx(99), TestHelper.PtToPx(113)));
        }

        [Fact]
        public void CustomRule_ThicknessAndColor()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Heading");
            layout.AddHorizontalRule(new HorizontalRuleOptions
            {
                Thickness = 4f,
                Color = PdfColor.Red,
                SpaceBefore = 12,
                SpaceAfter = 12
            });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: thick red rule 12pt below the heading");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/rule-custom");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/rule-custom");

            // Rule occupies y = 98.4..102.4; sample its center
            TestHelper.AssertPixelColor(bitmap,
                TestHelper.PtToPx(300), TestHelper.PtToPx(100.4f),
                255, 0, 0, tolerance: 60);
        }

        [Fact]
        public void Rule_WithIndents()
        {
            var layout = new PdfDocumentLayout();
            layout.AddHorizontalRule(new HorizontalRuleOptions
            {
                LeftIndent = 100,
                RightIndent = 100,
                Thickness = 2f
            });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: rule inset 100pt from both margins");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/rule-indents");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/rule-indents");

            // Rule spans x = 172..423 at y ≈ 78..80
            Assert.False(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(75), TestHelper.PtToPx(165),
                TestHelper.PtToPx(75), TestHelper.PtToPx(84)));
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(200), TestHelper.PtToPx(400),
                TestHelper.PtToPx(76), TestHelper.PtToPx(83)));
            Assert.False(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(430), TestHelper.PtToPx(520),
                TestHelper.PtToPx(75), TestHelper.PtToPx(84)));
        }
    }
}
