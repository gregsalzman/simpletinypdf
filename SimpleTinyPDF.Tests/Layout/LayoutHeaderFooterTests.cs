using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutHeaderFooterTests
    {
        [Fact]
        public void PrimaryHeader_AppearsOnAllPages()
        {
            var layout = new PdfDocumentLayout();
            layout.HeaderFooter.Header = (page, ctx) =>
            {
                page.DrawText("HEADER", page.Width / 2, 20,
                    PdfFont.HelveticaBold, 10, PdfColor.Black, TextAlignment.Center);
            };

            layout.AddParagraph("Page 1 content");
            layout.AddPageBreak();
            layout.AddParagraph("Page 2 content");

            var doc = layout.Generate();
            Assert.Equal(2, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: 'HEADER' at top center of every page");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/header-footer-primary-header");

            // Check header area on both pages
            int midX = TestHelper.PtToPx(595f / 2f);
            for (int i = 0; i < 2; i++)
            {
                var bmp = TestHelper.RasterizePage(bytes, "Layout/header-footer-primary-header", i);
                Assert.True(TestHelper.HasDarkPixelsInRegion(bmp,
                    midX - 40, midX + 40,
                    TestHelper.PtToPx(15), TestHelper.PtToPx(35)));
            }
        }

        [Fact]
        public void PrimaryFooter_AppearsOnAllPages()
        {
            var layout = new PdfDocumentLayout();
            layout.HeaderFooter.Footer = (page, ctx) =>
            {
                page.DrawText("FOOTER", page.Width / 2, page.Height - 30,
                    PdfFont.Helvetica, 10, PdfColor.Black, TextAlignment.Center);
            };

            layout.AddParagraph("Page 1");
            layout.AddPageBreak();
            layout.AddParagraph("Page 2");

            var doc = layout.Generate();
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/header-footer-primary-footer");

            int midX = TestHelper.PtToPx(595f / 2f);
            int footerY = TestHelper.PtToPx(842 - 30);
            for (int i = 0; i < 2; i++)
            {
                var bmp = TestHelper.RasterizePage(bytes, "Layout/header-footer-primary-footer", i);
                Assert.True(TestHelper.HasDarkPixelsInRegion(bmp,
                    midX - 40, midX + 40,
                    footerY - 10, footerY + 15));
            }
        }

        [Fact]
        public void PageNumbers_AreCorrect()
        {
            var layout = new PdfDocumentLayout();
            layout.HeaderFooter.Footer = (page, ctx) =>
            {
                page.DrawText($"Page {ctx.PageNumber}", page.Width / 2, page.Height - 30,
                    PdfFont.Helvetica, 10, PdfColor.Black, TextAlignment.Center);
            };

            for (int i = 0; i < 3; i++)
            {
                layout.AddParagraph($"Content for page {i + 1}");
                if (i < 2) layout.AddPageBreak();
            }

            var doc = layout.Generate();
            Assert.Equal(3, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: footer shows 'Page 1', 'Page 2', 'Page 3'");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/header-footer-page-numbers");

            // All 3 pages should have footer content
            int midX = TestHelper.PtToPx(595f / 2f);
            int footerY = TestHelper.PtToPx(842 - 30);
            for (int i = 0; i < 3; i++)
            {
                var bmp = TestHelper.RasterizePage(bytes, "Layout/header-footer-page-numbers", i);
                Assert.True(TestHelper.HasDarkPixelsInRegion(bmp,
                    midX - 50, midX + 50,
                    footerY - 10, footerY + 15));
            }
        }

        [Fact]
        public void TotalPages_IsCorrectViaTwoPass()
        {
            var layout = new PdfDocumentLayout();
            layout.HeaderFooter.Footer = (page, ctx) =>
            {
                page.DrawText($"Page {ctx.PageNumber} of {ctx.TotalPages}",
                    page.Width / 2, page.Height - 30,
                    PdfFont.Helvetica, 10, PdfColor.Black, TextAlignment.Center);
            };

            // Create exactly 3 pages
            for (int i = 0; i < 3; i++)
            {
                layout.AddParagraph($"Page {i + 1} content");
                if (i < 2) layout.AddPageBreak();
            }

            var doc = layout.Generate();
            Assert.Equal(3, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: footer shows 'Page X of 3'");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/header-footer-total-pages");

            // Verify the raw PDF text contains "of 3"
            var pdfText = TestHelper.GetPdfText(bytes);
            Assert.Contains("of 3", pdfText);
        }

        [Fact]
        public void FirstPageHeader_OverridesPrimary()
        {
            var layout = new PdfDocumentLayout();
            layout.HeaderFooter.Header = (page, ctx) =>
            {
                page.DrawText("NORMAL HEADER", page.Width / 2, 20,
                    PdfFont.Helvetica, 10, PdfColor.Black, TextAlignment.Center);
            };
            layout.HeaderFooter.FirstPageHeader = (page, ctx) =>
            {
                page.DrawText("FIRST PAGE HEADER", page.Width / 2, 20,
                    PdfFont.HelveticaBold, 14, PdfColor.Red, TextAlignment.Center);
            };

            layout.AddParagraph("First page");
            layout.AddPageBreak();
            layout.AddParagraph("Second page");

            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: page 1 has large red header, page 2 has normal");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/header-footer-first-page");

            // First page should have different header (larger text = more dark pixels)
            var bmp1 = TestHelper.RasterizePage(bytes, "Layout/header-footer-first-page", 0);
            var bmp2 = TestHelper.RasterizePage(bytes, "Layout/header-footer-first-page", 1);

            int midX = TestHelper.PtToPx(595f / 2f);
            int h1Pixels = TestHelper.CountDarkPixelsInRegion(bmp1,
                midX - 80, midX + 80, TestHelper.PtToPx(10), TestHelper.PtToPx(40));
            int h2Pixels = TestHelper.CountDarkPixelsInRegion(bmp2,
                midX - 80, midX + 80, TestHelper.PtToPx(10), TestHelper.PtToPx(40));

            Assert.True(h1Pixels > h2Pixels,
                $"First page header should be larger ({h1Pixels} vs {h2Pixels})");
        }

        [Fact]
        public void EvenPageFooter_OverridesPrimary()
        {
            var layout = new PdfDocumentLayout();
            layout.HeaderFooter.Footer = (page, ctx) =>
            {
                page.DrawText("ODD FOOTER", 72, page.Height - 30,
                    PdfFont.Helvetica, 10, PdfColor.Black, TextAlignment.Left);
            };
            layout.HeaderFooter.EvenPageFooter = (page, ctx) =>
            {
                page.DrawText("EVEN FOOTER", page.Width - 72, page.Height - 30,
                    PdfFont.Helvetica, 10, PdfColor.Black, TextAlignment.Right);
            };

            for (int i = 0; i < 3; i++)
            {
                layout.AddParagraph($"Page {i + 1}");
                if (i < 2) layout.AddPageBreak();
            }

            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: odd pages left footer, even page right footer");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/header-footer-even-page");

            // Page 1 (odd): dark pixels on the left side of footer
            var bmp1 = TestHelper.RasterizePage(bytes, "Layout/header-footer-even-page", 0);
            int footerY = TestHelper.PtToPx(842 - 30);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bmp1,
                TestHelper.PtToPx(72), TestHelper.PtToPx(200),
                footerY - 10, footerY + 15));

            // Page 2 (even): dark pixels on the right side of footer
            var bmp2 = TestHelper.RasterizePage(bytes, "Layout/header-footer-even-page", 1);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bmp2,
                TestHelper.PtToPx(400), TestHelper.PtToPx(523),
                footerY - 10, footerY + 15));
        }

        [Fact]
        public void NoHeaderFooter_SkipsTwoPass()
        {
            // Without headers/footers, should still work correctly
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Simple content");

            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/header-footer-none");
        }
    }
}
