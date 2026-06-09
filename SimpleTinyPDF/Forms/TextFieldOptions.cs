namespace SimpleTinyPDF
{
    /// <summary>Options for a text form field.</summary>
    public sealed class TextFieldOptions
    {
        /// <summary>Initial text value displayed in the field.</summary>
        public string Value { get; set; }

        /// <summary>Default value used when the form is reset.</summary>
        public string DefaultValue { get; set; }

        /// <summary>Font for the field text. Defaults to Helvetica.</summary>
        public PdfFontSource Font { get; set; }

        /// <summary>Font size in points. Defaults to 12.</summary>
        public float FontSize { get; set; } = 12;

        /// <summary>Text color. Defaults to black.</summary>
        public PdfColor? TextColor { get; set; }

        /// <summary>Background fill color. Defaults to white.</summary>
        public PdfColor? BackgroundColor { get; set; }

        /// <summary>Border color. Defaults to black.</summary>
        public PdfColor? BorderColor { get; set; }

        /// <summary>Border line width in points. Defaults to 1.</summary>
        public float BorderWidth { get; set; } = 1;

        /// <summary>Allow multiple lines of text.</summary>
        public bool MultiLine { get; set; }

        /// <summary>Mask the entered text (password field).</summary>
        public bool Password { get; set; }

        /// <summary>Prevent editing of the field value.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Mark the field as required.</summary>
        public bool Required { get; set; }

        /// <summary>Maximum number of characters allowed.</summary>
        public int? MaxLength { get; set; }

        /// <summary>Text alignment within the field.</summary>
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    }
}
