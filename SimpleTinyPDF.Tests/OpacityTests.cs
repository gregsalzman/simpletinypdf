using Xunit;
using SkiaSharp;

namespace SimpleTinyPDF.Tests
{
    public class OpacityTests
    {
        private static int PtToPx(float pt, int dpi = 150) => (int)(pt * dpi / 72.0);

        private static bool HasDarkPixelsInRegion(SKBitmap bitmap,
            int xMin, int xMax, int yMin, int yMax)
        {
            xMax = System.Math.Min(xMax, bitmap.Width - 1);
            yMax = System.Math.Min(yMax, bitmap.Height - 1);
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Red < 200 || p.Green < 200 || p.Blue < 200)
                        return true;
                }
            return false;
        }

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
            page1.DrawText("OPACITY", 50, 50, PdfFont.Helvetica, 48);
            var bytes1 = doc1.ToArray();
            TestHelper.SavePdf(bytes1, "opacity_text_full");
            var bmp1 = TestHelper.RasterizePage(bytes1, "opacity_text_full");

            // Render half-opacity black text
            var doc2 = new PdfDocument();
            var page2 = doc2.AddPage(PageSize.A4);
            page2.DrawText("OPACITY", 50, 50, PdfFont.Helvetica, 48, opacity: 0.5f);
            var bytes2 = doc2.ToArray();
            TestHelper.SavePdf(bytes2, "opacity_text_half");
            var bmp2 = TestHelper.RasterizePage(bytes2, "opacity_text_half");

            int yMin = PtToPx(50);
            int yMax = PtToPx(50 + 48);
            int xMin = PtToPx(50);
            int xMax = PtToPx(250);

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
            page1.DrawText("Test", 50, 50, PdfFont.Helvetica, 24);
            var bytes1 = doc1.ToArray();

            // Explicit opacity=1.0
            var doc2 = new PdfDocument();
            var page2 = doc2.AddPage(PageSize.A4);
            page2.DrawText("Test", 50, 50, PdfFont.Helvetica, 24, opacity: 1f);
            var bytes2 = doc2.ToArray();

            TestHelper.SavePdf(bytes1, "opacity_text_default");
            TestHelper.SavePdf(bytes2, "opacity_text_explicit_full");
            var bmp1 = TestHelper.RasterizePage(bytes1, "opacity_text_default");
            var bmp2 = TestHelper.RasterizePage(bytes2, "opacity_text_explicit_full");

            int yMin = PtToPx(50);
            int yMax = PtToPx(74);
            int xMin = PtToPx(50);
            int xMax = PtToPx(150);

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
            page1.DrawText("Semi transparent text box content here", 50, 50,
                PdfFont.Helvetica, 24, width: 300);
            var bytes1 = doc1.ToArray();
            TestHelper.SavePdf(bytes1, "opacity_textbox_full");
            var bmp1 = TestHelper.RasterizePage(bytes1, "opacity_textbox_full");

            var doc2 = new PdfDocument();
            var page2 = doc2.AddPage(PageSize.A4);
            page2.DrawText("Semi transparent text box content here", 50, 50,
                PdfFont.Helvetica, 24, opacity: 0.3f, width: 300);
            var bytes2 = doc2.ToArray();
            TestHelper.SavePdf(bytes2, "opacity_textbox_030");
            var bmp2 = TestHelper.RasterizePage(bytes2, "opacity_textbox_030");

            int yMin = PtToPx(50);
            int yMax = PtToPx(100);
            int xMin = PtToPx(50);
            int xMax = PtToPx(300);

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
            page1.DrawImage(image, 50, 50, 200, 200);
            var bytes1 = doc1.ToArray();
            TestHelper.SavePdf(bytes1, "opacity_image_full");
            var bmp1 = TestHelper.RasterizePage(bytes1, "opacity_image_full");

            // Half opacity
            var doc2 = new PdfDocument();
            doc2.AddImage(image);
            var page2 = doc2.AddPage(PageSize.A4);
            page2.DrawImage(image, 50, 50, 200, 200, opacity: 0.5f);
            var bytes2 = doc2.ToArray();
            TestHelper.SavePdf(bytes2, "opacity_image_half");
            var bmp2 = TestHelper.RasterizePage(bytes2, "opacity_image_half");

            // Check a region in the center of the image
            int xMin = PtToPx(100);
            int xMax = PtToPx(200);
            int yMin = PtToPx(100);
            int yMax = PtToPx(200);

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
            page1.DrawText(new[]
            {
                new TextSpan("AA ", PdfFont.Helvetica, 36, PdfColor.Black, opacity: 1f),
                new TextSpan("BB", PdfFont.Helvetica, 36, PdfColor.Black, opacity: 1f)
            }, 50, 50);
            var bytes1 = doc1.ToArray();
            TestHelper.SavePdf(bytes1, "opacity_richtext_allFull");
            var bmp1 = TestHelper.RasterizePage(bytes1, "opacity_richtext_allFull");

            // Render with second span at half opacity
            var doc2 = new PdfDocument();
            var page2 = doc2.AddPage(PageSize.A4);
            page2.DrawText(new[]
            {
                new TextSpan("AA ", PdfFont.Helvetica, 36, PdfColor.Black, opacity: 1f),
                new TextSpan("BB", PdfFont.Helvetica, 36, PdfColor.Black, opacity: 0.5f)
            }, 50, 50);
            var bytes2 = doc2.ToArray();
            TestHelper.SavePdf(bytes2, "opacity_richtext_mixed");
            var bmp2 = TestHelper.RasterizePage(bytes2, "opacity_richtext_mixed");

            int yMin = PtToPx(50);
            int yMax = PtToPx(86);
            // "AA " at 36pt is ~80pt wide. "BB" region starts around x=130pt
            int xBBMin = PtToPx(130);
            int xBBMax = PtToPx(200);

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

            var spans = new[]
            {
                new TextSpan("Opaque part ", PdfFont.Helvetica, 18, PdfColor.Black, opacity: 1f),
                new TextSpan("translucent part", PdfFont.Helvetica, 18, PdfColor.Black, opacity: 0.4f)
            };
            page.DrawText(spans, 50, 50, width: 300);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "opacity_richtextbox");
            var bmp = TestHelper.RasterizePage(bytes, "opacity_richtextbox");

            int yMin = PtToPx(50);
            int yMax = PtToPx(70);

            Assert.True(HasDarkPixelsInRegion(bmp, PtToPx(50), PtToPx(300), yMin, yMax),
                "Rich text box with opacity should have visible content");

            bmp.Dispose();
        }

        [Fact]
        public void DrawText_ZeroOpacity_IsInvisible()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("INVISIBLE", 50, 50, PdfFont.Helvetica, 48, opacity: 0f);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "opacity_text_zero");
            var bmp = TestHelper.RasterizePage(bytes, "opacity_text_zero");

            int yMin = PtToPx(50);
            int yMax = PtToPx(98);

            // Zero opacity text should be invisible — all pixels should be white
            Assert.False(HasDarkPixelsInRegion(bmp, PtToPx(50), PtToPx(300), yMin, yMax),
                "Zero opacity text should be invisible");

            bmp.Dispose();
        }
    }
}
