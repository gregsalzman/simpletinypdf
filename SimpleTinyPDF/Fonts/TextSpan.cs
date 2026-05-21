using System;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Represents a segment of text with its own font, size, and color.
    /// Used with DrawRichText and DrawRichTextBox for mixed-format text rendering.
    /// </summary>
    public class TextSpan
    {
        /// <summary>The text content of this span.</summary>
        public string Text { get; }

        /// <summary>The font for this span.</summary>
        public PdfFontSource Font { get; }

        /// <summary>The font size in points for this span.</summary>
        public float FontSize { get; }

        /// <summary>The fill color for this span.</summary>
        public PdfColor Color { get; }

        /// <summary>Whether this span is underlined.</summary>
        public bool Underline { get; }

        /// <summary>Opacity for this span (0.0 = fully transparent, 1.0 = fully opaque).</summary>
        public float Opacity { get; }

        /// <summary>A URI hyperlink. When set, the text region becomes a clickable link.</summary>
        public string Link { get; }

        /// <summary>
        /// Creates a new text span with the specified formatting.
        /// </summary>
        public TextSpan(string text, PdfFontSource font = null,
            float fontSize = 12f, PdfColor? color = null, bool underline = false,
            float opacity = 1f, string link = null)
        {
            Text = text ?? string.Empty;
            Font = font ?? (PdfFontSource)PdfFont.Helvetica;
            FontSize = fontSize;
            Color = color ?? PdfColor.Black;
            Underline = underline;
            Opacity = Math.Max(0f, Math.Min(1f, opacity));
            Link = link;
        }
    }
}
