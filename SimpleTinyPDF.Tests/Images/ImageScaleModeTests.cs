using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class ImageScaleModeTests
    {
        [Fact]
        public void Stretch_FillsEntireArea()
        {
            // A 200x100 landscape image drawn into a 200x200 square with Stretch
            // should fill the entire square (distorted).
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: image stretches to fill entire area");
            var jpeg = TestHelper.CreateQuadrantJpeg(200, 100);
            var image = PdfImage.FromBytes(jpeg);
            doc.AddImage(image);

            page.DrawImage(image, 100, 100, 200, 200, scaleMode: ImageScaleMode.Stretch);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/scale-mode-stretch");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/scale-mode-stretch");

            // All four quadrants visible; corners should not be white
            var tl = bitmap.GetPixel(TestHelper.PtToPx(120), TestHelper.PtToPx(120));
            Assert.True(tl.Red > 150, $"Stretch TL should be red, got ({tl.Red},{tl.Green},{tl.Blue})");

            var br = bitmap.GetPixel(TestHelper.PtToPx(280), TestHelper.PtToPx(280));
            Assert.True(br.Red > 150 && br.Green > 150,
                $"Stretch BR should be yellow, got ({br.Red},{br.Green},{br.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void Fit_LandscapeImage_InSquareBox_HasVerticalWhitespace()
        {
            // A 200x100 landscape image fit into a 200x200 square:
            // Image scales to 200x100 (width-limited), centered vertically.
            // Top 50pt and bottom 50pt of the box should be white (letterboxed).
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: landscape image fits within box with letterboxing");
            var jpeg = TestHelper.CreateTestJpeg(200, 100);
            var image = PdfImage.FromBytes(jpeg);
            doc.AddImage(image);

            page.DrawImage(image, 100, 100, 200, 200, scaleMode: ImageScaleMode.Fit);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/scale-mode-fit-landscape");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/scale-mode-fit-landscape");

            // Center of box should be the image (red)
            var center = bitmap.GetPixel(TestHelper.PtToPx(200), TestHelper.PtToPx(200));
            Assert.True(center.Red > 150,
                $"Fit center should be red image, got ({center.Red},{center.Green},{center.Blue})");

            // Top of box (within the letterbox band) should be white
            var topBand = bitmap.GetPixel(TestHelper.PtToPx(200), TestHelper.PtToPx(110));
            Assert.True(topBand.Red > 240 && topBand.Green > 240 && topBand.Blue > 240,
                $"Fit top band should be white, got ({topBand.Red},{topBand.Green},{topBand.Blue})");

            // Bottom of box (within the letterbox band) should be white
            var bottomBand = bitmap.GetPixel(TestHelper.PtToPx(200), TestHelper.PtToPx(290));
            Assert.True(bottomBand.Red > 240 && bottomBand.Green > 240 && bottomBand.Blue > 240,
                $"Fit bottom band should be white, got ({bottomBand.Red},{bottomBand.Green},{bottomBand.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void Fit_PortraitImage_InSquareBox_HasHorizontalWhitespace()
        {
            // A 100x200 portrait image fit into a 200x200 square:
            // Image scales to 100x200 (height-limited), centered horizontally.
            // Left 50pt and right 50pt of the box should be white (pillarboxed).
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: portrait image fits within box with pillarboxing");
            var jpeg = TestHelper.CreateTestJpeg(100, 200);
            var image = PdfImage.FromBytes(jpeg);
            doc.AddImage(image);

            page.DrawImage(image, 100, 100, 200, 200, scaleMode: ImageScaleMode.Fit);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/scale-mode-fit-portrait");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/scale-mode-fit-portrait");

            // Center should be image (red)
            var center = bitmap.GetPixel(TestHelper.PtToPx(200), TestHelper.PtToPx(200));
            Assert.True(center.Red > 150,
                $"Fit center should be red image, got ({center.Red},{center.Green},{center.Blue})");

            // Left side of box (within the pillarbox band) should be white
            var leftBand = bitmap.GetPixel(TestHelper.PtToPx(110), TestHelper.PtToPx(200));
            Assert.True(leftBand.Red > 240 && leftBand.Green > 240 && leftBand.Blue > 240,
                $"Fit left band should be white, got ({leftBand.Red},{leftBand.Green},{leftBand.Blue})");

            // Right side of box (within the pillarbox band) should be white
            var rightBand = bitmap.GetPixel(TestHelper.PtToPx(290), TestHelper.PtToPx(200));
            Assert.True(rightBand.Red > 240 && rightBand.Green > 240 && rightBand.Blue > 240,
                $"Fit right band should be white, got ({rightBand.Red},{rightBand.Green},{rightBand.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void Fit_PreservesAspectRatio_QuadrantColors()
        {
            // A 200x100 quadrant image fit into a 300x300 square:
            // Scales to 300x150, centered at y=175 (75pt offset top/bottom).
            // All four quadrants should be visible and undistorted.
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: quadrant image fitted preserving aspect ratio");
            var jpeg = TestHelper.CreateQuadrantJpeg(200, 100);
            var image = PdfImage.FromBytes(jpeg);
            doc.AddImage(image);

            page.DrawImage(image, 50, 50, 300, 300, scaleMode: ImageScaleMode.Fit);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/scale-mode-fit-quadrant");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/scale-mode-fit-quadrant");

            // Image is 300x150, centered in 300x300 box starting at (50,50)
            // Image occupies (50, 125) to (350, 275)
            // TL quadrant center: (125, 162)
            var tl = bitmap.GetPixel(TestHelper.PtToPx(125), TestHelper.PtToPx(162));
            Assert.True(tl.Red > 150 && tl.Green < 80,
                $"Fit TL should be red, got ({tl.Red},{tl.Green},{tl.Blue})");

            // TR quadrant center: (275, 162)
            var tr = bitmap.GetPixel(TestHelper.PtToPx(275), TestHelper.PtToPx(162));
            Assert.True(tr.Green > 80 && tr.Red < 80,
                $"Fit TR should be green, got ({tr.Red},{tr.Green},{tr.Blue})");

            // BL quadrant center: (125, 237)
            var bl = bitmap.GetPixel(TestHelper.PtToPx(125), TestHelper.PtToPx(237));
            Assert.True(bl.Blue > 150 && bl.Red < 80,
                $"Fit BL should be blue, got ({bl.Red},{bl.Green},{bl.Blue})");

            // BR quadrant center: (275, 237)
            var br = bitmap.GetPixel(TestHelper.PtToPx(275), TestHelper.PtToPx(237));
            Assert.True(br.Red > 150 && br.Green > 150,
                $"Fit BR should be yellow, got ({br.Red},{br.Green},{br.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void Fill_LandscapeImage_InSquareBox_CropsTopAndBottom()
        {
            // A 200x100 quadrant image drawn into a 200x200 square with Fill:
            // Image scales to 400x200 (height-limited → scale by width/pixelW gives 1,
            // but we need height filled, so scale = 200/100 = 2 → 400x200).
            // Wait, let's re-check: imgAspect = 200/100 = 2, boxAspect = 200/200 = 1.
            // imgAspect > boxAspect → Fill picks height/pixelHeight = 200/100 = 2.
            // drawW = 200*2 = 400, drawH = 100*2 = 200.
            // Centered: drawX = 100 + (200-400)/2 = 0, drawY = 100 + (200-200)/2 = 100.
            // The image overflows horizontally and is clipped to the 200x200 box.
            // So left/right edges of the image are clipped.
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: landscape image fills area with cropping");
            var jpeg = TestHelper.CreateQuadrantJpeg(200, 100);
            var image = PdfImage.FromBytes(jpeg);
            doc.AddImage(image);

            page.DrawImage(image, 100, 100, 200, 200, scaleMode: ImageScaleMode.Fill);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/scale-mode-fill-landscape");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/scale-mode-fill-landscape");

            // The box at (100,100)-(300,300) should be completely filled (no white)
            var topLeft = bitmap.GetPixel(TestHelper.PtToPx(110), TestHelper.PtToPx(110));
            Assert.True(!(topLeft.Red > 245 && topLeft.Green > 245 && topLeft.Blue > 245),
                $"Fill top-left corner should not be white, got ({topLeft.Red},{topLeft.Green},{topLeft.Blue})");

            var bottomRight = bitmap.GetPixel(TestHelper.PtToPx(290), TestHelper.PtToPx(290));
            Assert.True(!(bottomRight.Red > 245 && bottomRight.Green > 245 && bottomRight.Blue > 245),
                $"Fill bottom-right corner should not be white, got ({bottomRight.Red},{bottomRight.Green},{bottomRight.Blue})");

            // Center should be non-white (image content)
            var center = bitmap.GetPixel(TestHelper.PtToPx(200), TestHelper.PtToPx(200));
            Assert.True(!(center.Red > 245 && center.Green > 245 && center.Blue > 245),
                $"Fill center should not be white, got ({center.Red},{center.Green},{center.Blue})");

            // Outside the box should be white
            var outside = bitmap.GetPixel(TestHelper.PtToPx(90), TestHelper.PtToPx(200));
            Assert.True(outside.Red > 240 && outside.Green > 240 && outside.Blue > 240,
                $"Outside fill box should be white, got ({outside.Red},{outside.Green},{outside.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void Fill_PortraitImage_InSquareBox_CropsLeftAndRight()
        {
            // A 100x200 portrait quadrant image drawn into a 200x200 square with Fill:
            // imgAspect = 0.5, boxAspect = 1. imgAspect < boxAspect → scale = width/pixelWidth = 2.
            // drawW = 100*2 = 200, drawH = 200*2 = 400. Centered: drawY = 100+(200-400)/2 = 0.
            // The image overflows vertically and is clipped.
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: portrait image fills area with cropping");
            var jpeg = TestHelper.CreateQuadrantJpeg(100, 200);
            var image = PdfImage.FromBytes(jpeg);
            doc.AddImage(image);

            page.DrawImage(image, 100, 200, 200, 200, scaleMode: ImageScaleMode.Fill);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/scale-mode-fill-portrait");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/scale-mode-fill-portrait");

            // The box at (100,200)-(300,400) should be completely filled
            var topLeft = bitmap.GetPixel(TestHelper.PtToPx(110), TestHelper.PtToPx(210));
            Assert.True(!(topLeft.Red > 245 && topLeft.Green > 245 && topLeft.Blue > 245),
                $"Fill TL should not be white, got ({topLeft.Red},{topLeft.Green},{topLeft.Blue})");

            var bottomRight = bitmap.GetPixel(TestHelper.PtToPx(290), TestHelper.PtToPx(390));
            Assert.True(!(bottomRight.Red > 245 && bottomRight.Green > 245 && bottomRight.Blue > 245),
                $"Fill BR should not be white, got ({bottomRight.Red},{bottomRight.Green},{bottomRight.Blue})");

            // Outside the clipping box (above) should be white
            var above = bitmap.GetPixel(TestHelper.PtToPx(200), TestHelper.PtToPx(190));
            Assert.True(above.Red > 240 && above.Green > 240 && above.Blue > 240,
                $"Above fill box should be white, got ({above.Red},{above.Green},{above.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void Fill_ClipsOverflow_OutsideBoxIsWhite()
        {
            // Verify the clipping rect works: content outside the target box must not appear.
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: fill mode clips overflow");
            var jpeg = TestHelper.CreateTestJpeg(400, 100); // very wide
            var image = PdfImage.FromBytes(jpeg);
            doc.AddImage(image);

            // Draw into a small 100x100 box
            page.DrawImage(image, 200, 200, 100, 100, scaleMode: ImageScaleMode.Fill);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/scale-mode-fill-clipped");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/scale-mode-fill-clipped");

            // Inside the box should be red (the test JPEG is solid red)
            var inside = bitmap.GetPixel(TestHelper.PtToPx(250), TestHelper.PtToPx(250));
            Assert.True(inside.Red > 150,
                $"Inside fill box should be red, got ({inside.Red},{inside.Green},{inside.Blue})");

            // Just outside the box to the left should be white (clipped)
            var outsideLeft = bitmap.GetPixel(TestHelper.PtToPx(190), TestHelper.PtToPx(250));
            Assert.True(outsideLeft.Red > 240 && outsideLeft.Green > 240 && outsideLeft.Blue > 240,
                $"Left of fill box should be white (clipped), got ({outsideLeft.Red},{outsideLeft.Green},{outsideLeft.Blue})");

            // Just outside the box to the right should be white (clipped)
            var outsideRight = bitmap.GetPixel(TestHelper.PtToPx(310), TestHelper.PtToPx(250));
            Assert.True(outsideRight.Red > 240 && outsideRight.Green > 240 && outsideRight.Blue > 240,
                $"Right of fill box should be white (clipped), got ({outsideRight.Red},{outsideRight.Green},{outsideRight.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void Fit_SquareImage_InSquareBox_NoWhitespace()
        {
            // When aspect ratios match, Fit should fill the entire box with no letterboxing.
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: square image fitted in box");
            var jpeg = TestHelper.CreateTestJpeg(100, 100);
            var image = PdfImage.FromBytes(jpeg);
            doc.AddImage(image);

            page.DrawImage(image, 100, 100, 200, 200, scaleMode: ImageScaleMode.Fit);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/scale-mode-fit-square");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/scale-mode-fit-square");

            // All corners of the box should have image content (red)
            var tl = bitmap.GetPixel(TestHelper.PtToPx(110), TestHelper.PtToPx(110));
            Assert.True(tl.Red > 150, $"Fit square TL should be red, got ({tl.Red},{tl.Green},{tl.Blue})");

            var br = bitmap.GetPixel(TestHelper.PtToPx(290), TestHelper.PtToPx(290));
            Assert.True(br.Red > 150, $"Fit square BR should be red, got ({br.Red},{br.Green},{br.Blue})");

            bitmap.Dispose();
        }

        [Fact]
        public void DefaultScaleMode_IsStretch()
        {
            // Calling DrawImage without scaleMode should behave like Stretch
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: default scale mode behavior");
            var jpeg = TestHelper.CreateQuadrantJpeg(200, 100);
            var image = PdfImage.FromBytes(jpeg);
            doc.AddImage(image);

            // No scaleMode parameter — should default to Stretch
            page.DrawImage(image, 100, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/scale-mode-default");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/scale-mode-default");

            // Entire box should be filled (no white corners)
            var tl = bitmap.GetPixel(TestHelper.PtToPx(110), TestHelper.PtToPx(110));
            Assert.True(tl.Red > 150, $"Default TL should be red, got ({tl.Red},{tl.Green},{tl.Blue})");

            var br = bitmap.GetPixel(TestHelper.PtToPx(290), TestHelper.PtToPx(290));
            Assert.True(br.Red > 150 && br.Green > 150,
                $"Default BR should be yellow, got ({br.Red},{br.Green},{br.Blue})");

            bitmap.Dispose();
        }
    }
}
