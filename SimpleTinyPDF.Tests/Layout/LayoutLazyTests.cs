using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutLazyTests
    {
        [Fact]
        public void LazyRendering_ProducesValidMultiPagePdf()
        {
            var layout = new PdfDocumentLayout { LazyRendering = true };
            for (int i = 1; i <= 100; i++)
                layout.AddParagraph($"Lazy paragraph {i}", new ParagraphOptions { SpaceAfter = 4 });
            var doc = layout.Generate();

            Assert.True(doc.PageCount > 1);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: lazily rendered paragraphs flow across pages");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/lazy-multi-page");

            Assert.Equal(doc.PageCount, TestHelper.GetPageCount(bytes));
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/lazy-multi-page");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(300),
                TestHelper.PtToPx(72), TestHelper.PtToPx(90)));
        }

        [Fact]
        public void LazyRendering_TotalPagesIsZero()
        {
            var totals = new List<int>();
            var layout = new PdfDocumentLayout { LazyRendering = true };
            layout.HeaderFooter.Footer = (page, ctx) =>
            {
                totals.Add(ctx.TotalPages);
                page.DrawText($"Page {ctx.PageNumber} of {ctx.TotalPages}",
                    72, page.Height - 40, PdfFont.Helvetica, 10);
            };
            for (int i = 1; i <= 100; i++)
                layout.AddParagraph($"Paragraph {i}");
            var doc = layout.Generate();

            Assert.True(doc.PageCount > 1);
            Assert.NotEmpty(totals);
            Assert.All(totals, t => Assert.Equal(0, t));

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: footers read 'Page N of 0' (lazy mode has no totals)");
            TestHelper.SavePdf(doc.ToArray(), "Layout/lazy-total-pages-zero");
        }

        [Fact]
        public void NonLazyRendering_ProvidesRealTotals()
        {
            var totals = new List<int>();
            var layout = new PdfDocumentLayout();
            layout.HeaderFooter.Footer = (page, ctx) => totals.Add(ctx.TotalPages);
            for (int i = 1; i <= 100; i++)
                layout.AddParagraph($"Paragraph {i}");
            var doc = layout.Generate();

            // Two-pass rendering: the final pass reports the real page count
            Assert.Equal(doc.PageCount, totals.Last());
        }

        [Fact]
        public void LazyRendering_LargeDocumentCompletes()
        {
            var layout = new PdfDocumentLayout { LazyRendering = true };
            for (int i = 1; i <= 2000; i++)
                layout.AddParagraph($"Bulk paragraph {i} with some additional text content.");
            var doc = layout.Generate();

            Assert.True(doc.PageCount >= 40, $"Expected >= 40 pages, got {doc.PageCount}");

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: 2000 paragraphs rendered in a single lazy pass");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/lazy-large");
            Assert.Equal(doc.PageCount, TestHelper.GetPageCount(bytes));
        }
    }
}
