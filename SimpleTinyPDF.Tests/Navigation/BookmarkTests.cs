using System;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class BookmarkTests
    {
        [Fact]
        public void AddBookmark_NullTitle_Throws()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            Assert.Throws<ArgumentException>(() => doc.AddBookmark(null, page));
        }

        [Fact]
        public void AddBookmark_EmptyTitle_Throws()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            Assert.Throws<ArgumentException>(() => doc.AddBookmark("", page));
        }

        [Fact]
        public void AddBookmark_NullPage_Throws()
        {
            var doc = new PdfDocument();
            Assert.Throws<ArgumentNullException>(() => doc.AddBookmark("Test", null));
        }

        [Fact]
        public void NoBookmarks_NoOutlines()
        {
            var doc = new PdfDocument();
            doc.AddPage();
            var pdf = TestHelper.GetPdfText(doc.ToArray());
            Assert.DoesNotContain("/Outlines", pdf);
            Assert.DoesNotContain("/Type /Outlines", pdf);
        }

        [Fact]
        public void SingleBookmark_ProducesOutlines()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            doc.AddBookmark("Chapter 1", page);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/Type /Outlines", pdf);
            Assert.Contains("/Outlines", pdf);
            Assert.Contains("Chapter 1", pdf);
            Assert.Contains("/Fit", pdf);
        }

        [Fact]
        public void BookmarkWithY_ProducesXYZDestination()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            doc.AddBookmark("Section", page, 200);
            var pdf = TestHelper.GetPdfText(doc.ToArray());

            Assert.Contains("/XYZ", pdf);
        }

        [Fact]
        public void NestedBookmarks_ProduceParentChildStructure()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            var ch1 = doc.AddBookmark("Chapter 1", page);
            ch1.AddBookmark("Section 1.1", page, 200);
            ch1.AddBookmark("Section 1.2", page, 400);
            doc.AddBookmark("Chapter 2", page);

            var pdf = TestHelper.GetPdfText(doc.ToArray());
            Assert.Contains("Chapter 1", pdf);
            Assert.Contains("Section 1.1", pdf);
            Assert.Contains("Section 1.2", pdf);
            Assert.Contains("Chapter 2", pdf);
            Assert.Contains("/First", pdf);
            Assert.Contains("/Last", pdf);
            Assert.Contains("/Parent", pdf);
        }

        [Fact]
        public void MultipleTopLevelBookmarks_HaveNextPrevLinks()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            doc.AddBookmark("A", page);
            doc.AddBookmark("B", page);
            doc.AddBookmark("C", page);

            var pdf = TestHelper.GetPdfText(doc.ToArray());
            Assert.Contains("/Next", pdf);
            Assert.Contains("/Prev", pdf);
        }

        [Fact]
        public void ChildBookmark_AddBookmark_ReturnsNewChild()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            var parent = doc.AddBookmark("Parent", page);
            var child = parent.AddBookmark("Child", page);
            Assert.NotNull(child);
            var grandchild = child.AddBookmark("Grandchild", page, 100);
            Assert.NotNull(grandchild);
        }

        [Fact]
        public void BookmarkWithBottomUpCoordinate_PassesThroughY()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            page.CoordinateOrigin = CoordinateOrigin.BottomUp;
            doc.AddBookmark("Bottom Up Section", page, 500);

            var pdf = TestHelper.GetPdfText(doc.ToArray());
            Assert.Contains("/XYZ 0 500 0", pdf);
        }

        [Fact]
        public void BookmarkWithTopDownCoordinate_ConvertsY()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4); // height = 842
            doc.AddBookmark("Top Down Section", page, 100);

            var pdf = TestHelper.GetPdfText(doc.ToArray());
            // pdfY = 842 - 100 = 742
            Assert.Contains("/XYZ 0 742 0", pdf);
        }

        [Fact]
        public void Bookmarks_MultiPageDocument_ProducesValidPdf()
        {
            var doc = new PdfDocument { Title = "Bookmark Test Document" };

            // Create 10 pages with content
            var pages = new PdfPage[10];
            string[] chapterTitles =
            {
                "Introduction", "Getting Started", "Core Concepts",
                "Advanced Usage", "API Reference", "Configuration",
                "Troubleshooting", "Performance", "Security", "Appendix"
            };

            for (int i = 0; i < 10; i++)
            {
                pages[i] = doc.AddPage(PageSize.A4);
                pages[i].DrawText(chapterTitles[i], 50, 50, PdfFont.HelveticaBold, 24);
                pages[i].DrawLine(50, 80, 545, 80, PdfColor.LightGray, 1f);
                pages[i].DrawText($"Page {i + 1}", 297, 800, PdfFont.Helvetica, 9,
                    PdfColor.DarkGray, TextAlignment.Center);

                // Add section headings on each page
                pages[i].DrawText("Overview", 50, 120, PdfFont.HelveticaBold, 16);
                pages[i].DrawText(
                    $"This is the overview section for {chapterTitles[i]}. " +
                    "It provides a high-level summary of the topics covered in this chapter.",
                    50, 150, PdfFont.Helvetica, 11, width: 495, lineSpacing: 1.4f);

                pages[i].DrawText("Details", 50, 300, PdfFont.HelveticaBold, 16);
                pages[i].DrawText(
                    $"This section covers the detailed aspects of {chapterTitles[i]}. " +
                    "Each topic is explained with examples and best practices.",
                    50, 330, PdfFont.Helvetica, 11, width: 495, lineSpacing: 1.4f);

                pages[i].DrawText("Summary", 50, 500, PdfFont.HelveticaBold, 16);
                pages[i].DrawText(
                    $"In summary, {chapterTitles[i]} is an essential part of the documentation.",
                    50, 530, PdfFont.Helvetica, 11, width: 495, lineSpacing: 1.4f);
            }

            // Description on first page
            TestHelper.AddDescription(pages[0], "Verify: hierarchical bookmarks across multiple pages");

            // Build bookmark hierarchy with nested sections
            // Chapter 1: Introduction (with sub-sections)
            var intro = doc.AddBookmark("Introduction", pages[0]);
            intro.AddBookmark("Overview", pages[0], 120);
            intro.AddBookmark("Details", pages[0], 300);

            // Chapter 2: Getting Started (with sub-sections)
            var gettingStarted = doc.AddBookmark("Getting Started", pages[1]);
            gettingStarted.AddBookmark("Overview", pages[1], 120);
            gettingStarted.AddBookmark("Details", pages[1], 300);
            gettingStarted.AddBookmark("Summary", pages[1], 500);

            // Chapter 3: Core Concepts (with deeper nesting)
            var core = doc.AddBookmark("Core Concepts", pages[2]);
            var coreOverview = core.AddBookmark("Overview", pages[2], 120);
            coreOverview.AddBookmark("Key Principles", pages[2], 150);
            core.AddBookmark("Details", pages[2], 300);

            // Chapters 4-9 as top-level bookmarks with sub-sections
            for (int i = 3; i < 9; i++)
            {
                var ch = doc.AddBookmark(chapterTitles[i], pages[i]);
                ch.AddBookmark("Overview", pages[i], 120);
                ch.AddBookmark("Details", pages[i], 300);
            }

            // Chapter 10: Appendix
            var appendix = doc.AddBookmark("Appendix", pages[9]);
            appendix.AddBookmark("Glossary", pages[9], 120);
            appendix.AddBookmark("Index", pages[9], 300);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Navigation/bookmarks-multipage-hierarchy");

            // Verify page count
            Assert.Equal(10, TestHelper.GetPageCount(bytes));

            // Verify all pages rasterize
            for (int i = 0; i < 10; i++)
            {
                var bitmap = TestHelper.RasterizePage(bytes, "Navigation/bookmarks-multipage-hierarchy", i);
                Assert.True(bitmap.Width > 0);
                bitmap.Dispose();
            }

            // Verify bookmark structure in PDF output
            var pdf = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Type /Outlines", pdf);
            foreach (var title in chapterTitles)
                Assert.Contains(title, pdf);
            Assert.Contains("/First", pdf);
            Assert.Contains("/Last", pdf);
            Assert.Contains("/Parent", pdf);
            Assert.Contains("/Next", pdf);
            Assert.Contains("/Prev", pdf);
            Assert.Contains("/XYZ", pdf);
            Assert.Contains("/Fit", pdf);
        }
    }
}
