using System;
using System.Collections.Generic;
using System.IO;

namespace SimpleTinyPDF
{
    /// <summary>
    /// High-level layout API that provides automatic content flow, pagination,
    /// headers/footers, and page numbering on top of the existing PdfDocument API.
    /// </summary>
    public class PdfDocumentLayout
    {
        private readonly List<LayoutElement> _elements = new List<LayoutElement>();
        private PdfDocument _document;
        private bool _generated;

        /// <summary>Creates a layout with its own PdfDocument.</summary>
        public PdfDocumentLayout()
        {
            _document = new PdfDocument();
        }

        /// <summary>Wraps an existing PdfDocument. Layout pages are appended to it.</summary>
        public PdfDocumentLayout(PdfDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>Page size for new pages. Default is A4.</summary>
        public PageSize PageSize { get; set; } = PageSize.A4;

        /// <summary>Page margins. Default is 72pt (1 inch) on all sides.</summary>
        public PdfMargins Margins { get; set; } = new PdfMargins(72);

        /// <summary>Header and footer configuration.</summary>
        public HeaderFooterOptions HeaderFooter { get; set; } = new HeaderFooterOptions();

        /// <summary>Default paragraph options applied when no explicit options are given.</summary>
        public ParagraphOptions DefaultParagraphOptions { get; set; }

        /// <summary>The underlying PdfDocument (available after Generate).</summary>
        public PdfDocument Document => _document;

        // ── Content Methods ────────────────────────────────────────

        /// <summary>Adds a plain-text paragraph.</summary>
        public void AddParagraph(string text, ParagraphOptions options = null)
        {
            _elements.Add(LayoutElement.CreateParagraph(text, options));
        }

        /// <summary>Adds a rich-text paragraph with mixed formatting.</summary>
        public void AddParagraph(TextSpan[] spans, ParagraphOptions options = null)
        {
            _elements.Add(LayoutElement.CreateRichParagraph(spans, options));
        }

        /// <summary>Adds an image at the current flow position.</summary>
        public void AddImage(PdfImage image, ImageOptions options = null)
        {
            _elements.Add(LayoutElement.CreateImage(image, options));
        }

        /// <summary>Adds a table at the current flow position.</summary>
        public void AddTable(PdfTable table)
        {
            _elements.Add(LayoutElement.CreateTable(table));
        }

        /// <summary>Adds a list at the current flow position.</summary>
        public void AddList(ListItem[] items, ListStyle style = ListStyle.Bullet)
        {
            _elements.Add(LayoutElement.CreateList(items, style));
        }

        /// <summary>Forces a page break.</summary>
        public void AddPageBreak()
        {
            _elements.Add(LayoutElement.CreatePageBreak());
        }

        // ── Generate / Save ────────────────────────────────────────

        /// <summary>
        /// Renders all elements into the document. Uses a two-pass approach
        /// when headers/footers are configured to provide accurate TotalPages.
        /// </summary>
        public PdfDocument Generate()
        {
            if (_generated) return _document;
            _generated = true;

            if (_elements.Count == 0) return _document;

            if (HeaderFooter != null && HeaderFooter.HasAny)
            {
                // Pass 1: render to a temporary document to count pages
                var tempDoc = new PdfDocument();
                var engine1 = new FlowEngine(tempDoc, PageSize, Margins, HeaderFooter,
                    DefaultParagraphOptions, totalPages: 0);
                int totalPages = engine1.Render(_elements);

                // Pass 2: render to the real document with correct total
                var engine2 = new FlowEngine(_document, PageSize, Margins, HeaderFooter,
                    DefaultParagraphOptions, totalPages: totalPages);
                engine2.Render(_elements);
            }
            else
            {
                var engine = new FlowEngine(_document, PageSize, Margins, HeaderFooter,
                    DefaultParagraphOptions, totalPages: 0);
                engine.Render(_elements);
            }

            return _document;
        }

        /// <summary>Generates the document and saves to a file.</summary>
        public void Save(string filePath)
        {
            Generate();
            _document.Save(filePath);
        }

        /// <summary>Generates the document and saves to a stream.</summary>
        public void Save(Stream stream)
        {
            Generate();
            _document.Save(stream);
        }

        /// <summary>Generates the document and returns the PDF bytes.</summary>
        public byte[] ToArray()
        {
            Generate();
            return _document.ToArray();
        }
    }
}
