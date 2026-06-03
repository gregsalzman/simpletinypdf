using System.Text;
using SkiaSharp;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class ImageDeduplicationTests
    {
        [Fact]
        public void SameBytes_ProducesEqualImages()
        {
            var data = TestHelper.CreateTestJpeg(32, 32);
            var img1 = PdfImage.FromBytes(data);
            var img2 = PdfImage.FromBytes(data);
            Assert.Equal(img1, img2);
            Assert.Equal(img1.GetHashCode(), img2.GetHashCode());
            Assert.False(ReferenceEquals(img1, img2));
        }

        [Fact]
        public void DifferentBytes_ProducesUnequalImages()
        {
            var data1 = TestHelper.CreateTestJpeg(SKColors.Red, 32, 32);
            var data2 = TestHelper.CreateTestJpeg(SKColors.Blue, 32, 32);
            var img1 = PdfImage.FromBytes(data1);
            var img2 = PdfImage.FromBytes(data2);
            Assert.NotEqual(img1, img2);
        }

        [Fact]
        public void AddImage_ReturnsSameInstanceForDuplicates()
        {
            var data = TestHelper.CreateTestJpeg(16, 16);
            var doc = new PdfDocument();
            var img1 = doc.AddImage(PdfImage.FromBytes(data));
            var img2 = doc.AddImage(PdfImage.FromBytes(data));
            Assert.Same(img1, img2);
        }

        [Fact]
        public void AddImage_RegistersOnlyOnceForDuplicates()
        {
            var data = TestHelper.CreateTestJpeg(16, 16);
            var doc = new PdfDocument();
            doc.AddImage(PdfImage.FromBytes(data));
            doc.AddImage(PdfImage.FromBytes(data));
            doc.AddImage(PdfImage.FromBytes(data));
            Assert.Single(doc.GetImages());
        }

        [Fact]
        public void DuplicateImages_ProduceSingleXObjectInPdf()
        {
            var data = TestHelper.CreateTestJpeg(32, 32);
            var doc = new PdfDocument();
            for (int i = 0; i < 10; i++)
            {
                var page = doc.AddPage(PageSize.A4);
                var img = PdfImage.FromBytes(data);
                doc.AddImage(img);
                page.DrawImage(img, 10, 10, 100, 100);
            }
            var pdfBytes = doc.ToArray();
            var pdfText = Encoding.ASCII.GetString(pdfBytes);
            Assert.Equal(1, CountOccurrences(pdfText, "/Subtype /Image"));
        }

        [Fact]
        public void DrawImage_AutoRegistersWithDocument()
        {
            var data = TestHelper.CreateTestJpeg(16, 16);
            var doc = new PdfDocument();
            var page = doc.AddPage();
            var img = PdfImage.FromBytes(data);
            // No doc.AddImage — DrawImage should auto-register
            page.DrawImage(img, 10, 10, 100, 100);
            Assert.Single(doc.GetImages());
        }

        [Fact]
        public void DrawImage_AutoRegistration_DeduplicatesAcrossPages()
        {
            var data = TestHelper.CreateTestJpeg(16, 16);
            var doc = new PdfDocument();
            var page1 = doc.AddPage();
            var page2 = doc.AddPage();
            page1.DrawImage(PdfImage.FromBytes(data), 10, 10, 100, 100);
            page2.DrawImage(PdfImage.FromBytes(data), 10, 10, 100, 100);
            Assert.Single(doc.GetImages());
            var pdfBytes = doc.ToArray();
            var pdfText = Encoding.ASCII.GetString(pdfBytes);
            Assert.Equal(1, CountOccurrences(pdfText, "/Subtype /Image"));
        }

        [Fact]
        public void ExistingCode_WithExplicitAddImage_StillWorks()
        {
            var data = TestHelper.CreateTestJpeg(16, 16);
            var doc = new PdfDocument();
            var page = doc.AddPage();
            var img = PdfImage.FromBytes(data);
            doc.AddImage(img);
            page.DrawImage(img, 10, 10, 100, 100);
            var pdfBytes = doc.ToArray();
            Assert.True(pdfBytes.Length > 0);
        }

        [Fact]
        public void DifferentImages_NotDeduplicated()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            var img1 = PdfImage.FromBytes(TestHelper.CreateTestJpeg(SKColors.Red, 16, 16));
            var img2 = PdfImage.FromBytes(TestHelper.CreateTestJpeg(SKColors.Blue, 16, 16));
            page.DrawImage(img1, 10, 10, 100, 100);
            page.DrawImage(img2, 120, 10, 100, 100);
            Assert.Equal(2, doc.GetImages().Count);
        }

        [Fact]
        public void SameImageOnSamePage_SingleResourceId()
        {
            var data = TestHelper.CreateTestJpeg(16, 16);
            var doc = new PdfDocument();
            var page = doc.AddPage();
            // Draw the same content-equal image twice on the same page
            page.DrawImage(PdfImage.FromBytes(data), 10, 10, 100, 100);
            page.DrawImage(PdfImage.FromBytes(data), 200, 10, 100, 100);
            var pdfBytes = doc.ToArray();
            var pdfText = Encoding.ASCII.GetString(pdfBytes);
            // Only one image XObject and one /Im resource
            Assert.Equal(1, CountOccurrences(pdfText, "/Subtype /Image"));
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int idx = 0;
            while ((idx = text.IndexOf(pattern, idx)) >= 0)
            {
                count++;
                idx += pattern.Length;
            }
            return count;
        }
    }
}
