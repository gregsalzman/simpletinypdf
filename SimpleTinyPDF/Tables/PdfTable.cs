using System;
using System.Collections.Generic;
using System.IO;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Defines a table that can be rendered onto one or more pages.
    /// </summary>
    public sealed class PdfTable
    {
        internal readonly float[] ColumnWidths;
        internal readonly List<string[]> Rows = new List<string[]>();
        internal string[] Headers;
        internal readonly Dictionary<int, TextAlignment> ColumnAlignments = new Dictionary<int, TextAlignment>();

        /// <summary>Font for header cells. Default: HelveticaBold.</summary>
        public PdfFontSource HeaderFont { get; set; } = PdfFont.HelveticaBold;

        /// <summary>Font size for header cells. Default: 10.</summary>
        public float HeaderFontSize { get; set; } = 10f;

        /// <summary>Font for body cells. Default: Helvetica.</summary>
        public PdfFontSource CellFont { get; set; } = PdfFont.Helvetica;

        /// <summary>Font size for body cells. Default: 10.</summary>
        public float CellFontSize { get; set; } = 10f;

        /// <summary>Background color for the header row. Default: LightGray.</summary>
        public PdfColor HeaderBackground { get; set; } = PdfColor.LightGray;

        /// <summary>Text color for header cells. Default: Black.</summary>
        public PdfColor HeaderTextColor { get; set; } = PdfColor.Black;

        /// <summary>Border color. Default: Black.</summary>
        public PdfColor BorderColor { get; set; } = PdfColor.Black;

        /// <summary>Border line width in points. Default: 0.5.</summary>
        public float BorderWidth { get; set; } = 0.5f;

        /// <summary>Cell padding in points. Default: 4.</summary>
        public float CellPadding { get; set; } = 4f;

        /// <summary>Text color for body cells. Default: Black.</summary>
        public PdfColor TextColor { get; set; } = PdfColor.Black;

        /// <summary>If true, alternating rows have a tinted background.</summary>
        public bool AlternateRowShading { get; set; }

        /// <summary>Background color for alternate rows.</summary>
        public PdfColor AlternateRowColor { get; set; } = PdfColor.Rgb(0.95f, 0.95f, 0.95f);

        /// <summary>Line spacing multiplier for cell text. Default: 1.2.</summary>
        public float LineSpacing { get; set; } = 1.2f;

        /// <summary>Creates a table with the specified column widths in points.</summary>
        public PdfTable(params float[] columnWidths)
        {
            ColumnWidths = columnWidths;
        }

        /// <summary>Sets the header row text.</summary>
        public PdfTable SetHeaders(params string[] headers)
        {
            Headers = headers;
            return this;
        }

        /// <summary>Adds a data row to the table.</summary>
        public PdfTable AddRow(params string[] cells)
        {
            Rows.Add(cells);
            return this;
        }

        /// <summary>Sets text alignment for a specific column.</summary>
        public PdfTable SetColumnAlignment(int columnIndex, TextAlignment alignment)
        {
            ColumnAlignments[columnIndex] = alignment;
            return this;
        }

        /// <summary>Creates a table from a CSV file.</summary>
        /// <param name="filePath">Path to the CSV file.</param>
        /// <param name="firstRowIsHeader">If true, the first row is used as column headers.</param>
        /// <param name="delimiter">Field delimiter character.</param>
        /// <param name="columnWidths">Column widths in points. If null, columns are equally distributed across totalWidth.</param>
        /// <param name="totalWidth">Total table width in points, used when columnWidths is null. Default: 500.</param>
        public static PdfTable FromCsv(string filePath,
            bool firstRowIsHeader = true, char delimiter = ',',
            float[] columnWidths = null, float totalWidth = 500f)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            string content = File.ReadAllText(filePath);
            return FromCsvString(content, firstRowIsHeader, delimiter, columnWidths, totalWidth);
        }

        /// <summary>Creates a table from a CSV stream.</summary>
        /// <param name="stream">Stream containing CSV data.</param>
        /// <param name="firstRowIsHeader">If true, the first row is used as column headers.</param>
        /// <param name="delimiter">Field delimiter character.</param>
        /// <param name="columnWidths">Column widths in points. If null, columns are equally distributed across totalWidth.</param>
        /// <param name="totalWidth">Total table width in points, used when columnWidths is null. Default: 500.</param>
        public static PdfTable FromCsv(Stream stream,
            bool firstRowIsHeader = true, char delimiter = ',',
            float[] columnWidths = null, float totalWidth = 500f)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using (var reader = new StreamReader(stream))
            {
                string content = reader.ReadToEnd();
                return FromCsvString(content, firstRowIsHeader, delimiter, columnWidths, totalWidth);
            }
        }

        /// <summary>Creates a table from CSV string content.</summary>
        /// <param name="csvContent">CSV-formatted string.</param>
        /// <param name="firstRowIsHeader">If true, the first row is used as column headers.</param>
        /// <param name="delimiter">Field delimiter character.</param>
        /// <param name="columnWidths">Column widths in points. If null, columns are equally distributed across totalWidth.</param>
        /// <param name="totalWidth">Total table width in points, used when columnWidths is null. Default: 500.</param>
        public static PdfTable FromCsvString(string csvContent,
            bool firstRowIsHeader = true, char delimiter = ',',
            float[] columnWidths = null, float totalWidth = 500f)
        {
            if (csvContent == null) throw new ArgumentNullException(nameof(csvContent));

            var rows = CsvParser.Parse(csvContent, delimiter);
            if (rows.Count == 0)
                throw new ArgumentException("CSV content contains no data.", nameof(csvContent));

            int columnCount = rows[0].Length;

            float[] widths = columnWidths;
            if (widths == null || widths.Length == 0)
            {
                float colWidth = totalWidth / columnCount;
                widths = new float[columnCount];
                for (int i = 0; i < columnCount; i++)
                    widths[i] = colWidth;
            }

            var table = new PdfTable(widths);

            int startRow = 0;
            if (firstRowIsHeader && rows.Count > 0)
            {
                table.SetHeaders(rows[0]);
                startRow = 1;
            }

            for (int r = startRow; r < rows.Count; r++)
            {
                table.AddRow(rows[r]);
            }

            return table;
        }

        internal float Render(PdfPage page, float x, float y, float bottomMargin, float? continuationY = null)
        {
            var currentPage = page;
            float currentY = y;
            float topMargin = continuationY ?? y; // for continuation pages

            float headerHeight = 0;
            if (Headers != null)
            {
                headerHeight = CalculateRowHeight(Headers, HeaderFont, HeaderFontSize);
                DrawRow(currentPage, Headers, x, currentY, headerHeight, true, -1);
                currentY += headerHeight;
            }

            for (int rowIdx = 0; rowIdx < Rows.Count; rowIdx++)
            {
                var row = Rows[rowIdx];
                float rowHeight = CalculateRowHeight(row, CellFont, CellFontSize);

                // Check if row fits on current page
                if (currentY + rowHeight > currentPage.Height - bottomMargin)
                {
                    // Create continuation page
                    var doc = currentPage.Document;
                    if (doc == null) break; // can't create new pages without document reference
                    currentPage = doc.AddPage(new PageSize(currentPage.Width, currentPage.Height));
                    currentY = topMargin;

                    // Re-draw header on new page
                    if (Headers != null)
                    {
                        DrawRow(currentPage, Headers, x, currentY, headerHeight, true, -1);
                        currentY += headerHeight;
                    }
                }

                DrawRow(currentPage, row, x, currentY, rowHeight, false, rowIdx);
                currentY += rowHeight;
            }

            return currentY;
        }

        private float CalculateRowHeight(string[] cells, PdfFontSource font, float fontSize)
        {
            float maxHeight = 0;
            for (int i = 0; i < cells.Length && i < ColumnWidths.Length; i++)
            {
                float availWidth = ColumnWidths[i] - 2 * CellPadding;
                var lines = FontMetrics.WrapText(cells[i] ?? "", font, fontSize, availWidth);
                float cellHeight = lines.Count * fontSize * LineSpacing + 2 * CellPadding;
                if (cellHeight > maxHeight)
                    maxHeight = cellHeight;
            }
            return maxHeight < (fontSize + 2 * CellPadding) ? (fontSize + 2 * CellPadding) : maxHeight;
        }

        private void DrawRow(PdfPage page, string[] cells, float x, float y,
            float rowHeight, bool isHeader, int rowIndex)
        {
            float currentX = x;
            var font = isHeader ? HeaderFont : CellFont;
            var fontSize = isHeader ? HeaderFontSize : CellFontSize;
            var textColor = isHeader ? HeaderTextColor : TextColor;

            for (int i = 0; i < ColumnWidths.Length; i++)
            {
                float colWidth = ColumnWidths[i];

                // Background
                if (isHeader)
                {
                    page.DrawFilledRectangle(currentX, y, colWidth, rowHeight, HeaderBackground);
                }
                else if (AlternateRowShading && rowIndex % 2 == 1)
                {
                    page.DrawFilledRectangle(currentX, y, colWidth, rowHeight, AlternateRowColor);
                }

                // Border
                page.DrawRectangle(currentX, y, colWidth, rowHeight, BorderColor, BorderWidth);

                // Text
                string cellText = (i < cells.Length ? cells[i] : "") ?? "";
                if (cellText.Length > 0)
                {
                    float textX = currentX + CellPadding;
                    float textY = y + CellPadding;
                    float textWidth = colWidth - 2 * CellPadding;

                    TextAlignment align = TextAlignment.Left;
                    if (ColumnAlignments.ContainsKey(i))
                        align = ColumnAlignments[i];

                    page.DrawText(cellText, textX, textY, font, fontSize,
                        textColor, align, width: textWidth, lineSpacing: LineSpacing);
                }

                currentX += colWidth;
            }
        }
    }
}
