using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Md = MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace SimpleTinyPDF.Benchmarks;

/// <summary>
/// Benchmarks a flowing multi-page report with headers, footers, page numbers,
/// chapter headings, body paragraphs, and a summary table.
/// This is the use case where high-level layout engines compete directly.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 2, iterationCount: 5)]
public class LayoutReportBenchmark
{
    private const int ParagraphCount = 50;

    private static readonly string BodyText =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor " +
        "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud " +
        "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure " +
        "dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. " +
        "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt " +
        "mollit anim id est laborum.";

    private static readonly string[] Headers = { "Quarter", "Revenue", "Expenses", "Profit" };
    private static readonly string[][] TableRows =
    {
        new[] { "Q1", "$1.2M", "$0.9M", "$0.3M" },
        new[] { "Q2", "$1.5M", "$1.0M", "$0.5M" },
        new[] { "Q3", "$1.8M", "$1.1M", "$0.7M" },
        new[] { "Q4", "$2.1M", "$1.2M", "$0.9M" },
    };

    private string _htmlReport = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (PdfSharp.Fonts.GlobalFontSettings.FontResolver == null)
            PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        _htmlReport = BuildHtmlReport();

        try
        {
            var renderer = new global::IronPdf.ChromePdfRenderer();
            renderer.RenderHtmlAsPdf("<html><body><p>warmup</p></body></html>");
        }
        catch { }
    }

    // ── SimpleTinyPDF (Layout) ─────────────────────────────────

    [Benchmark(Description = "SimpleTinyPDF (Layout)")]
    public byte[] SimpleTinyPdfLayout()
    {
        var layout = new PdfDocumentLayout();
        layout.PageSize = SimpleTinyPDF.PageSize.Letter;
        layout.Margins = new PdfMargins(72);

        layout.HeaderFooter.Header = (page, ctx) =>
        {
            page.DrawText("Quarterly Report \u2014 Acme Corp", page.Width / 2, 30,
                PdfFont.HelveticaBold, 9, PdfColor.DarkGray, TextAlignment.Center);
            page.DrawLine(72, 45, page.Width - 72, 45, PdfColor.LightGray);
        };
        layout.HeaderFooter.Footer = (page, ctx) =>
        {
            page.DrawLine(72, page.Height - 50, page.Width - 72, page.Height - 50,
                PdfColor.LightGray);
            page.DrawText($"Page {ctx.PageNumber} of {ctx.TotalPages}",
                page.Width / 2, page.Height - 35,
                PdfFont.Helvetica, 8, PdfColor.DarkGray, TextAlignment.Center);
        };

        layout.AddParagraph("Quarterly Performance Report", new ParagraphOptions
        {
            Font = PdfFont.HelveticaBold, FontSize = 22,
            Alignment = TextAlignment.Center, SpaceAfter = 20
        });

        for (int i = 0; i < ParagraphCount; i++)
        {
            if (i % 5 == 0)
                layout.AddParagraph($"Chapter {i / 5 + 1}: Analysis", new ParagraphOptions
                {
                    Font = PdfFont.HelveticaBold, FontSize = 16,
                    SpaceBefore = 15, SpaceAfter = 8
                });
            layout.AddParagraph(BodyText, new ParagraphOptions { SpaceAfter = 6 });
        }

        layout.AddParagraph("Financial Summary", new ParagraphOptions
        {
            Font = PdfFont.HelveticaBold, FontSize = 16,
            SpaceBefore = 15, SpaceAfter = 8
        });

        var table = new PdfTable(150, 100, 100, 100);
        table.SetHeaders(Headers);
        foreach (var row in TableRows) table.AddRow(row);
        table.HeaderBackground = PdfColor.Rgb(51, 51, 51);
        table.HeaderTextColor = PdfColor.White;
        table.AlternateRowShading = true;
        layout.AddTable(table);

        return layout.ToArray();
    }

    // ── PDFsharp + MigraDoc ────────────────────────────────────

    [Benchmark(Description = "PDFsharp + MigraDoc")]
    public byte[] BenchPdfSharp()
    {
        var document = new Md.Document();
        var style = document.Styles["Normal"];
        style!.Font.Name = "Arial";
        style.Font.Size = 12;

        var section = document.AddSection();
        section.PageSetup.PageFormat = Md.PageFormat.Letter;
        section.PageSetup.LeftMargin = Md.Unit.FromPoint(72);
        section.PageSetup.RightMargin = Md.Unit.FromPoint(72);
        section.PageSetup.TopMargin = Md.Unit.FromPoint(72);
        section.PageSetup.BottomMargin = Md.Unit.FromPoint(72);
        section.PageSetup.HeaderDistance = Md.Unit.FromPoint(30);
        section.PageSetup.FooterDistance = Md.Unit.FromPoint(25);

        // Header
        var header = section.Headers.Primary;
        var hp = header.AddParagraph("Quarterly Report \u2014 Acme Corp");
        hp.Format.Alignment = Md.ParagraphAlignment.Center;
        hp.Format.Font.Bold = true;
        hp.Format.Font.Size = 9;
        hp.Format.Font.Color = new Md.Color(84, 84, 84);
        hp.Format.Borders.Bottom.Width = 0.5;
        hp.Format.Borders.Bottom.Color = new Md.Color(200, 200, 200);

        // Footer
        var footer = section.Footers.Primary;
        var fp = footer.AddParagraph();
        fp.Format.Alignment = Md.ParagraphAlignment.Center;
        fp.Format.Font.Size = 8;
        fp.Format.Font.Color = new Md.Color(84, 84, 84);
        fp.Format.Borders.Top.Width = 0.5;
        fp.Format.Borders.Top.Color = new Md.Color(200, 200, 200);
        fp.AddText("Page ");
        fp.AddPageField();
        fp.AddText(" of ");
        fp.AddNumPagesField();

        // Title
        var p = section.AddParagraph("Quarterly Performance Report");
        p.Format.Font.Bold = true;
        p.Format.Font.Size = 22;
        p.Format.Alignment = Md.ParagraphAlignment.Center;
        p.Format.SpaceAfter = 20;

        for (int i = 0; i < ParagraphCount; i++)
        {
            if (i % 5 == 0)
            {
                p = section.AddParagraph($"Chapter {i / 5 + 1}: Analysis");
                p.Format.Font.Bold = true;
                p.Format.Font.Size = 16;
                p.Format.SpaceBefore = 15;
                p.Format.SpaceAfter = 8;
            }
            p = section.AddParagraph(BodyText);
            p.Format.SpaceAfter = 6;
        }

        // Summary heading
        p = section.AddParagraph("Financial Summary");
        p.Format.Font.Bold = true;
        p.Format.Font.Size = 16;
        p.Format.SpaceBefore = 15;
        p.Format.SpaceAfter = 8;

        // Table
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        float[] colWidths = { 150, 100, 100, 100 };
        foreach (var w in colWidths)
            table.AddColumn(Md.Unit.FromPoint(w));

        var headerRow = table.AddRow();
        headerRow.Shading.Color = new Md.Color(51, 51, 51);
        headerRow.Format.Font.Bold = true;
        headerRow.Format.Font.Color = Md.Colors.White;
        for (int c = 0; c < Headers.Length; c++)
            headerRow.Cells[c].AddParagraph(Headers[c]);

        for (int r = 0; r < TableRows.Length; r++)
        {
            var row = table.AddRow();
            if (r % 2 == 1)
                row.Shading.Color = new Md.Color(242, 242, 242);
            for (int c = 0; c < TableRows[r].Length; c++)
                row.Cells[c].AddParagraph(TableRows[r][c]);
        }

        var renderer = new PdfDocumentRenderer();
        renderer.Document = document;
        renderer.RenderDocument();

        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    // ── QuestPDF ───────────────────────────────────────────────

    [Benchmark(Description = "QuestPDF")]
    public byte[] QuestPdf()
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(72);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("Quarterly Report \u2014 Acme Corp")
                        .FontSize(9).SemiBold()
                        .FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                    col.Item().PaddingTop(5).LineHorizontal(0.5f)
                        .LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);
                });

                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f)
                        .LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);
                    col.Item().PaddingTop(5).AlignCenter().Text(text =>
                    {
                        text.Span("Page ").FontSize(8)
                            .FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        text.CurrentPageNumber().FontSize(8)
                            .FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        text.Span(" of ").FontSize(8)
                            .FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        text.TotalPages().FontSize(8)
                            .FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                    });
                });

                page.Content().Column(col =>
                {
                    col.Item().PaddingBottom(20).AlignCenter()
                        .Text("Quarterly Performance Report")
                        .FontSize(22).SemiBold();

                    for (int i = 0; i < ParagraphCount; i++)
                    {
                        if (i % 5 == 0)
                            col.Item().PaddingTop(15).PaddingBottom(8)
                                .Text($"Chapter {i / 5 + 1}: Analysis")
                                .FontSize(16).SemiBold();

                        col.Item().PaddingBottom(6).Text(BodyText).FontSize(12);
                    }

                    col.Item().PaddingTop(15).PaddingBottom(8)
                        .Text("Financial Summary").FontSize(16).SemiBold();

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(150);
                            columns.ConstantColumn(100);
                            columns.ConstantColumn(100);
                            columns.ConstantColumn(100);
                        });

                        table.Header(header =>
                        {
                            foreach (var h in Headers)
                                header.Cell()
                                    .Background(QuestPDF.Helpers.Colors.Grey.Darken3)
                                    .Padding(4)
                                    .Text(h).FontColor(QuestPDF.Helpers.Colors.White)
                                    .SemiBold().FontSize(12);
                        });

                        for (int r = 0; r < TableRows.Length; r++)
                        {
                            bool alt = r % 2 == 1;
                            foreach (var cellText in TableRows[r])
                            {
                                var cell = table.Cell().Padding(4);
                                if (alt)
                                    cell = cell.Background(QuestPDF.Helpers.Colors.Grey.Lighten3);
                                cell.Text(cellText).FontSize(12);
                            }
                        }
                    });
                });
            });
        }).GeneratePdf();
    }

    // ── IronPDF ────────────────────────────────────────────────

    [Benchmark(Description = "IronPDF")]
    public byte[] BenchIronPdf()
    {
        var renderer = new global::IronPdf.ChromePdfRenderer();
        renderer.RenderingOptions.PaperSize = global::IronPdf.Rendering.PdfPaperSize.Letter;
        renderer.RenderingOptions.MarginTop = 72 * 0.352778;   // pt to mm
        renderer.RenderingOptions.MarginBottom = 72 * 0.352778;
        renderer.RenderingOptions.MarginLeft = 72 * 0.352778;
        renderer.RenderingOptions.MarginRight = 72 * 0.352778;
        renderer.RenderingOptions.HtmlHeader = new global::IronPdf.HtmlHeaderFooter
        {
            HtmlFragment = "<div style='text-align:center;font-size:9px;font-family:Helvetica;color:#555;border-bottom:1px solid #ccc;padding-bottom:5px;font-weight:bold;'>Quarterly Report \u2014 Acme Corp</div>"
        };
        renderer.RenderingOptions.HtmlFooter = new global::IronPdf.HtmlHeaderFooter
        {
            HtmlFragment = "<div style='text-align:center;font-size:8px;font-family:Helvetica;color:#555;border-top:1px solid #ccc;padding-top:5px;'>Page {page} of {total-pages}</div>"
        };

        using var pdf = renderer.RenderHtmlAsPdf(_htmlReport);
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

    // ── HTML builder ───────────────────────────────────────────

    private static string BuildHtmlReport()
    {
        var sb = new StringBuilder();
        sb.Append(@"<html><head><style>
            body { font-family: Helvetica, Arial, sans-serif; font-size: 12px; }
            h1 { font-size: 22px; text-align: center; margin-bottom: 20px; }
            h2 { font-size: 16px; margin-top: 15px; margin-bottom: 8px; }
            p { margin-bottom: 6px; }
            table { border-collapse: collapse; margin-top: 8px; }
            th { background: #333; color: white; padding: 4px 8px; text-align: left; }
            td { padding: 4px 8px; border: 1px solid #000; }
            tr:nth-child(even) { background: #f2f2f2; }
        </style></head><body>");

        sb.Append("<h1>Quarterly Performance Report</h1>");

        for (int i = 0; i < ParagraphCount; i++)
        {
            if (i % 5 == 0)
                sb.Append($"<h2>Chapter {i / 5 + 1}: Analysis</h2>");
            sb.Append($"<p>{BodyText}</p>");
        }

        sb.Append("<h2>Financial Summary</h2><table><tr>");
        foreach (var h in Headers) sb.Append($"<th>{h}</th>");
        sb.Append("</tr>");
        foreach (var row in TableRows)
        {
            sb.Append("<tr>");
            foreach (var cell in row) sb.Append($"<td>{cell}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</table></body></html>");

        return sb.ToString();
    }
}
