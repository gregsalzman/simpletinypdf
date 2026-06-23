using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutListTests
    {
        [Fact]
        public void BulletList_RendersWithinMargins()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Shopping list:");
            layout.AddList(new[]
            {
                new ListItem("Apples"),
                new ListItem("Bananas"),
                new ListItem("Cherries")
            }, ListStyle.Bullet);

            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: bullet list with 3 items below heading");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/list-bullet");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/list-bullet");

            // List items should appear below the heading
            float listStart = 72 + 12 * 1.2f; // after heading
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(300),
                TestHelper.PtToPx(listStart), TestHelper.PtToPx(listStart + 60)));
        }

        [Fact]
        public void NumberedList_RendersWithinMargins()
        {
            var layout = new PdfDocumentLayout();
            layout.AddList(new[]
            {
                new ListItem("First item"),
                new ListItem("Second item"),
                new ListItem("Third item")
            }, ListStyle.Numbered);

            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: numbered list 1. 2. 3.");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/list-numbered");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/list-numbered");

            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(300),
                TestHelper.PtToPx(72), TestHelper.PtToPx(130)));
        }

        [Fact]
        public void NestedList_RendersWithIndentation()
        {
            var layout = new PdfDocumentLayout();
            layout.AddList(new[]
            {
                new ListItem("Parent 1", new ListItem("Child 1a"), new ListItem("Child 1b")),
                new ListItem("Parent 2", new ListItem("Child 2a"))
            }, ListStyle.Bullet);

            var doc = layout.Generate();

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: nested bullets with indentation");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/list-nested");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/list-nested");

            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(350),
                TestHelper.PtToPx(72), TestHelper.PtToPx(180)));
        }

        [Fact]
        public void LongList_SpansMultiplePages()
        {
            var layout = new PdfDocumentLayout();

            var items = new ListItem[50];
            for (int i = 0; i < 50; i++)
                items[i] = new ListItem($"Item number {i + 1} with enough text to take up space on the page");
            layout.AddList(items, ListStyle.Numbered);

            var doc = layout.Generate();
            Assert.True(doc.PageCount >= 2, $"Expected at least 2 pages but got {doc.PageCount}");

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: numbered list spanning multiple pages");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/list-multipage");

            // Both pages should have content
            var bmp1 = TestHelper.RasterizePage(bytes, "Layout/list-multipage", 0);
            var bmp2 = TestHelper.RasterizePage(bytes, "Layout/list-multipage", 1);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bmp1,
                TestHelper.PtToPx(72), TestHelper.PtToPx(400),
                TestHelper.PtToPx(72), TestHelper.PtToPx(400)));
            Assert.True(TestHelper.HasDarkPixelsInRegion(bmp2,
                TestHelper.PtToPx(72), TestHelper.PtToPx(400),
                TestHelper.PtToPx(72), TestHelper.PtToPx(400)));
        }

        [Fact]
        public void ListAfterContent_FlowsCorrectly()
        {
            var layout = new PdfDocumentLayout();
            layout.AddParagraph("Introduction paragraph.", new ParagraphOptions { SpaceAfter = 10 });
            layout.AddList(new[]
            {
                new ListItem("Point A"),
                new ListItem("Point B")
            }, ListStyle.Bullet);
            layout.AddParagraph("Conclusion paragraph.", new ParagraphOptions { SpaceBefore = 10 });

            var doc = layout.Generate();
            Assert.Equal(1, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: paragraph, then list, then paragraph");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/list-with-content");
            var bitmap = TestHelper.RasterizePage(bytes, "Layout/list-with-content");

            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(72), TestHelper.PtToPx(400),
                TestHelper.PtToPx(72), TestHelper.PtToPx(180)));
        }
    }
}
