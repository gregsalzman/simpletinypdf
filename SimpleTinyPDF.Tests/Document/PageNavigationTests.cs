using System;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class PageNavigationTests
    {
        // --- PageCount ---

        [Fact]
        public void PageCount_EmptyDocument_ReturnsZero()
        {
            var doc = new PdfDocument();
            Assert.Equal(0, doc.PageCount);
        }

        [Fact]
        public void PageCount_AfterAddingPages_ReturnsCorrectCount()
        {
            var doc = new PdfDocument();
            doc.AddPage();
            doc.AddPage();
            doc.AddPage();
            Assert.Equal(3, doc.PageCount);
        }

        // --- FirstPage / LastPage ---

        [Fact]
        public void FirstPage_EmptyDocument_ReturnsNull()
        {
            var doc = new PdfDocument();
            Assert.Null(doc.FirstPage);
        }

        [Fact]
        public void LastPage_EmptyDocument_ReturnsNull()
        {
            var doc = new PdfDocument();
            Assert.Null(doc.LastPage);
        }

        [Fact]
        public void FirstPage_ReturnsFirstAddedPage()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage();
            doc.AddPage();
            Assert.Same(page1, doc.FirstPage);
        }

        [Fact]
        public void LastPage_ReturnsLastAddedPage()
        {
            var doc = new PdfDocument();
            doc.AddPage();
            var page2 = doc.AddPage();
            Assert.Same(page2, doc.LastPage);
        }

        [Fact]
        public void SinglePage_FirstAndLastAreSame()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            Assert.Same(page, doc.FirstPage);
            Assert.Same(page, doc.LastPage);
        }

        // --- GetPage ---

        [Fact]
        public void GetPage_ReturnsCorrectPage()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage();
            var page2 = doc.AddPage();
            var page3 = doc.AddPage();

            Assert.Same(page1, doc.GetPage(1));
            Assert.Same(page2, doc.GetPage(2));
            Assert.Same(page3, doc.GetPage(3));
        }

        [Fact]
        public void GetPage_PageZero_Throws()
        {
            var doc = new PdfDocument();
            doc.AddPage();
            Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetPage(0));
        }

        [Fact]
        public void GetPage_NegativePageNumber_Throws()
        {
            var doc = new PdfDocument();
            doc.AddPage();
            Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetPage(-1));
        }

        [Fact]
        public void GetPage_PageNumberExceedsCount_Throws()
        {
            var doc = new PdfDocument();
            doc.AddPage();
            doc.AddPage();
            Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetPage(3));
        }

        [Fact]
        public void GetPage_EmptyDocument_Throws()
        {
            var doc = new PdfDocument();
            Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetPage(1));
        }

        // --- GetPageNumber ---

        [Fact]
        public void GetPageNumber_ReturnsOneBased()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage();
            var page2 = doc.AddPage();
            var page3 = doc.AddPage();

            Assert.Equal(1, doc.GetPageNumber(page1));
            Assert.Equal(2, doc.GetPageNumber(page2));
            Assert.Equal(3, doc.GetPageNumber(page3));
        }

        [Fact]
        public void GetPageNumber_NullPage_Throws()
        {
            var doc = new PdfDocument();
            Assert.Throws<ArgumentNullException>(() => doc.GetPageNumber(null));
        }

        [Fact]
        public void GetPageNumber_PageFromAnotherDocument_Throws()
        {
            var doc1 = new PdfDocument();
            var doc2 = new PdfDocument();
            var page = doc2.AddPage();

            Assert.Throws<ArgumentException>(() => doc1.GetPageNumber(page));
        }

        // --- InsertPage ---

        [Fact]
        public void InsertPage_AtBeginning_ShiftsExistingPages()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage();
            var page2 = doc.AddPage();

            var inserted = doc.InsertPage(1);

            Assert.Equal(3, doc.PageCount);
            Assert.Same(inserted, doc.GetPage(1));
            Assert.Same(page1, doc.GetPage(2));
            Assert.Same(page2, doc.GetPage(3));
        }

        [Fact]
        public void InsertPage_InMiddle_ShiftsSubsequentPages()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage();
            var page2 = doc.AddPage();
            var page3 = doc.AddPage();

            var inserted = doc.InsertPage(2);

            Assert.Equal(4, doc.PageCount);
            Assert.Same(page1, doc.GetPage(1));
            Assert.Same(inserted, doc.GetPage(2));
            Assert.Same(page2, doc.GetPage(3));
            Assert.Same(page3, doc.GetPage(4));
        }

        [Fact]
        public void InsertPage_AtEnd_SameAsAddPage()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage();

            var inserted = doc.InsertPage(2);

            Assert.Equal(2, doc.PageCount);
            Assert.Same(page1, doc.GetPage(1));
            Assert.Same(inserted, doc.GetPage(2));
        }

        [Fact]
        public void InsertPage_IntoEmptyDocument_AtPositionOne()
        {
            var doc = new PdfDocument();
            var inserted = doc.InsertPage(1);

            Assert.Equal(1, doc.PageCount);
            Assert.Same(inserted, doc.GetPage(1));
        }

        [Fact]
        public void InsertPage_WithPageSize_UsesSpecifiedSize()
        {
            var doc = new PdfDocument();
            doc.AddPage(PageSize.A4);
            var inserted = doc.InsertPage(1, PageSize.Letter);

            Assert.Equal(PageSize.Letter.Width, inserted.Width);
            Assert.Equal(PageSize.Letter.Height, inserted.Height);
        }

        [Fact]
        public void InsertPage_PageZero_Throws()
        {
            var doc = new PdfDocument();
            doc.AddPage();
            Assert.Throws<ArgumentOutOfRangeException>(() => doc.InsertPage(0));
        }

        [Fact]
        public void InsertPage_ExceedsCountPlusOne_Throws()
        {
            var doc = new PdfDocument();
            doc.AddPage();
            Assert.Throws<ArgumentOutOfRangeException>(() => doc.InsertPage(3));
        }

        // --- Round-trip: GetPage + draw + save ---

        [Fact]
        public void GetPage_CanDrawOnRetrievedPage_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            doc.AddPage(PageSize.A4);
            doc.AddPage(PageSize.A4);
            doc.AddPage(PageSize.A4);

            // Go back to page 1 via GetPage and draw on it
            var page1 = doc.GetPage(1);
            TestHelper.AddDescription(page1, "Verify: pages can be navigated and retrieved by index");
            page1.DrawText("Added later to page 1", 50, 50);

            // Draw on page 3
            var page3 = doc.GetPage(3);
            page3.DrawText("Page 3 content", 50, 50);

            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/page-navigation-roundtrip");
            Assert.Equal(3, TestHelper.GetPageCount(bytes));

            // Verify page 1 has content
            var bitmap = TestHelper.RasterizePage(bytes, "Document/page-navigation-roundtrip", 0);
            Assert.True(bitmap.Width > 0);
            bitmap.Dispose();
        }

        [Fact]
        public void InsertPage_DrawAndSave_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            var page1 = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page1, "Verify: pages can be inserted at specific positions");
            page1.DrawText("Original page 1", 50, 50);

            var page2 = doc.AddPage(PageSize.A4);
            page2.DrawText("Original page 2", 50, 50);

            // Insert a new page between them
            var inserted = doc.InsertPage(2, PageSize.A4);
            inserted.DrawText("Inserted between pages", 50, 50);

            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/insert-page-roundtrip");
            Assert.Equal(3, TestHelper.GetPageCount(bytes));
        }
    }
}
