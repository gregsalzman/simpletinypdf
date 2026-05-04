using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class TableTests
    {
        private static int PtToPx(float pt) => (int)(pt * 150 / 72.0);

        private static bool HasDarkPixelsInRegion(SkiaSharp.SKBitmap bitmap,
            int xMin, int xMax, int yMin, int yMax)
        {
            xMax = System.Math.Min(xMax, bitmap.Width - 1);
            yMax = System.Math.Min(yMax, bitmap.Height - 1);
            for (int x = System.Math.Max(0, xMin); x <= xMax; x++)
                for (int y = System.Math.Max(0, yMin); y <= yMax; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Red < 200 || p.Green < 200 || p.Blue < 200) return true;
                }
            return false;
        }

        [Fact]
        public void BasicTable_Renders()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            var table = new PdfTable(200, 100, 100)
                .SetHeaders("Name", "Age", "City")
                .AddRow("Alice", "30", "Seattle")
                .AddRow("Bob", "25", "Portland")
                .AddRow("Charlie", "35", "Denver");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "table_basic");
            var bitmap = TestHelper.RasterizePage(bytes, "table_basic");
            // Verify header row has visible text
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250), PtToPx(50), PtToPx(75)),
                "Expected visible header text in first column");
            // Verify at least one data row is visible below header
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250), PtToPx(75), PtToPx(100)),
                "Expected visible data row text below header");
            // Verify third column has content
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(350), PtToPx(450), PtToPx(50), PtToPx(100)),
                "Expected visible text in third column area");
            bitmap.Dispose();
        }

        [Fact]
        public void Table_WithAlternateRowShading()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            var table = new PdfTable(200, 100, 100)
            {
                AlternateRowShading = true
            };
            table.SetHeaders("Col A", "Col B", "Col C");
            for (int i = 0; i < 10; i++)
                table.AddRow($"Row {i} A", $"Row {i} B", $"Row {i} C");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "table_alternating");
            var bitmap = TestHelper.RasterizePage(bytes, "table_alternating");
            // With 10 data rows + header, there should be content spanning a good portion of the page
            // Verify text is visible in the header and in a later data row
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(400), PtToPx(50), PtToPx(75)),
                "Expected visible header row content");
            // Check a row further down (row 5 should be around Y=50 + header + 5*rowHeight)
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(400), PtToPx(170), PtToPx(220)),
                "Expected visible data in middle rows");
            // Alternate rows should have slightly different background - check that even and odd rows differ
            // Pick centers of row 1 (even, white) and row 2 (odd, shaded)
            int evenRowY = PtToPx(85); // approx center of first data row
            int oddRowY = PtToPx(105); // approx center of second data row
            int sampleX = PtToPx(150); // middle of first column
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

            var table = new PdfTable(200, 100, 100)
                .SetHeaders("Left", "Center", "Right")
                .SetColumnAlignment(1, TextAlignment.Center)
                .SetColumnAlignment(2, TextAlignment.Right)
                .AddRow("Text L", "Text C", "Text R")
                .AddRow("More L", "More C", "More R");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "table_alignment");
            var bitmap = TestHelper.RasterizePage(bytes, "table_alignment");
            // Left-aligned text in col 0 should appear near the left edge of the column
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(55), PtToPx(150), PtToPx(75), PtToPx(100)),
                "Expected left-aligned text near left edge of first column");
            // Right-aligned text in col 2 should appear near the right edge (col starts at 350, width 100)
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(380), PtToPx(450), PtToPx(75), PtToPx(100)),
                "Expected right-aligned text near right edge of third column");
            bitmap.Dispose();
        }

        [Fact]
        public void Table_MultiPage_CreatesNewPages()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            var table = new PdfTable(200, 100, 100)
                .SetHeaders("ID", "Name", "Value");

            for (int i = 0; i < 60; i++)
                table.AddRow($"{i + 1}", $"Item {i + 1}", $"${(i + 1) * 10}");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "table_multipage");
            int pageCount = TestHelper.GetPageCount(bytes);
            Assert.True(pageCount >= 2, $"Expected at least 2 pages, got {pageCount}");

            // Verify all pages rasterize correctly
            for (int i = 0; i < pageCount; i++)
            {
                var bitmap = TestHelper.RasterizePage(bytes, "table_multipage", i);
                Assert.True(bitmap.Width > 0);
                bitmap.Dispose();
            }
        }

        [Fact]
        public void Table_MultiPage_RepeatsHeaders()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

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
            TestHelper.SavePdf(bytes, "table_multipage_headers");
            int pageCount = TestHelper.GetPageCount(bytes);
            Assert.True(pageCount >= 2);

            // Check that page 2 has content at the top (headers should be there)
            var bmp2 = TestHelper.RasterizePage(bytes, "table_multipage_headers", 1);
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

            var table = new PdfTable(150, 250)
                .SetHeaders("Short", "Description")
                .AddRow("Item 1", "This is a very long description that should wrap within the cell boundaries")
                .AddRow("Item 2", "Short desc");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "table_wordwrap");
            var bitmap = TestHelper.RasterizePage(bytes, "table_wordwrap");
            // The long description should wrap to multiple lines within the cell.
            // Header row is ~20pt, so first data row starts ~Y=70.
            // Check for text on both the first and second lines of the wrapped cell.
            float cellLineHeight = 10 * 1.2f; // default cell font size 10, lineSpacing 1.2
            float dataRowStart = 70; // approx
            // First line of wrapped text
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(200), PtToPx(450), PtToPx(dataRowStart), PtToPx(dataRowStart + 12)),
                "Expected text on line 1 of wrapped cell");
            // Second line of wrapped text (continuation)
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(200), PtToPx(450),
                PtToPx(dataRowStart + cellLineHeight), PtToPx(dataRowStart + cellLineHeight + 12)),
                "Expected wrapped text on line 2 of the long description cell");
            bitmap.Dispose();
        }

        [Fact]
        public void Table_CustomColors()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

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
            TestHelper.SavePdf(bytes, "table_custom_colors");
            var bitmap = TestHelper.RasterizePage(bytes, "table_custom_colors");
            // Header should have green background
            int headerCenterX = PtToPx(150);
            int headerCenterY = PtToPx(60);
            var headerPx = bitmap.GetPixel(headerCenterX, headerCenterY);
            Assert.True(headerPx.Green > 80 && headerPx.Red < 50 && headerPx.Blue < 50,
                $"Expected green header background, got ({headerPx.Red},{headerPx.Green},{headerPx.Blue})");
            // Data rows should have gray text (not black, not white)
            // Just verify there is visible content in the data area
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(400), PtToPx(75), PtToPx(120)),
                "Expected visible data row content");
            bitmap.Dispose();
        }

        [Fact]
        public void Table_NoHeaders_JustRows()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            var table = new PdfTable(150, 150, 150)
                .AddRow("A1", "B1", "C1")
                .AddRow("A2", "B2", "C2")
                .AddRow("A3", "B3", "C3");

            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "table_no_headers");
            var bitmap = TestHelper.RasterizePage(bytes, "table_no_headers");
            // Without headers, data should start right at Y=50
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(200), PtToPx(50), PtToPx(75)),
                "Expected visible text in first row, first column");
            // Second column should also have content
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(200), PtToPx(350), PtToPx(50), PtToPx(75)),
                "Expected visible text in first row, second column");
            // Third row should be visible further down
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(200), PtToPx(90), PtToPx(120)),
                "Expected visible text in third row area");
            bitmap.Dispose();
        }
    }
}
