using System;
using System.IO;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class CsvTableTests
    {
        // --- CsvParser tests ---

        [Fact]
        public void Parse_SimpleValues()
        {
            var rows = CsvParser.Parse("a,b,c");
            Assert.Single(rows);
            Assert.Equal(new[] { "a", "b", "c" }, rows[0]);
        }

        [Fact]
        public void Parse_MultipleRows()
        {
            var rows = CsvParser.Parse("a,b\nc,d");
            Assert.Equal(2, rows.Count);
            Assert.Equal(new[] { "a", "b" }, rows[0]);
            Assert.Equal(new[] { "c", "d" }, rows[1]);
        }

        [Fact]
        public void Parse_QuotedField_WithComma()
        {
            var rows = CsvParser.Parse("a,\"b,c\",d");
            Assert.Single(rows);
            Assert.Equal(new[] { "a", "b,c", "d" }, rows[0]);
        }

        [Fact]
        public void Parse_QuotedField_WithEscapedQuote()
        {
            var rows = CsvParser.Parse("a,\"b\"\"c\",d");
            Assert.Single(rows);
            Assert.Equal(new[] { "a", "b\"c", "d" }, rows[0]);
        }

        [Fact]
        public void Parse_QuotedField_WithNewline()
        {
            var rows = CsvParser.Parse("a,\"b\nc\",d");
            Assert.Single(rows);
            Assert.Equal(new[] { "a", "b\nc", "d" }, rows[0]);
        }

        [Fact]
        public void Parse_CrLf_LineEndings()
        {
            var rows = CsvParser.Parse("a,b\r\nc,d");
            Assert.Equal(2, rows.Count);
            Assert.Equal(new[] { "a", "b" }, rows[0]);
            Assert.Equal(new[] { "c", "d" }, rows[1]);
        }

        [Fact]
        public void Parse_EmptyFields()
        {
            var rows = CsvParser.Parse("a,,c");
            Assert.Single(rows);
            Assert.Equal(new[] { "a", "", "c" }, rows[0]);
        }

        [Fact]
        public void Parse_TrailingNewline_DoesNotCreateEmptyRow()
        {
            var rows = CsvParser.Parse("a,b\n");
            Assert.Single(rows);
            Assert.Equal(new[] { "a", "b" }, rows[0]);
        }

        [Fact]
        public void Parse_CustomDelimiter_Tab()
        {
            var rows = CsvParser.Parse("a\tb\tc", '\t');
            Assert.Single(rows);
            Assert.Equal(new[] { "a", "b", "c" }, rows[0]);
        }

        [Fact]
        public void Parse_EmptyInput_ReturnsEmptyList()
        {
            var rows = CsvParser.Parse("");
            Assert.Empty(rows);
        }

        [Fact]
        public void Parse_SingleField()
        {
            var rows = CsvParser.Parse("hello");
            Assert.Single(rows);
            Assert.Equal(new[] { "hello" }, rows[0]);
        }

        [Fact]
        public void Parse_WhitespacePreserved()
        {
            var rows = CsvParser.Parse(" a , b ");
            Assert.Single(rows);
            Assert.Equal(new[] { " a ", " b " }, rows[0]);
        }

        // --- Factory method tests ---

        [Fact]
        public void FromCsvString_FirstRowIsHeader()
        {
            var table = PdfTable.FromCsvString("Name,Age,City\nAlice,30,Seattle\nBob,25,Portland");
            Assert.Equal(new[] { "Name", "Age", "City" }, table.Headers);
            Assert.Equal(2, table.Rows.Count);
            Assert.Equal(new[] { "Alice", "30", "Seattle" }, table.Rows[0]);
        }

        [Fact]
        public void FromCsvString_NoHeader()
        {
            var table = PdfTable.FromCsvString("Alice,30,Seattle\nBob,25,Portland",
                firstRowIsHeader: false);
            Assert.Null(table.Headers);
            Assert.Equal(2, table.Rows.Count);
        }

        [Fact]
        public void FromCsvString_AutoWidths_EqualDistribution()
        {
            var table = PdfTable.FromCsvString("a,b,c\n1,2,3", totalWidth: 600f);
            Assert.Equal(3, table.ColumnWidths.Length);
            Assert.Equal(200f, table.ColumnWidths[0]);
            Assert.Equal(200f, table.ColumnWidths[1]);
            Assert.Equal(200f, table.ColumnWidths[2]);
        }

        [Fact]
        public void FromCsvString_ExplicitWidths()
        {
            var table = PdfTable.FromCsvString("a,b,c\n1,2,3",
                columnWidths: new float[] { 100, 200, 300 });
            Assert.Equal(new float[] { 100, 200, 300 }, table.ColumnWidths);
        }

        [Fact]
        public void FromCsvString_CustomDelimiter()
        {
            var table = PdfTable.FromCsvString("a\tb\n1\t2", delimiter: '\t');
            Assert.Equal(new[] { "a", "b" }, table.Headers);
            Assert.Single(table.Rows);
        }

        [Fact]
        public void FromCsvString_NullContent_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => PdfTable.FromCsvString(null));
        }

        [Fact]
        public void FromCsvString_EmptyContent_Throws()
        {
            Assert.Throws<ArgumentException>(() => PdfTable.FromCsvString(""));
        }

        [Fact]
        public void FromCsv_Stream_Works()
        {
            var csv = "Name,Age\nAlice,30\nBob,25";
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv)))
            {
                var table = PdfTable.FromCsv(stream);
                Assert.Equal(new[] { "Name", "Age" }, table.Headers);
                Assert.Equal(2, table.Rows.Count);
            }
        }

        [Fact]
        public void FromCsv_File_Works()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "Name,Age\nAlice,30\nBob,25");
                var table = PdfTable.FromCsv(path);
                Assert.Equal(new[] { "Name", "Age" }, table.Headers);
                Assert.Equal(2, table.Rows.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        // --- Integration / rendering test ---

        private static int PtToPx(float pt) => (int)(pt * 150 / 72.0);

        [Fact]
        public void FromCsvString_RendersTable()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var table = PdfTable.FromCsvString("Name,Age\nAlice,30\nBob,25");
            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "csv_table_basic");
            var bitmap = TestHelper.RasterizePage(bytes, "csv_table_basic");

            // Verify header area has visible content
            bool found = false;
            for (int x = PtToPx(50); x < PtToPx(300) && !found; x++)
                for (int y = PtToPx(50); y < PtToPx(70) && !found; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Red < 200 || p.Green < 200 || p.Blue < 200) found = true;
                }
            Assert.True(found, "Expected visible header content from CSV table");
            bitmap.Dispose();
        }
    }
}
