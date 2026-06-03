using System;
using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class SpotColorTests
    {
        [Fact]
        public void Spot_StoresNameAndCmyk()
        {
            var c = PdfColor.Spot("PANTONE 185 C", 0f, 0.91f, 0.76f, 0f);
            Assert.True(c.IsSpotColor);
            Assert.Equal("PANTONE 185 C", c.SpotColorName);
            Assert.Equal(0f, c.C, 4);
            Assert.Equal(0.91f, c.M, 4);
            Assert.Equal(0.76f, c.Y, 4);
            Assert.Equal(0f, c.K, 4);
            Assert.Equal(1f, c.Tint, 4);
        }

        [Fact]
        public void Spot_NullName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => PdfColor.Spot(null, 0, 0, 0, 0));
        }

        [Fact]
        public void WithTint_ReturnsDifferentTint()
        {
            var full = PdfColor.Spot("MySpot", 0.5f, 0.5f, 0f, 0f);
            var half = full.WithTint(0.5f);
            Assert.Equal(0.5f, half.Tint, 4);
            Assert.Equal("MySpot", half.SpotColorName);
            Assert.Equal(0.5f, half.C, 4);
        }

        [Fact]
        public void WithTint_ClampsValues()
        {
            var spot = PdfColor.Spot("Test", 0, 0, 0, 1f);
            Assert.Equal(0f, spot.WithTint(-0.5f).Tint, 4);
            Assert.Equal(1f, spot.WithTint(1.5f).Tint, 4);
        }

        [Fact]
        public void WithTint_OnNonSpotColor_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => PdfColor.Red.WithTint(0.5f));
        }

        [Fact]
        public void RegularColors_AreNotSpotColors()
        {
            Assert.False(PdfColor.Red.IsSpotColor);
            Assert.False(PdfColor.Black.IsSpotColor);
            Assert.Null(PdfColor.Red.SpotColorName);
        }

        [Fact]
        public void SpotColor_FilledRectangle_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var spot = PdfColor.Spot("PANTONE 185 C", 0f, 0.91f, 0.76f, 0f);
            page.DrawFilledRectangle(50, 50, 200, 100, spot);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "spot_filled_rect");
            var bitmap = TestHelper.RasterizePage(bytes, "spot_filled_rect");

            // The spot color fallback is a strong magenta-red (C=0, M=0.91, Y=0.76, K=0)
            // Check center of rectangle for non-white pixels
            int cx = (int)(150 * 150 / 72.0);
            int cy = (int)(100 * 150 / 72.0);
            var pixel = bitmap.GetPixel(cx, cy);
            Assert.True(pixel.Red > 150, $"Expected red > 150, got {pixel.Red}");
            Assert.True(pixel.Green < 100, $"Expected green < 100, got {pixel.Green}");
            bitmap.Dispose();
        }

        [Fact]
        public void SpotColor_HalfTint_LighterThanFull()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var full = PdfColor.Spot("TestInk", 1f, 0f, 0f, 0f);
            var half = full.WithTint(0.5f);
            page.DrawFilledRectangle(50, 50, 200, 100, full);
            page.DrawFilledRectangle(50, 200, 200, 100, half);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "spot_tint_comparison");
            var bitmap = TestHelper.RasterizePage(bytes, "spot_tint_comparison");

            // Full tint should be darker (more cyan = less red in RGB)
            int cx = (int)(150 * 150 / 72.0);
            int fullY = (int)(100 * 150 / 72.0);
            int halfY = (int)(250 * 150 / 72.0);
            var fullPixel = bitmap.GetPixel(cx, fullY);
            var halfPixel = bitmap.GetPixel(cx, halfY);

            // Half tint should have higher R+G+B (lighter) than full tint
            int fullSum = fullPixel.Red + fullPixel.Green + fullPixel.Blue;
            int halfSum = halfPixel.Red + halfPixel.Green + halfPixel.Blue;
            Assert.True(halfSum > fullSum, $"Half tint ({halfSum}) should be lighter than full tint ({fullSum})");
            bitmap.Dispose();
        }

        [Fact]
        public void SpotColor_ContentStream_ContainsCorrectOperators()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var spot = PdfColor.Spot("TestInk", 0.5f, 0.3f, 0.1f, 0f, tint: 0.75f);
            page.DrawFilledRectangle(50, 50, 200, 100, spot);
            var pdfText = Encoding.ASCII.GetString(doc.ToArray());

            // Content stream should contain spot color operators
            Assert.Contains("/CS1 cs", pdfText);
            Assert.Contains("0.75 scn", pdfText);
        }

        [Fact]
        public void SpotColor_PdfOutput_ContainsSeparation()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var spot = PdfColor.Spot("LogoBlue", 1f, 0.5f, 0f, 0f);
            page.DrawFilledRectangle(50, 50, 200, 100, spot);
            var pdfText = Encoding.ASCII.GetString(doc.ToArray());

            Assert.Contains("/Separation", pdfText);
            Assert.Contains("/LogoBlue", pdfText);
            Assert.Contains("/DeviceCMYK", pdfText);
            Assert.Contains("/FunctionType 2", pdfText);
        }

        [Fact]
        public void SpotColor_NameWithSpaces_EscapedInPdf()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var spot = PdfColor.Spot("PANTONE 185 C", 0f, 0.91f, 0.76f, 0f);
            page.DrawFilledRectangle(50, 50, 200, 100, spot);
            var pdfText = Encoding.ASCII.GetString(doc.ToArray());

            // Spaces encoded as #20 in PDF names
            Assert.Contains("/PANTONE#20185#20C", pdfText);
        }

        [Fact]
        public void SpotColor_TwoPages_SharedColorSpaceObject()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage(PageSize.A4);
            var page2 = doc.AddPage(PageSize.A4);
            var spot = PdfColor.Spot("SharedInk", 0f, 1f, 1f, 0f);
            page1.DrawFilledRectangle(50, 50, 200, 100, spot);
            page2.DrawFilledRectangle(50, 50, 200, 100, spot);
            var pdfText = Encoding.ASCII.GetString(doc.ToArray());

            // The /Separation array should appear only once (deduplicated)
            int sepCount = 0;
            int idx = 0;
            while ((idx = pdfText.IndexOf("/Separation /SharedInk", idx, StringComparison.Ordinal)) >= 0)
            {
                sepCount++;
                idx++;
            }
            Assert.Equal(1, sepCount);
        }

        [Fact]
        public void SpotColor_TintTransform_HasCorrectCmykValues()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var spot = PdfColor.Spot("Custom", 0.2f, 0.4f, 0.6f, 0.8f);
            page.DrawFilledRectangle(50, 50, 200, 100, spot);
            var pdfText = Encoding.ASCII.GetString(doc.ToArray());

            Assert.Contains("/C1 [0.2 0.4 0.6 0.8]", pdfText);
            Assert.Contains("/C0 [0 0 0 0]", pdfText);
        }

        [Fact]
        public void SpotColor_ResourceDictionary_ContainsColorSpace()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var spot = PdfColor.Spot("TestInk", 0f, 0f, 0f, 1f);
            page.DrawFilledRectangle(50, 50, 200, 100, spot);
            var pdfText = Encoding.ASCII.GetString(doc.ToArray());

            Assert.Contains("/ColorSpace <<", pdfText);
            Assert.Contains("/CS1", pdfText);
        }

        [Fact]
        public void SpotColor_MultipleDifferentSpots_GetSeparateIds()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var spot1 = PdfColor.Spot("Ink1", 1f, 0f, 0f, 0f);
            var spot2 = PdfColor.Spot("Ink2", 0f, 1f, 0f, 0f);
            page.DrawFilledRectangle(50, 50, 200, 100, spot1);
            page.DrawFilledRectangle(50, 200, 200, 100, spot2);
            var pdfText = Encoding.ASCII.GetString(doc.ToArray());

            Assert.Contains("/CS1", pdfText);
            Assert.Contains("/CS2", pdfText);
            Assert.Contains("/Ink1", pdfText);
            Assert.Contains("/Ink2", pdfText);
        }

        [Fact]
        public void SpotColor_SameNameDifferentTints_ShareColorSpace()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var full = PdfColor.Spot("SharedInk", 0f, 1f, 0f, 0f);
            var half = full.WithTint(0.5f);
            page.DrawFilledRectangle(50, 50, 200, 100, full);
            page.DrawFilledRectangle(50, 200, 200, 100, half);
            var pdfText = Encoding.ASCII.GetString(doc.ToArray());

            // Both should use CS1 (same color space, different tint values)
            Assert.Contains("/CS1 cs 1 scn", pdfText);
            Assert.Contains("/CS1 cs 0.5 scn", pdfText);
            // Should NOT have CS2
            Assert.DoesNotContain("/CS2", pdfText);
        }
    }
}
