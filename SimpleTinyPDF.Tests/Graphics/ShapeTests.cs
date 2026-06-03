using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class ShapeTests
    {
        [Fact]
        public void DrawLine_RendersVisibleLine()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: black line renders on page");
            page.DrawLine(50, 100, 500, 100, PdfColor.Black, 2);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/line-black-default");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/line-black-default");
            // Check for dark pixels along the line area (y=100 -> ~208px at 150dpi)
            int lineY = (int)(100 * 150 / 72.0);
            bool foundDark = false;
            for (int x = 130; x < 800; x++)
            {
                var pixel = bitmap.GetPixel(x, lineY);
                if (pixel.Red < 50 && pixel.Green < 50 && pixel.Blue < 50)
                {
                    foundDark = true;
                    break;
                }
            }
            Assert.True(foundDark, "Expected dark pixels along the drawn line");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawLine_RedColor_RendersRed()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: red colored line renders on page");
            page.DrawLine(50, 100, 500, 100, PdfColor.Red, 3);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/line-red-colored");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/line-red-colored");
            int lineY = (int)(100 * 150 / 72.0);
            bool foundRed = false;
            for (int x = 130; x < 800; x++)
            {
                var pixel = bitmap.GetPixel(x, lineY);
                if (pixel.Red > 200 && pixel.Green < 50 && pixel.Blue < 50)
                {
                    foundRed = true;
                    break;
                }
            }
            Assert.True(foundRed, "Expected red pixels along the red line");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRectangle_RendersOutline()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: rectangle outline (no fill) renders");
            page.DrawRectangle(100, 100, 200, 100, PdfColor.Black, 2);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/rectangle-outline");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rectangle-outline");
            // Top edge of rect at Y=100 -> check for dark pixels along top edge
            int topY = (int)(100 * 150 / 72.0);
            bool foundTopEdge = false;
            for (int x = (int)(100 * 150 / 72.0); x < (int)(300 * 150 / 72.0); x++)
            {
                var p = bitmap.GetPixel(x, topY);
                if (p.Red < 50 && p.Green < 50 && p.Blue < 50) { foundTopEdge = true; break; }
            }
            Assert.True(foundTopEdge, "Expected dark pixels along the top edge of the rectangle");
            // Interior should be white (stroke only, no fill)
            int cx = (int)(200 * 150 / 72.0);
            int cy = (int)(150 * 150 / 72.0);
            var center = bitmap.GetPixel(cx, cy);
            Assert.True(center.Red > 240 && center.Green > 240 && center.Blue > 240,
                $"Interior of stroke-only rect should be white, got ({center.Red},{center.Green},{center.Blue})");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawFilledRectangle_FillsArea()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: blue filled rectangle renders");
            page.DrawFilledRectangle(100, 100, 200, 100, PdfColor.Blue);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/rectangle-filled-blue");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rectangle-filled-blue");
            // Check center of the rectangle for blue
            int cx = (int)((100 + 100) * 150 / 72.0);
            int cy = (int)((100 + 50) * 150 / 72.0);
            var pixel = bitmap.GetPixel(cx, cy);
            Assert.True(pixel.Blue > 200, $"Expected blue fill, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
            Assert.True(pixel.Red < 50, $"Expected low red, got {pixel.Red}");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawFilledRectangle_WithStroke_HasBorder()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: filled rectangle with border renders");
            page.DrawFilledRectangle(100, 100, 200, 100,
                PdfColor.LightGray, PdfColor.Black, 2);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/rectangle-filled-with-border");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rectangle-filled-with-border");
            // Interior should be light gray (fill)
            int cx = (int)(200 * 150 / 72.0);
            int cy = (int)(150 * 150 / 72.0);
            var center = bitmap.GetPixel(cx, cy);
            Assert.True(center.Red > 180 && center.Red < 230,
                $"Interior should be light gray, got R={center.Red}");
            // Border (top edge) should be darker than interior
            int topY = (int)(100 * 150 / 72.0);
            bool foundDarkBorder = false;
            for (int x = (int)(100 * 150 / 72.0); x < (int)(300 * 150 / 72.0); x++)
            {
                var p = bitmap.GetPixel(x, topY);
                if (p.Red < 80 && p.Green < 80 && p.Blue < 80) { foundDarkBorder = true; break; }
            }
            Assert.True(foundDarkBorder, "Expected dark border pixels on the rectangle edge");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawLine_DifferentThicknesses()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: lines render at different thicknesses");
            page.DrawLine(50, 50, 500, 50, lineWidth: 0.5f);
            page.DrawLine(50, 80, 500, 80, lineWidth: 2f);
            page.DrawLine(50, 120, 500, 120, lineWidth: 5f);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/line-varying-thickness");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/line-varying-thickness");
            // Count dark pixels vertically at x=300 for each line to verify thickness differences
            int scanX = (int)(300 * 150 / 72.0);
            int thinDark = 0, medDark = 0, thickDark = 0;
            for (int dy = -10; dy <= 10; dy++)
            {
                int y1 = (int)(50 * 150 / 72.0) + dy;
                int y2 = (int)(80 * 150 / 72.0) + dy;
                int y3 = (int)(120 * 150 / 72.0) + dy;
                if (y1 >= 0 && y1 < bitmap.Height)
                {
                    var p = bitmap.GetPixel(scanX, y1);
                    if (p.Red < 128) thinDark++;
                }
                if (y2 >= 0 && y2 < bitmap.Height)
                {
                    var p = bitmap.GetPixel(scanX, y2);
                    if (p.Red < 128) medDark++;
                }
                if (y3 >= 0 && y3 < bitmap.Height)
                {
                    var p = bitmap.GetPixel(scanX, y3);
                    if (p.Red < 128) thickDark++;
                }
            }
            Assert.True(thinDark > 0, "Thin line should have at least some dark pixels");
            Assert.True(thickDark > medDark, $"5pt line ({thickDark}px) should be thicker than 2pt line ({medDark}px)");
            Assert.True(medDark > thinDark, $"2pt line ({medDark}px) should be thicker than 0.5pt line ({thinDark}px)");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawMultipleShapes_OnSamePage()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: multiple shapes render together on page");
            page.DrawFilledRectangle(50, 50, 100, 50, PdfColor.Red);
            page.DrawFilledRectangle(200, 50, 100, 50, PdfColor.Green);
            page.DrawFilledRectangle(350, 50, 100, 50, PdfColor.Blue);
            page.DrawLine(50, 130, 500, 130, PdfColor.Black, 1);
            page.DrawRectangle(50, 150, 400, 200, PdfColor.DarkGray, 2);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/multiple-shapes-combined");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/multiple-shapes-combined");
            // Verify each colored rectangle at its center
            // Red rect center: (100, 75)
            int rx = (int)(100 * 150 / 72.0), ry = (int)(75 * 150 / 72.0);
            var redPx = bitmap.GetPixel(rx, ry);
            Assert.True(redPx.Red > 200 && redPx.Green < 50 && redPx.Blue < 50,
                $"Expected red fill at ({rx},{ry}), got ({redPx.Red},{redPx.Green},{redPx.Blue})");
            // Green rect center: (250, 75)
            int gx = (int)(250 * 150 / 72.0), gy = (int)(75 * 150 / 72.0);
            var greenPx = bitmap.GetPixel(gx, gy);
            Assert.True(greenPx.Green > 100 && greenPx.Red < 50 && greenPx.Blue < 50,
                $"Expected green fill at ({gx},{gy}), got ({greenPx.Red},{greenPx.Green},{greenPx.Blue})");
            // Blue rect center: (400, 75)
            int bx = (int)(400 * 150 / 72.0), by = (int)(75 * 150 / 72.0);
            var bluePx = bitmap.GetPixel(bx, by);
            Assert.True(bluePx.Blue > 200 && bluePx.Red < 50 && bluePx.Green < 50,
                $"Expected blue fill at ({bx},{by}), got ({bluePx.Red},{bluePx.Green},{bluePx.Blue})");
            bitmap.Dispose();
        }
    }
}
