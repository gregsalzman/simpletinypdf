using SkiaSharp;
using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutImageTests
    {
        [Fact]
        public void Image_RendersAtCurrentPosition()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Image below:");

            var jpegBytes = TestHelper.CreateTestJpeg(SKColors.Blue, 100, 100);
            var image = PdfImage.FromBytes(jpegBytes);
            layout.AddImage(image, new ImageOptions { Width = 100, Height = 100 });

            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: blue image below text");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/image-basic");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/image-basic");

            // Image should appear below the text line (~72 + 14.4 = ~87pt)
            float imageY = 72 + 12 * 1.2f;
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(172),
                TestHelper.PtToPx(imageY), TestHelper.PtToPx(imageY + 100)));
        }

        [Fact]
        public void Image_CenterAlignment()
        {
            var layout = new PdfDocumentLayout();
            var jpegBytes = TestHelper.CreateTestJpeg(SKColors.Green, 50, 50);
            var image = PdfImage.FromBytes(jpegBytes);
            layout.AddImage(image, new ImageOptions
            {
                Width = 100,
                Height = 100,
                Alignment = TextAlignment.Center
            });

            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: green image centered horizontally");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/image-center");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/image-center");

            // Image centered: contentWidth = 595-72-72 = 451, image=100, so x ≈ 72 + (451-100)/2 ≈ 247.5
            int midX = TestHelper.PtToPx(595f / 2f);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                midX - 60, midX + 60,
                TestHelper.PtToPx(72), TestHelper.PtToPx(172)));
        }

        [Fact]
        public void Image_RightAlignment()
        {
            var layout = new PdfDocumentLayout();
            var jpegBytes = TestHelper.CreateTestJpeg(SKColors.Red, 50, 50);
            var image = PdfImage.FromBytes(jpegBytes);
            layout.AddImage(image, new ImageOptions
            {
                Width = 80,
                Height = 80,
                Alignment = TextAlignment.Right
            });

            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: red image right-aligned");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/image-right");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/image-right");

            // Image right-aligned: x = 72 + 451 - 80 = 443
            int rightEdge = TestHelper.PtToPx(595 - 72);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                rightEdge - TestHelper.PtToPx(80), rightEdge,
                TestHelper.PtToPx(72), TestHelper.PtToPx(152)));
        }

        [Fact]
        public void Image_PageOverflow()
        {
            var layout = new PdfDocumentLayout();

            // Fill most of page
            for (int i = 0; i < 50; i++)
                layout.AddParagraph($"Line {i + 1} filling up the page.");

            // Image that won't fit
            var jpegBytes = TestHelper.CreateTestJpeg(SKColors.Purple, 100, 100);
            var image = PdfImage.FromBytes(jpegBytes);
            layout.AddImage(image, new ImageOptions { Width = 200, Height = 300 });

            var doc = layout.Generate();
            Assert.True(doc.PageCount >= 2);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: image on page 2 after overflow");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/image-overflow");
        }

        [Fact]
        public void Image_SpaceBeforeAndAfter()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Before image");

            var jpegBytes = TestHelper.CreateTestJpeg(SKColors.Orange, 50, 50);
            var image = PdfImage.FromBytes(jpegBytes);
            layout.AddImage(image, new ImageOptions
            {
                Width = 80,
                Height = 80,
                SpaceBefore = 30,
                SpaceAfter = 30
            });

            layout.AddParagraph("After image");

            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: 30pt gap before and after image");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/image-spacing");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/image-spacing");

            // Image with spacing should push "After image" further down
            float textEnd = 72 + 12 * 1.2f; // ~86.4
            float imageStart = textEnd + 30; // ~116.4
            float imageEnd = imageStart + 80; // ~196.4
            float afterStart = imageEnd + 30; // ~226.4

            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(200),
                TestHelper.PtToPx(afterStart - 5), TestHelper.PtToPx(afterStart + 20)));
        }

        [Fact]
        public void Image_AutoSizeFromWidth()
        {
            var layout = new PdfDocumentLayout();

            // Create a 200x100 image (2:1 aspect ratio)
            var jpegBytes = TestHelper.CreateTestJpeg(SKColors.Teal, 200, 100);
            var image = PdfImage.FromBytes(jpegBytes);
            layout.AddImage(image, new ImageOptions { Width = 300 });

            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: 300pt wide image with proportional height (150pt)");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/image-auto-height");
        }

        [Fact]
        public void Image_NoSize_FitsContentWidth()
        {
            var layout = new PdfDocumentLayout();
            var jpegBytes = TestHelper.CreateTestJpeg(SKColors.Navy, 400, 200);
            var image = PdfImage.FromBytes(jpegBytes);
            layout.AddImage(image); // No options = fits content width

            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: image fills full content width");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/image-auto-fit");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/image-auto-fit");

            // Image should span most of the content width (72 to 523)
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(80), TestHelper.PtToPx(500),
                TestHelper.PtToPx(72), TestHelper.PtToPx(300)));
        }
    }
}
