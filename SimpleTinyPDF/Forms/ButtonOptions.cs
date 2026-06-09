namespace SimpleTinyPDF
{
    /// <summary>Options for a push button form field.</summary>
    public sealed class ButtonOptions
    {
        /// <summary>Font for the button label. Defaults to Helvetica.</summary>
        public PdfFontSource Font { get; set; }

        /// <summary>Font size in points. Defaults to 12.</summary>
        public float FontSize { get; set; } = 12;

        /// <summary>Label text color. Defaults to black.</summary>
        public PdfColor? TextColor { get; set; }

        /// <summary>Background fill color.</summary>
        public PdfColor? BackgroundColor { get; set; }

        /// <summary>Border color. Defaults to black.</summary>
        public PdfColor? BorderColor { get; set; }

        /// <summary>Border line width in points. Defaults to 1.</summary>
        public float BorderWidth { get; set; } = 1;
    }
}
