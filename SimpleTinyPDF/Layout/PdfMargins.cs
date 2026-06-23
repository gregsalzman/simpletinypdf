namespace SimpleTinyPDF
{
    /// <summary>
    /// Defines page margins in PDF points (72 points = 1 inch).
    /// </summary>
    public class PdfMargins
    {
        /// <summary>Top margin in points.</summary>
        public float Top { get; set; }

        /// <summary>Right margin in points.</summary>
        public float Right { get; set; }

        /// <summary>Bottom margin in points.</summary>
        public float Bottom { get; set; }

        /// <summary>Left margin in points.</summary>
        public float Left { get; set; }

        /// <summary>Creates margins with the same value on all sides.</summary>
        public PdfMargins(float all)
        {
            Top = Right = Bottom = Left = all;
        }

        /// <summary>Creates margins with separate vertical and horizontal values.</summary>
        public PdfMargins(float topBottom, float leftRight)
        {
            Top = Bottom = topBottom;
            Left = Right = leftRight;
        }

        /// <summary>Creates margins with individual values for each side.</summary>
        public PdfMargins(float top, float right, float bottom, float left)
        {
            Top = top;
            Right = right;
            Bottom = bottom;
            Left = left;
        }
    }
}
