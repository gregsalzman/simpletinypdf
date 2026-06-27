using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutColumnTests
    {
        [Fact]
        public void TwoColumn_TextFlows()
        {
            var layout = new PdfDocumentLayout();
            layout.AddSection(new SectionOptions { ColumnCount = 2, ColumnGap = 18 });

            // Add enough text to fill the first column and flow into the second
            var sb = new StringBuilder();
            for (int i = 0; i < 60; i++)
                sb.AppendLine($"Line {i + 1} of two-column text.");
            layout.AddParagraph(sb.ToString());

            var doc = layout.Generate();
            Assert.True(doc.PageCount >= 1);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: text flows across two columns");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/column-two-column-flow");

            var bitmap = TestHelper.RasterizePage(bytes, "Layout/column-two-column-flow");

            // A4 width = 595, margins = 72 each side → content = 451
            // Column width = (451 - 18) / 2 ≈ 216.5
            // Left column: 72 to ~289, Right column: ~307 to ~523
            float contentW = 595f - 72 - 72;
            float colW = (contentW - 18) / 2;

            // Left column should have text
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(72 + colW),
                TestHelper.PtToPx(72), TestHelper.PtToPx(400)));

            // Right column should also have text
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72 + colW + 18), TestHelper.PtToPx(595 - 72),
                TestHelper.PtToPx(72), TestHelper.PtToPx(400)));
        }

        [Fact]
        public void ThreeColumn_Layout()
        {
            var layout = new PdfDocumentLayout();
            layout.AddSection(new SectionOptions { ColumnCount = 3, ColumnGap = 12 });

            var sb = new StringBuilder();
            for (int i = 0; i < 200; i++)
                sb.AppendLine($"Line {i + 1} three columns.");
            layout.AddParagraph(sb.ToString());

            var doc = layout.Generate();
            Assert.True(doc.PageCount >= 1);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: text flows across three columns");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/column-three-column");

            var bitmap = TestHelper.RasterizePage(bytes, "Layout/column-three-column");

            // All three columns should have text (check left, middle, right thirds)
            float contentW = 595f - 72 - 72;
            float colW = (contentW - 2 * 12) / 3;

            // Column 1
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(72 + colW),
                TestHelper.PtToPx(72), TestHelper.PtToPx(200)));

            // Column 2
            float col2X = 72 + colW + 12;
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(col2X), TestHelper.PtToPx(col2X + colW),
                TestHelper.PtToPx(72), TestHelper.PtToPx(200)));

            // Column 3
            float col3X = col2X + colW + 12;
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(col3X), TestHelper.PtToPx(col3X + colW),
                TestHelper.PtToPx(72), TestHelper.PtToPx(200)));
        }

        [Fact]
        public void ColumnBreak_ForcesNextColumn()
        {
            var layout = new PdfDocumentLayout();
            layout.AddSection(new SectionOptions { ColumnCount = 2, ColumnGap = 18 });

            layout.AddParagraph("Column 1 text");
            layout.AddColumnBreak();
            layout.AddParagraph("Column 2 text");

            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: column break moves text to second column");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/column-break");

            var pdfText = TestHelper.GetPdfText(bytes);
            Assert.Contains("Column 1", pdfText);
            Assert.Contains("Column 2", pdfText);

            var bitmap = TestHelper.RasterizePage(bytes, "Layout/column-break");

            float contentW = 595f - 72 - 72;
            float colW = (contentW - 18) / 2;

            // Left column has text
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(72 + colW),
                TestHelper.PtToPx(72), TestHelper.PtToPx(100)));

            // Right column has text
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72 + colW + 18), TestHelper.PtToPx(595 - 72),
                TestHelper.PtToPx(72), TestHelper.PtToPx(100)));
        }

        [Fact]
        public void ColumnBreak_LastColumn_CreatesNewPage()
        {
            var layout = new PdfDocumentLayout();
            layout.AddSection(new SectionOptions { ColumnCount = 2, ColumnGap = 18 });

            layout.AddParagraph("Page 1, Col 1");
            layout.AddColumnBreak();
            layout.AddParagraph("Page 1, Col 2");
            layout.AddColumnBreak(); // Last column → new page
            layout.AddParagraph("Page 2, Col 1");

            var doc = layout.Generate();
            Assert.Equal(2, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: column break on last column creates new page");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/column-break-new-page");

            var pdfText = TestHelper.GetPdfText(bytes);
            Assert.Contains("Page 2", pdfText);
        }

        [Fact]
        public void Column_ImageCappedToColumnWidth()
        {
            var layout = new PdfDocumentLayout();
            layout.AddSection(new SectionOptions { ColumnCount = 2, ColumnGap = 18 });

            var jpegBytes = TestHelper.CreateTestJpeg(SkiaSharp.SKColors.Blue, 400, 200);
            var image = PdfImage.FromBytes(jpegBytes);

            layout.AddImage(image); // Should be capped to column width, not full page width

            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: image is confined to column width");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/column-image");

            var bitmap = TestHelper.RasterizePage(bytes, "Layout/column-image");

            float contentW = 595f - 72 - 72;
            float colW = (contentW - 18) / 2;

            // Image should be in left column only — no blue pixels in right column
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(72 + colW),
                TestHelper.PtToPx(72), TestHelper.PtToPx(300)));
        }

        [Fact]
        public void Column_FlowAcrossMultiplePages()
        {
            var layout = new PdfDocumentLayout();
            layout.AddSection(new SectionOptions { ColumnCount = 2, ColumnGap = 18 });

            // Enough text to fill multiple pages in 2-column layout
            var sb = new StringBuilder();
            for (int i = 0; i < 200; i++)
                sb.AppendLine($"Line {i + 1} of multi-page column text.");
            layout.AddParagraph(sb.ToString());

            var doc = layout.Generate();
            Assert.True(doc.PageCount >= 2,
                $"Expected at least 2 pages but got {doc.PageCount}");

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: columns flow across multiple pages");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/column-multipage");

            // Second page should have content
            var bmp2 = TestHelper.RasterizePage(bytes, "Layout/column-multipage", 1);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bmp2,
                TestHelper.PtToPx(72), TestHelper.PtToPx(400),
                TestHelper.PtToPx(72), TestHelper.PtToPx(200)));
        }
    }
}
