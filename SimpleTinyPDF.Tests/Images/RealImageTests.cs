using System;
using System.IO;
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
            TestHelper.AddDescription(page, "Verify: high-resolution landscape photo renders correctly");
            doc.AddImage(image);
            float scale = 500f / image.PixelWidth;
            float w = 500, h = image.PixelHeight * scale;
            page.DrawImage(image, 50, 50, w, h);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/real-photo-himalayan-landscape");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/real-photo-himalayan-landscape");

            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(60), TestHelper.PtToPx(540), TestHelper.PtToPx(60), TestHelper.PtToPx(50 + h - 10)),
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
            TestHelper.AddDescription(page, "Verify: high-resolution portrait photo renders correctly");
            doc.AddImage(image);
            float w = 200, h = 300; // portrait aspect
            page.DrawImage(image, 50, 50, w, h);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Images/real-photo-echinacea-portrait");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/real-photo-echinacea-portrait");

            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(60), TestHelper.PtToPx(240), TestHelper.PtToPx(60), TestHelper.PtToPx(340)),
                "Echinacea portrait JPEG should render visible content");
            bitmap.Dispose();
        }

        // ── Multi-image document ──

        [Fact]
        public void AllRealImages_OnOnePage()
        {
            var doc = new PdfDocument { Title = "Real Image Gallery" };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: multiple real photos across pages");

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
            TestHelper.SavePdf(bytes, "Images/real-photo-gallery-multipage");
            var bitmap = TestHelper.RasterizePage(bytes, "Images/real-photo-gallery-multipage");

            // Verify content is visible throughout the page
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(250), TestHelper.PtToPx(30), TestHelper.PtToPx(200)),
                "Top portion should have image content");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(260), TestHelper.PtToPx(500), TestHelper.PtToPx(30), TestHelper.PtToPx(100)),
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
