using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class FullDocumentTests
    {
        private static int PtToPx(float pt) => (int)(pt * 150 / 72.0);

        private static bool HasDarkPixelsInRegion(SkiaSharp.SKBitmap bitmap,
            int xMin, int xMax, int yMin, int yMax)
        {
            xMax = System.Math.Min(xMax, bitmap.Width - 1);
            yMax = System.Math.Min(yMax, bitmap.Height - 1);
            for (int x = System.Math.Max(0, xMin); x <= xMax; x++)
                for (int y = System.Math.Max(0, yMin); y <= yMax; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Red < 200 || p.Green < 200 || p.Blue < 200) return true;
                }
            return false;
        }

        [Fact]
        public void InvoiceDocument_AllFeatures()
        {
            var doc = new PdfDocument { Title = "Invoice #1001", Author = "Acme Corp" };
            var page = doc.AddPage(PageSize.A4);

            // Title
            page.DrawText("INVOICE", 50, 40, PdfFont.HelveticaBold, 28, PdfColor.Rgb(0, 51, 102));
            page.DrawText("#1001", 250, 40, PdfFont.Helvetica, 28, PdfColor.Rgb(100, 100, 100));

            // Horizontal rule
            page.DrawLine(50, 75, 545, 75, PdfColor.Rgb(0, 51, 102), 2);

            // Company info
            float y = 95;
            y = page.DrawTextBox("Acme Corporation\n123 Business Ave\nSeattle, WA 98101", 50, y, 250,
                PdfFont.Helvetica, 10, color: PdfColor.DarkGray);

            // Client info
            page.DrawTextBox("Bill To:\nJane Smith\n456 Client Street\nPortland, OR 97201", 350, 95, 195,
                PdfFont.Helvetica, 10, color: PdfColor.DarkGray);

            y += 20;

            // Items table
            var table = new PdfTable(285, 50, 80, 80)
            {
                HeaderBackground = PdfColor.Rgb(0, 51, 102),
                HeaderTextColor = PdfColor.White,
                AlternateRowShading = true,
                BorderWidth = 0.5f
            };
            table.SetHeaders("Description", "Qty", "Unit Price", "Total")
                .SetColumnAlignment(1, TextAlignment.Center)
                .SetColumnAlignment(2, TextAlignment.Right)
                .SetColumnAlignment(3, TextAlignment.Right)
                .AddRow("Web Development Services", "40", "$150.00", "$6,000.00")
                .AddRow("UI/UX Design", "20", "$125.00", "$2,500.00")
                .AddRow("Annual Hosting", "1", "$500.00", "$500.00")
                .AddRow("SSL Certificate", "1", "$75.00", "$75.00")
                .AddRow("Domain Registration", "2", "$15.00", "$30.00");

            y = page.DrawTable(table, 50, y);
            y += 5;

            // Totals
            page.DrawLine(350, y, 545, y, PdfColor.LightGray, 0.5f);
            y += 10;
            page.DrawText("Subtotal:", 350, y, PdfFont.Helvetica, 10);
            page.DrawText("$9,105.00", 545, y, PdfFont.Helvetica, 10, alignment: TextAlignment.Right);
            y += 15;
            page.DrawText("Tax (8.5%):", 350, y, PdfFont.Helvetica, 10);
            page.DrawText("$773.93", 545, y, PdfFont.Helvetica, 10, alignment: TextAlignment.Right);
            y += 15;
            page.DrawLine(350, y, 545, y, PdfColor.Rgb(0, 51, 102), 1);
            y += 8;
            page.DrawText("Total:", 350, y, PdfFont.HelveticaBold, 12, PdfColor.Rgb(0, 51, 102));
            page.DrawText("$9,878.93", 545, y, PdfFont.HelveticaBold, 12, PdfColor.Rgb(0, 51, 102),
                TextAlignment.Right);

            y += 40;

            // Terms
            page.DrawText("Terms & Conditions:", 50, y, PdfFont.HelveticaBold, 11);
            y += 18;
            y = page.DrawBulletList(new[]
            {
                "Payment is due within 30 days of receipt",
                "Late payments subject to 1.5% monthly interest",
                "Please make checks payable to Acme Corporation"
            }, 50, y, 450, PdfFont.Helvetica, 9, color: PdfColor.DarkGray);

            // Footer
            page.DrawLine(50, 790, 545, 790, PdfColor.LightGray, 0.5f);
            page.DrawText("Thank you for your business!", 297.5f, 800, PdfFont.HelveticaOblique, 9,
                PdfColor.DarkGray, TextAlignment.Center);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "full_invoice");
            var bitmap = TestHelper.RasterizePage(bytes, "full_invoice");
            // Verify title "INVOICE" is visible near top
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(200), PtToPx(40), PtToPx(70)),
                "Expected 'INVOICE' title text to be visible");
            // Verify the horizontal rule area has dark pixels
            int ruleY = PtToPx(75);
            bool foundRule = false;
            for (int x = PtToPx(50); x < PtToPx(545) && !foundRule; x++)
            {
                var p = bitmap.GetPixel(x, ruleY);
                if (p.Red < 100 && p.Green < 100) foundRule = true;
            }
            Assert.True(foundRule, "Expected horizontal rule to be visible");
            // Verify table area has content (table starts around Y=180-200)
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(500), PtToPx(180), PtToPx(350)),
                "Expected table content to be visible in the invoice");
            // Verify footer text near bottom
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(200), PtToPx(400), PtToPx(795), PtToPx(815)),
                "Expected footer text near bottom of page");
            bitmap.Dispose();
        }

        [Fact]
        public void MultiPageReport_WithAllFeatures()
        {
            var doc = new PdfDocument { Title = "Quarterly Report", Author = "Test" };

            // Page 1: Title and intro
            var page1 = doc.AddPage(PageSize.A4);
            page1.DrawFilledRectangle(0, 0, 595, 100, PdfColor.Rgb(0, 51, 102));
            page1.DrawText("Quarterly Report", 50, 40, PdfFont.HelveticaBold, 32, PdfColor.White);
            page1.DrawText("Q4 2025", 50, 75, PdfFont.Helvetica, 18, PdfColor.Rgb(200, 200, 255));

            float y = 120;
            y = page1.DrawTextBox(
                "This report summarizes the key performance metrics and financial results for the fourth quarter of 2025. " +
                "All divisions showed strong growth compared to the previous quarter.",
                50, y, 495, PdfFont.Helvetica, 11, 1.4f);

            y += 20;
            page1.DrawText("Key Highlights:", 50, y, PdfFont.HelveticaBold, 14);
            y += 20;
            y = page1.DrawBulletList(new[]
            {
                "Revenue increased 15% quarter-over-quarter",
                "Customer satisfaction scores reached an all-time high of 94%",
                "Successfully launched three new product lines",
                "Expanded into two new international markets"
            }, 50, y, 495, PdfFont.Helvetica, 11);

            y += 20;

            // Large table that should span multiple pages
            var table = new PdfTable(50, 180, 85, 85, 95)
            {
                HeaderBackground = PdfColor.Rgb(0, 51, 102),
                HeaderTextColor = PdfColor.White,
                AlternateRowShading = true,
                AlternateRowColor = PdfColor.Rgb(0.94f, 0.94f, 0.97f)
            };
            table.SetHeaders("#", "Product", "Units", "Revenue", "Growth")
                .SetColumnAlignment(0, TextAlignment.Center)
                .SetColumnAlignment(2, TextAlignment.Right)
                .SetColumnAlignment(3, TextAlignment.Right)
                .SetColumnAlignment(4, TextAlignment.Right);

            string[] products = { "Widget Alpha", "Widget Beta", "Widget Gamma", "Service Plan A",
                "Service Plan B", "Consulting Basic", "Consulting Pro", "Support Tier 1",
                "Support Tier 2", "Support Tier 3", "Training Online", "Training Onsite",
                "License Standard", "License Enterprise", "Hardware Module A", "Hardware Module B",
                "Custom Integration", "API Access Basic", "API Access Pro", "Data Analytics" };

            for (int i = 0; i < 40; i++)
            {
                string product = products[i % products.Length];
                int units = 100 + i * 37;
                int revenue = units * (50 + i * 3);
                float growth = 5.0f + (i * 1.7f) % 25;
                table.AddRow($"{i + 1}", product, units.ToString("N0"), $"${revenue:N0}", $"+{growth:F1}%");
            }

            page1.DrawTable(table, 50, y, continuationY: 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "full_report");
            int pageCount = TestHelper.GetPageCount(bytes);
            Assert.True(pageCount >= 2, $"Expected multi-page report, got {pageCount} pages");

            // Rasterize and verify every page has actual content (not just blank)
            for (int i = 0; i < pageCount; i++)
            {
                var bitmap = TestHelper.RasterizePage(bytes, "full_report", i);
                // Every page should have non-white pixels (content)
                bool hasContent = HasDarkPixelsInRegion(bitmap, PtToPx(30), bitmap.Width - PtToPx(30),
                    PtToPx(30), bitmap.Height - PtToPx(30));
                Assert.True(hasContent, $"Page {i + 1} should have visible content, not be blank");
                bitmap.Dispose();
            }

            // Page 1 should have the blue header banner
            var page1Bmp = TestHelper.RasterizePage(bytes, "full_report_verify", 0);
            int bannerY = PtToPx(50); // center of the 100pt tall banner
            var bannerPx = page1Bmp.GetPixel(PtToPx(297), bannerY);
            Assert.True(bannerPx.Red < 50 && bannerPx.Blue > 80,
                $"Expected dark blue banner at top of page 1, got ({bannerPx.Red},{bannerPx.Green},{bannerPx.Blue})");
            page1Bmp.Dispose();
        }

        [Fact]
        public void CmykColors_Work()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            page.DrawFilledRectangle(50, 50, 100, 50, PdfColor.Cmyk(1, 0, 0, 0)); // Cyan
            page.DrawFilledRectangle(200, 50, 100, 50, PdfColor.Cmyk(0, 1, 0, 0)); // Magenta
            page.DrawFilledRectangle(350, 50, 100, 50, PdfColor.Cmyk(0, 0, 1, 0)); // Yellow

            page.DrawText("CMYK Cyan", 50, 120, PdfFont.Helvetica, 12,
                PdfColor.Cmyk(1, 0, 0, 0));
            page.DrawText("CMYK Magenta", 200, 120, PdfFont.Helvetica, 12,
                PdfColor.Cmyk(0, 1, 0, 0));

            page.DrawLine(50, 150, 500, 150, PdfColor.Cmyk(0, 0, 0, 1), 2);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "full_cmyk");
            var bitmap = TestHelper.RasterizePage(bytes, "full_cmyk");
            // CMYK Cyan rect center at (100, 75) — should render as cyan-ish
            int cyanX = PtToPx(100), cyanY = PtToPx(75);
            var cyanPx = bitmap.GetPixel(cyanX, cyanY);
            Assert.True(cyanPx.Blue > 150 || cyanPx.Green > 150,
                $"Expected cyan-ish fill, got ({cyanPx.Red},{cyanPx.Green},{cyanPx.Blue})");
            Assert.True(cyanPx.Red < 100,
                $"Cyan should have low red component, got R={cyanPx.Red}");
            // CMYK Magenta rect center at (250, 75) — should render as magenta-ish
            int magX = PtToPx(250), magY = PtToPx(75);
            var magPx = bitmap.GetPixel(magX, magY);
            Assert.True(magPx.Red > 150 || magPx.Blue > 150,
                $"Expected magenta-ish fill, got ({magPx.Red},{magPx.Green},{magPx.Blue})");
            // CMYK Yellow rect center at (400, 75) — should render as yellow-ish
            int yelX = PtToPx(400), yelY = PtToPx(75);
            var yelPx = bitmap.GetPixel(yelX, yelY);
            Assert.True(yelPx.Red > 150 && yelPx.Green > 150,
                $"Expected yellow-ish fill, got ({yelPx.Red},{yelPx.Green},{yelPx.Blue})");
            // The black line at Y=150 should have dark pixels
            int lineY = PtToPx(150);
            bool foundLine = false;
            for (int x = PtToPx(100); x < PtToPx(400) && !foundLine; x++)
            {
                var p = bitmap.GetPixel(x, lineY);
                if (p.Red < 50 && p.Green < 50 && p.Blue < 50) foundLine = true;
            }
            Assert.True(foundLine, "Expected black CMYK line to be visible");
            bitmap.Dispose();
        }

        [Fact]
        public void AllPageSizes_Work()
        {
            var doc = new PdfDocument();
            doc.AddPage(PageSize.A3);
            doc.AddPage(PageSize.A4);
            doc.AddPage(PageSize.A5);
            doc.AddPage(PageSize.Letter);
            doc.AddPage(PageSize.Legal);
            doc.AddPage(PageSize.A4.Landscape());
            doc.AddPage(new PageSize(400, 600)); // custom

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "full_all_sizes");
            Assert.Equal(7, TestHelper.GetPageCount(bytes));

            // Verify different page sizes produce different rasterized dimensions
            var bmpA3 = TestHelper.RasterizePage(bytes, "full_all_sizes", 0);
            var bmpA4 = TestHelper.RasterizePage(bytes, "full_all_sizes", 1);
            var bmpA5 = TestHelper.RasterizePage(bytes, "full_all_sizes", 2);
            var bmpLandscape = TestHelper.RasterizePage(bytes, "full_all_sizes", 5);
            var bmpCustom = TestHelper.RasterizePage(bytes, "full_all_sizes", 6);

            // A3 should be larger than A4
            Assert.True(bmpA3.Width > bmpA4.Width, "A3 should be wider than A4");
            Assert.True(bmpA3.Height > bmpA4.Height, "A3 should be taller than A4");
            // A4 should be larger than A5
            Assert.True(bmpA4.Width > bmpA5.Width, "A4 should be wider than A5");
            // Landscape should be wider than tall
            Assert.True(bmpLandscape.Width > bmpLandscape.Height, "Landscape should be wider than tall");
            // Custom 400x600 — verify it rasterizes at roughly the right aspect ratio
            float customAspect = (float)bmpCustom.Width / bmpCustom.Height;
            Assert.True(customAspect > 0.5f && customAspect < 0.8f,
                $"Custom 400x600 page should have portrait aspect ratio, got {customAspect:F2}");

            bmpA3.Dispose();
            bmpA4.Dispose();
            bmpA5.Dispose();
            bmpLandscape.Dispose();
            bmpCustom.Dispose();
        }
    }
}
