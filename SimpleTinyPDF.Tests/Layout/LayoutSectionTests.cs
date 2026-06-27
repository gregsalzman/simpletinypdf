using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutSectionTests
    {
        [Fact]
        public void Section_DifferentPageSize()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Section 1 - A4");

            layout.AddSection(new SectionOptions { PageSize = PageSize.Letter.Landscape() });
            layout.AddParagraph("Section 2 - Letter Landscape");

            var doc = layout.Generate();
            Assert.Equal(2, doc.PageCount);

            // First page is A4 (595 x 842)
            Assert.Equal(595f, doc.Pages[0].Width);
            Assert.Equal(842f, doc.Pages[0].Height);

            // Second page is Letter Landscape (792 x 612)
            Assert.Equal(792f, doc.Pages[1].Width);
            Assert.Equal(612f, doc.Pages[1].Height);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: section changes page size");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/section-different-page-size");
        }

        [Fact]
        public void Section_DifferentMargins()
        {
            var layout = new PdfDocumentLayout();
            layout.Margins = new PdfMargins(72);
            layout.AddParagraph("Section 1 - 72pt margins");

            layout.AddSection(new SectionOptions { Margins = new PdfMargins(20) });
            layout.AddParagraph("Section 2 - 20pt margins (more content area)");

            var doc = layout.Generate();
            Assert.Equal(2, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: section 2 has narrower margins");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/section-different-margins");

            // Section 2 text should be closer to the left edge
            var bmp2 = TestHelper.RasterizePage(bytes, "Layout/section-different-margins", 1);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bmp2,
                TestHelper.PtToPx(20), TestHelper.PtToPx(70),
                TestHelper.PtToPx(20), TestHelper.PtToPx(40)));
        }

        [Fact]
        public void Section_DifferentHeaderFooter()
        {
            var layout = new PdfDocumentLayout();
            layout.HeaderFooter.Header = (page, ctx) =>
                page.DrawText("Default Header", 72, 36, PdfFont.Helvetica, 10, PdfColor.Black);

            layout.AddParagraph("Section 1 with default header");

            layout.AddSection(new SectionOptions
            {
                HeaderFooter = new HeaderFooterOptions
                {
                    Header = (page, ctx) =>
                        page.DrawText("Section 2 Header", 72, 36, PdfFont.Helvetica, 10, PdfColor.Black)
                }
            });
            layout.AddParagraph("Section 2 with custom header");

            var doc = layout.Generate();
            Assert.Equal(2, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: each section has its own header");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/section-different-header");

            var pdfText = TestHelper.GetPdfText(bytes);
            Assert.Contains("Default Header", pdfText);
            Assert.Contains("Section 2 Header", pdfText);
        }

        [Fact]
        public void Section_RestartPageNumbers()
        {
            var layout = new PdfDocumentLayout();
            layout.HeaderFooter.Footer = (page, ctx) =>
                page.DrawText($"Page {ctx.PageNumber} | Section Page {ctx.SectionPageNumber}",
                    72, page.Height - 36, PdfFont.Helvetica, 10, PdfColor.Black);

            layout.AddParagraph("Section 1, Page 1");
            layout.AddPageBreak();
            layout.AddParagraph("Section 1, Page 2");

            layout.AddSection(new SectionOptions { RestartPageNumbers = true });
            layout.AddParagraph("Section 2, Section-Page 1 (doc page 3)");

            var doc = layout.Generate();
            Assert.Equal(3, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: section page numbers restart");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/section-restart-page-numbers");

            var pdfText = TestHelper.GetPdfText(bytes);
            // Section 2 page should show "Section Page 1"
            Assert.Contains("Section Page 1", pdfText);
        }

        [Fact]
        public void Section_InheritsDefaults()
        {
            var layout = new PdfDocumentLayout();
            layout.PageSize = PageSize.Letter;
            layout.Margins = new PdfMargins(50);

            layout.AddParagraph("Section 1 - Letter page");

            // Section with no overrides — should inherit Letter + 50pt margins
            layout.AddSection(new SectionOptions());
            layout.AddParagraph("Section 2 - should also be Letter");

            var doc = layout.Generate();
            Assert.Equal(2, doc.PageCount);

            // Both pages should be Letter size (612 x 792)
            Assert.Equal(612f, doc.Pages[0].Width);
            Assert.Equal(612f, doc.Pages[1].Width);
            Assert.Equal(792f, doc.Pages[0].Height);
            Assert.Equal(792f, doc.Pages[1].Height);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: section inherits parent settings");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/section-inherits-defaults");
        }

        [Fact]
        public void MultipleSections_MixedSettings()
        {
            var layout = new PdfDocumentLayout();
            layout.HeaderFooter.Footer = (page, ctx) =>
                page.DrawText($"Doc Page {ctx.PageNumber} | Section {ctx.SectionIndex + 1}",
                    72, page.Height - 36, PdfFont.Helvetica, 10, PdfColor.Black);

            layout.AddParagraph("Section 1 (A4, default margins)");

            layout.AddSection(new SectionOptions
            {
                PageSize = PageSize.Letter,
                Margins = new PdfMargins(30)
            });
            layout.AddParagraph("Section 2 (Letter, 30pt margins)");

            layout.AddSection(new SectionOptions
            {
                PageSize = PageSize.A4.Landscape(),
                Margins = new PdfMargins(100)
            });
            layout.AddParagraph("Section 3 (A4 Landscape, 100pt margins)");

            var doc = layout.Generate();
            Assert.Equal(3, doc.PageCount);

            // Section 1: A4
            Assert.Equal(595f, doc.Pages[0].Width);
            // Section 2: Letter
            Assert.Equal(612f, doc.Pages[1].Width);
            // Section 3: A4 Landscape
            Assert.Equal(842f, doc.Pages[2].Width);
            Assert.Equal(595f, doc.Pages[2].Height);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: three sections with mixed settings");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/section-multiple-mixed");

            var pdfText = TestHelper.GetPdfText(bytes);
            Assert.Contains("Section 1", pdfText);
            Assert.Contains("Section 2", pdfText);
            Assert.Contains("Section 3", pdfText);
        }
    }
}
