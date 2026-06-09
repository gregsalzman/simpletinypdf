using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class FormFieldTests
    {
        // ── Text Fields ──

        [Fact]
        public void TextField_BasicStructure_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: text field produces valid AcroForm PDF structure");
            page.AddTextField("name", 50, 50, 200, 25);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Forms/textfield-basic");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/AcroForm", text);
            Assert.Contains("/FT /Tx", text);
            Assert.Contains("/Subtype /Widget", text);
            Assert.Contains("/T (name)", text);
        }

        [Fact]
        public void TextField_WithValue_ShowsText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: text field with value contains /V entry");
            page.AddTextField("greeting", 50, 50, 200, 25,
                new TextFieldOptions { Value = "Hello World" });
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Forms/textfield-with-value");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/V (Hello World)", text);
        }

        [Fact]
        public void TextField_MultiLine_SetsFfBit()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: multiline text field sets /Ff 4096 bit");
            page.AddTextField("notes", 50, 50, 200, 100,
                new TextFieldOptions { MultiLine = true });
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/FT /Tx", text);
            Assert.Contains("/Ff 4096", text);
        }

        [Fact]
        public void TextField_Password_SetsFfBit()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: password text field sets /Ff 8192 bit");
            page.AddTextField("pwd", 50, 50, 200, 25,
                new TextFieldOptions { Password = true });
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Ff 8192", text);
        }

        [Fact]
        public void TextField_ReadOnlyRequired_SetsFfBits()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: read-only + required sets /Ff 3 flags");
            page.AddTextField("locked", 50, 50, 200, 25,
                new TextFieldOptions { ReadOnly = true, Required = true });
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Ff 3", text); // ReadOnly=1 + Required=2
        }

        [Fact]
        public void TextField_CenterAlignment_SetsQ()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: center-aligned text field sets /Q 1");
            page.AddTextField("centered", 50, 50, 200, 25,
                new TextFieldOptions { Alignment = TextAlignment.Center });
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Q 1", text);
        }

        [Fact]
        public void TextField_Renders_HasDarkPixels()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: text field renders visible pixels");
            page.AddTextField("vis", 50, 50, 200, 25,
                new TextFieldOptions { Value = "Test" });
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Forms/textfield-render");

            var bitmap = TestHelper.RasterizePage(bytes, "Forms/textfield-render",
                withAnnotations: true, withFormFill: true);
            int px = TestHelper.PtToPx(50);
            int py = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                px, px + TestHelper.PtToPx(200), py, py + TestHelper.PtToPx(25)));
        }

        // ── Checkbox ──

        [Fact]
        public void Checkbox_Checked_Structure()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: checked checkbox has /AS /Yes and /V /Yes");
            page.AddCheckbox("agree", 50, 50, 15,
                new CheckboxOptions { Checked = true });
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Forms/checkbox-checked");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/FT /Btn", text);
            Assert.Contains("/AS /Yes", text);
            Assert.Contains("/V /Yes", text);
        }

        [Fact]
        public void Checkbox_Unchecked_Structure()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: unchecked checkbox has /AS /Off and /V /Off");
            page.AddCheckbox("agree", 50, 50, 15,
                new CheckboxOptions { Checked = false });
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/AS /Off", text);
            Assert.Contains("/V /Off", text);
        }

        [Fact]
        public void Checkbox_CustomExportValue()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: checkbox with custom export value");
            page.AddCheckbox("agree", 50, 50, 15,
                new CheckboxOptions { Checked = true, ExportValue = "Accepted" });
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Accepted", text);
        }

        // ── Radio Buttons ──

        [Fact]
        public void RadioGroup_Structure()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: radio group has parent /FT /Btn with /Kids");
            var group = doc.CreateRadioGroup("color",
                new RadioGroupOptions { SelectedValue = "red" });
            page.AddRadioButton(group, "red", 50, 50, 15);
            page.AddRadioButton(group, "blue", 50, 70, 15);
            page.AddRadioButton(group, "green", 50, 90, 15);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Forms/radio-group");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/FT /Btn", text);
            Assert.Contains("/T (color)", text);
            Assert.Contains("/V /red", text);
            Assert.Contains("/Kids", text);
        }

        [Fact]
        public void RadioGroup_Renders()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: radio buttons render visible pixels");
            var group = doc.CreateRadioGroup("size",
                new RadioGroupOptions { SelectedValue = "medium" });
            page.AddRadioButton(group, "small", 50, 50, 12);
            page.AddRadioButton(group, "medium", 50, 70, 12);
            page.AddRadioButton(group, "large", 50, 90, 12);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Forms/radio-render");

            var bitmap = TestHelper.RasterizePage(bytes, "Forms/radio-render",
                withAnnotations: true, withFormFill: true);
            int px = TestHelper.PtToPx(50);
            int py = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                px, px + TestHelper.PtToPx(12), py, py + TestHelper.PtToPx(12)));
        }

        // ── Dropdown ──

        [Fact]
        public void Dropdown_Structure()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: dropdown has /FT /Ch with /Opt and /V");
            page.AddDropdown("country", 50, 50, 200, 25,
                new[] { "USA", "Canada", "Mexico" },
                new DropdownOptions { SelectedValue = "USA" });
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Forms/dropdown-basic");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/FT /Ch", text);
            Assert.Contains("/Opt", text);
            Assert.Contains("/V (USA)", text);
            // Combo bit should be set
            Assert.Contains("/Ff", text);
        }

        [Fact]
        public void Dropdown_Editable_SetsFlag()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: editable dropdown sets Combo + Edit flags");
            page.AddDropdown("editable", 50, 50, 200, 25,
                new[] { "A", "B" },
                new DropdownOptions { Editable = true });
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            // Combo (131072) + Edit (262144) = 393216
            Assert.Contains("/Ff 393216", text);
        }

        // ── Listbox ──

        [Fact]
        public void Listbox_Structure()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: listbox has /FT /Ch with /Opt array");
            page.AddListbox("skills", 50, 50, 200, 100,
                new[] { "C#", "Python", "Java", "Go" },
                new ListboxOptions { SelectedValues = new[] { "C#" } });
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Forms/listbox-basic");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/FT /Ch", text);
            Assert.Contains("/Opt", text);
        }

        [Fact]
        public void Listbox_MultiSelect_SetsFlag()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: multi-select listbox sets /Ff 2097152");
            page.AddListbox("multi", 50, 50, 200, 100,
                new[] { "A", "B", "C" },
                new ListboxOptions { MultiSelect = true });
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Ff 2097152", text);
        }

        // ── Push Button ──

        [Fact]
        public void Button_Structure()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: push button has /FT /Btn with pushbutton flag");
            page.AddButton("submit", "Submit Form", 50, 50, 120, 30);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Forms/button-basic");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/FT /Btn", text);
            Assert.Contains("/T (submit)", text);
            Assert.Contains("/Ff 65536", text); // Pushbutton bit
        }

        // ── AcroForm Structure ──

        [Fact]
        public void AcroForm_HasFieldsAndDR()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: AcroForm dict has /Fields, /DR, and /DA");
            page.AddTextField("f1", 50, 50, 200, 25);
            page.AddCheckbox("f2", 50, 100, 15);
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/AcroForm", text);
            Assert.Contains("/Fields [", text);
            Assert.Contains("/DR", text);
            Assert.Contains("/DA", text);
        }

        [Fact]
        public void FormFields_WithExistingAnnotations_BothPresent()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: form fields coexist with text annotations");
            page.AddTextAnnotation(10, 10, "A note");
            page.AddTextField("name", 50, 50, 200, 25);
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Subtype /Text", text);  // annotation
            Assert.Contains("/FT /Tx", text);           // form field
            Assert.Contains("/AcroForm", text);
        }

        [Fact]
        public void AllFieldTypes_SingleDocument()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: all 6 field types in one document");

            page.AddTextField("name", 50, 50, 200, 25,
                new TextFieldOptions { Value = "John Doe" });
            page.AddCheckbox("agree", 50, 100, 15,
                new CheckboxOptions { Checked = true });

            var group = doc.CreateRadioGroup("size",
                new RadioGroupOptions { SelectedValue = "M" });
            page.AddRadioButton(group, "S", 50, 130, 12);
            page.AddRadioButton(group, "M", 80, 130, 12);
            page.AddRadioButton(group, "L", 110, 130, 12);

            page.AddDropdown("country", 50, 160, 200, 25,
                new[] { "USA", "Canada" },
                new DropdownOptions { SelectedValue = "USA" });
            page.AddListbox("skills", 50, 200, 200, 80,
                new[] { "C#", "Python", "Java" });
            page.AddButton("submit", "Submit", 50, 300, 100, 30);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Forms/all-field-types");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/FT /Tx", text);
            Assert.Contains("/FT /Btn", text);
            Assert.Contains("/FT /Ch", text);
            Assert.Contains("/AcroForm", text);

            // Should render without error
            var bitmap = TestHelper.RasterizePage(bytes, "Forms/all-field-types");
            Assert.True(bitmap.Width > 0);
        }

        [Fact]
        public void FormFields_WithEncryption_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "",
                OwnerPassword = "owner",
                Level = PdfEncryptionLevel.Aes128
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: form fields work with AES-128 encryption");
            page.AddTextField("name", 50, 50, 200, 25,
                new TextFieldOptions { Value = "Encrypted" });
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Forms/textfield-encrypted");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/AcroForm", text);
            Assert.Contains("/Encrypt", text);
        }

        [Fact]
        public void FormXObject_HasCorrectStructure()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: Form XObject has /Type /XObject /Subtype /Form");
            page.AddCheckbox("check", 50, 50, 15,
                new CheckboxOptions { Checked = true });
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Type /XObject", text);
            Assert.Contains("/Subtype /Form", text);
            Assert.Contains("/BBox", text);
        }
    }
}
