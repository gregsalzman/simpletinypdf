using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutIndentTests
    {
        private const string LongText =
            "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu " +
            "nu xi omicron pi rho sigma tau upsilon phi chi psi omega " +
            "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu";

        [Fact]
        public void LeftIndent_ShiftsTextRight()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Indented text here", new ParagraphOptions { LeftIndent = 72 });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: text starts 1in right of the left margin");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/indent-left");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/indent-left");

            // Nothing between the margin (72) and the indent start (144)
            Assert.False(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(138),
                TestHelper.PtToPx(73), TestHelper.PtToPx(84)));
            // Text present after the indent
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(144), TestHelper.PtToPx(280),
                TestHelper.PtToPx(72), TestHelper.PtToPx(88)));
        }

        [Fact]
        public void RightIndent_ShrinksContentFromRight()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Right side", new ParagraphOptions
            {
                Alignment = TextAlignment.Right,
                RightIndent = 72
            });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: right-aligned text ends 1in left of the right margin");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/indent-right");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/indent-right");

            // A4 width 595, right margin at 523; text should end at 523 - 72 = 451
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(380), TestHelper.PtToPx(451),
                TestHelper.PtToPx(72), TestHelper.PtToPx(88)));
            Assert.False(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(456), TestHelper.PtToPx(523),
                TestHelper.PtToPx(73), TestHelper.PtToPx(84)));
        }

        [Fact]
        public void FirstLineIndent_IndentsOnlyFirstLine()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph(LongText, new ParagraphOptions { FirstLineIndent = 36 });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: first line indented 36pt, wrapped lines at margin");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/indent-first-line");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/indent-first-line");

            // First line (y ≈ 72..86) starts at 108: nothing at the margin
            Assert.False(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(103),
                TestHelper.PtToPx(73), TestHelper.PtToPx(83)));
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(108), TestHelper.PtToPx(160),
                TestHelper.PtToPx(72), TestHelper.PtToPx(86)));
            // Second line (y ≈ 86.4..100.8) starts at the margin
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(103),
                TestHelper.PtToPx(88), TestHelper.PtToPx(98)));
        }

        [Fact]
        public void HangingIndent_OutdentsFirstLine()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph(LongText, new ParagraphOptions
            {
                LeftIndent = 36,
                FirstLineIndent = -36
            });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: hanging indent — first line at margin, wrapped lines 36pt in");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/indent-hanging");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/indent-hanging");

            // First line starts at the margin (72)
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(103),
                TestHelper.PtToPx(72), TestHelper.PtToPx(86)));
            // Second line starts at 108: nothing at the margin
            Assert.False(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(103),
                TestHelper.PtToPx(89), TestHelper.PtToPx(97)));
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(108), TestHelper.PtToPx(160),
                TestHelper.PtToPx(88), TestHelper.PtToPx(100)));
        }

        [Fact]
        public void CombinedIndents_RenderOnOnePage()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph(LongText, new ParagraphOptions
            {
                LeftIndent = 36,
                RightIndent = 36,
                FirstLineIndent = 18,
                Alignment = TextAlignment.Justify
            });
            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: justified paragraph with left/right/first-line indents");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/indent-combined");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/indent-combined");

            // Content exists inside the indented region
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(108), TestHelper.PtToPx(400),
                TestHelper.PtToPx(72), TestHelper.PtToPx(120)));
            // Nothing left of the left indent on wrapped lines
            Assert.False(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(103),
                TestHelper.PtToPx(89), TestHelper.PtToPx(97)));
        }
    }
}
