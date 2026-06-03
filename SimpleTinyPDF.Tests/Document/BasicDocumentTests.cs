using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class BasicDocumentTests
    {
        [Fact]
        public void BlankPdf_IsValid_RasterizesWithoutError()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: a blank A4 page renders without error");
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/blank-a4-page");
            var bitmap = TestHelper.RasterizePage(bytes, "Document/blank-a4-page");
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
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: document has exactly 3 pages");
            doc.AddPage(PageSize.Letter);
            doc.AddPage(PageSize.A5);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/three-page-document");
            Assert.Equal(3, TestHelper.GetPageCount(bytes));
        }

        [Fact]
        public void DifferentPageSizes_ProduceDifferentDimensions()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: A4, Letter, and Legal pages have different dimensions");
            doc.AddPage(PageSize.Letter);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/mixed-page-sizes");
            var bitmapA4 = TestHelper.RasterizePage(bytes, "Document/mixed-page-sizes", 0);
            var bitmapLetter = TestHelper.RasterizePage(bytes, "Document/mixed-page-sizes", 1);

            // A4 is taller than Letter (842 vs 792 points)
            Assert.True(bitmapA4.Height > bitmapLetter.Height);
            bitmapA4.Dispose();
            bitmapLetter.Dispose();
        }

        [Fact]
        public void LandscapePage_IsWiderThanTall()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4.Landscape());
            TestHelper.AddDescription(page, "Verify: landscape A4 page is wider than tall");
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/landscape-a4-page");
            var bitmap = TestHelper.RasterizePage(bytes, "Document/landscape-a4-page");
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
            var page = doc.AddPage();
            TestHelper.AddDescription(page, "Verify: PDF metadata (title, author, subject) is set");
            var bytes = doc.ToArray();

            // Verify valid PDF is generated and metadata strings appear in the raw bytes
            TestHelper.SavePdf(bytes, "Document/document-with-metadata");
            var bitmap = TestHelper.RasterizePage(bytes, "Document/document-with-metadata");
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
