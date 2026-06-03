using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class TableTests
    {
        [Fact]
        public void BasicTable_Renders()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: basic 3-column table renders with headers and data rows");

            var table = new PdfTable(200, 100, 100)
                .SetHeaders("Name", "Age", "City")
                .AddRow("Alice", "30", "Seattle")
                .AddRow("Bob", "25", "Portland")
                .AddRow("Charlie", "35", "Denver");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/basic-3col-table");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/basic-3col-table");
            // Verify header row has visible text
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(250), TestHelper.PtToPx(50), TestHelper.PtToPx(75)),
                "Expected visible header text in first column");
            // Verify at least one data row is visible below header
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(250), TestHelper.PtToPx(75), TestHelper.PtToPx(100)),
                "Expected visible data row text below header");
            // Verify third column has content
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(350), TestHelper.PtToPx(450), TestHelper.PtToPx(50), TestHelper.PtToPx(100)),
                "Expected visible text in third column area");
            bitmap.Dispose();
        }

        [Fact]
        public void Table_WithAlternateRowShading()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: table rows alternate between white and gray backgrounds");

            var table = new PdfTable(200, 100, 100)
            {
                AlternateRowShading = true
            };
            table.SetHeaders("Col A", "Col B", "Col C");
            for (int i = 0; i < 10; i++)
                table.AddRow($"Row {i} A", $"Row {i} B", $"Row {i} C");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/alternating-row-colors");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/alternating-row-colors");
            // With 10 data rows + header, there should be content spanning a good portion of the page
            // Verify text is visible in the header and in a later data row
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(50), TestHelper.PtToPx(75)),
                "Expected visible header row content");
            // Check a row further down (row 5 should be around Y=50 + header + 5*rowHeight)
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(170), TestHelper.PtToPx(220)),
                "Expected visible data in middle rows");
            // Alternate rows should have slightly different background - check that even and odd rows differ
            // Pick centers of row 1 (even, white) and row 2 (odd, shaded)
            int evenRowY = TestHelper.PtToPx(85); // approx center of first data row
            int oddRowY = TestHelper.PtToPx(105); // approx center of second data row
            int sampleX = TestHelper.PtToPx(150); // middle of first column
            var evenPx = bitmap.GetPixel(sampleX, evenRowY);
            var oddPx = bitmap.GetPixel(sampleX, oddRowY);
            // At least one pair should differ — the shaded row should be slightly darker
            bool rowsDiffer = System.Math.Abs(evenPx.Red - oddPx.Red) > 5 ||
                              System.Math.Abs(evenPx.Green - oddPx.Green) > 5 ||
                              System.Math.Abs(evenPx.Blue - oddPx.Blue) > 5;
            Assert.True(rowsDiffer, "Expected alternate rows to have different background shading");
            bitmap.Dispose();
        }

        [Fact]
        public void Table_ColumnAlignment()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: table columns support left, center, and right alignment");

            var table = new PdfTable(200, 100, 100)
                .SetHeaders("Left", "Center", "Right")
                .SetColumnAlignment(1, TextAlignment.Center)
                .SetColumnAlignment(2, TextAlignment.Right)
                .AddRow("Text L", "Text C", "Text R")
                .AddRow("More L", "More C", "More R");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/column-text-alignment");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/column-text-alignment");
            // Left-aligned text in col 0 should appear near the left edge of the column
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(55), TestHelper.PtToPx(150), TestHelper.PtToPx(75), TestHelper.PtToPx(100)),
                "Expected left-aligned text near left edge of first column");
            // Right-aligned text in col 2 should appear near the right edge (col starts at 350, width 100)
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(380), TestHelper.PtToPx(450), TestHelper.PtToPx(75), TestHelper.PtToPx(100)),
                "Expected right-aligned text near right edge of third column");
            bitmap.Dispose();
        }

        [Fact]
        public void Table_MultiPage_CreatesNewPages()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: long table spans across multiple pages");

            var table = new PdfTable(200, 100, 100)
                .SetHeaders("ID", "Name", "Value");

            for (int i = 0; i < 60; i++)
                table.AddRow($"{i + 1}", $"Item {i + 1}", $"${(i + 1) * 10}");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/multipage-long-table");
            int pageCount = TestHelper.GetPageCount(bytes);
            Assert.True(pageCount >= 2, $"Expected at least 2 pages, got {pageCount}");

            // Verify all pages rasterize correctly
            for (int i = 0; i < pageCount; i++)
            {
                var bitmap = TestHelper.RasterizePage(bytes, "Tables/multipage-long-table", i);
                Assert.True(bitmap.Width > 0);
                bitmap.Dispose();
            }
        }

        [Fact]
        public void Table_MultiPage_RepeatsHeaders()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: table headers repeat on each page");

            var table = new PdfTable(200, 100, 100)
            {
                HeaderBackground = PdfColor.Rgb(0, 51, 102),
                HeaderTextColor = PdfColor.White
            };
            table.SetHeaders("ID", "Name", "Value");

            for (int i = 0; i < 80; i++)
                table.AddRow($"{i + 1}", $"Item {i + 1}", $"${(i + 1) * 10}");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/multipage-repeated-headers");
            int pageCount = TestHelper.GetPageCount(bytes);
            Assert.True(pageCount >= 2);

            // Check that page 2 has content at the top (headers should be there)
            var bmp2 = TestHelper.RasterizePage(bytes, "Tables/multipage-repeated-headers", 1);
            // Near the top of page 2, there should be the dark header background
            int topY = (int)(55 * 150 / 72.0);
            bool foundDark = false;
            for (int x = 100; x < 500 && !foundDark; x++)
            {
                var pixel = bmp2.GetPixel(x, topY);
                if (pixel.Red < 80 && pixel.Green < 80 && pixel.Blue < 150)
                    foundDark = true;
            }
            Assert.True(foundDark, "Expected header background at top of page 2");
            bmp2.Dispose();
        }

        [Fact]
        public void Table_CellWordWrap()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: long text wraps within table cells");

            var table = new PdfTable(150, 250)
                .SetHeaders("Short", "Description")
                .AddRow("Item 1", "This is a very long description that should wrap within the cell boundaries")
                .AddRow("Item 2", "Short desc");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/cell-word-wrap");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/cell-word-wrap");
            // The long description should wrap to multiple lines within the cell.
            // Header row is ~20pt, so first data row starts ~Y=70.
            // Check for text on both the first and second lines of the wrapped cell.
            float cellLineHeight = 10 * 1.2f; // default cell font size 10, lineSpacing 1.2
            float dataRowStart = 70; // approx
            // First line of wrapped text
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(200), TestHelper.PtToPx(450), TestHelper.PtToPx(dataRowStart), TestHelper.PtToPx(dataRowStart + 12)),
                "Expected text on line 1 of wrapped cell");
            // Second line of wrapped text (continuation)
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(200), TestHelper.PtToPx(450),
                TestHelper.PtToPx(dataRowStart + cellLineHeight), TestHelper.PtToPx(dataRowStart + cellLineHeight + 12)),
                "Expected wrapped text on line 2 of the long description cell");
            bitmap.Dispose();
        }

        [Fact]
        public void Table_CustomColors()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: custom header and row colors render correctly");

            var table = new PdfTable(200, 100, 100)
            {
                HeaderBackground = PdfColor.Rgb(0, 100, 0),
                HeaderTextColor = PdfColor.White,
                BorderColor = PdfColor.Rgb(0, 100, 0),
                BorderWidth = 1f,
                TextColor = PdfColor.DarkGray
            };
            table.SetHeaders("A", "B", "C")
                .AddRow("1", "2", "3")
                .AddRow("4", "5", "6");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/custom-header-row-colors");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/custom-header-row-colors");
            // Header should have green background
            int headerCenterX = TestHelper.PtToPx(150);
            int headerCenterY = TestHelper.PtToPx(60);
            var headerPx = bitmap.GetPixel(headerCenterX, headerCenterY);
            Assert.True(headerPx.Green > 80 && headerPx.Red < 50 && headerPx.Blue < 50,
                $"Expected green header background, got ({headerPx.Red},{headerPx.Green},{headerPx.Blue})");
            // Data rows should have gray text (not black, not white)
            // Just verify there is visible content in the data area
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(75), TestHelper.PtToPx(120)),
                "Expected visible data row content");
            bitmap.Dispose();
        }

        [Fact]
        public void Table_NoHeaders_JustRows()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: table renders correctly without header row");

            var table = new PdfTable(150, 150, 150)
                .AddRow("A1", "B1", "C1")
                .AddRow("A2", "B2", "C2")
                .AddRow("A3", "B3", "C3");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/table-without-headers");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/table-without-headers");
            // Without headers, data should start right at Y=50
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(200), TestHelper.PtToPx(50), TestHelper.PtToPx(75)),
                "Expected visible text in first row, first column");
            // Second column should also have content
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(200), TestHelper.PtToPx(350), TestHelper.PtToPx(50), TestHelper.PtToPx(75)),
                "Expected visible text in first row, second column");
            // Third row should be visible further down
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(200), TestHelper.PtToPx(90), TestHelper.PtToPx(120)),
                "Expected visible text in third row area");
            bitmap.Dispose();
        }
    }
}
