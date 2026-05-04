using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class ListTests
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
        public void DrawBulletList_RendersItems()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[] { "First item", "Second item", "Third item" };
            float endY = page.DrawBulletList(items, 50, 50, 400);

            Assert.True(endY > 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_bullet");
            var bitmap = TestHelper.RasterizePage(bytes, "list_bullet");
            // Each item should produce visible text at its Y position
            // Items are at Y=50, then spaced by lineHeight(12*1.2=14.4) + 0.3*12=3.6 = ~18pt each
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(itemY), PtToPx(itemY + 14)),
                    $"Expected visible text for bullet item {i + 1} at Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawNumberedList_RendersWithNumbers()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[] { "First item", "Second item", "Third item" };
            float endY = page.DrawNumberedList(items, 50, 50, 400);

            Assert.True(endY > 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_numbered");
            var bitmap = TestHelper.RasterizePage(bytes, "list_numbered");
            // Each item should have visible text including the number marker
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                // Number marker should be at x=50, text at x=70
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(60), PtToPx(itemY), PtToPx(itemY + 14)),
                    $"Expected number marker for item {i + 1} at X=50, Y~{itemY}");
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(70), PtToPx(300), PtToPx(itemY), PtToPx(itemY + 14)),
                    $"Expected text for item {i + 1} at X=70, Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawBulletList_LongItems_WrapCorrectly()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[]
            {
                "This is a very long list item that should wrap to multiple lines when rendered in a narrow column width",
                "Short item",
                "Another long item that tests word wrapping behavior within bulleted lists to ensure everything looks correct"
            };
            float endY = page.DrawBulletList(items, 50, 50, 300);

            Assert.True(endY > 100, "Long items should take more vertical space");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_bullet_wrap");
            var bitmap = TestHelper.RasterizePage(bytes, "list_bullet_wrap");

            // First item is long — should wrap to at least 2 lines
            // Check for text on the first line (Y~50) and a wrapped continuation line (Y~64)
            float lineHeight = 12 * 1.2f;
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(65), PtToPx(350), PtToPx(50), PtToPx(50 + 12)),
                "Expected text on line 1 of first (long) bullet item");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(65), PtToPx(350), PtToPx(50 + lineHeight), PtToPx(50 + lineHeight + 12)),
                "Expected wrapped text on line 2 of first (long) bullet item");

            // The last item should also be visible further down the page
            float lastItemApproxY = endY - 12 * 1.2f - 12 * 0.3f; // rough estimate
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(350), PtToPx(lastItemApproxY - 20), PtToPx(endY)),
                "Expected visible text for the last bullet item");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawNumberedList_CustomStartNumber()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[] { "Item A", "Item B", "Item C" };
            float endY = page.DrawNumberedList(items, 50, 50, 400, startNumber: 5);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_numbered_start5");
            var bitmap = TestHelper.RasterizePage(bytes, "list_numbered_start5");
            // Verify text is rendered for all 3 items
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(itemY), PtToPx(itemY + 14)),
                    $"Expected visible text for numbered item {5 + i} at Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawBulletList_ColoredText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[] { "Red item one", "Red item two" };
            page.DrawBulletList(items, 50, 50, 400, color: PdfColor.Red);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_bullet_red");
            var bitmap = TestHelper.RasterizePage(bytes, "list_bullet_red");

            bool foundRed = false;
            for (int x = 80; x < 400 && !foundRed; x++)
            {
                for (int y = 70; y < 200 && !foundRed; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.Red > 200 && pixel.Green < 50 && pixel.Blue < 50)
                        foundRed = true;
                }
            }
            Assert.True(foundRed, "Expected red text in bullet list");
            bitmap.Dispose();
        }

        [Fact]
        public void BulletAndNumberedLists_Together()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float y = 50;
            page.DrawText("Bullet List:", 50, y, PdfFont.HelveticaBold, 14);
            y += 20;
            float bulletStartY = y;
            y = page.DrawBulletList(new[] { "Apples", "Bananas", "Cherries" }, 50, y, 400);
            y += 10;
            page.DrawText("Numbered List:", 50, y, PdfFont.HelveticaBold, 14);
            float numberedLabelY = y;
            y += 20;
            float numberedStartY = y;
            y = page.DrawNumberedList(new[] { "Step one", "Step two", "Step three" }, 50, y, 400);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_both");
            var bitmap = TestHelper.RasterizePage(bytes, "list_both");
            // Verify both list sections have visible content
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(bulletStartY), PtToPx(bulletStartY + 14)),
                "Expected visible text in bullet list section");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(numberedLabelY), PtToPx(numberedLabelY + 14)),
                "Expected 'Numbered List:' label to be visible");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(numberedStartY), PtToPx(numberedStartY + 14)),
                "Expected visible text in numbered list section");
            bitmap.Dispose();
        }
    }
}
