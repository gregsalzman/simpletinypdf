namespace SimpleTinyPDF
{
    /// <summary>
    /// The 14 standard PDF Type 1 fonts, available in every PDF viewer without embedding.
    /// </summary>
    public enum PdfFont
    {
        Helvetica,
        HelveticaBold,
        HelveticaOblique,
        HelveticaBoldOblique,
        TimesRoman,
        TimesBold,
        TimesItalic,
        TimesBoldItalic,
        Courier,
        CourierBold,
        CourierOblique,
        CourierBoldOblique,
        Symbol,
        ZapfDingbats
    }

    /// <summary>Text alignment within a cell or text block.</summary>
    public enum TextAlignment
    {
        Left,
        Center,
        Right,
        Justify
    }

    internal static class PdfFontNames
    {
        internal static string GetPdfName(PdfFont font)
        {
            switch (font)
            {
                case PdfFont.Helvetica: return "Helvetica";
                case PdfFont.HelveticaBold: return "Helvetica-Bold";
                case PdfFont.HelveticaOblique: return "Helvetica-Oblique";
                case PdfFont.HelveticaBoldOblique: return "Helvetica-BoldOblique";
                case PdfFont.TimesRoman: return "Times-Roman";
                case PdfFont.TimesBold: return "Times-Bold";
                case PdfFont.TimesItalic: return "Times-Italic";
                case PdfFont.TimesBoldItalic: return "Times-BoldItalic";
                case PdfFont.Courier: return "Courier";
                case PdfFont.CourierBold: return "Courier-Bold";
                case PdfFont.CourierOblique: return "Courier-Oblique";
                case PdfFont.CourierBoldOblique: return "Courier-BoldOblique";
                case PdfFont.Symbol: return "Symbol";
                case PdfFont.ZapfDingbats: return "ZapfDingbats";
                default: return "Helvetica";
            }
        }
    }
}
