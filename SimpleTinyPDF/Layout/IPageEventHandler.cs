namespace SimpleTinyPDF
{
    /// <summary>
    /// Page lifecycle event types fired during layout rendering.
    /// </summary>
    public enum PageEventType
    {
        /// <summary>A new page has been created and its header drawn.</summary>
        PageCreated,

        /// <summary>A page is about to have its footer drawn and be finalized.</summary>
        PageFinished,

        /// <summary>A new section has started.</summary>
        SectionStarted,

        /// <summary>A section has finished.</summary>
        SectionFinished
    }

    /// <summary>
    /// Interface for handling page lifecycle events during layout rendering.
    /// </summary>
    public interface IPageEventHandler
    {
        /// <summary>Called when a page lifecycle event occurs.</summary>
        void HandleEvent(PageEventType eventType, PdfPage page, PageContext context);
    }
}
