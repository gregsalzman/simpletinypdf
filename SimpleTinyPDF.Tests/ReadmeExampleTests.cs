using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class ReadmeExampleTests
    {
        [Fact]
        public void InvoiceExample_ProducesImage()
        {
            var doc = new PdfDocument { Title = "Invoice #1042" };
            var page = doc.AddPage(PageSize.Letter);

            // Company info (right-aligned) — skip logo since no file exists
            page.DrawText("Acme Corp", 562, 40, PdfFont.HelveticaBold, 14, alignment: TextAlignment.Right);
            page.DrawText("123 Main Street, Springfield", 562, 58, PdfFont.Helvetica, 9,
                PdfColor.DarkGray, TextAlignment.Right);
            page.DrawText("Tel: (555) 123-4567", 562, 70, PdfFont.Helvetica, 9,
                PdfColor.DarkGray, TextAlignment.Right);

            // Divider
            page.DrawLine(50, 100, 562, 100, PdfColor.LightGray, 1f);

            // Invoice title
            page.DrawText("INVOICE", 50, 120, PdfFont.HelveticaBold, 24, PdfColor.Rgb(51, 51, 51));

            // Invoice details
            page.DrawText("Invoice #: 1042", 50, 160, PdfFont.Helvetica, 10);
            page.DrawText("Date: April 16, 2026", 50, 175, PdfFont.Helvetica, 10);
            page.DrawText("Due: May 16, 2026", 50, 190, PdfFont.Helvetica, 10);

            // Bill to
            page.DrawText("Bill To:", 350, 160, PdfFont.HelveticaBold, 10);
            page.DrawText("John Smith", 350, 175, PdfFont.Helvetica, 10);
            page.DrawText("456 Oak Avenue", 350, 190, PdfFont.Helvetica, 10);
            page.DrawText("Shelbyville, IL 62565", 350, 205, PdfFont.Helvetica, 10);

            // Line items table
            var table = new PdfTable(240, 80, 80, 112)
                .SetHeaders("Description", "Quantity", "Unit Price", "Amount")
                .AddRow("Web Development Services", "40 hrs", "$75.00", "$3,000.00")
                .AddRow("UI/UX Design", "16 hrs", "$85.00", "$1,360.00")
                .AddRow("Hosting Setup", "1", "$200.00", "$200.00");

            table.SetColumnAlignment(1, TextAlignment.Center);
            table.SetColumnAlignment(2, TextAlignment.Right);
            table.SetColumnAlignment(3, TextAlignment.Right);
            table.HeaderBackground = PdfColor.Rgb(51, 51, 51);
            table.HeaderTextColor = PdfColor.White;
            table.AlternateRowShading = true;

            float tableEndY = page.DrawTable(table, 50, 240);

            // Totals
            float totalsX = 370;
            float totalsY = tableEndY + 15;
            page.DrawText("Subtotal:", totalsX, totalsY, PdfFont.Helvetica, 10);
            page.DrawText("$4,560.00", 562, totalsY, PdfFont.Helvetica, 10, alignment: TextAlignment.Right);
            page.DrawText("Tax (8%):", totalsX, totalsY + 18, PdfFont.Helvetica, 10);
            page.DrawText("$364.80", 562, totalsY + 18, PdfFont.Helvetica, 10, alignment: TextAlignment.Right);
            page.DrawLine(totalsX, totalsY + 34, 562, totalsY + 34, PdfColor.Black, 0.5f);
            page.DrawText("Total Due:", totalsX, totalsY + 42, PdfFont.HelveticaBold, 12);
            page.DrawText("$4,924.80", 562, totalsY + 42, PdfFont.HelveticaBold, 12, alignment: TextAlignment.Right);

            // QR code for online payment
            page.DrawBarcode("https://github.com/gregsalzman/simpletinypdf", BarcodeType.QrCode,
                50, totalsY, 80, 80);
            page.DrawText("Scan to pay online.", 90, totalsY + 85, PdfFont.Helvetica, 8,
                PdfColor.DarkGray, TextAlignment.Center);

            // Footer note
            page.DrawText("Payment is due within 30 days. Thank you for your business!",
                306, 720, PdfFont.HelveticaOblique, 9, PdfColor.DarkGray, TextAlignment.Center);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "readme_invoice");
            var bitmap = TestHelper.RasterizePage(bytes, "readme_invoice", dpi: 200);
            Assert.True(bitmap.Width > 0);
        }

        [Fact]
        public void CsvTableExample_ProducesImage()
        {
            var csvContent = "Region,Product,Q1,Q2,Q3,Q4\n"
                + "North,Widgets,1200,1350,1100,1500\n"
                + "South,Widgets,980,1050,1200,1180\n"
                + "East,Widgets,1100,1200,1300,1400\n"
                + "West,Widgets,850,900,950,1000\n"
                + "North,Gadgets,600,700,650,800\n"
                + "South,Gadgets,450,500,550,600\n"
                + "East,Gadgets,700,750,800,850\n"
                + "West,Gadgets,400,420,460,500\n";

            var doc = new PdfDocument { Title = "Quarterly Sales Report" };
            var page = doc.AddPage(PageSize.Letter.Landscape());

            // Report header
            page.DrawText("Quarterly Sales Report", 50, 40, PdfFont.HelveticaBold, 20);
            page.DrawText("Generated: April 16, 2026", 50, 65, PdfFont.Helvetica, 10, PdfColor.DarkGray);
            page.DrawLine(50, 85, 742, 85, PdfColor.LightGray, 1f);

            // Import CSV directly into a table
            var table = PdfTable.FromCsvString(csvContent,
                firstRowIsHeader: true,
                columnWidths: new float[] { 100, 120, 90, 90, 90, 90 });

            // Style it
            table.HeaderBackground = PdfColor.Rgb(0, 51, 102);
            table.HeaderTextColor = PdfColor.White;
            table.HeaderFont = PdfFont.HelveticaBold;
            table.HeaderFontSize = 11;
            table.CellFont = PdfFont.Helvetica;
            table.CellFontSize = 10;
            table.AlternateRowShading = true;
            table.AlternateRowColor = PdfColor.Rgb(235, 241, 250);
            table.CellPadding = 6;

            // Right-align the numeric columns
            table.SetColumnAlignment(2, TextAlignment.Right);
            table.SetColumnAlignment(3, TextAlignment.Right);
            table.SetColumnAlignment(4, TextAlignment.Right);
            table.SetColumnAlignment(5, TextAlignment.Right);

            page.DrawTable(table, 50, 100, bottomMargin: 50, continuationY: 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "readme_csv_report");
            var bitmap = TestHelper.RasterizePage(bytes, "readme_csv_report", dpi: 200);
            Assert.True(bitmap.Width > 0);
        }
    }
}
