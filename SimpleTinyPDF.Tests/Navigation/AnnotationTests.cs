using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class AnnotationTests
    {
        // ── Text annotations (sticky notes) ────────────────────

        [Fact]
        public void AddTextAnnotation_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddTextAnnotation(100, 100, "This is a note");
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Annots", pdf);
            Assert.Contains("/Subtype /Text", pdf);
            Assert.Contains("/Contents", pdf);
            Assert.Contains("/Name /Comment", pdf);
            Assert.Contains("/F 4", pdf);
        }

        [Fact]
        public void AddTextAnnotation_WithTitle_HasTitle()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddTextAnnotation(100, 100, "Note text", title: "Author");
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/T", pdf);
        }

        [Fact]
        public void AddTextAnnotation_WithIcon_HasIconName()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddTextAnnotation(100, 100, "Note", icon: TextAnnotationIcon.Note);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Name /Note", pdf);
        }

        [Fact]
        public void AddTextAnnotation_WithColor_HasColorArray()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddTextAnnotation(100, 100, "Note", color: PdfColor.Red);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/C [1 0 0]", pdf);
        }

        [Fact]
        public void AddTextAnnotation_Open_HasOpenTrue()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddTextAnnotation(100, 100, "Note", open: true);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Open true", pdf);
        }

        [Fact]
        public void AddTextAnnotation_Closed_HasOpenFalse()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddTextAnnotation(100, 100, "Note");
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Open false", pdf);
        }

        [Fact]
        public void AddTextAnnotation_BottomUp_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.CoordinateOrigin = CoordinateOrigin.BottomUp;
            page.AddTextAnnotation(100, 700, "Note");
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Subtype /Text", pdf);
        }

        [Fact]
        public void AddTextAnnotation_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: text annotation (sticky note) appears on page");
            page.AddTextAnnotation(50, 50, "Sticky note content", title: "Reviewer");
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Navigation/annotation-text-note");
            var bitmap = TestHelper.RasterizePage(bytes, "Navigation/annotation-text-note");
            Assert.True(bitmap.Width > 0);
            bitmap.Dispose();
        }

        // ── Markup annotations ─────────────────────────────────

        [Fact]
        public void AddMarkupAnnotation_Highlight_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddMarkupAnnotation(50, 50, 200, 14, MarkupAnnotationType.Highlight);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Annots", pdf);
            Assert.Contains("/Subtype /Highlight", pdf);
        }

        [Fact]
        public void AddMarkupAnnotation_Underline_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddMarkupAnnotation(50, 50, 200, 14, MarkupAnnotationType.Underline);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Subtype /Underline", pdf);
        }

        [Fact]
        public void AddMarkupAnnotation_StrikeOut_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddMarkupAnnotation(50, 50, 200, 14, MarkupAnnotationType.StrikeOut);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Subtype /StrikeOut", pdf);
        }

        [Fact]
        public void AddMarkupAnnotation_HasQuadPoints()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddMarkupAnnotation(50, 50, 200, 14);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/QuadPoints", pdf);
        }

        [Fact]
        public void AddMarkupAnnotation_WithColor_HasColorArray()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddMarkupAnnotation(50, 50, 200, 14, color: PdfColor.Blue);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/C [0 0 1]", pdf);
        }

        [Fact]
        public void AddMarkupAnnotation_WithContents_HasContents()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddMarkupAnnotation(50, 50, 200, 14, contents: "Review comment");
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Contents", pdf);
        }

        [Fact]
        public void AddMarkupAnnotation_BottomUp_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.CoordinateOrigin = CoordinateOrigin.BottomUp;
            page.AddMarkupAnnotation(50, 700, 200, 14);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Subtype /Highlight", pdf);
        }

        [Fact]
        public void AddMarkupAnnotation_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: highlight markup annotation renders");
            page.DrawText("Highlighted text here", 50, 50);
            page.AddMarkupAnnotation(50, 50, 150, 14, MarkupAnnotationType.Highlight,
                color: PdfColor.Rgb(1f, 1f, 0f));
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Navigation/annotation-highlight-markup");
            var bitmap = TestHelper.RasterizePage(bytes, "Navigation/annotation-highlight-markup");
            Assert.True(bitmap.Width > 0);
            bitmap.Dispose();
        }

        // ── Stamp annotations ──────────────────────────────────

        [Fact]
        public void AddStampAnnotation_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddStampAnnotation(100, 100, 200, 60);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Annots", pdf);
            Assert.Contains("/Subtype /Stamp", pdf);
            Assert.Contains("/Name /Draft", pdf);
        }

        [Theory]
        [InlineData(StampType.Approved, "Approved")]
        [InlineData(StampType.Confidential, "Confidential")]
        [InlineData(StampType.Draft, "Draft")]
        [InlineData(StampType.Expired, "Expired")]
        [InlineData(StampType.Final, "Final")]
        [InlineData(StampType.NotApproved, "NotApproved")]
        [InlineData(StampType.TopSecret, "TopSecret")]
        [InlineData(StampType.ForPublicRelease, "ForPublicRelease")]
        public void AddStampAnnotation_EachType_HasCorrectName(StampType stamp, string expectedName)
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddStampAnnotation(100, 100, 200, 60, stamp: stamp);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains($"/Name /{expectedName}", pdf);
        }

        [Fact]
        public void AddStampAnnotation_WithContents_HasContents()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddStampAnnotation(100, 100, 200, 60, contents: "Tooltip text");
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Contents", pdf);
        }

        [Fact]
        public void AddStampAnnotation_WithColor_HasColorArray()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.AddStampAnnotation(100, 100, 200, 60, color: PdfColor.Red);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/C [1 0 0]", pdf);
        }

        [Fact]
        public void AddStampAnnotation_BottomUp_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.CoordinateOrigin = CoordinateOrigin.BottomUp;
            page.AddStampAnnotation(100, 700, 200, 60, stamp: StampType.Approved);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Subtype /Stamp", pdf);
        }

        [Fact]
        public void AddStampAnnotation_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: stamp annotation appears on page");
            page.AddStampAnnotation(50, 50, 200, 60, stamp: StampType.Approved);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Navigation/annotation-stamp");
            var bitmap = TestHelper.RasterizePage(bytes, "Navigation/annotation-stamp");
            Assert.True(bitmap.Width > 0);
            bitmap.Dispose();
        }

        // ── Internal links (GoTo) ─────────────────────────────

        [Fact]
        public void AddLinkToPage_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage(PageSize.A4);
            var page2 = doc.AddPage(PageSize.A4);
            page1.AddLinkToPage(50, 50, 100, 20, page2);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Annots", pdf);
            Assert.Contains("/Subtype /Link", pdf);
            Assert.Contains("/Dest", pdf);
            Assert.DoesNotContain("/URI", pdf);
        }

        [Fact]
        public void AddLinkToPage_WithoutTargetY_HasFitDestination()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage(PageSize.A4);
            var page2 = doc.AddPage(PageSize.A4);
            page1.AddLinkToPage(50, 50, 100, 20, page2);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Fit", pdf);
        }

        [Fact]
        public void AddLinkToPage_WithTargetY_HasXYZDestination()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage(PageSize.A4);
            var page2 = doc.AddPage(PageSize.A4);
            page1.AddLinkToPage(50, 50, 100, 20, page2, targetY: 200);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/XYZ", pdf);
        }

        [Fact]
        public void AddLinkToPage_NullTargetPage_Throws()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            Assert.Throws<System.ArgumentNullException>(() =>
                page.AddLinkToPage(50, 50, 100, 20, null));
        }

        [Fact]
        public void AddLinkToPage_BottomUp_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage(PageSize.A4);
            var page2 = doc.AddPage(PageSize.A4);
            page1.CoordinateOrigin = CoordinateOrigin.BottomUp;
            page1.AddLinkToPage(50, 700, 100, 20, page2);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Dest", pdf);
        }

        [Fact]
        public void AddLinkToPage_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage(PageSize.A4);
            var page2 = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page1, "Verify: internal page link annotation works");
            page1.DrawText("Go to page 2", 50, 50);
            page1.AddLinkToPage(50, 50, 120, 14, page2);
            page2.DrawText("Page 2 content", 50, 50);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Navigation/annotation-internal-link");
            var bitmap = TestHelper.RasterizePage(bytes, "Navigation/annotation-internal-link");
            Assert.True(bitmap.Width > 0);
            bitmap.Dispose();
        }

        // ── Regression: existing link annotations still work ───

        [Fact]
        public void ExistingLinkAnnotation_StillWorks()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Click here", 50, 50, link: "https://example.com");
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Annots", pdf);
            Assert.Contains("/Subtype /Link", pdf);
            Assert.Contains("/URI", pdf);
            Assert.Contains("https://example.com", pdf);
            Assert.Contains("/Border [0 0 0]", pdf);
        }

        // ── Mixed annotations on one page ──────────────────────

        [Fact]
        public void MixedAnnotationsOnPage_AllInAnnotsArray()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage(PageSize.A4);
            var page2 = doc.AddPage(PageSize.A4);

            page1.DrawText("Link text", 50, 50, link: "https://example.com");
            page1.AddTextAnnotation(50, 80, "A note");
            page1.AddMarkupAnnotation(50, 110, 200, 14);
            page1.AddStampAnnotation(50, 140, 200, 60, stamp: StampType.Approved);
            page1.AddLinkToPage(50, 220, 100, 20, page2);

            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/URI", pdf);
            Assert.Contains("/Subtype /Text", pdf);
            Assert.Contains("/Subtype /Highlight", pdf);
            Assert.Contains("/Subtype /Stamp", pdf);
            Assert.Contains("/Dest", pdf);
        }

        [Fact]
        public void AllAnnotationTypes_OnSinglePage_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage(PageSize.A4);
            var page2 = doc.AddPage(PageSize.A4);

            TestHelper.AddDescription(page1, "Verify: all annotation types on single page");
            page1.DrawText("Some text to annotate", 50, 50);
            page1.DrawText("Click for link", 50, 80, link: "https://example.com");
            page1.AddTextAnnotation(300, 50, "Review note", title: "Reviewer",
                icon: TextAnnotationIcon.Note, color: PdfColor.Red, open: true);
            page1.AddMarkupAnnotation(50, 46, 180, 16, MarkupAnnotationType.Highlight,
                color: PdfColor.Rgb(1f, 1f, 0f));
            page1.AddMarkupAnnotation(50, 110, 180, 14, MarkupAnnotationType.Underline,
                color: PdfColor.Green);
            page1.AddMarkupAnnotation(50, 140, 180, 14, MarkupAnnotationType.StrikeOut,
                color: PdfColor.Red);
            page1.AddStampAnnotation(300, 200, 200, 60, stamp: StampType.Approved);
            page1.AddLinkToPage(50, 170, 100, 20, page2, targetY: 0);

            page2.DrawText("Target page", 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Navigation/annotations-all-types");
            var bitmap = TestHelper.RasterizePage(bytes, "Navigation/annotations-all-types");
            Assert.True(bitmap.Width > 0);
            bitmap.Dispose();
        }

        // ── CMYK color conversion ──────────────────────────────

        [Fact]
        public void AddTextAnnotation_CmykColor_ConvertsToRgb()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            // Pure cyan in CMYK = (1, 0, 0, 0) → RGB (0, 1, 1)
            page.AddTextAnnotation(100, 100, "Note", color: PdfColor.Cyan);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/C [0 1 1]", pdf);
        }

        // ── No annotations when none added ─────────────────────

        [Fact]
        public void NoAnnotations_NoAnnotsArray()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Plain text", 50, 50);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.DoesNotContain("/Annots", pdf);
        }
    }
}
