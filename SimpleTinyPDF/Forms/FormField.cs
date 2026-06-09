namespace SimpleTinyPDF
{
    internal struct FormField
    {
        // Common
        internal FormFieldType Type;
        internal string Name;
        internal float X, Y, Width, Height; // PDF coordinates (bottom-up)
        internal bool ReadOnly;
        internal bool Required;

        // Visual
        internal PdfFontSource Font;
        internal float FontSize;
        internal PdfColor? TextColor;
        internal PdfColor? BackgroundColor;
        internal PdfColor? BorderColor;
        internal float BorderWidth;

        // Text field
        internal string Value;
        internal string DefaultValue;
        internal bool MultiLine;
        internal bool Password;
        internal int? MaxLength;
        internal TextAlignment Alignment;

        // Checkbox
        internal bool Checked;
        internal string ExportValue;
        internal PdfColor? CheckColor;

        // Radio button
        internal PdfRadioGroup RadioGroup;
        internal string RadioValue;
        internal PdfColor? DotColor;

        // Choice (Dropdown / Listbox)
        internal string[] Items;
        internal string SelectedValue;
        internal string[] SelectedValues;
        internal bool Editable;
        internal bool MultiSelect;

        // Push button
        internal string Label;
    }
}
