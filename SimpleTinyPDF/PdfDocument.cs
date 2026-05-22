using System;
using System.Collections.Generic;
using System.IO;

namespace SimpleTinyPDF
{
    /// <summary>
    /// The main entry point for creating PDF documents.
    /// </summary>
    public sealed class PdfDocument
    {
        private readonly List<PdfPage> _pages = new List<PdfPage>();
        private readonly List<PdfImage> _images = new List<PdfImage>();
        private readonly List<PdfBookmark> _bookmarks = new List<PdfBookmark>();

        /// <summary>Document title (appears in PDF metadata).</summary>
        public string Title { get; set; }

        /// <summary>Document author (appears in PDF metadata).</summary>
        public string Author { get; set; }

        /// <summary>
        /// Optional encryption settings. When set, the saved PDF will be encrypted
        /// with the specified algorithm and passwords.
        /// </summary>
        public PdfEncryptionOptions Encryption { get; set; }

        /// <summary>The list of pages in this document.</summary>
        public IReadOnlyList<PdfPage> Pages => _pages;

        /// <summary>The number of pages in this document.</summary>
        public int PageCount => _pages.Count;

        /// <summary>The first page in this document, or null if empty.</summary>
        public PdfPage FirstPage => _pages.Count > 0 ? _pages[0] : null;

        /// <summary>The last page in this document, or null if empty.</summary>
        public PdfPage LastPage => _pages.Count > 0 ? _pages[_pages.Count - 1] : null;

        /// <summary>
        /// Gets a page by its 1-based page number.
        /// </summary>
        /// <param name="pageNumber">The 1-based page number.</param>
        /// <returns>The requested page.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the page number is less than 1 or greater than <see cref="PageCount"/>.</exception>
        public PdfPage GetPage(int pageNumber)
        {
            if (pageNumber < 1 || pageNumber > _pages.Count)
                throw new ArgumentOutOfRangeException(nameof(pageNumber),
                    $"Page number must be between 1 and {_pages.Count}.");
            return _pages[pageNumber - 1];
        }

        /// <summary>
        /// Gets the 1-based page number of the specified page.
        /// </summary>
        /// <param name="page">The page to find.</param>
        /// <returns>The 1-based page number.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the page does not belong to this document.</exception>
        public int GetPageNumber(PdfPage page)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));
            int index = _pages.IndexOf(page);
            if (index < 0)
                throw new ArgumentException("The specified page does not belong to this document.", nameof(page));
            return index + 1;
        }

        /// <summary>
        /// Adds a new page with the specified size.
        /// </summary>
        /// <param name="pageSize">Page dimensions. Defaults to A4 if null.</param>
        /// <returns>The new page, ready for drawing.</returns>
        public PdfPage AddPage(PageSize pageSize = null)
        {
            var size = pageSize ?? PageSize.A4;
            var page = new PdfPage(size.Width, size.Height);
            page.Document = this;
            _pages.Add(page);
            return page;
        }

        /// <summary>
        /// Inserts a new page at the specified 1-based position.
        /// </summary>
        /// <param name="pageNumber">The 1-based position where the page should be inserted (1 inserts at the beginning).</param>
        /// <param name="pageSize">Page dimensions. Defaults to A4 if null.</param>
        /// <returns>The new page, ready for drawing.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the page number is less than 1 or greater than <see cref="PageCount"/> + 1.</exception>
        public PdfPage InsertPage(int pageNumber, PageSize pageSize = null)
        {
            if (pageNumber < 1 || pageNumber > _pages.Count + 1)
                throw new ArgumentOutOfRangeException(nameof(pageNumber),
                    $"Page number must be between 1 and {_pages.Count + 1}.");
            var size = pageSize ?? PageSize.A4;
            var page = new PdfPage(size.Width, size.Height);
            page.Document = this;
            _pages.Insert(pageNumber - 1, page);
            return page;
        }

        /// <summary>
        /// Registers an image for use in the document.
        /// If a content-identical image is already registered, the existing instance is returned.
        /// </summary>
        /// <param name="image">The image to register.</param>
        /// <returns>The registered image, or an existing content-identical image.</returns>
        public PdfImage AddImage(PdfImage image)
        {
            for (int i = 0; i < _images.Count; i++)
            {
                if (_images[i].Equals(image))
                    return _images[i];
            }
            _images.Add(image);
            return image;
        }

        /// <summary>Returns the list of images registered in this document.</summary>
        internal IReadOnlyList<PdfImage> GetImages() => _images;

        /// <summary>
        /// Adds a top-level bookmark to the document's outline (table of contents).
        /// </summary>
        /// <param name="title">The display title shown in the bookmark panel.</param>
        /// <param name="page">The page to navigate to when clicked.</param>
        /// <param name="y">Optional Y position on the page (in the page's coordinate system).
        /// When omitted, the bookmark navigates to fit the entire page.</param>
        /// <returns>The newly created bookmark, which can have child bookmarks added to it.</returns>
        public PdfBookmark AddBookmark(string title, PdfPage page, float? y = null)
        {
            var bookmark = new PdfBookmark(title, page, y);
            _bookmarks.Add(bookmark);
            return bookmark;
        }

        /// <summary>Returns the top-level bookmarks in this document.</summary>
        internal IReadOnlyList<PdfBookmark> GetBookmarks() => _bookmarks;

        /// <summary>Saves the PDF document to a file.</summary>
        /// <param name="filePath">The path of the file to create or overwrite.</param>
        public void Save(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                Save(fs);
        }

        /// <summary>Writes the PDF document to a stream.</summary>
        /// <param name="stream">The stream to write the PDF content to.</param>
        public void Save(Stream stream) => PdfWriter.Write(this, stream);

        /// <summary>Returns the PDF document as a byte array.</summary>
        public byte[] ToArray()
        {
            using (var ms = new MemoryStream())
            {
                Save(ms);
                return ms.ToArray();
            }
        }
    }
}
