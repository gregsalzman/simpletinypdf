namespace SimpleTinyPDF
{
    /// <summary>
    /// Represents a group of radio buttons that share a single field name.
    /// Create via <see cref="PdfDocument.CreateRadioGroup"/>.
    /// </summary>
    public sealed class PdfRadioGroup
    {
        internal PdfRadioGroup(string name, RadioGroupOptions options)
        {
            Name = name;
            if (options != null)
            {
                SelectedValue = options.SelectedValue;
                ReadOnly = options.ReadOnly;
                Required = options.Required;
                BorderColor = options.BorderColor;
                BackgroundColor = options.BackgroundColor;
                DotColor = options.DotColor;
                BorderWidth = options.BorderWidth;
            }
        }

        /// <summary>The field name for this radio group.</summary>
        public string Name { get; }

        /// <summary>The export value of the initially selected radio button, or null for none selected.</summary>
        public string SelectedValue { get; set; }

        internal bool ReadOnly { get; }
        internal bool Required { get; }
        internal PdfColor? BorderColor { get; }
        internal PdfColor? BackgroundColor { get; }
        internal PdfColor? DotColor { get; }
        internal float BorderWidth { get; } = 1;
    }
}
