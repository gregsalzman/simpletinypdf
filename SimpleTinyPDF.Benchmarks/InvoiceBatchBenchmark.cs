using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Md = MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace SimpleTinyPDF.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 2, iterationCount: 5)]
public class InvoiceBatchBenchmark
{
    private string _invoiceHtml = null!;

    [GlobalSetup]
    public void Setup()
    {
        // PDFsharp font resolver (required in .NET Core builds)
        if (PdfSharp.Fonts.GlobalFontSettings.FontResolver == null)
            PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;

        // QuestPDF license
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // Pre-build HTML for IronPDF (all 1000 invoices in one HTML)
        _invoiceHtml = BuildInvoiceHtml();

        // Warm up IronPDF's Chromium engine (requires license for production use)
        try
        {
            var renderer = new global::IronPdf.ChromePdfRenderer();
            renderer.RenderHtmlAsPdf("<html><body><p>warmup</p></body></html>");
        }
        catch { /* IronPDF requires a commercial license — benchmark will show as N/A */ }
    }

    private static string BuildInvoiceHtml()
    {
        var sb = new StringBuilder();
        sb.Append(@"<html><head><style>
            body { font-family: Helvetica, Arial, sans-serif; margin: 0; }
            .invoice { page-break-after: always; padding: 50px; box-sizing: border-box; }
            .invoice:last-child { page-break-after: auto; }
            .company { text-align: right; }
            .company h2 { margin: 0; font-size: 14px; }
            .company p { margin: 2px 0; font-size: 9px; color: #555; }
            hr { border: none; border-top: 1px solid #d4d4d4; margin: 15px 0; }
            .title { font-size: 24px; font-weight: bold; color: #333; }
            .details { display: flex; justify-content: space-between; margin-top: 20px; }
            .details p { margin: 2px 0; font-size: 10px; }
            .details strong { font-size: 10px; }
            table { width: 100%; border-collapse: collapse; margin-top: 20px; }
            th { background: #333; color: white; padding: 4px 8px; text-align: left; font-size: 10px; }
            td { padding: 4px 8px; border: 1px solid #000; font-size: 10px; }
            tr:nth-child(even) { background: #f2f2f2; }
            .totals { text-align: right; margin-top: 15px; font-size: 10px; }
            .totals .total-line { font-weight: bold; font-size: 12px; border-top: 1px solid #000; padding-top: 5px; }
            .footer { text-align: center; font-style: italic; font-size: 9px; color: #555; margin-top: 40px; }
        </style></head><body>");

        for (int i = 1; i <= InvoiceData.BatchSize; i++)
        {
            sb.Append($@"<div class='invoice'>
                <div class='company'>
                    <h2>{InvoiceData.CompanyName}</h2>
                    <p>{InvoiceData.CompanyAddress}</p>
                    <p>{InvoiceData.CompanyPhone}</p>
                </div>
                <hr/>
                <div class='title'>INVOICE</div>
                <div class='details'>
                    <div>
                        <p>Invoice #: {i}</p>
                        <p>Date: April 16, 2026</p>
                        <p>Due: May 16, 2026</p>
                    </div>
                    <div>
                        <p><strong>Bill To:</strong></p>
                        <p>{InvoiceData.BillToName}</p>
                        <p>{InvoiceData.BillToAddress}</p>
                        <p>{InvoiceData.BillToCityState}</p>
                    </div>
                </div>
                <table>
                    <tr><th>Description</th><th style='text-align:center'>Quantity</th><th style='text-align:right'>Unit Price</th><th style='text-align:right'>Amount</th></tr>");

            foreach (var item in InvoiceData.LineItems)
                sb.Append($"<tr><td>{item.Desc}</td><td style='text-align:center'>{item.Qty}</td><td style='text-align:right'>{item.Price}</td><td style='text-align:right'>{item.Amount}</td></tr>");

            sb.Append($@"</table>
                <div class='totals'>
                    <p>Subtotal: {InvoiceData.Subtotal}</p>
                    <p>Tax (8%): {InvoiceData.Tax}</p>
                    <p class='total-line'>Total Due: {InvoiceData.Total}</p>
                </div>
                <div class='footer'>{InvoiceData.FooterNote}</div>
            </div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    [Benchmark(Description = "SimpleTinyPDF")]
    public byte[] SimpleTinyPdf()
    {
        var doc = new PdfDocument { Title = "Invoice Batch" };

        for (int i = 1; i <= InvoiceData.BatchSize; i++)
        {
            var page = doc.AddPage(PageSize.Letter);
            DrawInvoice(page, i);
        }

        return doc.ToArray();
    }

    private static void DrawInvoice(PdfPage page, int invoiceNumber)
    {
        // Company info (right-aligned)
        page.DrawText(InvoiceData.CompanyName, 562, 40, PdfFont.HelveticaBold, 14,
            alignment: TextAlignment.Right);
        page.DrawText(InvoiceData.CompanyAddress, 562, 58, PdfFont.Helvetica, 9,
            PdfColor.DarkGray, TextAlignment.Right);
        page.DrawText(InvoiceData.CompanyPhone, 562, 70, PdfFont.Helvetica, 9,
            PdfColor.DarkGray, TextAlignment.Right);

        // Divider
        page.DrawLine(50, 100, 562, 100, PdfColor.LightGray, 1f);

        // Invoice title
        page.DrawText("INVOICE", 50, 120, PdfFont.HelveticaBold, 24, PdfColor.Rgb(51, 51, 51));

        // Invoice details
        page.DrawText($"Invoice #: {invoiceNumber}", 50, 160, PdfFont.Helvetica, 10);
        page.DrawText("Date: April 16, 2026", 50, 175, PdfFont.Helvetica, 10);
        page.DrawText("Due: May 16, 2026", 50, 190, PdfFont.Helvetica, 10);

        // Bill to
        page.DrawText("Bill To:", 350, 160, PdfFont.HelveticaBold, 10);
        page.DrawText(InvoiceData.BillToName, 350, 175, PdfFont.Helvetica, 10);
        page.DrawText(InvoiceData.BillToAddress, 350, 190, PdfFont.Helvetica, 10);
        page.DrawText(InvoiceData.BillToCityState, 350, 205, PdfFont.Helvetica, 10);

        // Line items table
        var table = new PdfTable(240, 80, 80, 112)
            .SetHeaders("Description", "Quantity", "Unit Price", "Amount");

        foreach (var item in InvoiceData.LineItems)
            table.AddRow(item.Desc, item.Qty, item.Price, item.Amount);

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
        page.DrawText(InvoiceData.Subtotal, 562, totalsY, PdfFont.Helvetica, 10,
            alignment: TextAlignment.Right);
        page.DrawText("Tax (8%):", totalsX, totalsY + 18, PdfFont.Helvetica, 10);
        page.DrawText(InvoiceData.Tax, 562, totalsY + 18, PdfFont.Helvetica, 10,
            alignment: TextAlignment.Right);
        page.DrawLine(totalsX, totalsY + 34, 562, totalsY + 34, PdfColor.Black, 0.5f);
        page.DrawText("Total Due:", totalsX, totalsY + 42, PdfFont.HelveticaBold, 12);
        page.DrawText(InvoiceData.Total, 562, totalsY + 42, PdfFont.HelveticaBold, 12,
            alignment: TextAlignment.Right);

        // Footer
        page.DrawText(InvoiceData.FooterNote, 306, 720, PdfFont.HelveticaOblique, 9,
            PdfColor.DarkGray, TextAlignment.Center);
    }

    [Benchmark(Description = "PDFsharp + MigraDoc")]
    public byte[] BenchPdfSharp()
    {
        var document = new Md.Document();
        document.Info.Title = "Invoice Batch";

        var style = document.Styles["Normal"];
        style!.Font.Name = "Arial";
        style.Font.Size = 10;

        for (int inv = 1; inv <= InvoiceData.BatchSize; inv++)
        {
            var section = document.AddSection();
            section.PageSetup.PageFormat = Md.PageFormat.Letter;
            section.PageSetup.LeftMargin = Md.Unit.FromPoint(50);
            section.PageSetup.RightMargin = Md.Unit.FromPoint(50);
            section.PageSetup.TopMargin = Md.Unit.FromPoint(40);
            section.PageSetup.BottomMargin = Md.Unit.FromPoint(40);

            // Company info (right-aligned)
            var p = section.AddParagraph(InvoiceData.CompanyName);
            p.Format.Alignment = Md.ParagraphAlignment.Right;
            p.Format.Font.Bold = true;
            p.Format.Font.Size = 14;
            p.Format.SpaceAfter = 0;

            p = section.AddParagraph(InvoiceData.CompanyAddress);
            p.Format.Alignment = Md.ParagraphAlignment.Right;
            p.Format.Font.Size = 9;
            p.Format.Font.Color = new Md.Color(84, 84, 84);
            p.Format.SpaceBefore = 0;
            p.Format.SpaceAfter = 0;

            p = section.AddParagraph(InvoiceData.CompanyPhone);
            p.Format.Alignment = Md.ParagraphAlignment.Right;
            p.Format.Font.Size = 9;
            p.Format.Font.Color = new Md.Color(84, 84, 84);
            p.Format.SpaceBefore = 0;

            // Divider
            p = section.AddParagraph();
            p.Format.Borders.Bottom.Width = 1;
            p.Format.Borders.Bottom.Color = new Md.Color(212, 212, 212);
            p.Format.SpaceBefore = Md.Unit.FromPoint(10);
            p.Format.SpaceAfter = Md.Unit.FromPoint(10);

            // Invoice title
            p = section.AddParagraph("INVOICE");
            p.Format.Font.Bold = true;
            p.Format.Font.Size = 24;
            p.Format.Font.Color = new Md.Color(51, 51, 51);
            p.Format.SpaceAfter = Md.Unit.FromPoint(10);

            // Details + Bill To (2-column borderless table)
            var detailsTable = section.AddTable();
            detailsTable.AddColumn(Md.Unit.FromPoint(250));
            detailsTable.AddColumn(Md.Unit.FromPoint(250));
            var detailsRow = detailsTable.AddRow();

            detailsRow.Cells[0].AddParagraph($"Invoice #: {inv}");
            detailsRow.Cells[0].AddParagraph("Date: April 16, 2026");
            detailsRow.Cells[0].AddParagraph("Due: May 16, 2026");

            var bp = detailsRow.Cells[1].AddParagraph("Bill To:");
            bp.Format.Font.Bold = true;
            detailsRow.Cells[1].AddParagraph(InvoiceData.BillToName);
            detailsRow.Cells[1].AddParagraph(InvoiceData.BillToAddress);
            detailsRow.Cells[1].AddParagraph(InvoiceData.BillToCityState);

            // Line items table
            var table = section.AddTable();
            table.Borders.Width = 0.5;
            table.Format.Font.Size = 10;

            float[] colWidths = { 240, 80, 80, 112 };
            foreach (var w in colWidths)
                table.AddColumn(Md.Unit.FromPoint(w));

            string[] headers = { "Description", "Quantity", "Unit Price", "Amount" };
            var headerRow = table.AddRow();
            headerRow.Shading.Color = new Md.Color(51, 51, 51);
            headerRow.Format.Font.Bold = true;
            headerRow.Format.Font.Color = Md.Colors.White;
            for (int c = 0; c < 4; c++)
            {
                headerRow.Cells[c].AddParagraph(headers[c]);
                if (c == 1) headerRow.Cells[c].Format.Alignment = Md.ParagraphAlignment.Center;
                if (c >= 2) headerRow.Cells[c].Format.Alignment = Md.ParagraphAlignment.Right;
            }

            for (int r = 0; r < InvoiceData.LineItems.Length; r++)
            {
                var item = InvoiceData.LineItems[r];
                var row = table.AddRow();
                if (r % 2 == 1)
                    row.Shading.Color = new Md.Color(242, 242, 242);

                string[] cells = { item.Desc, item.Qty, item.Price, item.Amount };
                for (int c = 0; c < 4; c++)
                {
                    row.Cells[c].AddParagraph(cells[c]);
                    if (c == 1) row.Cells[c].Format.Alignment = Md.ParagraphAlignment.Center;
                    if (c >= 2) row.Cells[c].Format.Alignment = Md.ParagraphAlignment.Right;
                }
            }

            // Totals
            p = section.AddParagraph($"Subtotal: {InvoiceData.Subtotal}");
            p.Format.Alignment = Md.ParagraphAlignment.Right;
            p.Format.SpaceBefore = Md.Unit.FromPoint(10);

            p = section.AddParagraph($"Tax (8%): {InvoiceData.Tax}");
            p.Format.Alignment = Md.ParagraphAlignment.Right;
            p.Format.SpaceBefore = 0;

            p = section.AddParagraph($"Total Due: {InvoiceData.Total}");
            p.Format.Alignment = Md.ParagraphAlignment.Right;
            p.Format.Font.Bold = true;
            p.Format.Font.Size = 12;
            p.Format.Borders.Top.Width = 0.5;
            p.Format.SpaceBefore = Md.Unit.FromPoint(5);

            // Footer
            p = section.AddParagraph(InvoiceData.FooterNote);
            p.Format.Alignment = Md.ParagraphAlignment.Center;
            p.Format.Font.Italic = true;
            p.Format.Font.Size = 9;
            p.Format.Font.Color = new Md.Color(84, 84, 84);
            p.Format.SpaceBefore = Md.Unit.FromPoint(30);
        }

        var renderer = new PdfDocumentRenderer();
        renderer.Document = document;
        renderer.RenderDocument();

        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

#if INCLUDE_ITEXT
    [Benchmark(Description = "iText")]
    public byte[] IText()
    {
        using var ms = new MemoryStream();
        var writer = new iText.Kernel.Pdf.PdfWriter(ms);
        var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
        pdf.SetDefaultPageSize(iText.Kernel.Geom.PageSize.LETTER);
        var document = new iText.Layout.Document(pdf);

        var boldFont14 = iText.Kernel.Font.PdfFontFactory.CreateFont(
            iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
        var boldFont = iText.Kernel.Font.PdfFontFactory.CreateFont(
            iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
        var normalFont = iText.Kernel.Font.PdfFontFactory.CreateFont(
            iText.IO.Font.Constants.StandardFonts.HELVETICA);
        var italicFont = iText.Kernel.Font.PdfFontFactory.CreateFont(
            iText.IO.Font.Constants.StandardFonts.HELVETICA_OBLIQUE);

        var darkGray = new iText.Kernel.Colors.DeviceRgb(84, 84, 84);
        var headerBg = new iText.Kernel.Colors.DeviceRgb(51, 51, 51);
        var altBg = new iText.Kernel.Colors.DeviceRgb(242, 242, 242);
        var lightGray = new iText.Kernel.Colors.DeviceRgb(212, 212, 212);
        var titleColor = new iText.Kernel.Colors.DeviceRgb(51, 51, 51);

        for (int inv = 1; inv <= InvoiceData.BatchSize; inv++)
        {
            if (inv > 1)
                document.Add(new iText.Layout.Element.AreaBreak(
                    iText.Layout.Properties.AreaBreakType.NEXT_PAGE));

            // Company info (right-aligned)
            document.Add(new iText.Layout.Element.Paragraph(InvoiceData.CompanyName)
                .SetFont(boldFont14).SetFontSize(14)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT)
                .SetMarginBottom(0));
            document.Add(new iText.Layout.Element.Paragraph(InvoiceData.CompanyAddress)
                .SetFont(normalFont).SetFontSize(9).SetFontColor(darkGray)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT)
                .SetMarginBottom(0).SetMarginTop(0));
            document.Add(new iText.Layout.Element.Paragraph(InvoiceData.CompanyPhone)
                .SetFont(normalFont).SetFontSize(9).SetFontColor(darkGray)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT)
                .SetMarginTop(0));

            // Divider
            document.Add(new iText.Layout.Element.LineSeparator(
                new iText.Kernel.Pdf.Canvas.Draw.SolidLine(1))
                .SetStrokeColor(lightGray));

            // Invoice title
            document.Add(new iText.Layout.Element.Paragraph("INVOICE")
                .SetFont(boldFont).SetFontSize(24).SetFontColor(titleColor));

            // Invoice details + bill to as a 2-column table
            var detailsTable = new iText.Layout.Element.Table(
                iText.Layout.Properties.UnitValue.CreatePercentArray(new float[] { 1, 1 }))
                .UseAllAvailableWidth().SetBorder(iText.Layout.Borders.Border.NO_BORDER);

            var leftCell = new iText.Layout.Element.Cell()
                .SetBorder(iText.Layout.Borders.Border.NO_BORDER);
            leftCell.Add(new iText.Layout.Element.Paragraph($"Invoice #: {inv}")
                .SetFont(normalFont).SetFontSize(10).SetMarginBottom(0));
            leftCell.Add(new iText.Layout.Element.Paragraph("Date: April 16, 2026")
                .SetFont(normalFont).SetFontSize(10).SetMarginBottom(0).SetMarginTop(0));
            leftCell.Add(new iText.Layout.Element.Paragraph("Due: May 16, 2026")
                .SetFont(normalFont).SetFontSize(10).SetMarginTop(0));

            var rightCell = new iText.Layout.Element.Cell()
                .SetBorder(iText.Layout.Borders.Border.NO_BORDER);
            rightCell.Add(new iText.Layout.Element.Paragraph("Bill To:")
                .SetFont(boldFont).SetFontSize(10).SetMarginBottom(0));
            rightCell.Add(new iText.Layout.Element.Paragraph(InvoiceData.BillToName)
                .SetFont(normalFont).SetFontSize(10).SetMarginBottom(0).SetMarginTop(0));
            rightCell.Add(new iText.Layout.Element.Paragraph(InvoiceData.BillToAddress)
                .SetFont(normalFont).SetFontSize(10).SetMarginBottom(0).SetMarginTop(0));
            rightCell.Add(new iText.Layout.Element.Paragraph(InvoiceData.BillToCityState)
                .SetFont(normalFont).SetFontSize(10).SetMarginTop(0));

            detailsTable.AddCell(leftCell);
            detailsTable.AddCell(rightCell);
            document.Add(detailsTable);

            // Line items table
            float[] colWidths = { 240, 80, 80, 112 };
            var table = new iText.Layout.Element.Table(
                iText.Layout.Properties.UnitValue.CreatePointArray(colWidths));

            string[] headers = { "Description", "Quantity", "Unit Price", "Amount" };
            var alignments = new[] {
                iText.Layout.Properties.TextAlignment.LEFT,
                iText.Layout.Properties.TextAlignment.CENTER,
                iText.Layout.Properties.TextAlignment.RIGHT,
                iText.Layout.Properties.TextAlignment.RIGHT
            };

            for (int c = 0; c < 4; c++)
            {
                table.AddHeaderCell(new iText.Layout.Element.Cell()
                    .Add(new iText.Layout.Element.Paragraph(headers[c])
                        .SetFont(boldFont).SetFontSize(10)
                        .SetFontColor(iText.Kernel.Colors.ColorConstants.WHITE)
                        .SetTextAlignment(alignments[c]))
                    .SetBackgroundColor(headerBg).SetPadding(4));
            }

            for (int r = 0; r < InvoiceData.LineItems.Length; r++)
            {
                var item = InvoiceData.LineItems[r];
                string[] cells = { item.Desc, item.Qty, item.Price, item.Amount };
                bool alt = r % 2 == 1;
                for (int c = 0; c < 4; c++)
                {
                    var cell = new iText.Layout.Element.Cell()
                        .Add(new iText.Layout.Element.Paragraph(cells[c])
                            .SetFont(normalFont).SetFontSize(10)
                            .SetTextAlignment(alignments[c]))
                        .SetPadding(4);
                    if (alt) cell.SetBackgroundColor(altBg);
                    table.AddCell(cell);
                }
            }

            document.Add(table);

            // Totals
            var totalsTable = new iText.Layout.Element.Table(
                iText.Layout.Properties.UnitValue.CreatePercentArray(new float[] { 1, 1 }))
                .SetWidth(iText.Layout.Properties.UnitValue.CreatePercentValue(40))
                .SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.RIGHT)
                .SetBorder(iText.Layout.Borders.Border.NO_BORDER);

            void AddTotalRow(string label, string value, bool isBold)
            {
                var font = isBold ? boldFont : normalFont;
                var size = isBold ? 12 : 10;
                totalsTable.AddCell(new iText.Layout.Element.Cell()
                    .Add(new iText.Layout.Element.Paragraph(label).SetFont(font).SetFontSize(size))
                    .SetBorder(iText.Layout.Borders.Border.NO_BORDER));
                totalsTable.AddCell(new iText.Layout.Element.Cell()
                    .Add(new iText.Layout.Element.Paragraph(value).SetFont(font).SetFontSize(size)
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT))
                    .SetBorder(iText.Layout.Borders.Border.NO_BORDER));
            }

            AddTotalRow("Subtotal:", InvoiceData.Subtotal, false);
            AddTotalRow("Tax (8%):", InvoiceData.Tax, false);
            AddTotalRow("Total Due:", InvoiceData.Total, true);
            document.Add(totalsTable);

            // Footer
            document.Add(new iText.Layout.Element.Paragraph(InvoiceData.FooterNote)
                .SetFont(italicFont).SetFontSize(9).SetFontColor(darkGray)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                .SetMarginTop(30));
        }

        document.Close();
        return ms.ToArray();
    }
#endif

    [Benchmark(Description = "QuestPDF")]
    public byte[] QuestPdf()
    {
        return Document.Create(container =>
        {
            for (int inv = 1; inv <= InvoiceData.BatchSize; inv++)
            {
                int invoiceNum = inv;
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.Letter);
                    page.Margin(50);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Content().Column(col =>
                    {
                        // Company info
                        col.Item().AlignRight().Text(InvoiceData.CompanyName)
                            .FontSize(14).SemiBold();
                        col.Item().AlignRight().Text(InvoiceData.CompanyAddress)
                            .FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        col.Item().AlignRight().Text(InvoiceData.CompanyPhone)
                            .FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);

                        // Divider
                        col.Item().PaddingVertical(10).LineHorizontal(1)
                            .LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                        // Invoice title
                        col.Item().Text("INVOICE").FontSize(24).SemiBold()
                            .FontColor(QuestPDF.Helpers.Colors.Grey.Darken3);

                        // Details row
                        col.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text($"Invoice #: {invoiceNum}");
                                left.Item().Text("Date: April 16, 2026");
                                left.Item().Text("Due: May 16, 2026");
                            });
                            row.RelativeItem().Column(right =>
                            {
                                right.Item().Text("Bill To:").SemiBold();
                                right.Item().Text(InvoiceData.BillToName);
                                right.Item().Text(InvoiceData.BillToAddress);
                                right.Item().Text(InvoiceData.BillToCityState);
                            });
                        });

                        // Line items table
                        col.Item().PaddingTop(15).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(240);
                                columns.ConstantColumn(80);
                                columns.ConstantColumn(80);
                                columns.ConstantColumn(112);
                            });

                            string[] headers = { "Description", "Quantity", "Unit Price", "Amount" };
                            table.Header(header =>
                            {
                                foreach (var h in headers)
                                {
                                    header.Cell()
                                        .Background(QuestPDF.Helpers.Colors.Grey.Darken3)
                                        .Padding(4)
                                        .Text(h).FontColor(QuestPDF.Helpers.Colors.White)
                                        .SemiBold().FontSize(10);
                                }
                            });

                            for (int r = 0; r < InvoiceData.LineItems.Length; r++)
                            {
                                var item = InvoiceData.LineItems[r];
                                string[] cells = { item.Desc, item.Qty, item.Price, item.Amount };
                                bool alt = r % 2 == 1;
                                foreach (var cellText in cells)
                                {
                                    var cell = table.Cell().Padding(4);
                                    if (alt)
                                        cell = cell.Background(QuestPDF.Helpers.Colors.Grey.Lighten3);
                                    cell.Text(cellText).FontSize(10);
                                }
                            }
                        });

                        // Totals
                        col.Item().PaddingTop(15).AlignRight().Column(totals =>
                        {
                            totals.Item().Text($"Subtotal: {InvoiceData.Subtotal}");
                            totals.Item().Text($"Tax (8%): {InvoiceData.Tax}");
                            totals.Item().PaddingTop(5).Text($"Total Due: {InvoiceData.Total}")
                                .FontSize(12).SemiBold();
                        });

                        // Footer
                        col.Item().PaddingTop(30).AlignCenter()
                            .Text(InvoiceData.FooterNote).FontSize(9).Italic()
                            .FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                    });
                });
            }
        }).GeneratePdf();
    }

    [Benchmark(Description = "IronPDF")]
    public byte[] BenchIronPdf()
    {
        var renderer = new global::IronPdf.ChromePdfRenderer();
        renderer.RenderingOptions.PaperSize = global::IronPdf.Rendering.PdfPaperSize.Letter;
        using var pdf = renderer.RenderHtmlAsPdf(_invoiceHtml);
        var tempFile = Path.Combine(Path.GetTempPath(), $"ironpdf_bench_{Guid.NewGuid()}.pdf");
        try
        {
            pdf.SaveAs(tempFile);
            return File.ReadAllBytes(tempFile);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
