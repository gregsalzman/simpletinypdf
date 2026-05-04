using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class BasicDocumentTests
    {
        [Fact]
        public void BlankPdf_IsValid_RasterizesWithoutError()
        {
            var doc = new PdfDocument();
            doc.AddPage(PageSize.A4);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "blank_a4");
            var bitmap = TestHelper.RasterizePage(bytes, "blank_a4");
            // A4 at 150 DPI: 595*150/72 ≈ 1240px wide, 842*150/72 ≈ 1754px tall
            Assert.True(bitmap.Width > 1200, $"Expected A4 width ~1240px, got {bitmap.Width}");
            Assert.True(bitmap.Height > 1700, $"Expected A4 height ~1754px, got {bitmap.Height}");
            // A blank page should be entirely white
            var center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
            Assert.True(center.Red > 250 && center.Green > 250 && center.Blue > 250,
                $"Blank page center should be white, got ({center.Red},{center.Green},{center.Blue})");
            bitmap.Dispose();
        }

        [Fact]
        public void MultiPage_HasCorrectPageCount()
        {
            var doc = new PdfDocument();
            doc.AddPage(PageSize.A4);
            doc.AddPage(PageSize.Letter);
            doc.AddPage(PageSize.A5);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "multi_page");
            Assert.Equal(3, TestHelper.GetPageCount(bytes));
        }

        [Fact]
        public void DifferentPageSizes_ProduceDifferentDimensions()
        {
            var doc = new PdfDocument();
            doc.AddPage(PageSize.A4);
            doc.AddPage(PageSize.Letter);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "different_sizes");
            var bitmapA4 = TestHelper.RasterizePage(bytes, "different_sizes", 0);
            var bitmapLetter = TestHelper.RasterizePage(bytes, "different_sizes", 1);

            // A4 is taller than Letter (842 vs 792 points)
            Assert.True(bitmapA4.Height > bitmapLetter.Height);
            bitmapA4.Dispose();
            bitmapLetter.Dispose();
        }

        [Fact]
        public void LandscapePage_IsWiderThanTall()
        {
            var doc = new PdfDocument();
            doc.AddPage(PageSize.A4.Landscape());
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "landscape_a4");
            var bitmap = TestHelper.RasterizePage(bytes, "landscape_a4");
            Assert.True(bitmap.Width > bitmap.Height);
            bitmap.Dispose();
        }

        [Fact]
        public void Metadata_CanBeSet()
        {
            var doc = new PdfDocument
            {
                Title = "Test Title",
                Author = "Test Author"
            };
            doc.AddPage();
            var bytes = doc.ToArray();

            // Verify valid PDF is generated and metadata strings appear in the raw bytes
            TestHelper.SavePdf(bytes, "metadata");
            var bitmap = TestHelper.RasterizePage(bytes, "metadata");
            Assert.True(bitmap.Width > 0);
            bitmap.Dispose();
            // Check that metadata strings appear in the PDF bytes
            var pdfText = System.Text.Encoding.ASCII.GetString(bytes);
            Assert.Contains("Test Title", pdfText);
            Assert.Contains("Test Author", pdfText);
        }

        [Fact]
        public void ToArray_ProducesValidPdfHeader()
        {
            var doc = new PdfDocument();
            doc.AddPage();
            var bytes = doc.ToArray();

            // Check PDF header
            Assert.Equal((byte)'%', bytes[0]);
            Assert.Equal((byte)'P', bytes[1]);
            Assert.Equal((byte)'D', bytes[2]);
            Assert.Equal((byte)'F', bytes[3]);
        }
    }
}
