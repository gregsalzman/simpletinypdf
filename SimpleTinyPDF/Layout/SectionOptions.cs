namespace SimpleTinyPDF
{
    /// <summary>
    /// Configuration for a document section — a group of pages with
    /// independent page size, margins, headers/footers, and column layout.
    /// Null properties inherit from the parent PdfDocumentLayout.
    /// </summary>
    public class SectionOptions
    {
        /// <summary>Page size for this section. Null inherits from layout.</summary>
        public PageSize PageSize { get; set; }

        /// <summary>Page margins for this section. Null inherits from layout.</summary>
        public PdfMargins Margins { get; set; }

        /// <summary>Header/footer for this section. Null inherits from layout.</summary>
        public HeaderFooterOptions HeaderFooter { get; set; }

        /// <summary>When true, resets the page number to 1 at the start of this section.</summary>
        public bool RestartPageNumbers { get; set; }

        /// <summary>Number of text columns (1 = single column). Default is 1.</summary>
        public int ColumnCount { get; set; } = 1;

        /// <summary>Gap between columns in points. Default is 18pt.</summary>
        public float ColumnGap { get; set; } = 18f;
    }
}
