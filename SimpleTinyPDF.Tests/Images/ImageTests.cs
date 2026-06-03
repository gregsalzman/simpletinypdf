using System;
using System.IO;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class ImageTests
    {
        [Fact]
        public void FromBytes_ParsesJpegDimensions()
        {
            var jpegData = TestHelper.CreateTestJpeg(32, 16);
            var image = PdfImage.FromBytes(jpegData);
            Assert.Equal(32, image.PixelWidth);
            Assert.Equal(16, image.PixelHeight);
        }

        [Fact]
        public void FromBytes_InvalidData_Throws()
        {
            Assert.Throws<ArgumentException>(() => PdfImage.FromBytes(new byte[] { 0, 1, 2 }));
        }

        [Fact]
        public void FromStream_Works()
        {
            var jpegData = TestHelper.CreateTestJpeg(24, 24);
            using (var ms = new MemoryStream(jpegData))
            {
                var image = PdfImage.FromStream(ms);
                Assert.Equal(24, image.PixelWidth);
                Assert.Equal(24, image.PixelHeight);
            }
        }

        [Fact]
        public void FromFile_Works()
        {
            var jpegData = TestHelper.CreateTestJpeg(16, 16);
            var tempPath = Path.Combine(Path.GetTempPath(), "simpletinypdf_test.jpg");
            try
            {
                File.WriteAllBytes(tempPath, jpegData);
                var image = PdfImage.FromFile(tempPath);
                Assert.Equal(16, image.PixelWidth);
                Assert.Equal(16, image.PixelHeight);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public void DrawImage_RendersInPdf()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: 8x8 red JPEG renders on page");
            var jpegData = TestHelper.CreateTestJpeg(64, 64);
            var image = PdfImage.FromBytes(jpegData);
            doc.AddImage(image);
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/jpeg-basic-red-square");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/jpeg-basic-red-square");
            // Check center of where image should be (~200pt from left, ~200pt from top at 150dpi)
            int cx = (int)(200 * 150 / 72.0);
            int cy = (int)(200 * 150 / 72.0);
            // The test JPEG is red, so we expect red-ish pixels
            var pixel = bitmap.GetPixel(cx, cy);
            Assert.True(pixel.Red > 150, $"Expected red-ish pixel, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawImage_MultiplePages_SameImage()
        {
            var doc = new PdfDocument();
            var jpegData = TestHelper.CreateTestJpeg(32, 32);
            var image = PdfImage.FromBytes(jpegData);
            doc.AddImage(image);

            var page1 = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page1, "Verify: same image renders on multiple pages");
            page1.DrawImage(image, 50, 50, 100, 100);

            var page2 = doc.AddPage(PageSize.A4);
            page2.DrawImage(image, 100, 100, 150, 150);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/jpeg-on-multiple-pages");
            Assert.Equal(2, TestHelper.GetPageCount(bytes));

            var bmp1 = TestHelper.RasterizePage(bytes, "Images/jpeg-on-multiple-pages", 0);
            var bmp2 = TestHelper.RasterizePage(bytes, "Images/jpeg-on-multiple-pages", 1);
            // Page 1: image at (50,50) 100x100 — center at (100, 100) → red pixel expected
            int cx1 = (int)(100 * 150 / 72.0), cy1 = (int)(100 * 150 / 72.0);
            var px1 = bmp1.GetPixel(cx1, cy1);
            Assert.True(px1.Red > 150, $"Page 1: expected red-ish pixel at image center, got ({px1.Red},{px1.Green},{px1.Blue})");
            // Page 2: image at (100,100) 150x150 — center at (175, 175) → red pixel expected
            int cx2 = (int)(175 * 150 / 72.0), cy2 = (int)(175 * 150 / 72.0);
            var px2 = bmp2.GetPixel(cx2, cy2);
            Assert.True(px2.Red > 150, $"Page 2: expected red-ish pixel at image center, got ({px2.Red},{px2.Green},{px2.Blue})");
            bmp1.Dispose();
            bmp2.Dispose();
        }

        [Fact]
        public void DrawImage_WithOtherContent()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: image renders alongside text content");

            page.DrawText("Image below:", 50, 30, PdfFont.HelveticaBold, 16);

            var jpegData = TestHelper.CreateTestJpeg(48, 48);
            var image = PdfImage.FromBytes(jpegData);
            doc.AddImage(image);
            page.DrawImage(image, 50, 60, 150, 150);

            page.DrawText("Image above", 50, 230, PdfFont.Helvetica, 12);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/jpeg-with-text-overlay");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/jpeg-with-text-overlay");
            // Text "Image below:" should be visible near top
            int textY = (int)(30 * 150 / 72.0);
            bool foundText = false;
            for (int x = (int)(50 * 150 / 72.0); x < (int)(200 * 150 / 72.0); x++)
            {
                var p = bitmap.GetPixel(x, textY + 10);
                if (p.Red < 200 || p.Green < 200 || p.Blue < 200) { foundText = true; break; }
            }
            Assert.True(foundText, "Expected 'Image below:' text to be visible");
            // Image center at (125, 135) should be red
            int imgCx = (int)(125 * 150 / 72.0), imgCy = (int)(135 * 150 / 72.0);
            var imgPx = bitmap.GetPixel(imgCx, imgCy);
            Assert.True(imgPx.Red > 150, $"Expected red image at center, got ({imgPx.Red},{imgPx.Green},{imgPx.Blue})");
            // Text below image should also be visible — scan a wider Y range
            // DrawText at Y=230 with fontSize=12 → baseline at Height-230-12 in PDF coords
            // In pixel coords, text is roughly at Y=230pt → ~479px, spanning several pixels
            bool foundBelowText = false;
            for (int scanY = (int)(228 * 150 / 72.0); scanY < (int)(245 * 150 / 72.0) && !foundBelowText; scanY++)
            {
                for (int x = (int)(50 * 150 / 72.0); x < (int)(200 * 150 / 72.0); x++)
                {
                    if (scanY >= 0 && scanY < bitmap.Height)
                    {
                        var p = bitmap.GetPixel(x, scanY);
                        if (p.Red < 200 || p.Green < 200 || p.Blue < 200) { foundBelowText = true; break; }
                    }
                }
            }
            Assert.True(foundBelowText, "Expected 'Image above' text below the image");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawImage_QuadrantColors_NotRotated()
        {
            // Uses a JPEG with four distinct colored quadrants:
            //   Top-left: Red, Top-right: Green, Bottom-left: Blue, Bottom-right: Yellow
            // This catches rotation, mirroring, and flipping bugs.
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: quadrant image colors in correct positions");
            var jpegData = TestHelper.CreateQuadrantJpeg(100, 100);
            var image = PdfImage.FromBytes(jpegData);
            doc.AddImage(image);
            // Draw at (100, 100) with size 200x200pt
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/jpeg-four-color-quadrants");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/jpeg-four-color-quadrants");

            // Sample the center of each quadrant in the rendered output
            // Image spans from (100,100) to (300,300) in page coords
            // Top-left quadrant center: (150, 150)
            int tlx = TestHelper.PtToPx(150), tly = TestHelper.PtToPx(150);
            var tl = bitmap.GetPixel(tlx, tly);
            Assert.True(tl.Red > 180 && tl.Green < 80 && tl.Blue < 80,
                $"Top-left should be red, got ({tl.Red},{tl.Green},{tl.Blue})");

            // Top-right quadrant center: (250, 150)
            int trx = TestHelper.PtToPx(250), try_ = TestHelper.PtToPx(150);
            var tr = bitmap.GetPixel(trx, try_);
            Assert.True(tr.Green > 100 && tr.Red < 80 && tr.Blue < 80,
                $"Top-right should be green, got ({tr.Red},{tr.Green},{tr.Blue})");

            // Bottom-left quadrant center: (150, 250)
            int blx = TestHelper.PtToPx(150), bly = TestHelper.PtToPx(250);
            var bl = bitmap.GetPixel(blx, bly);
            Assert.True(bl.Blue > 180 && bl.Red < 80 && bl.Green < 80,
                $"Bottom-left should be blue, got ({bl.Red},{bl.Green},{bl.Blue})");

            // Bottom-right quadrant center: (250, 250)
            int brx = TestHelper.PtToPx(250), bry = TestHelper.PtToPx(250);
            var br = bitmap.GetPixel(brx, bry);
            Assert.True(br.Red > 180 && br.Green > 180 && br.Blue < 80,
                $"Bottom-right should be yellow, got ({br.Red},{br.Green},{br.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void DrawImage_NonSquareAspectRatio_RendersCorrectly()
        {
            // Create a wide landscape JPEG (200x100) with quadrant colors
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: landscape image renders correctly");
            var jpegData = TestHelper.CreateQuadrantJpeg(200, 100);
            var image = PdfImage.FromBytes(jpegData);
            Assert.Equal(200, image.PixelWidth);
            Assert.Equal(100, image.PixelHeight);
            doc.AddImage(image);

            // Draw at native aspect ratio: 300x150pt
            page.DrawImage(image, 50, 50, 300, 150);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/jpeg-landscape-orientation");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/jpeg-landscape-orientation");

            // Image spans (50,50)→(350,200). Check quadrant colors.
            // Top-left quadrant center: (125, 87.5)
            var tl = bitmap.GetPixel(TestHelper.PtToPx(125), TestHelper.PtToPx(87));
            Assert.True(tl.Red > 180 && tl.Green < 80,
                $"Landscape top-left should be red, got ({tl.Red},{tl.Green},{tl.Blue})");

            // Top-right quadrant center: (275, 87.5)
            var tr = bitmap.GetPixel(TestHelper.PtToPx(275), TestHelper.PtToPx(87));
            Assert.True(tr.Green > 100 && tr.Red < 80,
                $"Landscape top-right should be green, got ({tr.Red},{tr.Green},{tr.Blue})");

            // Bottom-left quadrant center: (125, 162.5)
            var bl = bitmap.GetPixel(TestHelper.PtToPx(125), TestHelper.PtToPx(162));
            Assert.True(bl.Blue > 180 && bl.Red < 80,
                $"Landscape bottom-left should be blue, got ({bl.Red},{bl.Green},{bl.Blue})");

            // Bottom-right quadrant center: (275, 162.5)
            var br = bitmap.GetPixel(TestHelper.PtToPx(275), TestHelper.PtToPx(162));
            Assert.True(br.Red > 180 && br.Green > 180,
                $"Landscape bottom-right should be yellow, got ({br.Red},{br.Green},{br.Blue})");

            // Area outside image (right of image) should be white
            var outside = bitmap.GetPixel(TestHelper.PtToPx(370), TestHelper.PtToPx(125));
            Assert.True(outside.Red > 240 && outside.Green > 240 && outside.Blue > 240,
                $"Area outside image should be white, got ({outside.Red},{outside.Green},{outside.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void DrawImage_TallPortrait_RendersCorrectly()
        {
            // Create a tall portrait JPEG (100x200) with quadrant colors
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: portrait image renders correctly");
            var jpegData = TestHelper.CreateQuadrantJpeg(100, 200);
            var image = PdfImage.FromBytes(jpegData);
            Assert.Equal(100, image.PixelWidth);
            Assert.Equal(200, image.PixelHeight);
            doc.AddImage(image);

            // Draw at 150x300pt
            page.DrawImage(image, 50, 50, 150, 300);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/jpeg-portrait-orientation");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/jpeg-portrait-orientation");

            // Image spans (50,50)→(200,350). Check quadrant colors.
            // Top-left center: (87.5, 125)
            var tl = bitmap.GetPixel(TestHelper.PtToPx(87), TestHelper.PtToPx(125));
            Assert.True(tl.Red > 180 && tl.Green < 80,
                $"Portrait top-left should be red, got ({tl.Red},{tl.Green},{tl.Blue})");

            // Top-right center: (162, 125)
            var tr = bitmap.GetPixel(TestHelper.PtToPx(162), TestHelper.PtToPx(125));
            Assert.True(tr.Green > 100 && tr.Red < 80,
                $"Portrait top-right should be green, got ({tr.Red},{tr.Green},{tr.Blue})");

            // Bottom-left center: (87.5, 275)
            var bl = bitmap.GetPixel(TestHelper.PtToPx(87), TestHelper.PtToPx(275));
            Assert.True(bl.Blue > 180 && bl.Red < 80,
                $"Portrait bottom-left should be blue, got ({bl.Red},{bl.Green},{bl.Blue})");

            // Bottom-right center: (162, 275)
            var br = bitmap.GetPixel(TestHelper.PtToPx(162), TestHelper.PtToPx(275));
            Assert.True(br.Red > 180 && br.Green > 180,
                $"Portrait bottom-right should be yellow, got ({br.Red},{br.Green},{br.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void DrawImage_StretchedToNonNativeAspect_FillsEntireArea()
        {
            // Draw a square image into a wide rectangle — should stretch, not crop
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: image stretches to fill specified dimensions");
            var jpegData = TestHelper.CreateQuadrantJpeg(100, 100);
            var image = PdfImage.FromBytes(jpegData);
            doc.AddImage(image);

            // Draw 100x100 source into 400x100 — horizontally stretched
            page.DrawImage(image, 50, 50, 400, 100);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/jpeg-stretched-to-box");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/jpeg-stretched-to-box");

            // All four quadrants should still be visible even though stretched
            // Left side (red or blue) should have red on top, blue on bottom
            var topLeft = bitmap.GetPixel(TestHelper.PtToPx(100), TestHelper.PtToPx(70));
            Assert.True(topLeft.Red > 150,
                $"Stretched top-left should still be reddish, got ({topLeft.Red},{topLeft.Green},{topLeft.Blue})");

            var bottomRight = bitmap.GetPixel(TestHelper.PtToPx(350), TestHelper.PtToPx(130));
            Assert.True(bottomRight.Red > 150 && bottomRight.Green > 150,
                $"Stretched bottom-right should still be yellowish, got ({bottomRight.Red},{bottomRight.Green},{bottomRight.Blue})");

            // Entire draw area should be filled — no white gaps in the middle
            var midPixel = bitmap.GetPixel(TestHelper.PtToPx(250), TestHelper.PtToPx(100));
            bool isFilled = midPixel.Red > 50 || midPixel.Green > 50 || midPixel.Blue > 50;
            // The center might be a blend of colors due to stretching, but shouldn't be white
            Assert.True(!(midPixel.Red > 245 && midPixel.Green > 245 && midPixel.Blue > 245),
                $"Center of stretched image should not be white, got ({midPixel.Red},{midPixel.Green},{midPixel.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void DrawImage_EdgePositioning_ContentOnlyInDrawArea()
        {
            // Place image in bottom-right corner and verify nothing bleeds outside
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: image renders at page edge");
            var jpegData = TestHelper.CreateTestJpeg(64, 64);
            var image = PdfImage.FromBytes(jpegData);
            doc.AddImage(image);

            float imgW = 100, imgH = 100;
            float imgX = page.Width - imgW - 20; // 20pt from right edge
            float imgY = page.Height - imgH - 20; // 20pt from bottom

            page.DrawImage(image, imgX, imgY, imgW, imgH);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/jpeg-at-page-edge");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/jpeg-at-page-edge");

            // Center of image should be red
            int cx = TestHelper.PtToPx(imgX + imgW / 2), cy = TestHelper.PtToPx(imgY + imgH / 2);
            var center = bitmap.GetPixel(cx, cy);
            Assert.True(center.Red > 150,
                $"Image center should be red, got ({center.Red},{center.Green},{center.Blue})");

            // Above image (10pt above) should be white
            int aboveY = TestHelper.PtToPx(imgY - 10);
            if (aboveY > 0)
            {
                var above = bitmap.GetPixel(cx, aboveY);
                Assert.True(above.Red > 240 && above.Green > 240 && above.Blue > 240,
                    $"Area above image should be white, got ({above.Red},{above.Green},{above.Blue})");
            }

            bitmap.Dispose();
        }

        [Fact]
        public void DrawImage_GrayscaleJpeg_RendersCorrectly()
        {
            // Create a grayscale JPEG — components=1 should set /DeviceGray
            var jpegData = CreateGrayscaleJpeg(80, 80);
            var image = PdfImage.FromBytes(jpegData);
            Assert.Equal(80, image.PixelWidth);
            Assert.Equal(80, image.PixelHeight);
            Assert.Equal(1, image.ComponentCount);

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: grayscale JPEG renders correctly");
            doc.AddImage(image);
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/jpeg-grayscale");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/jpeg-grayscale");

            // Center of image should be a gray tone (R≈G≈B, not colorful)
            int cx = TestHelper.PtToPx(200), cy = TestHelper.PtToPx(200);
            var pixel = bitmap.GetPixel(cx, cy);
            // Gray means R, G, B are close to each other
            int maxDiff = System.Math.Max(
                System.Math.Abs(pixel.Red - pixel.Green),
                System.Math.Max(System.Math.Abs(pixel.Green - pixel.Blue),
                    System.Math.Abs(pixel.Red - pixel.Blue)));
            Assert.True(maxDiff < 30,
                $"Grayscale image should have R≈G≈B, got ({pixel.Red},{pixel.Green},{pixel.Blue}), diff={maxDiff}");
            // Should not be white (image has content)
            Assert.True(pixel.Red < 200,
                $"Grayscale image center should not be white, got R={pixel.Red}");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawImage_LargeJpeg_DimensionsParsedCorrectly()
        {
            // Use dimensions > 255 to catch byte-order bugs in JPEG header parsing
            // (dimensions are stored as big-endian 16-bit values)
            var jpegData = TestHelper.CreateTestJpeg(SkiaSharp.SKColors.Blue, 400, 300);
            var image = PdfImage.FromBytes(jpegData);
            Assert.Equal(400, image.PixelWidth);
            Assert.Equal(300, image.PixelHeight);

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: large dimension image renders correctly");
            doc.AddImage(image);
            page.DrawImage(image, 50, 50, 300, 225); // maintain 4:3 aspect

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/jpeg-large-dimensions");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/jpeg-large-dimensions");

            // Center should be blue
            int cx = TestHelper.PtToPx(200), cy = TestHelper.PtToPx(162);
            var pixel = bitmap.GetPixel(cx, cy);
            Assert.True(pixel.Blue > 180 && pixel.Red < 50 && pixel.Green < 50,
                $"Large image center should be blue, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawImage_VeryLargeJpeg_DimensionsParsedCorrectly()
        {
            // 1000x600 — both dimensions require two bytes in the JPEG header
            var jpegData = TestHelper.CreateTestJpeg(SkiaSharp.SKColors.Green, 1000, 600);
            var image = PdfImage.FromBytes(jpegData);
            Assert.Equal(1000, image.PixelWidth);
            Assert.Equal(600, image.PixelHeight);
        }

        [Fact]
        public void DrawImage_QuadrantColors_VerifyNoCropping()
        {
            // Draw image at exact size and verify ALL four edges have colored pixels
            // (cropping would cut off one or more edges)
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: image renders without cropping");
            var jpegData = TestHelper.CreateQuadrantJpeg(100, 100);
            var image = PdfImage.FromBytes(jpegData);
            doc.AddImage(image);
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/jpeg-no-crop-mode");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/jpeg-no-crop-mode");

            // Image spans (100,100)→(300,300)
            // Check near all four edges (5pt inset) for non-white content
            // Top edge — should have red (left half) or green (right half)
            int nearTop = TestHelper.PtToPx(107);
            var topLeft = bitmap.GetPixel(TestHelper.PtToPx(150), nearTop);
            Assert.True(topLeft.Red > 150 || topLeft.Green > 100,
                $"Near top-left edge should have color, got ({topLeft.Red},{topLeft.Green},{topLeft.Blue})");

            // Bottom edge — should have blue (left half) or yellow (right half)
            int nearBottom = TestHelper.PtToPx(293);
            var bottomRight = bitmap.GetPixel(TestHelper.PtToPx(250), nearBottom);
            Assert.True(bottomRight.Red > 150 || bottomRight.Blue > 150,
                $"Near bottom-right edge should have color, got ({bottomRight.Red},{bottomRight.Green},{bottomRight.Blue})");

            // Left edge — should have red (top half) or blue (bottom half)
            int nearLeft = TestHelper.PtToPx(107);
            var leftEdge = bitmap.GetPixel(nearLeft, TestHelper.PtToPx(200));
            Assert.True(leftEdge.Red > 150 || leftEdge.Blue > 150,
                $"Near left edge should have color, got ({leftEdge.Red},{leftEdge.Green},{leftEdge.Blue})");

            // Right edge — should have green (top half) or yellow (bottom half)
            int nearRight = TestHelper.PtToPx(293);
            var rightEdge = bitmap.GetPixel(nearRight, TestHelper.PtToPx(200));
            Assert.True(rightEdge.Green > 100 || rightEdge.Red > 150,
                $"Near right edge should have color, got ({rightEdge.Red},{rightEdge.Green},{rightEdge.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void FromBytes_TruncatedJpeg_Throws()
        {
            // A JPEG with valid SOI but truncated before SOF should throw
            var validJpeg = TestHelper.CreateTestJpeg(32, 32);
            // Truncate to just the SOI marker + a few bytes
            var truncated = new byte[10];
            Array.Copy(validJpeg, truncated, 10);
            Assert.Throws<ArgumentException>(() => PdfImage.FromBytes(truncated));
        }

        [Fact]
        public void FromBytes_NullData_Throws()
        {
            Assert.Throws<ArgumentException>(() => PdfImage.FromBytes(null));
        }

        [Fact]
        public void FromBytes_EmptyData_Throws()
        {
            Assert.Throws<ArgumentException>(() => PdfImage.FromBytes(new byte[0]));
        }

        [Fact]
        public void FromBytes_UnsupportedFormat_Throws()
        {
            // Neither JPEG nor PNG magic bytes
            Assert.Throws<ArgumentException>(() => PdfImage.FromBytes(
                new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })); // GIF header
        }

        [Fact]
        public void FromBytes_TruncatedPng_Throws()
        {
            // Valid PNG signature but no data after it
            Assert.Throws<ArgumentException>(() => PdfImage.FromBytes(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        }

        // ── Input Validation ─────────────────────────────────────

        [Fact]
        public void DrawImage_NullImage_Throws()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            Assert.Throws<ArgumentNullException>(() => page.DrawImage(null, 50, 50, 100, 100));
        }

        [Fact]
        public void DrawImage_NegativeWidth_Throws()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var image = PdfImage.FromBytes(TestHelper.CreateTestJpeg(8, 8));
            Assert.Throws<ArgumentException>(() => page.DrawImage(image, 50, 50, -100, 100));
        }

        [Fact]
        public void DrawImage_ZeroHeight_Throws()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var image = PdfImage.FromBytes(TestHelper.CreateTestJpeg(8, 8));
            Assert.Throws<ArgumentException>(() => page.DrawImage(image, 50, 50, 100, 0));
        }

        // ── EXIF Orientation ─────────────────────────────────────

        [Fact]
        public void ExifOrientation1_NoTransform()
        {
            // Orientation 1 = normal — quadrants should appear in standard positions
            var jpeg = TestHelper.CreateQuadrantJpeg(100, 100);
            var oriented = InjectExifOrientation(jpeg, 1);
            var image = PdfImage.FromBytes(oriented);
            Assert.Equal(100, image.PixelWidth);
            Assert.Equal(100, image.PixelHeight);

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: EXIF orientation 1 (normal) renders correctly");
            doc.AddImage(image);
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/exif-orientation-1-normal");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/exif-orientation-1-normal");

            // Same as normal quadrant test: TL=Red, TR=Green, BL=Blue, BR=Yellow
            AssertQuadrantColors(bitmap, 100, 100, 200, 200);
            bitmap.Dispose();
        }

        [Fact]
        public void ExifOrientation3_Rotate180()
        {
            // Orientation 3 = rotate 180°
            // Original: TL=Red, TR=Green, BL=Blue, BR=Yellow
            // After 180°: TL=Yellow, TR=Blue, BL=Green, BR=Red
            var jpeg = TestHelper.CreateQuadrantJpeg(100, 100);
            var oriented = InjectExifOrientation(jpeg, 3);
            var image = PdfImage.FromBytes(oriented);
            Assert.Equal(100, image.PixelWidth);
            Assert.Equal(100, image.PixelHeight);

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: EXIF orientation 3 (180\u00b0 rotation) renders correctly");
            doc.AddImage(image);
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/exif-orientation-3-rotate180");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/exif-orientation-3-rotate180");

            // After 180° rotation: TL=Yellow, TR=Blue, BL=Green, BR=Red
            var tl = bitmap.GetPixel(TestHelper.PtToPx(150), TestHelper.PtToPx(150));
            Assert.True(tl.Red > 180 && tl.Green > 180 && tl.Blue < 80,
                $"Rot180 TL should be yellow, got ({tl.Red},{tl.Green},{tl.Blue})");

            var tr = bitmap.GetPixel(TestHelper.PtToPx(250), TestHelper.PtToPx(150));
            Assert.True(tr.Blue > 180 && tr.Red < 80 && tr.Green < 80,
                $"Rot180 TR should be blue, got ({tr.Red},{tr.Green},{tr.Blue})");

            var bl = bitmap.GetPixel(TestHelper.PtToPx(150), TestHelper.PtToPx(250));
            Assert.True(bl.Green > 100 && bl.Red < 80 && bl.Blue < 80,
                $"Rot180 BL should be green, got ({bl.Red},{bl.Green},{bl.Blue})");

            var br = bitmap.GetPixel(TestHelper.PtToPx(250), TestHelper.PtToPx(250));
            Assert.True(br.Red > 180 && br.Green < 80 && br.Blue < 80,
                $"Rot180 BR should be red, got ({br.Red},{br.Green},{br.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void ExifOrientation6_Rotate90CW()
        {
            // Orientation 6 = rotate 90° CW
            // Original (100x100): TL=Red, TR=Green, BL=Blue, BR=Yellow
            // After 90° CW: the display image is rotated, so:
            //   Display TL = original BL = Blue
            //   Display TR = original TL = Red
            //   Display BL = original BR = Yellow
            //   Display BR = original TR = Green
            // Orientations 5-8 swap PixelWidth/Height, so 100x100 stays 100x100
            var jpeg = TestHelper.CreateQuadrantJpeg(100, 100);
            var oriented = InjectExifOrientation(jpeg, 6);
            var image = PdfImage.FromBytes(oriented);
            Assert.Equal(100, image.PixelWidth); // swapped for 5-8, but original is square
            Assert.Equal(100, image.PixelHeight);

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: EXIF orientation 6 (90\u00b0 CW rotation) renders correctly");
            doc.AddImage(image);
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/exif-orientation-6-rotate90");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/exif-orientation-6-rotate90");

            // After 90° CW rotation:
            var tl = bitmap.GetPixel(TestHelper.PtToPx(150), TestHelper.PtToPx(150));
            Assert.True(tl.Blue > 180 && tl.Red < 80 && tl.Green < 80,
                $"Rot90CW TL should be blue, got ({tl.Red},{tl.Green},{tl.Blue})");

            var tr = bitmap.GetPixel(TestHelper.PtToPx(250), TestHelper.PtToPx(150));
            Assert.True(tr.Red > 180 && tr.Green < 80 && tr.Blue < 80,
                $"Rot90CW TR should be red, got ({tr.Red},{tr.Green},{tr.Blue})");

            var bl = bitmap.GetPixel(TestHelper.PtToPx(150), TestHelper.PtToPx(250));
            Assert.True(bl.Red > 180 && bl.Green > 180 && bl.Blue < 80,
                $"Rot90CW BL should be yellow, got ({bl.Red},{bl.Green},{bl.Blue})");

            var br = bitmap.GetPixel(TestHelper.PtToPx(250), TestHelper.PtToPx(250));
            Assert.True(br.Green > 100 && br.Red < 80 && br.Blue < 80,
                $"Rot90CW BR should be green, got ({br.Red},{br.Green},{br.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void ExifOrientation8_Rotate90CCW()
        {
            // Orientation 8 = rotate 90° CCW
            // After 90° CCW:
            //   Display TL = original TR = Green
            //   Display TR = original BR = Yellow
            //   Display BL = original TL = Red
            //   Display BR = original BL = Blue
            var jpeg = TestHelper.CreateQuadrantJpeg(100, 100);
            var oriented = InjectExifOrientation(jpeg, 8);
            var image = PdfImage.FromBytes(oriented);

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: EXIF orientation 8 (270\u00b0 CW rotation) renders correctly");
            doc.AddImage(image);
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/exif-orientation-8-rotate270");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/exif-orientation-8-rotate270");

            var tl = bitmap.GetPixel(TestHelper.PtToPx(150), TestHelper.PtToPx(150));
            Assert.True(tl.Green > 100 && tl.Red < 80 && tl.Blue < 80,
                $"Rot90CCW TL should be green, got ({tl.Red},{tl.Green},{tl.Blue})");

            var tr = bitmap.GetPixel(TestHelper.PtToPx(250), TestHelper.PtToPx(150));
            Assert.True(tr.Red > 180 && tr.Green > 180 && tr.Blue < 80,
                $"Rot90CCW TR should be yellow, got ({tr.Red},{tr.Green},{tr.Blue})");

            var bl = bitmap.GetPixel(TestHelper.PtToPx(150), TestHelper.PtToPx(250));
            Assert.True(bl.Red > 180 && bl.Green < 80 && bl.Blue < 80,
                $"Rot90CCW BL should be red, got ({bl.Red},{bl.Green},{bl.Blue})");

            var br = bitmap.GetPixel(TestHelper.PtToPx(250), TestHelper.PtToPx(250));
            Assert.True(br.Blue > 180 && br.Red < 80 && br.Green < 80,
                $"Rot90CCW BR should be blue, got ({br.Red},{br.Green},{br.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void ExifOrientation6_NonSquare_SwapsDimensions()
        {
            // A 200x100 JPEG with orientation 6 (90° CW) should report 100x200 display dimensions
            var jpeg = TestHelper.CreateQuadrantJpeg(200, 100);
            var oriented = InjectExifOrientation(jpeg, 6);
            var image = PdfImage.FromBytes(oriented);
            // Raw is 200x100, but orientation 6 swaps → display is 100x200
            Assert.Equal(100, image.PixelWidth);
            Assert.Equal(200, image.PixelHeight);
        }

        // ── PNG Tests ────────────────────────────────────────────

        [Fact]
        public void PngImage_RgbSolidColor()
        {
            var pngData = CreateTestPng(SkiaSharp.SKColors.Red, 50, 50);
            var image = PdfImage.FromBytes(pngData);
            Assert.Equal(50, image.PixelWidth);
            Assert.Equal(50, image.PixelHeight);

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: solid red PNG renders on page");
            doc.AddImage(image);
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/png-solid-red");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/png-solid-red");

            int cx = TestHelper.PtToPx(200), cy = TestHelper.PtToPx(200);
            var pixel = bitmap.GetPixel(cx, cy);
            Assert.True(pixel.Red > 180 && pixel.Green < 50 && pixel.Blue < 50,
                $"PNG center should be red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
            bitmap.Dispose();
        }

        [Fact]
        public void PngImage_WithAlpha_TransparentRegion()
        {
            // Create PNG with left half opaque red, right half transparent
            var pngData = CreateHalfTransparentPng(100, 100);
            var image = PdfImage.FromBytes(pngData);
            Assert.Equal(100, image.PixelWidth);
            Assert.Equal(100, image.PixelHeight);

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: PNG with alpha transparency renders correctly");
            doc.AddImage(image);
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/png-with-alpha-transparency");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/png-with-alpha-transparency");

            // Left half should be red (opaque)
            int leftX = TestHelper.PtToPx(150), centerY = TestHelper.PtToPx(200);
            var leftPixel = bitmap.GetPixel(leftX, centerY);
            Assert.True(leftPixel.Red > 180 && leftPixel.Green < 50,
                $"PNG opaque left should be red, got ({leftPixel.Red},{leftPixel.Green},{leftPixel.Blue})");

            // Right half should be white (transparent over white page background)
            int rightX = TestHelper.PtToPx(250);
            var rightPixel = bitmap.GetPixel(rightX, centerY);
            Assert.True(rightPixel.Red > 230 && rightPixel.Green > 230 && rightPixel.Blue > 230,
                $"PNG transparent right should be white, got ({rightPixel.Red},{rightPixel.Green},{rightPixel.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void PngImage_Grayscale()
        {
            var pngData = CreateGrayscalePng(60, 60);
            var image = PdfImage.FromBytes(pngData);
            Assert.Equal(60, image.PixelWidth);
            Assert.Equal(60, image.PixelHeight);
            Assert.Equal(1, image.ComponentCount);

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: grayscale PNG renders correctly");
            doc.AddImage(image);
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/png-grayscale");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/png-grayscale");

            int cx = TestHelper.PtToPx(200), cy = TestHelper.PtToPx(200);
            var pixel = bitmap.GetPixel(cx, cy);
            // Gray means R≈G≈B
            int maxDiff = Math.Max(Math.Abs(pixel.Red - pixel.Green),
                Math.Max(Math.Abs(pixel.Green - pixel.Blue), Math.Abs(pixel.Red - pixel.Blue)));
            Assert.True(maxDiff < 30,
                $"Grayscale PNG should have R≈G≈B, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
            Assert.True(pixel.Red < 200,
                $"Grayscale PNG should not be white, got R={pixel.Red}");
            bitmap.Dispose();
        }

        [Fact]
        public void PngImage_DimensionsParsed()
        {
            var pngData = CreateTestPng(SkiaSharp.SKColors.Blue, 320, 240);
            var image = PdfImage.FromBytes(pngData);
            Assert.Equal(320, image.PixelWidth);
            Assert.Equal(240, image.PixelHeight);
        }

        [Fact]
        public void PngImage_InvalidSignature_Throws()
        {
            // Valid-looking but incomplete PNG
            Assert.Throws<ArgumentException>(() => PdfImage.FromBytes(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 }));
        }

        [Fact]
        public void PngImage_QuadrantColors()
        {
            // Quadrant-color PNG to verify no rotation/flip
            var pngData = CreateQuadrantPng(100, 100);
            var image = PdfImage.FromBytes(pngData);

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: PNG quadrant image colors in correct positions");
            doc.AddImage(image);
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/png-four-color-quadrants");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/png-four-color-quadrants");

            AssertQuadrantColors(bitmap, 100, 100, 200, 200);
            bitmap.Dispose();
        }

        // ── Helpers ──────────────────────────────────────────────

        private void AssertQuadrantColors(SkiaSharp.SKBitmap bitmap,
            float imgX, float imgY, float imgW, float imgH)
        {
            // TL = Red
            var tl = bitmap.GetPixel(TestHelper.PtToPx(imgX + imgW * 0.25f), TestHelper.PtToPx(imgY + imgH * 0.25f));
            Assert.True(tl.Red > 180 && tl.Green < 80 && tl.Blue < 80,
                $"TL should be red, got ({tl.Red},{tl.Green},{tl.Blue})");
            // TR = Green
            var tr = bitmap.GetPixel(TestHelper.PtToPx(imgX + imgW * 0.75f), TestHelper.PtToPx(imgY + imgH * 0.25f));
            Assert.True(tr.Green > 100 && tr.Red < 80 && tr.Blue < 80,
                $"TR should be green, got ({tr.Red},{tr.Green},{tr.Blue})");
            // BL = Blue
            var bl = bitmap.GetPixel(TestHelper.PtToPx(imgX + imgW * 0.25f), TestHelper.PtToPx(imgY + imgH * 0.75f));
            Assert.True(bl.Blue > 180 && bl.Red < 80 && bl.Green < 80,
                $"BL should be blue, got ({bl.Red},{bl.Green},{bl.Blue})");
            // BR = Yellow
            var br = bitmap.GetPixel(TestHelper.PtToPx(imgX + imgW * 0.75f), TestHelper.PtToPx(imgY + imgH * 0.75f));
            Assert.True(br.Red > 180 && br.Green > 180 && br.Blue < 80,
                $"BR should be yellow, got ({br.Red},{br.Green},{br.Blue})");
        }

        /// <summary>
        /// Injects an EXIF APP1 segment with the specified orientation tag into a JPEG.
        /// Inserts the APP1 right after the SOI marker.
        /// </summary>
        private static byte[] InjectExifOrientation(byte[] jpeg, int orientation)
        {
            // Build a minimal EXIF APP1 segment
            // Structure: FF E1 [length] "Exif\0\0" [TIFF header] [IFD0 with orientation]
            var exifData = new byte[]
            {
                // TIFF header (little-endian)
                0x49, 0x49,       // "II" (little-endian)
                0x2A, 0x00,       // magic 42
                0x08, 0x00, 0x00, 0x00, // offset to IFD0 = 8

                // IFD0: 1 entry
                0x01, 0x00,       // entry count = 1

                // Entry: Orientation (tag 0x0112)
                0x12, 0x01,       // tag = 0x0112
                0x03, 0x00,       // type = SHORT
                0x01, 0x00, 0x00, 0x00, // count = 1
                (byte)orientation, 0x00, 0x00, 0x00, // value (LE short)

                // Next IFD offset = 0 (no more IFDs)
                0x00, 0x00, 0x00, 0x00
            };

            var exifHeader = new byte[] { 0x45, 0x78, 0x69, 0x66, 0x00, 0x00 }; // "Exif\0\0"
            int segmentDataLen = exifHeader.Length + exifData.Length;
            int segmentTotalLen = segmentDataLen + 2; // +2 for length field itself

            // Build: SOI + APP1 + rest of original JPEG (skip original SOI)
            var result = new byte[2 + 2 + 2 + segmentDataLen + jpeg.Length - 2];
            int pos = 0;

            // SOI
            result[pos++] = 0xFF;
            result[pos++] = 0xD8;

            // APP1 marker
            result[pos++] = 0xFF;
            result[pos++] = 0xE1;

            // Segment length (big-endian)
            result[pos++] = (byte)(segmentTotalLen >> 8);
            result[pos++] = (byte)(segmentTotalLen & 0xFF);

            // Exif header
            Buffer.BlockCopy(exifHeader, 0, result, pos, exifHeader.Length);
            pos += exifHeader.Length;

            // TIFF data
            Buffer.BlockCopy(exifData, 0, result, pos, exifData.Length);
            pos += exifData.Length;

            // Rest of original JPEG (after SOI)
            Buffer.BlockCopy(jpeg, 2, result, pos, jpeg.Length - 2);

            return result;
        }

        private static byte[] CreateTestPng(SkiaSharp.SKColor color, int width, int height)
        {
            using (var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(width, height)))
            {
                surface.Canvas.Clear(color);
                using (var img = surface.Snapshot())
                using (var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                {
                    return data.ToArray();
                }
            }
        }

        private static byte[] CreateQuadrantPng(int width, int height)
        {
            using (var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(width, height)))
            {
                var canvas = surface.Canvas;
                int hw = width / 2, hh = height / 2;
                canvas.DrawRect(new SkiaSharp.SKRect(0, 0, hw, hh),
                    new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Red });
                canvas.DrawRect(new SkiaSharp.SKRect(hw, 0, width, hh),
                    new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Green });
                canvas.DrawRect(new SkiaSharp.SKRect(0, hh, hw, height),
                    new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Blue });
                canvas.DrawRect(new SkiaSharp.SKRect(hw, hh, width, height),
                    new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Yellow });
                using (var img = surface.Snapshot())
                using (var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                {
                    return data.ToArray();
                }
            }
        }

        private static byte[] CreateHalfTransparentPng(int width, int height)
        {
            using (var surface = SkiaSharp.SKSurface.Create(
                new SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul)))
            {
                var canvas = surface.Canvas;
                canvas.Clear(SkiaSharp.SKColors.Transparent);
                // Left half = opaque red
                canvas.DrawRect(new SkiaSharp.SKRect(0, 0, width / 2, height),
                    new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Red });
                // Right half stays transparent
                using (var img = surface.Snapshot())
                using (var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                {
                    return data.ToArray();
                }
            }
        }

        private static byte[] CreateGrayscalePng(int width, int height)
        {
            using (var surface = SkiaSharp.SKSurface.Create(
                new SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Gray8)))
            {
                surface.Canvas.Clear(new SkiaSharp.SKColor(128, 128, 128));
                using (var img = surface.Snapshot())
                using (var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                {
                    return data.ToArray();
                }
            }
        }

        /// <summary>
        /// Creates a grayscale JPEG for testing /DeviceGray color space.
        /// </summary>
        private static byte[] CreateGrayscaleJpeg(int width, int height)
        {
            using (var surface = SkiaSharp.SKSurface.Create(
                new SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Gray8)))
            {
                surface.Canvas.Clear(new SkiaSharp.SKColor(128, 128, 128));
                using (var img = surface.Snapshot())
                using (var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90))
                {
                    return data.ToArray();
                }
            }
        }
    }
}
