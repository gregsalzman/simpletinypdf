namespace SimpleTinyPDF
{
    internal enum AnnotationKind
    {
        Link,
        Text,
        Markup,
        Stamp,
        InternalLink
    }

    internal struct PageAnnotation
    {
        // Common
        internal AnnotationKind Kind;
        internal float X0, Y0, X1, Y1;

        // Link
        internal string Url;

        // Text (sticky note)
        internal string Contents;
        internal string Title;
        internal TextAnnotationIcon Icon;
        internal PdfColor? Color;
        internal bool Open;

        // Markup
        internal MarkupAnnotationType MarkupType;
        internal float[] QuadPoints;

        // Stamp
        internal StampType Stamp;

        // Internal link (GoTo)
        internal PdfPage TargetPage;
        internal float? TargetY;
    }
}
