using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutTabStopTests
    {
        [Fact]
        public void LeftTabStop_PositionsText()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Name\tJohn Smith", new ParagraphOptions
            {
                TabStops = new[] { new TabStop(200, TabAlignment.Left) }
            });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: 'Name' at left, 'John Smith' at 200pt tab");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/tabstop-left");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/tabstop-left");

            // "Name" near left margin
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(120),
                TestHelper.PtToPx(72), TestHelper.PtToPx(90)));

            // "John Smith" at 72 + 200 = 272pt
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(270), TestHelper.PtToPx(370),
                TestHelper.PtToPx(72), TestHelper.PtToPx(90)));
        }

        [Fact]
        public void RightTabStop_RightAlignsAtPosition()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Item\t$100.00", new ParagraphOptions
            {
                TabStops = new[] { new TabStop(300, TabAlignment.Right) }
            });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: '$100.00' right-aligned at 300pt tab");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/tabstop-right");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/tabstop-right");

            // The right-aligned text should end near 72+300=372pt
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(320), TestHelper.PtToPx(375),
                TestHelper.PtToPx(72), TestHelper.PtToPx(90)));
        }

        [Fact]
        public void CenterTabStop_CentersAtPosition()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Label\tCentered Text", new ParagraphOptions
            {
                TabStops = new[] { new TabStop(250, TabAlignment.Center) }
            });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: 'Centered Text' centered at 250pt tab");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/tabstop-center");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/tabstop-center");

            // Centered text around 72+250=322pt
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(280), TestHelper.PtToPx(370),
                TestHelper.PtToPx(72), TestHelper.PtToPx(90)));
        }

        [Fact]
        public void DecimalTabStop_AlignsOnDecimalPoint()
        {
            var layout = new PdfDocumentLayout();
            var tabStops = new[] { new TabStop(200, TabAlignment.Decimal) };
            layout.AddParagraph("Price\t1.50", new ParagraphOptions { TabStops = tabStops });
            layout.AddParagraph("Tax\t12.99", new ParagraphOptions { TabStops = tabStops });
            layout.AddParagraph("Total\t123.456", new ParagraphOptions { TabStops = tabStops });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: decimal points vertically aligned at 200pt");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/tabstop-decimal");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/tabstop-decimal");

            // All three values should have content near the tab position
            for (int line = 0; line < 3; line++)
            {
                float y = 72 + line * 12 * 1.2f;
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                    TestHelper.PtToPx(250), TestHelper.PtToPx(330),
                    TestHelper.PtToPx(y), TestHelper.PtToPx(y + 15)));
            }
        }

        [Fact]
        public void LeaderCharacters_FillGap()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Chapter 1\t1", new ParagraphOptions
            {
                TabStops = new[] { new TabStop(400, TabAlignment.Right, '.') }
            });
            layout.AddParagraph("Chapter 2\t15", new ParagraphOptions
            {
                TabStops = new[] { new TabStop(400, TabAlignment.Right, '.') }
            });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: dots fill gap between chapter name and page number");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/tabstop-leaders");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/tabstop-leaders");

            // Leader dots should be visible in the gap area
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(200), TestHelper.PtToPx(400),
                TestHelper.PtToPx(72), TestHelper.PtToPx(90)));
        }

        [Fact]
        public void MultipleTabsPerLine()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Name\tAge\tCity", new ParagraphOptions
            {
                TabStops = new[]
                {
                    new TabStop(150, TabAlignment.Left),
                    new TabStop(250, TabAlignment.Left)
                }
            });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: three columns via two tab stops");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/tabstop-multiple");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/tabstop-multiple");

            // Three separate text regions
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(120),
                TestHelper.PtToPx(72), TestHelper.PtToPx(90)));
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(220), TestHelper.PtToPx(270),
                TestHelper.PtToPx(72), TestHelper.PtToPx(90)));
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(320), TestHelper.PtToPx(370),
                TestHelper.PtToPx(72), TestHelper.PtToPx(90)));
        }

        [Fact]
        public void TabbedText_WithMultipleLines()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Item\tQty\tPrice\nApples\t5\t$1.50\nBananas\t3\t$0.75", new ParagraphOptions
            {
                TabStops = new[]
                {
                    new TabStop(150, TabAlignment.Left),
                    new TabStop(250, TabAlignment.Right)
                }
            });
            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: tabbed text with multiple lines");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/tabstop-multiline");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/tabstop-multiline");

            // Three rows of content
            for (int row = 0; row < 3; row++)
            {
                float y = 72 + row * 12 * 1.2f;
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                    TestHelper.PtToPx(72), TestHelper.PtToPx(400),
                    TestHelper.PtToPx(y), TestHelper.PtToPx(y + 15)));
            }
        }
    }
}
