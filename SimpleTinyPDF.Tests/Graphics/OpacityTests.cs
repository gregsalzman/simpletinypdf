using Xunit;
using SkiaSharp;

namespace SimpleTinyPDF.Tests
{
    public class OpacityTests
    {
        /// <summary>
        /// Finds the darkest pixel value (lowest R channel) in a region.
        /// For black text on white: fully opaque → near 0, semi-transparent → blended toward 255.
        /// </summary>
        private static byte DarkestRedInRegion(SKBitmap bitmap,
            int xMin, int xMax, int yMin, int yMax)
        {
            byte darkest = 255;
            xMax = System.Math.Min(xMax, bitmap.Width - 1);
            yMax = System.Math.Min(yMax, bitmap.Height - 1);
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Red < darkest) darkest = p.Red;
                }
            return darkest;
        }

        [Fact]
        public void DrawText_HalfOpacity_IsLighterThanFullOpacity()
        {
            // Render fully opaque black text
            var doc1 = new PdfDocument();
            var page1 = doc1.AddPage(PageSize.A4);
            TestHelper.AddDescription(page1, "Verify: text at 100% opacity is fully visible");
            page1.DrawText("OPACITY", 50, 50, PdfFont.Helvetica, 48);
            var bytes1 = doc1.ToArray();
            TestHelper.SavePdf(bytes1, "Graphics/opacity-text-100pct");
            var bmp1 = TestHelper.RasterizePage(bytes1, "Graphics/opacity-text-100pct");

            // Render half-opacity black text
            var doc2 = new PdfDocument();
            var page2 = doc2.AddPage(PageSize.A4);
            TestHelper.AddDescription(page2, "Verify: text at 50% opacity is semi-transparent");
            page2.DrawText("OPACITY", 50, 50, PdfFont.Helvetica, 48, opacity: 0.5f);
            var bytes2 = doc2.ToArray();
            TestHelper.SavePdf(bytes2, "Graphics/opacity-text-50pct");
            var bmp2 = TestHelper.RasterizePage(bytes2, "Graphics/opacity-text-50pct");

            int yMin = TestHelper.PtToPx(50);
            int yMax = TestHelper.PtToPx(50 + 48);
            int xMin = TestHelper.PtToPx(50);
            int xMax = TestHelper.PtToPx(250);

            byte darkestFull = DarkestRedInRegion(bmp1, xMin, xMax, yMin, yMax);
            byte darkestHalf = DarkestRedInRegion(bmp2, xMin, xMax, yMin, yMax);

            // Semi-transparent text should be noticeably lighter (higher R value)
            Assert.True(darkestHalf > darkestFull + 30,
                $"Half-opacity text ({darkestHalf}) should be significantly lighter than full ({darkestFull})");

            bmp1.Dispose();
            bmp2.Dispose();
        }

        [Fact]
        public void DrawText_FullOpacity_MatchesDefault()
        {
            // Default (no opacity parameter)
            var doc1 = new PdfDocument();
            var page1 = doc1.AddPage(PageSize.A4);
            TestHelper.AddDescription(page1, "Verify: default opacity text is fully visible");
            page1.DrawText("Test", 50, 50, PdfFont.Helvetica, 24);
            var bytes1 = doc1.ToArray();

            // Explicit opacity=1.0
            var doc2 = new PdfDocument();
            var page2 = doc2.AddPage(PageSize.A4);
            TestHelper.AddDescription(page2, "Verify: explicitly set 100% opacity matches default");
            page2.DrawText("Test", 50, 50, PdfFont.Helvetica, 24, opacity: 1f);
            var bytes2 = doc2.ToArray();

            TestHelper.SavePdf(bytes1, "Graphics/opacity-text-default");
            TestHelper.SavePdf(bytes2, "Graphics/opacity-text-explicit-100pct");
            var bmp1 = TestHelper.RasterizePage(bytes1, "Graphics/opacity-text-default");
            var bmp2 = TestHelper.RasterizePage(bytes2, "Graphics/opacity-text-explicit-100pct");

            int yMin = TestHelper.PtToPx(50);
            int yMax = TestHelper.PtToPx(74);
            int xMin = TestHelper.PtToPx(50);
            int xMax = TestHelper.PtToPx(150);

            byte darkest1 = DarkestRedInRegion(bmp1, xMin, xMax, yMin, yMax);
            byte darkest2 = DarkestRedInRegion(bmp2, xMin, xMax, yMin, yMax);

            // Should be identical or very close
            Assert.True(System.Math.Abs(darkest1 - darkest2) < 5,
                $"Full opacity ({darkest2}) should match default ({darkest1})");

            bmp1.Dispose();
            bmp2.Dispose();
        }

        [Fact]
        public void DrawText_TextBox_HalfOpacity_IsLighterThanFullOpacity()
        {
            var doc1 = new PdfDocument();
            var page1 = doc1.AddPage(PageSize.A4);
            TestHelper.AddDescription(page1, "Verify: textbox at 100% opacity is fully visible");
            page1.DrawText("Semi transparent text box content here", 50, 50,
                PdfFont.Helvetica, 24, width: 300);
            var bytes1 = doc1.ToArray();
            TestHelper.SavePdf(bytes1, "Graphics/opacity-textbox-100pct");
            var bmp1 = TestHelper.RasterizePage(bytes1, "Graphics/opacity-textbox-100pct");

            var doc2 = new PdfDocument();
            var page2 = doc2.AddPage(PageSize.A4);
            TestHelper.AddDescription(page2, "Verify: textbox at 30% opacity is very transparent");
            page2.DrawText("Semi transparent text box content here", 50, 50,
                PdfFont.Helvetica, 24, opacity: 0.3f, width: 300);
            var bytes2 = doc2.ToArray();
            TestHelper.SavePdf(bytes2, "Graphics/opacity-textbox-30pct");
            var bmp2 = TestHelper.RasterizePage(bytes2, "Graphics/opacity-textbox-30pct");

            int yMin = TestHelper.PtToPx(50);
            int yMax = TestHelper.PtToPx(100);
            int xMin = TestHelper.PtToPx(50);
            int xMax = TestHelper.PtToPx(300);

            byte darkestFull = DarkestRedInRegion(bmp1, xMin, xMax, yMin, yMax);
            byte darkestPartial = DarkestRedInRegion(bmp2, xMin, xMax, yMin, yMax);

            Assert.True(darkestPartial > darkestFull + 30,
                $"30% opacity text ({darkestPartial}) should be lighter than full ({darkestFull})");

            bmp1.Dispose();
            bmp2.Dispose();
        }

        /// <summary>
        /// Finds the darkest green channel value in a region. For a red image on white,
        /// full opacity → G near 0, semi-transparent → G blended toward 255.
        /// </summary>
        private static byte DarkestGreenInRegion(SKBitmap bitmap,
            int xMin, int xMax, int yMin, int yMax)
        {
            byte darkest = 255;
            xMax = System.Math.Min(xMax, bitmap.Width - 1);
            yMax = System.Math.Min(yMax, bitmap.Height - 1);
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Green < darkest) darkest = p.Green;
                }
            return darkest;
        }

        [Fact]
        public void DrawImage_HalfOpacity_IsLighterThanFullOpacity()
        {
            var jpegData = TestHelper.CreateTestJpeg(SKColors.Red, 100, 100);
            var image = PdfImage.FromBytes(jpegData);

            // Full opacity
            var doc1 = new PdfDocument();
            doc1.AddImage(image);
            var page1 = doc1.AddPage(PageSize.A4);
            TestHelper.AddDescription(page1, "Verify: image at 100% opacity is fully visible");
            page1.DrawImage(image, 50, 50, 200, 200);
            var bytes1 = doc1.ToArray();
            TestHelper.SavePdf(bytes1, "Graphics/opacity-image-100pct");
            var bmp1 = TestHelper.RasterizePage(bytes1, "Graphics/opacity-image-100pct");

            // Half opacity
            var doc2 = new PdfDocument();
            doc2.AddImage(image);
            var page2 = doc2.AddPage(PageSize.A4);
            TestHelper.AddDescription(page2, "Verify: image at 50% opacity is semi-transparent");
            page2.DrawImage(image, 50, 50, 200, 200, opacity: 0.5f);
            var bytes2 = doc2.ToArray();
            TestHelper.SavePdf(bytes2, "Graphics/opacity-image-50pct");
            var bmp2 = TestHelper.RasterizePage(bytes2, "Graphics/opacity-image-50pct");

            // Check a region in the center of the image
            int xMin = TestHelper.PtToPx(100);
            int xMax = TestHelper.PtToPx(200);
            int yMin = TestHelper.PtToPx(100);
            int yMax = TestHelper.PtToPx(200);

            // For a red image: full opacity has G near 0, half opacity has G blended toward white
            byte greenFull = DarkestGreenInRegion(bmp1, xMin, xMax, yMin, yMax);
            byte greenHalf = DarkestGreenInRegion(bmp2, xMin, xMax, yMin, yMax);

            Assert.True(greenFull < 50,
                $"Full opacity red image should have low green ({greenFull})");
            Assert.True(greenHalf > greenFull + 30,
                $"Half-opacity image green ({greenHalf}) should be lighter than full ({greenFull})");

            bmp1.Dispose();
            bmp2.Dispose();
        }

        [Fact]
        public void DrawRichText_MixedOpacity_BothSpansVisible()
        {
            // Render with second span at full opacity as baseline
            var doc1 = new PdfDocument();
            var page1 = doc1.AddPage(PageSize.A4);
            TestHelper.AddDescription(page1, "Verify: rich text at 100% opacity is fully visible");
            page1.DrawText(new[]
            {
                new TextSpan("AA ", PdfFont.Helvetica, 36, PdfColor.Black, opacity: 1f),
                new TextSpan("BB", PdfFont.Helvetica, 36, PdfColor.Black, opacity: 1f)
            }, 50, 50);
            var bytes1 = doc1.ToArray();
            TestHelper.SavePdf(bytes1, "Graphics/opacity-richtext-100pct");
            var bmp1 = TestHelper.RasterizePage(bytes1, "Graphics/opacity-richtext-100pct");

            // Render with second span at half opacity
            var doc2 = new PdfDocument();
            var page2 = doc2.AddPage(PageSize.A4);
            TestHelper.AddDescription(page2, "Verify: rich text spans with different opacities");
            page2.DrawText(new[]
            {
                new TextSpan("AA ", PdfFont.Helvetica, 36, PdfColor.Black, opacity: 1f),
                new TextSpan("BB", PdfFont.Helvetica, 36, PdfColor.Black, opacity: 0.5f)
            }, 50, 50);
            var bytes2 = doc2.ToArray();
            TestHelper.SavePdf(bytes2, "Graphics/opacity-richtext-mixed");
            var bmp2 = TestHelper.RasterizePage(bytes2, "Graphics/opacity-richtext-mixed");

            int yMin = TestHelper.PtToPx(50);
            int yMax = TestHelper.PtToPx(86);
            // "AA " at 36pt is ~80pt wide. "BB" region starts around x=130pt
            int xBBMin = TestHelper.PtToPx(130);
            int xBBMax = TestHelper.PtToPx(200);

            byte darkestAllFull = DarkestRedInRegion(bmp1, xBBMin, xBBMax, yMin, yMax);
            byte darkestHalfOpacity = DarkestRedInRegion(bmp2, xBBMin, xBBMax, yMin, yMax);

            // The half-opacity version should be lighter in the BB region
            Assert.True(darkestAllFull < 50, $"Full opacity BB should have dark pixels ({darkestAllFull})");
            Assert.True(darkestHalfOpacity > darkestAllFull + 20,
                $"Half-opacity BB ({darkestHalfOpacity}) should be lighter than full ({darkestAllFull})");

            bmp1.Dispose();
            bmp2.Dispose();
        }

        [Fact]
        public void DrawText_RichTextBox_WithOpacity_RendersVisibly()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: rich textbox with mixed opacity spans");

            var spans = new[]
            {
                new TextSpan("Opaque part ", PdfFont.Helvetica, 18, PdfColor.Black, opacity: 1f),
                new TextSpan("translucent part", PdfFont.Helvetica, 18, PdfColor.Black, opacity: 0.4f)
            };
            page.DrawText(spans, 50, 50, width: 300);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Graphics/opacity-richtextbox-mixed");
            var bmp = TestHelper.RasterizePage(bytes, "Graphics/opacity-richtextbox-mixed");

            int yMin = TestHelper.PtToPx(50);
            int yMax = TestHelper.PtToPx(70);

            Assert.True(TestHelper.HasDarkPixelsInRegion(bmp, TestHelper.PtToPx(50), TestHelper.PtToPx(300), yMin, yMax),
                "Rich text box with opacity should have visible content");

            bmp.Dispose();
        }

        [Fact]
        public void DrawText_ZeroOpacity_IsInvisible()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: text at 0% opacity is invisible");
            page.DrawText("INVISIBLE", 50, 50, PdfFont.Helvetica, 48, opacity: 0f);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Graphics/opacity-text-0pct-invisible");
            var bmp = TestHelper.RasterizePage(bytes, "Graphics/opacity-text-0pct-invisible");

            int yMin = TestHelper.PtToPx(50);
            int yMax = TestHelper.PtToPx(98);

            // Zero opacity text should be invisible — all pixels should be white
            Assert.False(TestHelper.HasDarkPixelsInRegion(bmp, TestHelper.PtToPx(50), TestHelper.PtToPx(300), yMin, yMax),
                "Zero opacity text should be invisible");

            bmp.Dispose();
        }
    }
}
