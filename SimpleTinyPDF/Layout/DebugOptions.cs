using System;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Layout debugging aids. Overlays are drawn on the final rendered document
    /// and warnings are reported once per generated document.
    /// </summary>
    public class DebugOptions
    {
        /// <summary>Draws dashed lines at the margin boundaries on each page.</summary>
        public bool ShowMargins { get; set; }

        /// <summary>Draws dashed vertical lines between columns in multi-column sections.</summary>
        public bool ShowColumns { get; set; }

        /// <summary>Draws a rectangle around each element's bounding box.</summary>
        public bool ShowElementBounds { get; set; }

        /// <summary>Color used for debug overlays. Default is magenta.</summary>
        public PdfColor DebugColor { get; set; } = PdfColor.Rgb(255, 0, 255);

        /// <summary>
        /// Callback invoked with a message when the engine detects a layout
        /// problem (oversized image, empty paragraph, keep-together failure, ...).
        /// </summary>
        public Action<string> OnLayoutWarning { get; set; }
    }
}
