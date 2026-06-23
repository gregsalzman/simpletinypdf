namespace SimpleTinyPDF
{
    /// <summary>
    /// Styling and layout options for a paragraph in a layout document.
    /// </summary>
    public class ParagraphOptions
    {
        /// <summary>Font to use. Default is Helvetica.</summary>
        public PdfFontSource Font { get; set; }

        /// <summary>Font size in points. Default is 12.</summary>
        public float FontSize { get; set; } = 12f;

        /// <summary>Text color. Default is black.</summary>
        public PdfColor? Color { get; set; }

        /// <summary>Text alignment. Default is Left.</summary>
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;

        /// <summary>Line spacing multiplier. Default is 1.2.</summary>
        public float LineSpacing { get; set; } = 1.2f;

        /// <summary>Space before the paragraph in points.</summary>
        public float SpaceBefore { get; set; }

        /// <summary>Space after the paragraph in points.</summary>
        public float SpaceAfter { get; set; }

        /// <summary>Whether to underline text.</summary>
        public bool Underline { get; set; }

        /// <summary>Faux bold effect.</summary>
        public bool Bold { get; set; }

        /// <summary>Faux italic effect.</summary>
        public bool Italic { get; set; }

        /// <summary>Character spacing in points.</summary>
        public float CharacterSpacing { get; set; }

        /// <summary>Text opacity (0.0 to 1.0). Default is 1.0.</summary>
        public float Opacity { get; set; } = 1f;

        /// <summary>Tab stop definitions for tab-delimited text.</summary>
        public TabStop[] TabStops { get; set; }
    }
}
