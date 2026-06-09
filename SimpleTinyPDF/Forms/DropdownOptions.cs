namespace SimpleTinyPDF
{
    /// <summary>Options for a dropdown (combo box) form field.</summary>
    public sealed class DropdownOptions
    {
        /// <summary>Initially selected item value.</summary>
        public string SelectedValue { get; set; }

        /// <summary>Font for the field text. Defaults to Helvetica.</summary>
        public PdfFontSource Font { get; set; }

        /// <summary>Font size in points. Defaults to 12.</summary>
        public float FontSize { get; set; } = 12;

        /// <summary>Text color. Defaults to black.</summary>
        public PdfColor? TextColor { get; set; }

        /// <summary>Background fill color.</summary>
        public PdfColor? BackgroundColor { get; set; }

        /// <summary>Border color. Defaults to black.</summary>
        public PdfColor? BorderColor { get; set; }

        /// <summary>Border line width in points. Defaults to 1.</summary>
        public float BorderWidth { get; set; } = 1;

        /// <summary>Allow the user to type a custom value (editable combo box).</summary>
        public bool Editable { get; set; }

        /// <summary>Prevent editing of the field value.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Mark the field as required.</summary>
        public bool Required { get; set; }
    }
}
