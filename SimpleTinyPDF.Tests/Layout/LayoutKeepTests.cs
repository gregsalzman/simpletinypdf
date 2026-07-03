using System.Collections.Generic;
using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutKeepTests
    {
        // A4: 595 x 842, margins 72 → content y 72..770 (698pt), line height 14.4

        private static string Lines(int count, string prefix = "Line")
        {
            var lines = new string[count];
            for (int i = 0; i < count; i++)
                lines[i] = $"{prefix} {i + 1}";
            return string.Join("\n", lines);
        }

        [Fact]
        public void KeepTogether_MovesParagraphToNextPage()
        {
            var layout = new PdfDocumentLayout();
            // 40 lines * 14.4 = 576pt → ends at y = 648, leaving 122pt
            layout.AddParagraph(Lines(40, "Filler"));
            // 20 lines * 14.4 = 288pt — doesn't fit in 122pt, fits on a fresh page
            layout.AddParagraph(Lines(20, "Kept"), new ParagraphOptions { KeepTogether = true });
            var doc = layout.Generate();

            Assert.Equal(2, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: 20-line block moved intact to page 2");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/keep-together-moves");

            // Page 1: nothing rendered below the filler (no partial kept block)
            var page1 = TestHelper.RasterizePage(bytes, "Layout/keep-together-moves", 0);
            Assert.False(TestHelper.HasDarkPixelsInRegion(page1,
                TestHelper.PtToPx(72), TestHelper.PtToPx(523),
                TestHelper.PtToPx(655), TestHelper.PtToPx(765)));

            // Page 2: kept block starts at the top
            var page2 = TestHelper.RasterizePage(bytes, "Layout/keep-together-moves", 1);
            Assert.True(TestHelper.HasDarkPixelsInRegion(page2,
                TestHelper.PtToPx(72), TestHelper.PtToPx(200),
                TestHelper.PtToPx(72), TestHelper.PtToPx(90)));
        }

        [Fact]
        public void KeepTogether_TallerThanPage_SplitsWithWarning()
        {
            var warnings = new List<string>();
            var layout = new PdfDocumentLayout();
            layout.Debug = new DebugOptions { OnLayoutWarning = warnings.Add };
            // 60 lines * 14.4 = 864pt > 698pt page capacity → must split
            layout.AddParagraph(Lines(60, "Tall"), new ParagraphOptions { KeepTogether = true });
            var doc = layout.Generate();

            Assert.Equal(2, doc.PageCount);
            Assert.Contains(warnings, w => w.Contains("KeepTogether"));

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: over-tall keep-together block splits across pages");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/keep-together-splits");
        }

        [Fact]
        public void KeepWithNext_HeadingMovesToNextPage()
        {
            var layout = new PdfDocumentLayout();
            // 46 lines * 14.4 = 662.4 → y = 734.4, leaving 35.6pt.
            // Heading alone (21.6pt) fits, heading + body first line (36pt) does not.
            layout.AddParagraph(Lines(46, "Filler"));
            layout.AddParagraph("Section Heading", new ParagraphOptions
            {
                Font = PdfFont.HelveticaBold,
                FontSize = 18,
                KeepWithNext = true
            });
            layout.AddParagraph("Body text that must start on the same page as the heading.");
            var doc = layout.Generate();

            Assert.Equal(2, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: heading moved to page 2 with its body text");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/keep-with-next-moves");

            // Page 1: heading did NOT render in the leftover space at the bottom
            var page1 = TestHelper.RasterizePage(bytes, "Layout/keep-with-next-moves", 0);
            Assert.False(TestHelper.HasDarkPixelsInRegion(page1,
                TestHelper.PtToPx(72), TestHelper.PtToPx(523),
                TestHelper.PtToPx(740), TestHelper.PtToPx(765)));

            // Page 2: heading at the top
            var page2 = TestHelper.RasterizePage(bytes, "Layout/keep-with-next-moves", 1);
            Assert.True(TestHelper.HasDarkPixelsInRegion(page2,
                TestHelper.PtToPx(72), TestHelper.PtToPx(300),
                TestHelper.PtToPx(72), TestHelper.PtToPx(95)));
        }

        [Fact]
        public void WithoutKeepWithNext_HeadingStaysOnPage1()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph(Lines(46, "Filler"));
            layout.AddParagraph("Section Heading", new ParagraphOptions
            {
                Font = PdfFont.HelveticaBold,
                FontSize = 18
            });
            layout.AddParagraph("Body text that lands on the next page.");
            var doc = layout.Generate();

            Assert.Equal(2, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: without keep-with-next, heading is orphaned at page 1 bottom");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/keep-with-next-control");

            // Page 1: heading rendered in the leftover space (y ≈ 734.4..756)
            var page1 = TestHelper.RasterizePage(bytes, "Layout/keep-with-next-control", 0);
            Assert.True(TestHelper.HasDarkPixelsInRegion(page1,
                TestHelper.PtToPx(72), TestHelper.PtToPx(300),
                TestHelper.PtToPx(736), TestHelper.PtToPx(758)));
        }

        [Fact]
        public void KeepWithNext_FitsTogether_NoBreak()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Heading", new ParagraphOptions
            {
                FontSize = 16,
                KeepWithNext = true
            });
            layout.AddParagraph("Body right below the heading.");
            var doc = layout.Generate();

            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: heading and body together at top of a single page");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/keep-with-next-fits");
        }
    }
}
