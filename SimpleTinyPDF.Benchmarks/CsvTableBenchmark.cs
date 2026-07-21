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
public class CsvTableBenchmark
{
    private string _csvData = null!;
    private string[][] _parsedRows = null!;
    private string[] _headers = null!;
    private string _htmlTable = null!;

    [GlobalSetup]
    public void Setup()
    {
        _csvData = CsvDataGenerator.Generate(10_000);

        // Pre-parse for libraries that don't have CSV support
        var lines = _csvData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        _headers = lines[0].Split(',');
        _parsedRows = new string[lines.Length - 1][];
        for (int i = 1; i < lines.Length; i++)
            _parsedRows[i - 1] = lines[i].Split(',');

        // Pre-build HTML table for IronPDF
        _htmlTable = BuildHtmlTable();

        // PDFsharp font resolver (required in .NET Core builds)
        if (PdfSharp.Fonts.GlobalFontSettings.FontResolver == null)
            PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;

        // QuestPDF license
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // Warm up IronPDF's Chromium engine (requires license for production use)
        try
        {
            var renderer = new global::IronPdf.ChromePdfRenderer();
            renderer.RenderHtmlAsPdf("<html><body><p>warmup</p></body></html>");
        }
        catch { /* IronPDF requires a commercial license — benchmark will show as N/A */ }
    }

    private string BuildHtmlTable()
    {
        var sb = new StringBuilder();
        sb.Append(@"<html><head><style>
            body { font-family: Helvetica, Arial, sans-serif; font-size: 10px; }
            table { border-collapse: collapse; width: 100%; }
            th { background: #333; color: white; padding: 4px; text-align: left; font-size: 10px; }
            td { padding: 4px; border: 1px solid #000; font-size: 10px; }
            tr:nth-child(even) { background: #f2f2f2; }
        </style></head><body><table><tr>");

        foreach (var h in _headers)
            sb.Append($"<th>{h}</th>");
        sb.Append("</tr>");

        foreach (var row in _parsedRows)
        {
            sb.Append("<tr>");
            foreach (var cell in row)
                sb.Append($"<td>{cell}</td>");
            sb.Append("</tr>");
        }

        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    [Benchmark(Description = "SimpleTinyPDF")]
    public byte[] SimpleTinyPdf()
    {
        var doc = new PdfDocument { Title = "CSV Table Benchmark" };
        var page = doc.AddPage(PageSize.Letter.Landscape());

        var table = PdfTable.FromCsvString(_csvData,
            firstRowIsHeader: true,
            columnWidths: new float[] { 60, 120, 100, 60, 80, 80 });

        table.HeaderBackground = PdfColor.Rgb(51, 51, 51);
        table.HeaderTextColor = PdfColor.White;
        table.AlternateRowShading = true;
        table.CellPadding = 4;

        page.DrawTable(table, 50, 50, bottomMargin: 40, continuationY: 40);
        return doc.ToArray();
    }

    [Benchmark(Description = "PDFsharp + MigraDoc")]
    public byte[] BenchPdfSharp()
    {
        var document = new Md.Document();
        document.Info.Title = "CSV Table Benchmark";

        var style = document.Styles["Normal"];
        style!.Font.Name = "Arial";
        style.Font.Size = 10;

        var section = document.AddSection();
        section.PageSetup.PageFormat = Md.PageFormat.Letter;
        section.PageSetup.Orientation = Md.Orientation.Landscape;
        section.PageSetup.LeftMargin = Md.Unit.FromPoint(50);
        section.PageSetup.RightMargin = Md.Unit.FromPoint(50);
        section.PageSetup.TopMargin = Md.Unit.FromPoint(50);
        section.PageSetup.BottomMargin = Md.Unit.FromPoint(40);

        var table = section.AddTable();
        table.Borders.Width = 0.5;

        float[] colWidths = { 60, 120, 100, 60, 80, 80 };
        for (int c = 0; c < colWidths.Length; c++)
            table.AddColumn(Md.Unit.FromPoint(colWidths[c]));

        // Header row (repeats on each page)
        var headerRow = table.AddRow();
        headerRow.HeadingFormat = true;
        headerRow.Shading.Color = new Md.Color(51, 51, 51);
        headerRow.Format.Font.Bold = true;
        headerRow.Format.Font.Color = Md.Colors.White;
        for (int c = 0; c < _headers.Length && c < colWidths.Length; c++)
            headerRow.Cells[c].AddParagraph(_headers[c]);

        // Data rows
        for (int r = 0; r < _parsedRows.Length; r++)
        {
            var row = table.AddRow();
            if (r % 2 == 1)
                row.Shading.Color = new Md.Color(242, 242, 242);

            var rowData = _parsedRows[r];
            for (int c = 0; c < rowData.Length && c < colWidths.Length; c++)
                row.Cells[c].AddParagraph(rowData[c]);
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
        pdf.SetDefaultPageSize(iText.Kernel.Geom.PageSize.LETTER.Rotate());
        var document = new iText.Layout.Document(pdf);

        var headerFont = iText.Kernel.Font.PdfFontFactory.CreateFont(
            iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
        var cellFontObj = iText.Kernel.Font.PdfFontFactory.CreateFont(
            iText.IO.Font.Constants.StandardFonts.HELVETICA);

        float[] colWidths = { 60, 120, 100, 60, 80, 80 };
        var table = new iText.Layout.Element.Table(
            iText.Layout.Properties.UnitValue.CreatePointArray(colWidths));

        // Header
        foreach (var h in _headers)
        {
            table.AddHeaderCell(new iText.Layout.Element.Cell()
                .Add(new iText.Layout.Element.Paragraph(h)
                    .SetFont(headerFont).SetFontSize(10)
                    .SetFontColor(iText.Kernel.Colors.ColorConstants.WHITE))
                .SetBackgroundColor(new iText.Kernel.Colors.DeviceRgb(51, 51, 51))
                .SetPadding(4));
        }

        // Data rows
        for (int r = 0; r < _parsedRows.Length; r++)
        {
            bool alt = r % 2 == 1;
            foreach (var cellText in _parsedRows[r])
            {
                var cell = new iText.Layout.Element.Cell()
                    .Add(new iText.Layout.Element.Paragraph(cellText)
                        .SetFont(cellFontObj).SetFontSize(10))
                    .SetPadding(4);
                if (alt)
                    cell.SetBackgroundColor(new iText.Kernel.Colors.DeviceRgb(242, 242, 242));
                table.AddCell(cell);
            }
        }

        document.Add(table);
        document.Close();
        return ms.ToArray();
    }
#endif

    [Benchmark(Description = "QuestPDF")]
    public byte[] QuestPdf()
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(120);
                        columns.ConstantColumn(100);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(80);
                    });

                    table.Header(header =>
                    {
                        foreach (var h in _headers)
                        {
                            header.Cell()
                                .Background(QuestPDF.Helpers.Colors.Grey.Darken3)
                                .Padding(4)
                                .Text(h).FontColor(QuestPDF.Helpers.Colors.White)
                                .SemiBold().FontSize(10);
                        }
                    });

                    for (int r = 0; r < _parsedRows.Length; r++)
                    {
                        bool alt = r % 2 == 1;
                        foreach (var cellText in _parsedRows[r])
                        {
                            var cell = table.Cell().Padding(4);
                            if (alt)
                                cell = cell.Background(QuestPDF.Helpers.Colors.Grey.Lighten3);
                            cell.Text(cellText).FontSize(10);
                        }
                    }
                });
            });
        }).GeneratePdf();
    }

    [Benchmark(Description = "IronPDF")]
    public byte[] BenchIronPdf()
    {
        var renderer = new global::IronPdf.ChromePdfRenderer();
        renderer.RenderingOptions.PaperSize = global::IronPdf.Rendering.PdfPaperSize.Letter;
        renderer.RenderingOptions.PaperOrientation = global::IronPdf.Rendering.PdfPaperOrientation.Landscape;
        using var pdf = renderer.RenderHtmlAsPdf(_htmlTable);
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
