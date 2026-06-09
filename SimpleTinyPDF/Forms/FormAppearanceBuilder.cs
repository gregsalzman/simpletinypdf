using System;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Generates PDF content streams for form field appearance Form XObjects.
    /// </summary>
    internal static class FormAppearanceBuilder
    {
        internal static byte[] BuildTextFieldAppearance(FormField field)
        {
            var sb = new StringBuilder();
            float w = field.Width, h = field.Height;
            float fontSize = field.FontSize;

            // Match iText structure: /Tx BMC, clip, text, EMC
            sb.Append("/Tx BMC\n");
            sb.Append("q\n");
            sb.Append($"0 0 {F(w)} {F(h)} re\nW\nn\n");

            if (!string.IsNullOrEmpty(field.Value))
            {
                sb.Append("q\n");
                sb.Append("BT\n");
                sb.Append($"/F1 {F(fontSize)} Tf\n");
                // iText positions at x=2, y ≈ height * 0.35 for 25pt height / 12pt font
                float textY = (h - fontSize * 0.705f) / 2;
                sb.Append($"2 {F(textY)} Td\n");
                string displayText = field.Password
                    ? new string('*', field.Value.Length)
                    : field.Value;
                sb.Append($"({EscapePdfString(displayText)}) Tj\n");
                sb.Append("ET\n");
                sb.Append("Q\n");
            }

            sb.Append("Q\n");
            sb.Append("EMC\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        internal static byte[] BuildCheckboxAppearance(bool isChecked, float size,
            PdfColor? borderColor, PdfColor? backgroundColor, PdfColor? checkColor, float borderWidth)
        {
            var sb = new StringBuilder();
            float bw = borderWidth;

            // Background
            if (backgroundColor.HasValue)
            {
                AppendFillColor(sb, backgroundColor.Value);
                sb.Append($"0 0 {F(size)} {F(size)} re f\n");
            }

            // Border
            AppendStrokeColor(sb, borderColor ?? PdfColor.Rgb(0f, 0f, 0f));
            sb.Append($"{F(bw)} w\n");
            float hb = bw / 2;
            sb.Append($"{F(hb)} {F(hb)} {F(size - bw)} {F(size - bw)} re S\n");

            // Checkmark
            if (isChecked)
            {
                AppendStrokeColor(sb, checkColor ?? PdfColor.Rgb(0f, 0f, 0f));
                float m = size * 0.2f;
                float lw = Math.Max(1.5f, size * 0.1f);
                sb.Append($"{F(lw)} w\n");
                sb.Append("1 J\n"); // round line cap
                // Checkmark path: short leg then long leg
                sb.Append($"{F(m)} {F(size * 0.5f)} m\n");
                sb.Append($"{F(size * 0.4f)} {F(m)} l\n");
                sb.Append($"{F(size - m)} {F(size - m)} l\n");
                sb.Append("S\n");
            }

            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        internal static byte[] BuildRadioButtonAppearance(bool isSelected, float size,
            PdfColor? borderColor, PdfColor? backgroundColor, PdfColor? dotColor, float borderWidth)
        {
            var sb = new StringBuilder();
            float r = size / 2;
            float cx = r, cy = r;
            float bw = borderWidth;

            // Background circle
            if (backgroundColor.HasValue)
            {
                AppendFillColor(sb, backgroundColor.Value);
                AppendCircle(sb, cx, cy, r);
                sb.Append("f\n");
            }

            // Border circle
            AppendStrokeColor(sb, borderColor ?? PdfColor.Rgb(0f, 0f, 0f));
            sb.Append($"{F(bw)} w\n");
            AppendCircle(sb, cx, cy, r - bw / 2);
            sb.Append("S\n");

            // Filled dot
            if (isSelected)
            {
                AppendFillColor(sb, dotColor ?? PdfColor.Rgb(0f, 0f, 0f));
                float dotR = r * 0.4f;
                AppendCircle(sb, cx, cy, dotR);
                sb.Append("f\n");
            }

            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        internal static byte[] BuildDropdownAppearance(FormField field)
        {
            var sb = new StringBuilder();
            float w = field.Width, h = field.Height;
            float fontSize = field.FontSize;

            // Match iText structure: /Tx BMC, clip, text, EMC
            sb.Append("/Tx BMC\n");
            sb.Append("q\n");
            sb.Append($"0 0 {F(w)} {F(h)} re\nW\nn\n");

            if (!string.IsNullOrEmpty(field.SelectedValue))
            {
                sb.Append("q\n");
                sb.Append("BT\n");
                sb.Append($"/F1 {F(fontSize)} Tf\n");
                float textY = (h - fontSize * 0.705f) / 2;
                sb.Append($"1.5 {F(textY)} Td\n");
                sb.Append($"({EscapePdfString(field.SelectedValue)}) Tj\n");
                sb.Append("ET\n");
                sb.Append("Q\n");
            }

            sb.Append("Q\n");
            sb.Append("EMC\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        internal static byte[] BuildListboxAppearance(FormField field)
        {
            var sb = new StringBuilder();
            float w = field.Width, h = field.Height;
            float fontSize = field.FontSize;
            float lineHeight = fontSize * 1.11f; // iText uses ~13.32 for 12pt

            // Match iText structure: /Tx BMC, wide clip, items, EMC
            sb.Append("/Tx BMC\n");
            sb.Append("q\n");
            // iText uses a very wide clip rect to avoid horizontal clipping
            sb.Append($"-500000 0 1000000 {F(h)} re\nW\nn\n");

            if (field.Items != null && field.Items.Length > 0)
            {
                float y = h - lineHeight + (lineHeight - fontSize) / 2;

                for (int i = 0; i < field.Items.Length; i++)
                {
                    // Highlight selected items (iText uses blue background)
                    if (IsSelected(field, field.Items[i]))
                    {
                        sb.Append("q\n");
                        sb.Append("0.66275 0.8 0.88235 rg\n");
                        sb.Append($"1 {F(y - (lineHeight - fontSize) / 2)} {F(w - 2)} {F(lineHeight)} re f\n");
                        sb.Append("Q\n");
                    }

                    sb.Append("q\n");
                    sb.Append("BT\n");
                    sb.Append($"/F1 {F(fontSize)} Tf\n");
                    sb.Append($"1 {F(y)} Td\n");
                    if (IsSelected(field, field.Items[i]))
                        sb.Append("0 0 0 rg\n");
                    sb.Append($"({EscapePdfString(field.Items[i])}) Tj\n");
                    sb.Append("ET\n");
                    sb.Append("Q\n");
                    y -= lineHeight;
                }
            }

            sb.Append("Q\n");
            sb.Append("EMC\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        internal static byte[] BuildButtonAppearance(FormField field)
        {
            var sb = new StringBuilder();
            float w = field.Width, h = field.Height;
            float bw = field.BorderWidth;

            // Background (default light gray for buttons)
            AppendFillColor(sb, field.BackgroundColor ?? PdfColor.Rgb(220, 220, 220));
            sb.Append($"0 0 {F(w)} {F(h)} re f\n");

            // Border
            AppendStrokeColor(sb, field.BorderColor ?? PdfColor.Rgb(0f, 0f, 0f));
            sb.Append($"{F(bw)} w\n");
            float hb = bw / 2;
            sb.Append($"{F(hb)} {F(hb)} {F(w - bw)} {F(h - bw)} re S\n");

            // Centered label
            if (!string.IsNullOrEmpty(field.Label))
            {
                sb.Append("BT\n");
                AppendFillColor(sb, field.TextColor ?? PdfColor.Rgb(0f, 0f, 0f));
                sb.Append($"/F1 {F(field.FontSize)} Tf\n");
                float textWidth = EstimateTextWidth(field.Label, field.FontSize);
                float textX = (w - textWidth) / 2;
                float textY = (h - field.FontSize) / 2 + field.FontSize * 0.2f;
                sb.Append($"{F(textX)} {F(textY)} Td\n");
                sb.Append($"({EscapePdfString(field.Label)}) Tj\n");
                sb.Append("ET\n");
            }

            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        internal static byte[] BuildSignatureAppearance(float width, float height,
            string signerName, string reason, string location, DateTime signingDate)
        {
            var sb = new StringBuilder();
            float fontSize = Math.Min(10f, height / 5);
            float margin = 4;
            float lineHeight = fontSize * 1.3f;

            // Light background
            sb.Append("0.95 0.95 0.95 rg\n");
            sb.Append($"0 0 {F(width)} {F(height)} re f\n");

            // Border
            sb.Append("0 0 0 RG\n0.5 w\n");
            sb.Append($"0.25 0.25 {F(width - 0.5f)} {F(height - 0.5f)} re S\n");

            // Text content
            sb.Append("BT\n0 0 0 rg\n");
            sb.Append($"/F1 {F(fontSize)} Tf\n");

            float y = height - margin - fontSize;
            if (!string.IsNullOrEmpty(signerName))
            {
                sb.Append($"{F(margin)} {F(y)} Td\n");
                sb.Append($"(Signed by: {EscapePdfString(signerName)}) Tj\n");
                y -= lineHeight;
            }

            sb.Append($"{F(margin)} {F(y)} Td\n");
            sb.Append($"(Date: {EscapePdfString(signingDate.ToString("yyyy-MM-dd HH:mm:ss"))}) Tj\n");
            y -= lineHeight;

            if (!string.IsNullOrEmpty(reason))
            {
                sb.Append($"{F(margin)} {F(y)} Td\n");
                sb.Append($"(Reason: {EscapePdfString(reason)}) Tj\n");
                y -= lineHeight;
            }

            if (!string.IsNullOrEmpty(location))
            {
                sb.Append($"{F(margin)} {F(y)} Td\n");
                sb.Append($"(Location: {EscapePdfString(location)}) Tj\n");
            }

            sb.Append("ET\n");

            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        // ── Helpers ──

        private static bool IsSelected(FormField field, string item)
        {
            if (field.SelectedValue != null && field.SelectedValue == item) return true;
            if (field.SelectedValues != null)
            {
                for (int i = 0; i < field.SelectedValues.Length; i++)
                    if (field.SelectedValues[i] == item) return true;
            }
            return false;
        }

        private static void AppendCircle(StringBuilder sb, float cx, float cy, float r)
        {
            // Approximate circle with 4 Bézier curves (kappa = 0.5523)
            float k = r * 0.5523f;
            sb.Append($"{F(cx)} {F(cy + r)} m\n");
            sb.Append($"{F(cx + k)} {F(cy + r)} {F(cx + r)} {F(cy + k)} {F(cx + r)} {F(cy)} c\n");
            sb.Append($"{F(cx + r)} {F(cy - k)} {F(cx + k)} {F(cy - r)} {F(cx)} {F(cy - r)} c\n");
            sb.Append($"{F(cx - k)} {F(cy - r)} {F(cx - r)} {F(cy - k)} {F(cx - r)} {F(cy)} c\n");
            sb.Append($"{F(cx - r)} {F(cy + k)} {F(cx - k)} {F(cy + r)} {F(cx)} {F(cy + r)} c\n");
        }

        private static void AppendFillColor(StringBuilder sb, PdfColor color)
        {
            sb.Append($"{F(color.R)} {F(color.G)} {F(color.B)} rg\n");
        }

        private static void AppendStrokeColor(StringBuilder sb, PdfColor color)
        {
            sb.Append($"{F(color.R)} {F(color.G)} {F(color.B)} RG\n");
        }

        private static float EstimateTextWidth(string text, float fontSize)
        {
            // Approximate width using average Helvetica character width (~0.5 of font size)
            return text.Length * fontSize * 0.5f;
        }

        private static string EscapePdfString(string text)
        {
            if (text == null) return "";
            return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

        private static string F(float value) => PdfStringHelper.F(value);
    }
}
