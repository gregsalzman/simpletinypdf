namespace SimpleTinyPDF
{
    /// <summary>
    /// Provides page numbering context to header/footer callbacks.
    /// </summary>
    public class PageContext
    {
        /// <summary>Current page number (1-based).</summary>
        public int PageNumber { get; internal set; }

        /// <summary>Total number of pages in the document (0 if unknown during first pass).</summary>
        public int TotalPages { get; internal set; }

        /// <summary>True if this is the first page.</summary>
        public bool IsFirstPage => PageNumber == 1;

        /// <summary>True if the page number is even.</summary>
        public bool IsEvenPage => PageNumber % 2 == 0;

        /// <summary>Section page number (reserved for v0.71).</summary>
        public int SectionPageNumber { get; internal set; }

        /// <summary>Section total pages (reserved for v0.71).</summary>
        public int SectionTotalPages { get; internal set; }
    }
}
