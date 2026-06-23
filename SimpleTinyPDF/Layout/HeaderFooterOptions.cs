using System;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Configures header and footer callbacks for a layout document.
    /// </summary>
    public class HeaderFooterOptions
    {
        /// <summary>Header drawn on every page (unless overridden by first-page or even-page header).</summary>
        public Action<PdfPage, PageContext> Header { get; set; }

        /// <summary>Footer drawn on every page (unless overridden by first-page or even-page footer).</summary>
        public Action<PdfPage, PageContext> Footer { get; set; }

        /// <summary>Header drawn only on the first page, overriding the primary header.</summary>
        public Action<PdfPage, PageContext> FirstPageHeader { get; set; }

        /// <summary>Footer drawn only on the first page, overriding the primary footer.</summary>
        public Action<PdfPage, PageContext> FirstPageFooter { get; set; }

        /// <summary>Header drawn on even-numbered pages, overriding the primary header.</summary>
        public Action<PdfPage, PageContext> EvenPageHeader { get; set; }

        /// <summary>Footer drawn on even-numbered pages, overriding the primary footer.</summary>
        public Action<PdfPage, PageContext> EvenPageFooter { get; set; }

        /// <summary>Distance in points from the top page edge where headers are drawn. Default is 36 (0.5 inch).</summary>
        public float HeaderDistance { get; set; } = 36f;

        /// <summary>Distance in points from the bottom page edge where footers are drawn. Default is 36 (0.5 inch).</summary>
        public float FooterDistance { get; set; } = 36f;

        internal bool HasAny =>
            Header != null || Footer != null ||
            FirstPageHeader != null || FirstPageFooter != null ||
            EvenPageHeader != null || EvenPageFooter != null;
    }
}
