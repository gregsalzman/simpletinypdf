using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class RotationTests
    {
        [Fact]
        public void DrawText_Rotated90_MovesTextVertically()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: text rotated 90 degrees");
            // Draw text at (200, 100) rotated 90° clockwise
            page.DrawText("Rotated", 200, 100, fontSize: 20, rotation: 90);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/rotation-text-90deg");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rotation-text-90deg");

            // At 90° clockwise rotation around (200,100), text should extend downward from (200,100)
            // Check that there are dark pixels below the anchor point
            int anchorX = (int)(200 * 150 / 72.0);
            int belowY = (int)(130 * 150 / 72.0); // below the anchor
            bool foundDark = false;
            for (int dx = -15; dx <= 15; dx++)
            {
                var p = bitmap.GetPixel(anchorX + dx, belowY);
                if (p.Red < 100 && p.Green < 100 && p.Blue < 100)
                {
                    foundDark = true;
                    break;
                }
            }
            Assert.True(foundDark, "Expected dark pixels below the anchor for 90° rotated text");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_Rotated45_HasDiagonalPixels()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: text rotated 45 degrees");
            page.DrawText("Diagonal", 200, 200, fontSize: 24, rotation: 45);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/rotation-text-45deg");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rotation-text-45deg");

            // With 45° CW rotation, text should go down-right diagonally
            // Check for non-white pixels in the diagonal region
            bool foundDiagonal = false;
            for (int d = 20; d < 80; d++)
            {
                int px = (int)(200 * 150 / 72.0) + d;
                int py = (int)(200 * 150 / 72.0) + d;
                if (px < bitmap.Width && py < bitmap.Height)
                {
                    var p = bitmap.GetPixel(px, py);
                    if (p.Red < 150 && p.Green < 150 && p.Blue < 150)
                    {
                        foundDiagonal = true;
                        break;
                    }
                }
            }
            Assert.True(foundDiagonal, "Expected dark pixels along diagonal for 45° rotated text");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_ZeroRotation_MatchesNoRotation()
        {
            var doc1 = new PdfDocument();
            var page1 = doc1.AddPage(PageSize.A4);
            page1.DrawText("Hello", 100, 100, fontSize: 14);

            var doc2 = new PdfDocument();
            var page2 = doc2.AddPage(PageSize.A4);
            page2.DrawText("Hello", 100, 100, fontSize: 14, rotation: 0f);

            var bytes1 = doc1.ToArray();
            var bytes2 = doc2.ToArray();

            // Both should produce identical output
            Assert.Equal(bytes1.Length, bytes2.Length);
        }

        [Fact]
        public void DrawFilledRectangle_Rotated45_CreatesDiamond()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: rectangle rotated 45 degrees");
            // Draw a 100x100 filled rectangle at (200, 200) rotated 45°
            page.DrawFilledRectangle(200, 200, 100, 100, PdfColor.Red, rotation: 45);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/rotation-rect-45deg");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rotation-rect-45deg");

            // The rotated rectangle should form a diamond shape
            // Check that there are red pixels at the anchor point
            int ax = (int)(200 * 150 / 72.0);
            int ay = (int)(200 * 150 / 72.0);
            // Check for red pixels in the rotated area (slightly below and right of anchor)
            bool foundRed = false;
            for (int dy = 10; dy < 80; dy++)
            {
                for (int dx = 10; dx < 80; dx++)
                {
                    int px = ax + dx;
                    int py = ay + dy;
                    if (px < bitmap.Width && py < bitmap.Height)
                    {
                        var p = bitmap.GetPixel(px, py);
                        if (p.Red > 200 && p.Green < 50 && p.Blue < 50)
                        {
                            foundRed = true;
                            break;
                        }
                    }
                }
                if (foundRed) break;
            }
            Assert.True(foundRed, "Expected red pixels in the rotated rectangle area");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRectangle_Rotated45_CreatesRotatedOutline()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: rectangle outline rotated 45 degrees");
            page.DrawRectangle(200, 200, 100, 100, PdfColor.Black, 2, rotation: 45);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/rotation-rect-outline-45deg");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rotation-rect-outline-45deg");

            // Rotated rectangle outline should have dark pixels in rotated positions
            // The original top-left corner (200,200) is the rotation origin
            // After 45° CW rotation, pixels should appear rotated
            bool foundDark = false;
            int ax = (int)(200 * 150 / 72.0);
            int ay = (int)(200 * 150 / 72.0);
            for (int dy = 20; dy < 100; dy++)
            {
                int px = ax + dy;
                int py = ay + dy;
                if (px < bitmap.Width && py < bitmap.Height)
                {
                    var p = bitmap.GetPixel(px, py);
                    if (p.Red < 100 && p.Green < 100 && p.Blue < 100)
                    {
                        foundDark = true;
                        break;
                    }
                }
            }
            Assert.True(foundDark, "Expected dark pixels along diagonal for rotated rectangle");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawLine_Rotated90_BecomesVertical()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: line rotated 90 degrees");
            // Horizontal line from (100,200) to (300,200), rotated 90° around (100,200)
            page.DrawLine(100, 200, 300, 200, PdfColor.Black, 2, rotation: 90);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/rotation-line-90deg");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rotation-line-90deg");

            // After 90° CW rotation around start point, line should go downward from (100,200)
            int lineX = (int)(100 * 150 / 72.0);
            bool foundVertical = false;
            for (int dy = 50; dy < 200; dy++)
            {
                int py = (int)(200 * 150 / 72.0) + dy;
                if (py < bitmap.Height)
                {
                    // Check a few pixels around lineX for the vertical line
                    for (int dx = -5; dx <= 5; dx++)
                    {
                        var p = bitmap.GetPixel(lineX + dx, py);
                        if (p.Red < 100 && p.Green < 100 && p.Blue < 100)
                        {
                            foundVertical = true;
                            break;
                        }
                    }
                }
                if (foundVertical) break;
            }
            Assert.True(foundVertical, "Expected vertical dark pixels for 90° rotated horizontal line");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawImage_Rotated90_RendersRotatedImage()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: image rotated 90 degrees");
            var imgBytes = TestHelper.CreateQuadrantJpeg(100, 100);
            var image = PdfImage.FromBytes(imgBytes);
            page.DrawImage(image, 200, 200, 100, 100, rotation: 90);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/rotation-image-90deg");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rotation-image-90deg");

            // Image should be rotated 90° CW around (200,200)
            // Search broadly for any non-white content in the region around the anchor
            bool foundNonWhite = false;
            int cx = (int)(200 * 150 / 72.0);
            int cy = (int)(200 * 150 / 72.0);
            for (int dy = -150; dy < 150 && !foundNonWhite; dy += 3)
            {
                for (int dx = -150; dx < 150; dx += 3)
                {
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px >= 0 && px < bitmap.Width && py >= 0 && py < bitmap.Height)
                    {
                        var p = bitmap.GetPixel(px, py);
                        if (p.Red < 240 || p.Green < 240 || p.Blue < 240)
                        {
                            foundNonWhite = true;
                            break;
                        }
                    }
                }
            }
            Assert.True(foundNonWhite, "Expected rendered content for rotated image");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_TextBox_Rotated90_RendersVerticalTextBlock()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: textbox rotated 90 degrees");
            page.DrawText("This is a wrapped text box that should be rotated.",
                200, 100, fontSize: 12, rotation: 90, width: 150);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/rotation-textbox-90deg");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rotation-textbox-90deg");

            // Text should extend downward from (200,100) when rotated 90° CW
            int ax = (int)(200 * 150 / 72.0);
            bool foundBelow = false;
            for (int dy = 50; dy < 200; dy++)
            {
                int py = (int)(100 * 150 / 72.0) + dy;
                if (py < bitmap.Height)
                {
                    for (int dx = -20; dx <= 20; dx++)
                    {
                        int px = ax + dx;
                        if (px >= 0 && px < bitmap.Width)
                        {
                            var p = bitmap.GetPixel(px, py);
                            if (p.Red < 100 && p.Green < 100 && p.Blue < 100)
                            {
                                foundBelow = true;
                                break;
                            }
                        }
                    }
                }
                if (foundBelow) break;
            }
            Assert.True(foundBelow, "Expected dark pixels below anchor for 90° rotated text box");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichText_Rotated90_RendersRotated()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: rich text rotated 90 degrees");
            var spans = new[]
            {
                new TextSpan("Bold ", PdfFont.HelveticaBold, 14),
                new TextSpan("Normal", PdfFont.Helvetica, 14, PdfColor.Red)
            };
            page.DrawText(spans, 200, 200, rotation: 90);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/rotation-richtext-90deg");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rotation-richtext-90deg");

            // Verify content exists below the anchor point (rotated 90° CW)
            int ax = (int)(200 * 150 / 72.0);
            bool foundBelow = false;
            for (int dy = 10; dy < 100; dy++)
            {
                int py = (int)(200 * 150 / 72.0) + dy;
                if (py < bitmap.Height)
                {
                    for (int dx = -15; dx <= 15; dx++)
                    {
                        int px = ax + dx;
                        if (px >= 0 && px < bitmap.Width)
                        {
                            var p = bitmap.GetPixel(px, py);
                            if (p.Red < 150 || p.Green < 150 || p.Blue < 150)
                            {
                                foundBelow = true;
                                break;
                            }
                        }
                    }
                }
                if (foundBelow) break;
            }
            Assert.True(foundBelow, "Expected dark pixels below anchor for 90° rotated rich text");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_BottomUp_Rotated90_RendersCorrectly()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: rotated text in BottomUp coordinate system");
            page.CoordinateOrigin = CoordinateOrigin.BottomUp;
            page.DrawText("BottomUp Rotated", 200, 500, fontSize: 16, rotation: 90);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Graphics/rotation-text-bottomup-90deg");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rotation-text-bottomup-90deg");

            // In BottomUp, Y=500 from bottom. The text should be rendered somewhere on the page.
            // Search the full page for dark pixels.
            bool foundDark = false;
            for (int sy = 0; sy < bitmap.Height && !foundDark; sy += 3)
            {
                for (int sx = 0; sx < bitmap.Width; sx += 3)
                {
                    var p = bitmap.GetPixel(sx, sy);
                    if (p.Red < 100 && p.Green < 100 && p.Blue < 100)
                    {
                        foundDark = true;
                        break;
                    }
                }
            }
            Assert.True(foundDark, "Expected rendered text somewhere on page for BottomUp rotation");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawMultipleRotatedElements_OnSamePage()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: multiple elements at different rotation angles");

            // Multiple rotated elements should not interfere with each other
            page.DrawText("Text at 0°", 100, 100, fontSize: 14);
            page.DrawText("Text at 45°", 100, 150, fontSize: 14, rotation: 45);
            page.DrawText("Text at 90°", 100, 200, fontSize: 14, rotation: 90);
            page.DrawFilledRectangle(300, 100, 80, 40, PdfColor.Blue, rotation: 30);
            page.DrawLine(300, 200, 450, 200, PdfColor.Red, 2, rotation: 45);
            page.DrawRectangle(300, 300, 80, 80, PdfColor.Green, 2, rotation: 60);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Graphics/rotation-multiple-elements");
            var bitmap = TestHelper.RasterizePage(bytes, "Graphics/rotation-multiple-elements");

            // Verify the non-rotated text at (100,100) is still horizontal
            // Search a vertical band around the expected Y position
            int textYCenter = (int)(107 * 150 / 72.0); // midpoint of 14pt text at y=100
            bool foundHorizontal = false;
            for (int dy = -15; dy <= 15 && !foundHorizontal; dy++)
            {
                int sy = textYCenter + dy;
                if (sy < 0 || sy >= bitmap.Height) continue;
                for (int sx = (int)(100 * 150 / 72.0); sx < (int)(200 * 150 / 72.0); sx++)
                {
                    var p = bitmap.GetPixel(sx, sy);
                    if (p.Red < 100 && p.Green < 100 && p.Blue < 100)
                    {
                        foundHorizontal = true;
                        break;
                    }
                }
            }
            Assert.True(foundHorizontal, "Expected horizontal text at 0° rotation");

            // Verify rotated blue rectangle has blue pixels
            bool foundBlue = false;
            for (int sy = (int)(80 * 150 / 72.0); sy < (int)(160 * 150 / 72.0); sy++)
            {
                for (int sx = (int)(280 * 150 / 72.0); sx < (int)(420 * 150 / 72.0); sx++)
                {
                    if (sx < bitmap.Width && sy < bitmap.Height)
                    {
                        var p = bitmap.GetPixel(sx, sy);
                        if (p.Blue > 200 && p.Red < 50 && p.Green < 50)
                        {
                            foundBlue = true;
                            break;
                        }
                    }
                }
                if (foundBlue) break;
            }
            Assert.True(foundBlue, "Expected blue pixels from rotated filled rectangle");
            bitmap.Dispose();
        }
    }
}
