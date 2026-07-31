using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleTinyPDF
{
    internal static partial class PdfWriter
    {
        private static PdfDict CreateFormWidget(FormField field, PdfObj pageDict,
            List<PdfObj> objects, Func<PdfObj, PdfObj> addObj,
            List<string> drFontParts, HashSet<string> drFontNames,
            bool isRadioChild, ref PdfDict formFontObj)
        {
            var widget = new PdfDict();
            widget.Set("Type", "/Annot");
            widget.Set("Subtype", "/Widget");
            widget.Set("Rect", $"[{PdfStringHelper.F(field.X)} {PdfStringHelper.F(field.Y)} " +
                $"{PdfStringHelper.F(field.X + field.Width)} {PdfStringHelper.F(field.Y + field.Height)}]");
            widget.Set("P", pageDict.Ref);

            // Field flags
            int ff = 0;
            if (field.ReadOnly) ff |= 1;
            if (field.Required) ff |= 2;

            // Font base name for DA
            string fontBaseName = "Helvetica";
            if (field.Font != null && field.Font.IsBuiltIn)
                fontBaseName = PdfFontNames.GetPdfName(field.Font.BuiltInFont);

            // Lazily create a shared font object (with /Encoding) for form fields.
            // iText uses a single indirect font reference in DR and all AP Resources.
            if (formFontObj == null)
            {
                formFontObj = new PdfDict();
                formFontObj.Set("Type", "/Font");
                formFontObj.Set("Subtype", "/Type1");
                formFontObj.Set("BaseFont", $"/{fontBaseName}");
                formFontObj.Set("Encoding", "/WinAnsiEncoding");
                addObj(formFontObj);
            }
            string fontRef = formFontObj.Ref;

            // Ensure font is in DR (using indirect reference)
            if (!drFontNames.Contains(fontBaseName))
            {
                drFontParts.Add($"/F1 {fontRef}");
                drFontNames.Add(fontBaseName);
            }

            switch (field.Type)
            {
                case FormFieldType.Text:
                {
                    widget.Set("FT", "/Tx");
                    widget.Set("T", PdfStringHelper.Escape(field.Name));
                    if (field.MultiLine) ff |= 4096;
                    if (field.Password) ff |= 8192;
                    if (field.Value != null)
                        widget.Set("V", PdfStringHelper.Escape(field.Value));
                    if (field.DefaultValue != null)
                        widget.Set("DV", PdfStringHelper.Escape(field.DefaultValue));
                    if (field.MaxLength.HasValue)
                        widget.Set("MaxLen", field.MaxLength.Value.ToString());

                    // Alignment: 0=left, 1=center, 2=right
                    if (field.Alignment == TextAlignment.Center) widget.Set("Q", "1");
                    else if (field.Alignment == TextAlignment.Right) widget.Set("Q", "2");

                    // Match iText: DA has font only, no /MK or /BS
                    widget.Set("DA", $"(/F1 {PdfStringHelper.F(field.FontSize)} Tf)");

                    var apData = FormAppearanceBuilder.BuildTextFieldAppearance(field);
                    var apStream = CreateFormXObject(field.Width, field.Height, fontRef, apData, addObj);
                    widget.Set("AP", $"<< /N {apStream.Ref} >>");
                    break;
                }

                case FormFieldType.Checkbox:
                {
                    widget.Set("FT", "/Btn");
                    widget.Set("T", PdfStringHelper.Escape(field.Name));
                    string exportVal = field.ExportValue ?? "Yes";

                    // Checked appearance
                    var checkedData = FormAppearanceBuilder.BuildCheckboxAppearance(
                        true, field.Width, field.BorderColor, field.BackgroundColor, field.CheckColor, field.BorderWidth);
                    var checkedXObj = CreateFormXObject(field.Width, field.Height, null, checkedData, addObj);

                    // Unchecked appearance
                    var uncheckedData = FormAppearanceBuilder.BuildCheckboxAppearance(
                        false, field.Width, field.BorderColor, field.BackgroundColor, field.CheckColor, field.BorderWidth);
                    var uncheckedXObj = CreateFormXObject(field.Width, field.Height, null, uncheckedData, addObj);

                    widget.Set("AP", $"<< /N << /{exportVal} {checkedXObj.Ref} /Off {uncheckedXObj.Ref} >> >>");
                    widget.Set("AS", field.Checked ? $"/{exportVal}" : "/Off");
                    widget.Set("V", field.Checked ? $"/{exportVal}" : "/Off");
                    break;
                }

                case FormFieldType.RadioButton:
                {
                    // Widget only (parent field created separately)
                    string radioVal = field.RadioValue ?? "Option";
                    bool selected = field.RadioGroup.SelectedValue == radioVal;

                    var selData = FormAppearanceBuilder.BuildRadioButtonAppearance(
                        true, field.Width, field.BorderColor, field.BackgroundColor, field.DotColor, field.BorderWidth);
                    var selXObj = CreateFormXObject(field.Width, field.Height, null, selData, addObj);

                    var unselData = FormAppearanceBuilder.BuildRadioButtonAppearance(
                        false, field.Width, field.BorderColor, field.BackgroundColor, field.DotColor, field.BorderWidth);
                    var unselXObj = CreateFormXObject(field.Width, field.Height, null, unselData, addObj);

                    widget.Set("AP", $"<< /N << /{radioVal} {selXObj.Ref} /Off {unselXObj.Ref} >> >>");
                    widget.Set("AS", selected ? $"/{radioVal}" : "/Off");
                    break;
                }

                case FormFieldType.Dropdown:
                {
                    widget.Set("FT", "/Ch");
                    widget.Set("T", PdfStringHelper.Escape(field.Name));
                    ff |= 131072; // Combo bit
                    if (field.Editable) ff |= 262144;
                    if (field.Items != null)
                        widget.Set("Opt", FormatOptArray(field.Items));
                    if (field.SelectedValue != null)
                        widget.Set("V", PdfStringHelper.Escape(field.SelectedValue));

                    widget.Set("DA", $"(/F1 {PdfStringHelper.F(field.FontSize)} Tf)");

                    var ddApData = FormAppearanceBuilder.BuildDropdownAppearance(field);
                    var ddApStream = CreateFormXObject(field.Width, field.Height, fontRef, ddApData, addObj);
                    widget.Set("AP", $"<< /N {ddApStream.Ref} >>");
                    break;
                }

                case FormFieldType.Listbox:
                {
                    widget.Set("FT", "/Ch");
                    widget.Set("T", PdfStringHelper.Escape(field.Name));
                    if (field.MultiSelect) ff |= 2097152;
                    if (field.Items != null)
                        widget.Set("Opt", FormatOptArray(field.Items));
                    if (field.SelectedValues != null && field.SelectedValues.Length == 1)
                        widget.Set("V", PdfStringHelper.Escape(field.SelectedValues[0]));
                    else if (field.SelectedValues != null && field.SelectedValues.Length > 1)
                    {
                        var sv = new StringBuilder("[");
                        foreach (var v in field.SelectedValues)
                            sv.Append(PdfStringHelper.Escape(v)).Append(' ');
                        sv.Append(']');
                        widget.Set("V", sv.ToString());
                    }

                    widget.Set("DA", $"(/F1 {PdfStringHelper.F(field.FontSize)} Tf)");

                    var lbApData = FormAppearanceBuilder.BuildListboxAppearance(field);
                    var lbApStream = CreateFormXObject(field.Width, field.Height, fontRef, lbApData, addObj);
                    widget.Set("AP", $"<< /N {lbApStream.Ref} >>");
                    break;
                }

                case FormFieldType.PushButton:
                {
                    widget.Set("FT", "/Btn");
                    widget.Set("T", PdfStringHelper.Escape(field.Name));
                    ff |= 65536; // Pushbutton bit

                    var apData = FormAppearanceBuilder.BuildButtonAppearance(field);
                    var apStream = CreateFormXObject(field.Width, field.Height, fontRef, apData, addObj);
                    widget.Set("AP", $"<< /N {apStream.Ref} >>");
                    break;
                }
            }

            if (ff != 0)
                widget.Set("Ff", ff.ToString());

            addObj(widget);
            return widget;
        }

        private static PdfStream CreateFormXObject(float width, float height,
            string fontRef, byte[] content, Func<PdfObj, PdfObj> addObj)
        {
            var stream = new PdfStream();
            stream.Set("Type", "/XObject");
            stream.Set("Subtype", "/Form");
            stream.Set("BBox", $"[0 0 {PdfStringHelper.F(width)} {PdfStringHelper.F(height)}]");
            if (fontRef != null)
                stream.Set("Resources", $"<< /Font << /F1 {fontRef} >> >>");
            stream.Data = content;
            addObj(stream);
            return stream;
        }

        private static string FormatOptArray(string[] items)
        {
            var sb = new StringBuilder("[");
            foreach (var item in items)
                sb.Append(PdfStringHelper.Escape(item)).Append(' ');
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Appends an annotation to a page's /Annots, dispatching on the page's kind:
        /// generated pages hold a serialized array; imported pages get a writer-side
        /// reference injected into their parsed /Annots array.
        /// </summary>
        private static void AppendAnnotToPage(PdfObj pageObj, PdfDict annotDict)
        {
            if (pageObj is ImportedObj imported && imported.Body is CosDict body)
            {
                if (!(body.Get("Annots") is CosArray annots))
                {
                    annots = new CosArray();
                    body.Set("Annots", annots);
                }
                annots.Items.Add(new CosWriterRef(annotDict));
                return;
            }
            AppendAnnotToPage((PdfDict)pageObj, annotDict);
        }

        private static void AppendAnnotToPage(PdfDict pageDict, PdfDict annotDict)
        {
            // Find existing Annots entry and append, or create new
            for (int i = 0; i < pageDict.Entries.Count; i++)
            {
                if (pageDict.Entries[i].Key == "Annots")
                {
                    var existing = pageDict.Entries[i].Value;
                    // Insert before closing ']'
                    var updated = existing.TrimEnd(']') + " " + annotDict.Ref + "]";
                    pageDict.Entries[i] = new KeyValuePair<string, string>("Annots", updated);
                    return;
                }
            }
            pageDict.Set("Annots", "[" + annotDict.Ref + "]");
        }
    }
}
