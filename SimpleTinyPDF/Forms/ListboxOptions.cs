namespace SimpleTinyPDF
{
    /// <summary>Options for a listbox form field.</summary>
    public sealed class ListboxOptions
    {
        /// <summary>Initially selected item values.</summary>
        public string[] SelectedValues { get; set; }

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

        /// <summary>Allow selecting multiple items.</summary>
        public bool MultiSelect { get; set; }

        /// <summary>Prevent editing of the field value.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Mark the field as required.</summary>
        public bool Required { get; set; }
    }
}
