using System;
using System.IO;
using SkiaSharp;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    /// <summary>
    /// Tests using real-world images from TestAssets/ to verify
    /// parsing, rendering, and edge cases not covered by synthetic images.
    /// </summary>
    public class RealImageTests
    {
        private static readonly string AssetsDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "TestAssets");

        private static int PtToPx(float pt) => (int)(pt * 150 / 72.0);

        private static bool HasDarkPixelsInRegion(SKBitmap bitmap,
            int xMin, int xMax, int yMin, int yMax)
        {
            xMax = Math.Min(xMax, bitmap.Width - 1);
            yMax = Math.Min(yMax, bitmap.Height - 1);
            for (int x = Math.Max(0, xMin); x <= xMax; x += 3)
                for (int y = Math.Max(0, yMin); y <= yMax; y += 3)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Red < 240 || p.Green < 240 || p.Blue < 240) return true;
                }
            return false;
        }

        // ── JPEG: Himalayan mountains (progressive, landscape, 1920x1285) ──

        [Fact]
        public void HimalayanJpeg_ParsesDimensions()
        {
            var path = Path.Combine(AssetsDir, "himalayan-mountains-1389998575T1I.jpg");
            var image = PdfImage.FromFile(path);
            Assert.Equal(1920, image.PixelWidth);
            Assert.Equal(1285, image.PixelHeight);
        }

        [Fact]
        public void HimalayanJpeg_RendersInPdf()
        {
            var path = Path.Combine(AssetsDir, "himalayan-mountains-1389998575T1I.jpg");
            var image = PdfImage.FromFile(path);

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            doc.AddImage(image);
            float scale = 500f / image.PixelWidth;
            float w = 500, h = image.PixelHeight * scale;
            page.DrawImage(image, 50, 50, w, h);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "real_himalayan");
            var bitmap = TestHelper.RasterizePage(bytes, "real_himalayan");

            Assert.True(HasDarkPixelsInRegion(bitmap,
                PtToPx(60), PtToPx(540), PtToPx(60), PtToPx(50 + h - 10)),
                "Himalayan JPEG should render visible content");
            bitmap.Dispose();
        }

        // ── JPEG: Echinacea flower art (progressive, portrait, 1534x1920) ──

        [Fact]
        public void EchinaceaJpeg_ParsesDimensions()
        {
            var path = Path.Combine(AssetsDir, "echinacea-flower-art.jpg");
            var image = PdfImage.FromFile(path);
            Assert.Equal(1534, image.PixelWidth);
            Assert.Equal(1920, image.PixelHeight);
        }

        [Fact]
        public void EchinaceaJpeg_RendersPortrait()
        {
            var path = Path.Combine(AssetsDir, "echinacea-flower-art.jpg");
            var image = PdfImage.FromFile(path);

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            doc.AddImage(image);
            float w = 200, h = 300; // portrait aspect
            page.DrawImage(image, 50, 50, w, h);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "real_echinacea_portrait");
            var bitmap = TestHelper.RasterizePage(bytes, "real_echinacea_portrait");

            Assert.True(HasDarkPixelsInRegion(bitmap,
                PtToPx(60), PtToPx(240), PtToPx(60), PtToPx(340)),
                "Echinacea portrait JPEG should render visible content");
            bitmap.Dispose();
        }

        // ── Multi-image document ──

        [Fact]
        public void AllRealImages_OnOnePage()
        {
            var doc = new PdfDocument { Title = "Real Image Gallery" };
            var page = doc.AddPage(PageSize.A4);

            float y = 30;
            string[] files = { "himalayan-mountains-1389998575T1I.jpg",
                               "echinacea-flower-art.jpg" };

            foreach (var file in files)
            {
                var path = Path.Combine(AssetsDir, file);
                var image = PdfImage.FromFile(path);
                doc.AddImage(image);

                float w = 200;
                float h = w * image.PixelHeight / image.PixelWidth;
                if (y + h > page.Height - 30)
                {
                    h = page.Height - 30 - y;
                    w = h * image.PixelWidth / image.PixelHeight;
                }
                page.DrawImage(image, 50, y, w, h);
                page.DrawText(file, 260, y + 5, PdfFont.Helvetica, 9, PdfColor.DarkGray);
                page.DrawText($"{image.PixelWidth}x{image.PixelHeight}", 260, y + 18,
                    PdfFont.Helvetica, 8, PdfColor.LightGray);
                y += h + 10;
            }

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "real_gallery");
            var bitmap = TestHelper.RasterizePage(bytes, "real_gallery");

            // Verify content is visible throughout the page
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250), PtToPx(30), PtToPx(200)),
                "Top portion should have image content");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(260), PtToPx(500), PtToPx(30), PtToPx(100)),
                "Labels should be visible");
            bitmap.Dispose();
        }

        // ── Progressive JPEG parsing ──

        [Fact]
        public void ProgressiveJpeg_ParsesCorrectly()
        {
            // Both test images are progressive JPEGs (SOF2)
            var path = Path.Combine(AssetsDir, "himalayan-mountains-1389998575T1I.jpg");
            var image = PdfImage.FromFile(path);
            Assert.True(image.PixelWidth > 0 && image.PixelHeight > 0);
            Assert.Equal(3, image.ComponentCount); // RGB
        }

        [Fact]
        public void ProgressivePortraitJpeg_ParsesCorrectly()
        {
            var path = Path.Combine(AssetsDir, "echinacea-flower-art.jpg");
            var image = PdfImage.FromFile(path);
            Assert.True(image.PixelWidth > 0 && image.PixelHeight > 0);
            Assert.Equal(3, image.ComponentCount);
        }
    }
}
