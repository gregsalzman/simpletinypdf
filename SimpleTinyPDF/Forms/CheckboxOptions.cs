namespace SimpleTinyPDF
{
    /// <summary>Options for a checkbox form field.</summary>
    public sealed class CheckboxOptions
    {
        /// <summary>Whether the checkbox is initially checked.</summary>
        public bool Checked { get; set; }

        /// <summary>Export value when checked. Defaults to "Yes".</summary>
        public string ExportValue { get; set; } = "Yes";

        /// <summary>Border color. Defaults to black.</summary>
        public PdfColor? BorderColor { get; set; }

        /// <summary>Background fill color.</summary>
        public PdfColor? BackgroundColor { get; set; }

        /// <summary>Color of the checkmark. Defaults to black.</summary>
        public PdfColor? CheckColor { get; set; }

        /// <summary>Border line width in points. Defaults to 1.</summary>
        public float BorderWidth { get; set; } = 1;

        /// <summary>Prevent editing of the field value.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Mark the field as required.</summary>
        public bool Required { get; set; }
    }
}
