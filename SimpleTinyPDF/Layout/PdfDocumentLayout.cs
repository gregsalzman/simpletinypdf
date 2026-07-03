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
        private readonly List<object> _eventHandlers = new List<object>();
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

        /// <summary>Custom renderer for overriding default element rendering.</summary>
        public CustomRenderer Renderer { get; set; }

        /// <summary>
        /// When true, renders in a single streaming pass and skips the
        /// page-counting pass. Faster for very large documents, but
        /// PageContext.TotalPages and SectionTotalPages remain 0.
        /// </summary>
        public bool LazyRendering { get; set; }

        /// <summary>
        /// Debug helpers: margin/column guides, element bounding boxes,
        /// and layout warnings.
        /// </summary>
        public DebugOptions Debug { get; set; }

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

        /// <summary>Adds a horizontal rule spanning the content width.</summary>
        public void AddHorizontalRule(HorizontalRuleOptions options = null)
        {
            _elements.Add(LayoutElement.CreateHorizontalRule(options));
        }

        /// <summary>Forces a page break.</summary>
        public void AddPageBreak()
        {
            _elements.Add(LayoutElement.CreatePageBreak());
        }

        /// <summary>
        /// Starts a new section with independent page settings.
        /// Null properties inherit from the parent layout.
        /// </summary>
        public void AddSection(SectionOptions options = null)
        {
            _elements.Add(LayoutElement.CreateSectionBreak(options ?? new SectionOptions()));
        }

        /// <summary>
        /// Forces a column break. Advances to the next column, or to a
        /// new page if already in the last column.
        /// </summary>
        public void AddColumnBreak()
        {
            _elements.Add(LayoutElement.CreateColumnBreak());
        }

        /// <summary>Adds a page event handler using a delegate.</summary>
        public void AddEventHandler(Action<PageEventType, PdfPage, PageContext> handler)
        {
            if (handler != null)
                _eventHandlers.Add(handler);
        }

        /// <summary>Adds a page event handler using the IPageEventHandler interface.</summary>
        public void AddEventHandler(IPageEventHandler handler)
        {
            if (handler != null)
                _eventHandlers.Add(handler);
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

            if (LazyRendering)
            {
                // Single streaming pass; TotalPages stays 0
                var lazy = new LazyFlowEngine(_document, PageSize, Margins, HeaderFooter,
                    DefaultParagraphOptions, _eventHandlers, Renderer, Debug);
                lazy.Render(_elements);
            }
            else if (HeaderFooter != null && HeaderFooter.HasAny)
            {
                // Pass 1: render to a temporary document to count pages
                // (no debug — overlays and warnings belong to the final pass)
                var tempDoc = new PdfDocument();
                var engine1 = new FlowEngine(tempDoc, PageSize, Margins, HeaderFooter,
                    DefaultParagraphOptions, totalPages: 0, _eventHandlers, Renderer);
                var result1 = engine1.Render(_elements);

                // Pass 2: render to the real document with correct totals
                var engine2 = new FlowEngine(_document, PageSize, Margins, HeaderFooter,
                    DefaultParagraphOptions, totalPages: result1.TotalPages,
                    _eventHandlers, Renderer, result1.SectionPageCounts, Debug);
                engine2.Render(_elements);
            }
            else
            {
                var engine = new FlowEngine(_document, PageSize, Margins, HeaderFooter,
                    DefaultParagraphOptions, totalPages: 0, _eventHandlers, Renderer,
                    sectionTotalPages: null, debug: Debug);
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
