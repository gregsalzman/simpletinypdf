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
        private readonly List<PdfRadioGroup> _radioGroups = new List<PdfRadioGroup>();
        private readonly Dictionary<PdfReadDocument, ImportContext> _importContexts =
            new Dictionary<PdfReadDocument, ImportContext>();

        /// <summary>Document title (appears in PDF metadata).</summary>
        public string Title { get; set; }

        /// <summary>Document author (appears in PDF metadata).</summary>
        public string Author { get; set; }

        /// <summary>
        /// Optional encryption settings. When set, the saved PDF will be encrypted
        /// with the specified algorithm and passwords.
        /// </summary>
        public PdfEncryptionOptions Encryption { get; set; }

        /// <summary>
        /// Optional digital signature settings. When set, the saved PDF will be
        /// digitally signed with the specified certificate.
        /// </summary>
        public PdfSignatureOptions Signature { get; set; }

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
        /// Imports a page from an existing PDF and appends it to this document.
        /// The page is copied in full (content, fonts, images, links); the source document
        /// can be disposed afterwards. Saving produces a completely new file, so any digital
        /// signatures in the source are not carried over.
        /// </summary>
        /// <param name="source">The opened source PDF.</param>
        /// <param name="pageNumber">The 1-based page number in the source.</param>
        /// <returns>The imported page. Drawing on it is not supported yet.</returns>
        public PdfPage ImportPage(PdfReadDocument source, int pageNumber) =>
            ImportPage(source, pageNumber, _pages.Count + 1);

        /// <summary>
        /// Imports a page from an existing PDF at the specified 1-based position.
        /// </summary>
        /// <param name="source">The opened source PDF.</param>
        /// <param name="pageNumber">The 1-based page number in the source.</param>
        /// <param name="insertAt">The 1-based position in this document (1 inserts at the beginning).</param>
        /// <returns>The imported page. Drawing on it is not supported yet.</returns>
        public PdfPage ImportPage(PdfReadDocument source, int pageNumber, int insertAt)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (insertAt < 1 || insertAt > _pages.Count + 1)
                throw new ArgumentOutOfRangeException(nameof(insertAt),
                    $"Insert position must be between 1 and {_pages.Count + 1}.");

            if (!_importContexts.TryGetValue(source, out var context))
            {
                context = new ImportContext(source);
                PageImporter.CaptureDocumentDefaults(context);
                _importContexts[source] = context;
            }
            var content = PageImporter.Import(context, pageNumber);
            var size = source.GetPageSize(pageNumber);
            var page = new PdfPage(content, size.Width, size.Height);
            page.Document = this;
            _pages.Insert(insertAt - 1, page);
            return page;
        }

        /// <summary>
        /// Imports a consecutive range of pages from an existing PDF, appending them in order.
        /// </summary>
        /// <param name="source">The opened source PDF.</param>
        /// <param name="firstPage">The 1-based first page to import.</param>
        /// <param name="lastPage">The 1-based last page to import (inclusive).</param>
        public IReadOnlyList<PdfPage> ImportPages(PdfReadDocument source, int firstPage, int lastPage)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (firstPage < 1 || lastPage < firstPage)
                throw new ArgumentOutOfRangeException(nameof(firstPage),
                    "Page range must satisfy 1 <= firstPage <= lastPage.");
            var imported = new List<PdfPage>();
            for (int i = firstPage; i <= lastPage; i++)
                imported.Add(ImportPage(source, i));
            return imported;
        }

        /// <summary>
        /// Imports specific pages from an existing PDF, appending them in the given order.
        /// The same source page may appear more than once.
        /// </summary>
        /// <param name="source">The opened source PDF.</param>
        /// <param name="pageNumbers">1-based page numbers in the source.</param>
        public IReadOnlyList<PdfPage> ImportPages(PdfReadDocument source, params int[] pageNumbers)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (pageNumbers == null)
                throw new ArgumentNullException(nameof(pageNumbers));
            var imported = new List<PdfPage>();
            foreach (int pageNumber in pageNumbers)
                imported.Add(ImportPage(source, pageNumber));
            return imported;
        }

        /// <summary>
        /// Removes the page at the given 1-based position. Bookmarks or links that
        /// target the removed page lose their destination.
        /// </summary>
        /// <param name="pageNumber">The 1-based page number to remove.</param>
        public void RemovePage(int pageNumber)
        {
            if (pageNumber < 1 || pageNumber > _pages.Count)
                throw new ArgumentOutOfRangeException(nameof(pageNumber),
                    $"Page number must be between 1 and {_pages.Count}.");
            _pages.RemoveAt(pageNumber - 1);
        }

        /// <summary>
        /// Moves a page to a new 1-based position, shifting the pages in between.
        /// </summary>
        /// <param name="fromPageNumber">The 1-based page number of the page to move.</param>
        /// <param name="toPageNumber">The 1-based position it should end up at.</param>
        public void MovePage(int fromPageNumber, int toPageNumber)
        {
            if (fromPageNumber < 1 || fromPageNumber > _pages.Count)
                throw new ArgumentOutOfRangeException(nameof(fromPageNumber),
                    $"Page number must be between 1 and {_pages.Count}.");
            if (toPageNumber < 1 || toPageNumber > _pages.Count)
                throw new ArgumentOutOfRangeException(nameof(toPageNumber),
                    $"Page number must be between 1 and {_pages.Count}.");
            var page = _pages[fromPageNumber - 1];
            _pages.RemoveAt(fromPageNumber - 1);
            _pages.Insert(toPageNumber - 1, page);
        }

        /// <summary>
        /// Creates a new document containing all pages of the given source PDFs, in order.
        /// </summary>
        /// <param name="sources">The opened source PDFs to combine.</param>
        public static PdfDocument Merge(params PdfReadDocument[] sources)
        {
            if (sources == null)
                throw new ArgumentNullException(nameof(sources));
            var doc = new PdfDocument();
            foreach (var source in sources)
            {
                if (source == null)
                    throw new ArgumentException("Source documents must not be null.", nameof(sources));
                doc.ImportPages(source, 1, source.PageCount);
            }
            return doc;
        }

        /// <summary>
        /// Creates a new document containing all pages of the given PDF files, in order.
        /// </summary>
        /// <param name="filePaths">Paths of the PDF files to combine.</param>
        public static PdfDocument Merge(params string[] filePaths)
        {
            if (filePaths == null)
                throw new ArgumentNullException(nameof(filePaths));
            var doc = new PdfDocument();
            foreach (var path in filePaths)
            {
                using (var source = PdfReadDocument.Open(path))
                    doc.ImportPages(source, 1, source.PageCount);
            }
            return doc;
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

        /// <summary>
        /// Creates a radio button group. Add individual radio buttons to pages
        /// with <see cref="PdfPage.AddRadioButton"/>.
        /// </summary>
        /// <param name="name">The field name shared by all radio buttons in this group.</param>
        /// <param name="options">Optional group settings (selected value, colors, flags).</param>
        /// <returns>The radio group, which is passed to <see cref="PdfPage.AddRadioButton"/>.</returns>
        public PdfRadioGroup CreateRadioGroup(string name, RadioGroupOptions options = null)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            var group = new PdfRadioGroup(name, options);
            _radioGroups.Add(group);
            return group;
        }

        /// <summary>Returns the radio groups in this document.</summary>
        internal IReadOnlyList<PdfRadioGroup> GetRadioGroups() => _radioGroups;

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
