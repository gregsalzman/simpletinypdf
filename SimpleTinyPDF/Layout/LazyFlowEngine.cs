using System.Collections.Generic;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Single-pass streaming layout engine. Renders elements as they are
    /// enumerated, skipping the page-counting pass, so generation cost stays
    /// flat for very large documents. Trade-off: PageContext.TotalPages and
    /// SectionTotalPages remain 0.
    /// </summary>
    internal class LazyFlowEngine
    {
        private readonly FlowEngine _engine;

        internal LazyFlowEngine(PdfDocument doc, PageSize pageSize, PdfMargins margins,
            HeaderFooterOptions headerFooter, ParagraphOptions defaultOptions,
            List<object> eventHandlers, CustomRenderer renderer, DebugOptions debug)
        {
            _engine = new FlowEngine(doc, pageSize, margins, headerFooter, defaultOptions,
                totalPages: 0, eventHandlers, renderer, sectionTotalPages: null, debug: debug);
        }

        internal FlowEngine.RenderResult Render(IEnumerable<LayoutElement> elements) =>
            _engine.Render(elements);
    }
}
