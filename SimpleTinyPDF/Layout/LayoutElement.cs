namespace SimpleTinyPDF
{
    internal enum LayoutElementType
    {
        Paragraph,
        RichParagraph,
        Image,
        Table,
        List,
        PageBreak
    }

    internal class LayoutElement
    {
        internal LayoutElementType Type { get; private set; }
        internal string Text { get; private set; }
        internal TextSpan[] Spans { get; private set; }
        internal ParagraphOptions ParagraphOptions { get; private set; }
        internal PdfImage Image { get; private set; }
        internal ImageOptions ImageOptions { get; private set; }
        internal PdfTable Table { get; private set; }
        internal ListItem[] ListItems { get; private set; }
        internal ListStyle ListStyle { get; private set; }

        private LayoutElement() { }

        internal static LayoutElement CreateParagraph(string text, ParagraphOptions options) =>
            new LayoutElement { Type = LayoutElementType.Paragraph, Text = text, ParagraphOptions = options };

        internal static LayoutElement CreateRichParagraph(TextSpan[] spans, ParagraphOptions options) =>
            new LayoutElement { Type = LayoutElementType.RichParagraph, Spans = spans, ParagraphOptions = options };

        internal static LayoutElement CreateImage(PdfImage image, ImageOptions options) =>
            new LayoutElement { Type = LayoutElementType.Image, Image = image, ImageOptions = options };

        internal static LayoutElement CreateTable(PdfTable table) =>
            new LayoutElement { Type = LayoutElementType.Table, Table = table };

        internal static LayoutElement CreateList(ListItem[] items, ListStyle style) =>
            new LayoutElement { Type = LayoutElementType.List, ListItems = items, ListStyle = style };

        internal static LayoutElement CreatePageBreak() =>
            new LayoutElement { Type = LayoutElementType.PageBreak };
    }
}
