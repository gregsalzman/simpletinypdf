using System.Text;
using SkiaSharp;
using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutIntegrationTests
    {
        [Fact]
        public void FullReport_MixedContent()
        {
            var layout = new PdfDocumentLayout();
            layout.Margins = new PdfMargins(72, 72, 72, 72);

            // Header and footer with page numbers
            layout.HeaderFooter.Header = (page, ctx) =>
            {
                page.DrawText("SimpleTinyPDF Layout Report", page.Width / 2, 25,
                    PdfFont.HelveticaBold, 10, PdfColor.Rgb(100, 100, 100), TextAlignment.Center);
                page.DrawLine(72, 40, page.Width - 72, 40, PdfColor.Rgb(200, 200, 200));
            };
            layout.HeaderFooter.Footer = (page, ctx) =>
            {
                page.DrawLine(72, page.Height - 45, page.Width - 72, page.Height - 45,
                    PdfColor.Rgb(200, 200, 200));
                page.DrawText($"Page {ctx.PageNumber} of {ctx.TotalPages}",
                    page.Width / 2, page.Height - 30,
                    PdfFont.Helvetica, 9, PdfColor.Rgb(120, 120, 120), TextAlignment.Center);
            };

            // Title
            layout.AddParagraph("Annual Report 2026", new ParagraphOptions
            {
                Font = PdfFont.HelveticaBold,
                FontSize = 24,
                Alignment = TextAlignment.Center,
                SpaceAfter = 20
            });

            // Subtitle
            layout.AddParagraph("A comprehensive summary of operations and financial performance.",
                new ParagraphOptions
                {
                    FontSize = 12,
                    Color = PdfColor.Rgb(100, 100, 100),
                    Alignment = TextAlignment.Center,
                    SpaceAfter = 30
                });

            // Section heading
            layout.AddParagraph("1. Executive Summary", new ParagraphOptions
            {
                Font = PdfFont.HelveticaBold,
                FontSize = 16,
                SpaceBefore = 10,
                SpaceAfter = 8
            });

            // Body paragraphs
            layout.AddParagraph(
                "The fiscal year 2026 has been a period of significant growth and transformation. " +
                "Revenue increased by 23% year-over-year, driven primarily by expansion into new markets " +
                "and the successful launch of three new product lines. Operating margins improved by " +
                "2.3 percentage points, reflecting our commitment to operational efficiency.",
                new ParagraphOptions { SpaceAfter = 8 });

            layout.AddParagraph(
                "Key achievements include the acquisition of two strategic partners, the opening of " +
                "regional offices in four new cities, and the completion of our digital transformation " +
                "initiative. Employee satisfaction scores reached an all-time high of 87%.",
                new ParagraphOptions { SpaceAfter = 12 });

            // Table
            layout.AddParagraph("Financial Highlights", new ParagraphOptions
            {
                Font = PdfFont.HelveticaBold,
                FontSize = 14,
                SpaceBefore = 10,
                SpaceAfter = 8
            });

            var table = new PdfTable(150, 100, 100, 100);
            table.SetHeaders("Metric", "2024", "2025", "2026");
            table.AddRow("Revenue ($M)", "142.3", "165.8", "203.9");
            table.AddRow("Net Income ($M)", "18.5", "22.1", "31.7");
            table.AddRow("Employees", "1,240", "1,580", "1,920");
            table.AddRow("Market Share", "12.4%", "14.1%", "17.3%");
            layout.AddTable(table);

            // Rich text paragraph
            layout.AddParagraph(new[]
            {
                new TextSpan("Note: ", PdfFont.HelveticaBold, 10, PdfColor.Red),
                new TextSpan("All figures are unaudited and subject to final review. ",
                    PdfFont.Helvetica, 10),
                new TextSpan("See Appendix A for detailed methodology.",
                    PdfFont.Helvetica, 10, PdfColor.Blue, underline: true)
            }, new ParagraphOptions { SpaceBefore = 12, SpaceAfter = 20 });

            // List section
            layout.AddParagraph("2. Strategic Priorities", new ParagraphOptions
            {
                Font = PdfFont.HelveticaBold,
                FontSize = 16,
                SpaceBefore = 10,
                SpaceAfter = 8
            });

            layout.AddList(new[]
            {
                new ListItem("Expand market presence in Asia-Pacific region"),
                new ListItem("Launch next-generation product platform"),
                new ListItem("Invest in AI and machine learning capabilities"),
                new ListItem("Strengthen cybersecurity infrastructure"),
                new ListItem("Improve sustainability metrics by 30%")
            }, ListStyle.Numbered);

            // Image
            layout.AddParagraph("Company Logo", new ParagraphOptions
            {
                Font = PdfFont.HelveticaBold,
                FontSize = 14,
                SpaceBefore = 20,
                SpaceAfter = 8
            });

            var imgBytes = TestHelper.CreateQuadrantJpeg(200, 100);
            var img = PdfImage.FromBytes(imgBytes);
            layout.AddImage(img, new ImageOptions
            {
                Width = 200,
                Height = 100,
                Alignment = TextAlignment.Center,
                SpaceAfter = 20
            });

            // More text to potentially push to page 2
            layout.AddParagraph("3. Outlook", new ParagraphOptions
            {
                Font = PdfFont.HelveticaBold,
                FontSize = 16,
                SpaceBefore = 10,
                SpaceAfter = 8
            });

            var outlook = new StringBuilder();
            for (int i = 0; i < 15; i++)
                outlook.AppendLine(
                    "Looking ahead, we expect continued momentum across all business segments. " +
                    "The investments we have made in technology and talent position us well for " +
                    "sustained growth in an increasingly competitive landscape.");
            layout.AddParagraph(outlook.ToString(), new ParagraphOptions { SpaceAfter = 10 });

            // Generate and validate
            var doc = layout.Generate();
            Assert.True(doc.PageCount >= 2, $"Report should span multiple pages, got {doc.PageCount}");

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: full report with headers, footers, table, list, image");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/integration-full-report");

            // Verify "Page X of Y" appears in PDF
            var pdfText = TestHelper.GetPdfText(bytes);
            Assert.Contains($"of {doc.PageCount}", pdfText);

            // Verify content on first page
            var bmp1 = TestHelper.RasterizePage(bytes, "Layout/integration-full-report", 0);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bmp1,
                TestHelper.PtToPx(72), TestHelper.PtToPx(500),
                TestHelper.PtToPx(72), TestHelper.PtToPx(700)));

            // Verify header on first page
            int midX = TestHelper.PtToPx(595f / 2f);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bmp1,
                midX - 80, midX + 80,
                TestHelper.PtToPx(20), TestHelper.PtToPx(35)));
        }

        [Fact]
        public void LetterSizePage_WorksCorrectly()
        {
            var layout = new PdfDocumentLayout();
            layout.PageSize = PageSize.Letter;
            layout.AddParagraph("US Letter format document", new ParagraphOptions
            {
                Font = PdfFont.HelveticaBold,
                FontSize = 18,
                Alignment = TextAlignment.Center
            });

            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);
            Assert.Equal(612f, doc.Pages[0].Width);
            Assert.Equal(792f, doc.Pages[0].Height);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: US Letter page dimensions");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/integration-letter-size");
        }

        [Fact]
        public void JustifiedText_DistributesEvenly()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph(
                "This is a justified paragraph that should distribute its words evenly across " +
                "the full width of the content area. Each line except the last should be stretched " +
                "to fill the entire available width, creating clean, even margins on both sides " +
                "of the text block. This is a common typographic treatment used in books and " +
                "formal documents to create a polished, professional appearance.",
                new ParagraphOptions
                {
                    Alignment = TextAlignment.Justify,
                    SpaceAfter = 10
                });

            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: text fills full width on non-last lines");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/integration-justified");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/integration-justified");

            // Check text near the right margin (non-last lines should reach it)
            int rightEdge = TestHelper.PtToPx(595 - 72);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                rightEdge - 30, rightEdge,
                TestHelper.PtToPx(72), TestHelper.PtToPx(90)));
        }

        [Fact]
        public void SaveToStream_ProducesValidPdf()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Stream output test");

            using (var ms = new System.IO.MemoryStream())
            {
                layout.Save(ms);
                Assert.True(ms.Length > 0);

                var bytes = ms.ToArray();
                Assert.Equal(1, TestHelper.GetPageCount(bytes));
            }
        }

        [Fact]
        public void ToArray_ProducesValidPdf()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Byte array output test");

            var bytes = layout.ToArray();
            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
            Assert.Equal(1, TestHelper.GetPageCount(bytes));

            TestHelper.SavePdf(bytes, "Layout/integration-to-array");
        }
    }
}
