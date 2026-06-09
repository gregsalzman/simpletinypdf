namespace SimpleTinyPDF
{
    /// <summary>Options for a radio button group.</summary>
    public sealed class RadioGroupOptions
    {
        /// <summary>Export value of the initially selected radio button, or null for none.</summary>
        public string SelectedValue { get; set; }

        /// <summary>Border color. Defaults to black.</summary>
        public PdfColor? BorderColor { get; set; }

        /// <summary>Background fill color.</summary>
        public PdfColor? BackgroundColor { get; set; }

        /// <summary>Color of the selected dot. Defaults to black.</summary>
        public PdfColor? DotColor { get; set; }

        /// <summary>Border line width in points. Defaults to 1.</summary>
        public float BorderWidth { get; set; } = 1;

        /// <summary>Prevent editing of the field value.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Mark the field as required.</summary>
        public bool Required { get; set; }
    }
}
