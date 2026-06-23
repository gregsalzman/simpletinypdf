using System.Linq;
using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutPaginationTests
    {
        [Fact]
        public void LongText_SpansMultiplePages()
        {
            var layout = new PdfDocumentLayout();

            // Generate enough text to fill multiple pages
            var sb = new StringBuilder();
            for (int i = 0; i < 100; i++)
                sb.AppendLine($"This is line {i + 1} of a long document that should span multiple pages.");
            layout.AddParagraph(sb.ToString());

            var doc = layout.Generate();
            Assert.True(doc.PageCount >= 2, $"Expected at least 2 pages but got {doc.PageCount}");

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: long text flows across pages");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/pagination-long-text");

            // Verify text on first page
            var bitmap1 = TestHelper.RasterizePage(bytes, "Layout/pagination-long-text", 0);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap1,
                TestHelper.PtToPx(72), TestHelper.PtToPx(400),
                TestHelper.PtToPx(72), TestHelper.PtToPx(200)));

            // Verify text on second page
            var bitmap2 = TestHelper.RasterizePage(bytes, "Layout/pagination-long-text", 1);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap2,
                TestHelper.PtToPx(72), TestHelper.PtToPx(400),
                TestHelper.PtToPx(72), TestHelper.PtToPx(200)));
        }

        [Fact]
        public void ManualPageBreak_CreatesNewPage()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Page one content");
            layout.AddPageBreak();
            layout.AddParagraph("Page two content");

            var doc = layout.Generate();
            Assert.Equal(2, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: content on separate pages via page break");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/pagination-manual-break");

            // Both pages should have content
            var bmp1 = TestHelper.RasterizePage(bytes, "Layout/pagination-manual-break", 0);
            var bmp2 = TestHelper.RasterizePage(bytes, "Layout/pagination-manual-break", 1);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bmp1,
                TestHelper.PtToPx(72), TestHelper.PtToPx(300),
                TestHelper.PtToPx(72), TestHelper.PtToPx(100)));
            Assert.True(TestHelper.HasDarkPixelsInRegion(bmp2,
                TestHelper.PtToPx(72), TestHelper.PtToPx(300),
                TestHelper.PtToPx(72), TestHelper.PtToPx(100)));
        }

        [Fact]
        public void ConsecutivePageBreaks_CreateEmptyPages()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Page 1");
            layout.AddPageBreak();
            layout.AddPageBreak();
            layout.AddParagraph("Page 3");

            var doc = layout.Generate();
            Assert.Equal(3, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: page 2 is empty");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/pagination-consecutive-breaks");
        }

        [Fact]
        public void ImageOverflow_MovesToNextPage()
        {
            var layout = new PdfDocumentLayout();

            // Fill most of the first page with text
            var sb = new StringBuilder();
            for (int i = 0; i < 45; i++)
                sb.AppendLine($"Line {i + 1} filling up the page with text content.");
            layout.AddParagraph(sb.ToString());

            // Add a tall image that won't fit
            var jpegBytes = TestHelper.CreateTestJpeg(SkiaSharp.SKColors.Green, 200, 200);
            var image = PdfImage.FromBytes(jpegBytes);
            layout.AddImage(image, new ImageOptions { Width = 200, Height = 200 });

            var doc = layout.Generate();
            Assert.True(doc.PageCount >= 2, $"Expected at least 2 pages but got {doc.PageCount}");

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: image overflows to next page");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/pagination-image-overflow");
        }

        [Fact]
        public void TableOverflow_CreatesPages()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Table follows:");

            var table = new PdfTable(100, 100, 100);
            table.SetHeaders("Col A", "Col B", "Col C");
            for (int i = 0; i < 60; i++)
                table.AddRow($"R{i}A", $"R{i}B", $"R{i}C");
            layout.AddTable(table);

            var doc = layout.Generate();
            Assert.True(doc.PageCount >= 2, $"Expected at least 2 pages but got {doc.PageCount}");

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: table spans multiple pages");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/pagination-table-overflow");
        }

        [Fact]
        public void ParagraphSplitsAcrossPages()
        {
            var layout = new PdfDocumentLayout();

            // Fill most of page 1 with content
            var filler = new StringBuilder();
            for (int i = 0; i < 42; i++)
                filler.AppendLine($"Filler line {i + 1} to nearly fill the page.");
            layout.AddParagraph(filler.ToString());

            // Add a paragraph that will need to split across the page boundary
            var splitPara = new StringBuilder();
            for (int i = 0; i < 20; i++)
                splitPara.AppendLine($"Split paragraph line {i + 1} that crosses the boundary.");
            layout.AddParagraph(splitPara.ToString());

            var doc = layout.Generate();
            Assert.True(doc.PageCount >= 2, $"Expected at least 2 pages but got {doc.PageCount}");

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: paragraph splits across page boundary");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/pagination-paragraph-split");
        }

        [Fact]
        public void SpaceBefore_CanTriggerPageBreak()
        {
            var layout = new PdfDocumentLayout();

            // Fill most of the page
            var filler = new StringBuilder();
            for (int i = 0; i < 46; i++)
                filler.AppendLine($"Line {i + 1}.");
            layout.AddParagraph(filler.ToString());

            // Large space before should push to next page
            layout.AddParagraph("After big space", new ParagraphOptions { SpaceBefore = 100 });

            var doc = layout.Generate();
            Assert.True(doc.PageCount >= 2);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: SpaceBefore pushes content to next page");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/pagination-space-before");
        }
    }
}
