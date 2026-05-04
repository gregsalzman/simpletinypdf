namespace SimpleTinyPDF
{
    /// <summary>
    /// Defines page dimensions in PDF points (72 points = 1 inch).
    /// </summary>
    public sealed class PageSize
    {
        /// <summary>Width in points.</summary>
        public float Width { get; }

        /// <summary>Height in points.</summary>
        public float Height { get; }

        /// <summary>Creates a custom page size.</summary>
        public PageSize(float width, float height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>Returns a new PageSize with width and height swapped (landscape orientation).</summary>
        public PageSize Landscape() => new PageSize(Height, Width);

        /// <summary>A4: 595 x 842 points (210 x 297 mm).</summary>
        public static readonly PageSize A4 = new PageSize(595f, 842f);

        /// <summary>A3: 842 x 1191 points (297 x 420 mm).</summary>
        public static readonly PageSize A3 = new PageSize(842f, 1191f);

        /// <summary>A5: 420 x 595 points (148 x 210 mm).</summary>
        public static readonly PageSize A5 = new PageSize(420f, 595f);

        /// <summary>US Letter: 612 x 792 points (8.5 x 11 in).</summary>
        public static readonly PageSize Letter = new PageSize(612f, 792f);

        /// <summary>US Legal: 612 x 1008 points (8.5 x 14 in).</summary>
        public static readonly PageSize Legal = new PageSize(612f, 1008f);
    }
}
