using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class HyperlinkTests
    {
        // ── TextSpan.Link ────────────────────────────────────────

        [Fact]
        public void TextSpan_Link_DefaultIsNull()
        {
            var span = new TextSpan("Hello");
            Assert.Null(span.Link);
        }

        [Fact]
        public void TextSpan_Link_CanBeSet()
        {
            var span = new TextSpan("Click", link: "https://example.com");
            Assert.Equal("https://example.com", span.Link);
        }

        [Fact]
        public void TextSpan_Link_DoesNotAffectOtherDefaults()
        {
            var span = new TextSpan("Click", link: "https://example.com");
            Assert.Equal(PdfFont.Helvetica, span.Font);
            Assert.Equal(12f, span.FontSize);
            Assert.Equal(PdfColor.Black, span.Color);
            Assert.False(span.Underline);
            Assert.Equal(1f, span.Opacity);
        }

        // ── DrawText with link ───────────────────────────────────

        [Fact]
        public void DrawText_WithLink_ProducesAnnotation()
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

        [Fact]
        public void DrawText_WithoutLink_NoAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("No link", 50, 50);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.DoesNotContain("/Annots", pdf);
        }

        [Fact]
        public void DrawText_WithLink_HasRect()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Click", 50, 50, link: "https://example.com");
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Rect", pdf);
        }

        [Fact]
        public void DrawText_WithLink_SpecialCharsInUrl_AreEscaped()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Click", 50, 50, link: "https://example.com/path?a=1&b=(2)");
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("https://example.com/path?a=1&b=\\(2\\)", pdf);
        }

        [Fact]
        public void DrawText_WithLink_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: clickable hyperlink on drawn text");
            page.DrawText("Visit site", 50, 50, PdfFont.Helvetica, 12,
                PdfColor.Blue, underline: true, link: "https://example.com");
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Navigation/hyperlink-on-drawtext");
            var bitmap = TestHelper.RasterizePage(bytes, "Navigation/hyperlink-on-drawtext");
            Assert.True(bitmap.Width > 0);
            bitmap.Dispose();
        }

        // ── DrawText (rich) with link ─────────────────────────────

        [Fact]
        public void DrawRichText_SpanWithLink_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("See "),
                new TextSpan("docs", PdfFont.Helvetica, 12, PdfColor.Blue,
                    underline: true, link: "https://docs.example.com"),
                new TextSpan(" for info.")
            }, 50, 50);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Annots", pdf);
            Assert.Contains("https://docs.example.com", pdf);
        }

        [Fact]
        public void DrawRichText_MultipleLinkedSpans_ProducesMultipleAnnotations()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Link1", link: "https://one.com"),
                new TextSpan(" "),
                new TextSpan("Link2", link: "https://two.com")
            }, 50, 50);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("https://one.com", pdf);
            Assert.Contains("https://two.com", pdf);
        }

        [Fact]
        public void DrawRichText_NoLinks_NoAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Hello "),
                new TextSpan("world")
            }, 50, 50);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.DoesNotContain("/Annots", pdf);
        }

        // ── DrawText (text box) with link ────────────────────────

        [Fact]
        public void DrawTextBox_WithLink_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Click here for details", 50, 50,
                link: "https://example.com", width: 200);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Annots", pdf);
            Assert.Contains("https://example.com", pdf);
        }

        [Fact]
        public void DrawTextBox_WithLink_MultipleLines_ProducesMultipleAnnotations()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            // Use a narrow width to force wrapping
            page.DrawText(
                "This is a long text that should wrap across multiple lines in the text box",
                50, 50, PdfFont.Helvetica, 12, link: "https://example.com", width: 80);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Annots", pdf);
            // Count annotation objects — should have more than one /Subtype /Link
            int count = 0;
            int idx = 0;
            while ((idx = pdf.IndexOf("/Subtype /Link", idx)) >= 0)
            {
                count++;
                idx++;
            }
            Assert.True(count > 1, $"Expected multiple link annotations for wrapped text, got {count}");
        }

        [Fact]
        public void DrawTextBox_WithoutLink_NoAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("No link text", 50, 50, width: 200);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.DoesNotContain("/Annots", pdf);
        }

        // ── DrawText (rich text box) with link ────────────────────

        [Fact]
        public void DrawRichTextBox_SpanWithLink_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Read the "),
                new TextSpan("documentation", PdfFont.Helvetica, 12, PdfColor.Blue,
                    underline: true, link: "https://docs.example.com"),
                new TextSpan(" for more.")
            }, 50, 50, width: 400);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Annots", pdf);
            Assert.Contains("https://docs.example.com", pdf);
        }

        [Fact]
        public void DrawRichTextBox_MixedLinkedAndUnlinked_OnlyLinkedHasAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Normal text "),
                new TextSpan("linked text", link: "https://example.com"),
                new TextSpan(" more normal text")
            }, 50, 50, width: 400);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("https://example.com", pdf);
            // Should only have one link annotation
            int count = 0;
            int idx = 0;
            while ((idx = pdf.IndexOf("/Subtype /Link", idx)) >= 0)
            {
                count++;
                idx++;
            }
            Assert.Equal(1, count);
        }

        [Fact]
        public void DrawRichTextBox_LinkedSpanWrapsAcrossLines_ProducesMultipleAnnotations()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            // Use narrow width to force wrapping within the linked span
            page.DrawText(new[]
            {
                new TextSpan("This linked text should wrap across lines", PdfFont.Helvetica, 12,
                    PdfColor.Blue, underline: true, link: "https://example.com")
            }, 50, 50, width: 100);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("https://example.com", pdf);
            int count = 0;
            int idx = 0;
            while ((idx = pdf.IndexOf("/Subtype /Link", idx)) >= 0)
            {
                count++;
                idx++;
            }
            Assert.True(count > 1, $"Expected multiple link annotations for wrapped linked span, got {count}");
        }

        // ── Coordinate system ────────────────────────────────────

        [Fact]
        public void DrawText_WithLink_BottomUp_ProducesAnnotation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.CoordinateOrigin = CoordinateOrigin.BottomUp;
            page.DrawText("Click", 50, 700, link: "https://example.com");
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Annots", pdf);
            Assert.Contains("https://example.com", pdf);
        }

        // ── Integration test ─────────────────────────────────────

        [Fact]
        public void MixedLinksOnPage_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: multiple hyperlinks inline with plain text");

            // DrawText with link
            page.DrawText("Visit Example", 50, 50, PdfFont.Helvetica, 14,
                PdfColor.Blue, underline: true, link: "https://example.com");

            // DrawText (rich) with link
            page.DrawText(new[]
            {
                new TextSpan("See "),
                new TextSpan("GitHub", PdfFont.HelveticaBold, 12, PdfColor.Blue,
                    underline: true, link: "https://github.com"),
                new TextSpan(" for code.")
            }, 50, 80);

            // DrawText (text box) with link
            page.DrawText("Click for full terms and conditions", 50, 110,
                PdfFont.Helvetica, 10, link: "https://example.com/terms", width: 150);

            // DrawText (rich text box) with mixed links
            page.DrawText(new[]
            {
                new TextSpan("Contact us at "),
                new TextSpan("support@example.com", PdfFont.Helvetica, 11, PdfColor.Blue,
                    underline: true, link: "mailto:support@example.com"),
                new TextSpan(" or visit our "),
                new TextSpan("help page", PdfFont.Helvetica, 11, PdfColor.Blue,
                    underline: true, link: "https://help.example.com"),
                new TextSpan(".")
            }, 50, 180, width: 300);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Navigation/hyperlinks-mixed-inline");
            var bitmap = TestHelper.RasterizePage(bytes, "Navigation/hyperlinks-mixed-inline");
            Assert.True(bitmap.Width > 0);
            bitmap.Dispose();

            var pdf = TestHelper.GetPdfText(bytes);
            Assert.Contains("https://example.com", pdf);
            Assert.Contains("https://github.com", pdf);
            Assert.Contains("https://example.com/terms", pdf);
            Assert.Contains("mailto:support@example.com", pdf);
            Assert.Contains("https://help.example.com", pdf);
        }
    }
}
