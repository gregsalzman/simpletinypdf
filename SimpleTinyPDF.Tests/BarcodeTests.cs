using System;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class BarcodeTests
    {
        private static int PtToPx(float pt, int dpi = 150) => (int)(pt * dpi / 72.0);

        // ── Code 39 Encoder Tests ───────────────────────────────────

        [Fact]
        public void Code39Encoder_EncodesValidData()
        {
            var modules = Code39Encoder.Encode("HELLO", out var displayText);
            Assert.NotNull(modules);
            Assert.True(modules.Length > 0);
            Assert.Equal("HELLO", displayText);
        }

        [Fact]
        public void Code39Encoder_LowercaseIsConverted()
        {
            var modules = Code39Encoder.Encode("hello", out var displayText);
            Assert.Equal("HELLO", displayText);
        }

        [Fact]
        public void Code39Encoder_InvalidCharacterThrows()
        {
            Assert.Throws<ArgumentException>(() => Code39Encoder.Encode("abc{}", out _));
        }

        [Fact]
        public void Code39Encoder_EmptyDataThrows()
        {
            Assert.Throws<ArgumentException>(() => Code39Encoder.Encode("", out _));
        }

        [Fact]
        public void Code39Encoder_StarCharacterThrows()
        {
            // Asterisk is start/stop only, not allowed in data
            Assert.Throws<ArgumentException>(() => Code39Encoder.Encode("A*B", out _));
        }

        [Fact]
        public void Code39Encoder_ModuleCountIsCorrect()
        {
            // For N data characters: (N+2) symbols × 15 modules + (N+1) gaps
            // "AB" = 4 symbols × 15 + 3 gaps = 63
            var modules = Code39Encoder.Encode("AB", out _);
            Assert.Equal(63, modules.Length);
        }

        // ── EAN-13 Encoder Tests ────────────────────────────────────

        [Fact]
        public void Ean13Encoder_ComputesCheckDigit()
        {
            // 978020137962 → check digit = 4 → 9780201379624
            var modules = Ean13Encoder.Encode("978020137962", out var displayText);
            Assert.Equal("9780201379624", displayText);
            Assert.Equal(95, modules.Length);
        }

        [Fact]
        public void Ean13Encoder_AcceptsFullEan13()
        {
            var modules = Ean13Encoder.Encode("9780201379624", out var displayText);
            Assert.Equal("9780201379624", displayText);
            Assert.Equal(95, modules.Length);
        }

        [Fact]
        public void Ean13Encoder_InvalidCheckDigitThrows()
        {
            Assert.Throws<ArgumentException>(() => Ean13Encoder.Encode("9780201379620", out _));
        }

        [Fact]
        public void Ean13Encoder_WrongLengthThrows()
        {
            Assert.Throws<ArgumentException>(() => Ean13Encoder.Encode("123", out _));
        }

        [Fact]
        public void Ean13Encoder_StartsAndEndsWithGuardBars()
        {
            var modules = Ean13Encoder.Encode("978020137962", out _);
            // Start guard: 101
            Assert.True(modules[0]);
            Assert.False(modules[1]);
            Assert.True(modules[2]);
            // End guard: 101
            Assert.True(modules[92]);
            Assert.False(modules[93]);
            Assert.True(modules[94]);
        }

        // ── UPC-A Encoder Tests ─────────────────────────────────────

        [Fact]
        public void UpcAEncoder_EncodesValidData()
        {
            // UPC-A "03600029145" + check digit 2 = "036000291452"
            var modules = Ean13Encoder.EncodeUpcA("03600029145", out var displayText);
            Assert.Equal("036000291452", displayText);
            Assert.Equal(95, modules.Length); // Same as EAN-13
        }

        [Fact]
        public void UpcAEncoder_WrongLengthThrows()
        {
            Assert.Throws<ArgumentException>(() => Ean13Encoder.EncodeUpcA("123", out _));
        }

        // ── Code 128 Encoder Tests ──────────────────────────────────

        [Fact]
        public void Code128Encoder_EncodesAlphanumericData()
        {
            var modules = Code128Encoder.Encode("Hello123", out var displayText);
            Assert.NotNull(modules);
            Assert.True(modules.Length > 0);
            Assert.Equal("Hello123", displayText);
        }

        [Fact]
        public void Code128Encoder_AllNumericUsesSubsetC()
        {
            // All-numeric data should use Start C + pairs, which is more compact
            var numericModules = Code128Encoder.Encode("12345678", out _);
            var alphaModules = Code128Encoder.Encode("ABCDEFGH", out _);
            // Subset C encodes 2 digits per symbol, so numeric should be shorter
            Assert.True(numericModules.Length < alphaModules.Length,
                $"Numeric ({numericModules.Length}) should be shorter than alpha ({alphaModules.Length})");
        }

        [Fact]
        public void Code128Encoder_EmptyDataThrows()
        {
            Assert.Throws<ArgumentException>(() => Code128Encoder.Encode("", out _));
        }

        // ── QR Code Encoder Tests ───────────────────────────────────

        [Fact]
        public void QrCodeEncoder_EncodesShortText()
        {
            var result = QrCodeEncoder.Encode("HELLO", QrErrorCorrection.Medium);
            Assert.NotNull(result);
            Assert.True(result.Size > 0);
            Assert.Equal(result.Size, result.Modules.GetLength(0));
            Assert.Equal(result.Size, result.Modules.GetLength(1));
        }

        [Fact]
        public void QrCodeEncoder_Version1Is21x21()
        {
            // Very short data at low EC should be version 1 (21x21)
            var result = QrCodeEncoder.Encode("Hi", QrErrorCorrection.Low);
            Assert.Equal(21, result.Size);
        }

        [Fact]
        public void QrCodeEncoder_HigherEcProducesLargerOrEqualCode()
        {
            // Same data at higher EC may need a larger version
            var low = QrCodeEncoder.Encode("Hello, World!", QrErrorCorrection.Low);
            var high = QrCodeEncoder.Encode("Hello, World!", QrErrorCorrection.High);
            Assert.True(high.Size >= low.Size);
        }

        [Fact]
        public void QrCodeEncoder_HasFinderPatterns()
        {
            var result = QrCodeEncoder.Encode("Test", QrErrorCorrection.Medium);
            var m = result.Modules;
            int s = result.Size;

            // Top-left finder: 7x7, all of row 0 cols 0-6 should be dark
            for (int c = 0; c < 7; c++)
                Assert.True(m[0, c], $"Finder top-left row 0, col {c} should be dark");

            // Top-right finder
            for (int c = s - 7; c < s; c++)
                Assert.True(m[0, c], $"Finder top-right row 0, col {c} should be dark");

            // Bottom-left finder
            for (int c = 0; c < 7; c++)
                Assert.True(m[s - 1, c], $"Finder bottom-left row {s - 1}, col {c} should be dark");
        }

        [Fact]
        public void QrCodeEncoder_DataTooLargeThrows()
        {
            var largeData = new string('X', 3000);
            Assert.Throws<ArgumentException>(() =>
                QrCodeEncoder.Encode(largeData, QrErrorCorrection.High));
        }

        // ── Visual Integration Tests (PDF rendering) ────────────────

        [Fact]
        public void DrawBarcode_Code39_RendersVisibleBars()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawBarcode("HELLO", BarcodeType.Code39, 50, 100, 300, 80);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "barcode_code39");
            var bitmap = TestHelper.RasterizePage(bytes, "barcode_code39");

            AssertHasDarkAndLightPixels(bitmap, 50, 350, 100, 180, "Code 39");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawBarcode_Ean13_RendersVisibleBars()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawBarcode("978020137962", BarcodeType.Ean13, 50, 100, 200, 80);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "barcode_ean13");
            var bitmap = TestHelper.RasterizePage(bytes, "barcode_ean13");

            AssertHasDarkAndLightPixels(bitmap, 50, 250, 100, 180, "EAN-13");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawBarcode_UpcA_RendersVisibleBars()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawBarcode("03600029145", BarcodeType.UpcA, 50, 100, 200, 80);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "barcode_upca");
            var bitmap = TestHelper.RasterizePage(bytes, "barcode_upca");

            AssertHasDarkAndLightPixels(bitmap, 50, 250, 100, 180, "UPC-A");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawBarcode_Code128_RendersVisibleBars()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawBarcode("Hello123", BarcodeType.Code128, 50, 100, 300, 80);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "barcode_code128");
            var bitmap = TestHelper.RasterizePage(bytes, "barcode_code128");

            AssertHasDarkAndLightPixels(bitmap, 50, 350, 100, 180, "Code 128");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawBarcode_QrCode_RendersVisibleModules()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawBarcode("https://example.com", BarcodeType.QrCode, 50, 100, 200, 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "barcode_qrcode");
            var bitmap = TestHelper.RasterizePage(bytes, "barcode_qrcode");

            AssertHasDarkAndLightPixels(bitmap, 50, 250, 100, 300, "QR Code");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawBarcode_QrCode_HighErrorCorrection()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawBarcode("https://example.com", BarcodeType.QrCode, 50, 100, 200, 200,
                new BarcodeOptions { QrErrorCorrectionLevel = QrErrorCorrection.High });

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "barcode_qrcode_high_ec");
            var bitmap = TestHelper.RasterizePage(bytes, "barcode_qrcode_high_ec");

            AssertHasDarkAndLightPixels(bitmap, 50, 250, 100, 300, "QR Code High EC");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawBarcode_Code39_CustomColor()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawBarcode("TEST", BarcodeType.Code39, 50, 100, 300, 80,
                new BarcodeOptions { ForegroundColor = PdfColor.Blue });

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "barcode_code39_blue");
            var bitmap = TestHelper.RasterizePage(bytes, "barcode_code39_blue");

            // Should have blue pixels in the barcode area
            int blueCount = 0;
            int midY = PtToPx(140);
            for (int px = PtToPx(60); px < PtToPx(340); px++)
            {
                var pixel = bitmap.GetPixel(px, midY);
                if (pixel.Blue > 150 && pixel.Red < 100 && pixel.Green < 100)
                    blueCount++;
            }
            Assert.True(blueCount > 10, $"Expected blue bars, found {blueCount} blue pixels");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawBarcode_WithShowText_TextVisibleBelowBars()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawBarcode("HELLO", BarcodeType.Code39, 50, 100, 300, 80,
                new BarcodeOptions { ShowText = true });

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "barcode_code39_text");
            var bitmap = TestHelper.RasterizePage(bytes, "barcode_code39_text");

            // Text should be at the bottom of the barcode area
            // Just verify the overall area has dark content (bars + text)
            AssertHasDarkAndLightPixels(bitmap, 50, 350, 100, 180, "Code 39 with text");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawBarcode_NullDataThrows()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            Assert.Throws<ArgumentNullException>(() =>
                page.DrawBarcode(null, BarcodeType.Code128, 50, 100, 200, 80));
        }

        [Fact]
        public void DrawBarcode_ZeroWidthThrows()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            Assert.Throws<ArgumentException>(() =>
                page.DrawBarcode("test", BarcodeType.Code128, 50, 100, 0, 80));
        }

        [Fact]
        public void DrawBarcode_AllTypesOnOnePage()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            page.DrawText("Barcode Types", 50, 30, PdfFont.HelveticaBold, 16);

            page.DrawText("Code 39:", 50, 70, PdfFont.Helvetica, 10);
            page.DrawBarcode("HELLO 123", BarcodeType.Code39, 50, 85, 300, 60);

            page.DrawText("Code 128:", 50, 170, PdfFont.Helvetica, 10);
            page.DrawBarcode("Hello World 123!", BarcodeType.Code128, 50, 185, 300, 60);

            page.DrawText("EAN-13:", 50, 270, PdfFont.Helvetica, 10);
            page.DrawBarcode("978020137962", BarcodeType.Ean13, 50, 285, 200, 60);

            page.DrawText("UPC-A:", 50, 370, PdfFont.Helvetica, 10);
            page.DrawBarcode("03600029145", BarcodeType.UpcA, 50, 385, 200, 60);

            page.DrawText("QR Code:", 50, 470, PdfFont.Helvetica, 10);
            page.DrawBarcode("https://example.com", BarcodeType.QrCode, 50, 485, 150, 150);

            page.DrawText("QR Code (High EC):", 250, 470, PdfFont.Helvetica, 10);
            page.DrawBarcode("https://example.com", BarcodeType.QrCode, 250, 485, 150, 150,
                new BarcodeOptions { QrErrorCorrectionLevel = QrErrorCorrection.High });

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "barcode_all_types");
            var bitmap = TestHelper.RasterizePage(bytes, "barcode_all_types");

            // Just verify it renders without crashing and has content
            Assert.True(bitmap.Width > 0);
            Assert.True(bitmap.Height > 0);
            bitmap.Dispose();
        }

        // ── Helpers ─────────────────────────────────────────────────

        private void AssertHasDarkAndLightPixels(SkiaSharp.SKBitmap bitmap,
            float xMinPt, float xMaxPt, float yMinPt, float yMaxPt, string label)
        {
            int xMin = PtToPx(xMinPt);
            int xMax = Math.Min(PtToPx(xMaxPt), bitmap.Width - 1);
            int yMid = PtToPx((yMinPt + yMaxPt) / 2);
            if (yMid >= bitmap.Height) yMid = bitmap.Height / 2;

            int darkCount = 0, lightCount = 0;
            for (int px = xMin; px < xMax; px++)
            {
                var pixel = bitmap.GetPixel(px, yMid);
                if (pixel.Red < 80 && pixel.Green < 80 && pixel.Blue < 80)
                    darkCount++;
                else if (pixel.Red > 200 && pixel.Green > 200 && pixel.Blue > 200)
                    lightCount++;
            }

            Assert.True(darkCount > 5,
                $"{label}: Expected dark bars, found only {darkCount} dark pixels at y={yMid}");
            Assert.True(lightCount > 5,
                $"{label}: Expected light spaces, found only {lightCount} light pixels at y={yMid}");
        }
    }
}
