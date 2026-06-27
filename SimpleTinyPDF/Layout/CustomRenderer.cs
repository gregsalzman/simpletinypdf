namespace SimpleTinyPDF
{
    /// <summary>
    /// Base class for custom element rendering. Override methods to replace
    /// default rendering behavior. Return null to use the default renderer.
    /// </summary>
    public abstract class CustomRenderer
    {
        /// <summary>
        /// Custom paragraph rendering. Return the new Y position to skip default
        /// rendering, or null to use the default renderer.
        /// </summary>
        public virtual float? RenderParagraph(PdfPage page, string text, float x, float y,
            float width, ParagraphOptions options, PageContext context) => null;

        /// <summary>
        /// Custom rich paragraph rendering. Return the new Y position to skip default
        /// rendering, or null to use the default renderer.
        /// </summary>
        public virtual float? RenderRichParagraph(PdfPage page, TextSpan[] spans, float x, float y,
            float width, ParagraphOptions options, PageContext context) => null;

        /// <summary>
        /// Custom image rendering. Return the new Y position to skip default
        /// rendering, or null to use the default renderer.
        /// </summary>
        public virtual float? RenderImage(PdfPage page, PdfImage image, float x, float y,
            float width, ImageOptions options, PageContext context) => null;

        /// <summary>
        /// Custom table rendering. Return the new Y position to skip default
        /// rendering, or null to use the default renderer.
        /// </summary>
        public virtual float? RenderTable(PdfPage page, PdfTable table, float x, float y,
            PageContext context) => null;

        /// <summary>
        /// Override page break decision. Return true to force a break, false to
        /// prevent a break, or null to use the default logic.
        /// </summary>
        public virtual bool? ShouldBreakPage(float remainingHeight, float elementHeight,
            PageContext context) => null;
    }
}
