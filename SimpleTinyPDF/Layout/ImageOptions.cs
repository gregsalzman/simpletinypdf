namespace SimpleTinyPDF
{
    /// <summary>
    /// Layout options for an image in a layout document.
    /// </summary>
    public class ImageOptions
    {
        /// <summary>Display width in points. If null, scales proportionally from Height or fits content width.</summary>
        public float? Width { get; set; }

        /// <summary>Display height in points. If null, calculated from Width and aspect ratio.</summary>
        public float? Height { get; set; }

        /// <summary>Space before the image in points.</summary>
        public float SpaceBefore { get; set; }

        /// <summary>Space after the image in points.</summary>
        public float SpaceAfter { get; set; }

        /// <summary>Horizontal alignment of the image. Default is Left.</summary>
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;

        /// <summary>Image opacity (0.0 to 1.0). Default is 1.0.</summary>
        public float Opacity { get; set; } = 1f;

        /// <summary>How the image is scaled within its bounds. Default is Fit.</summary>
        public ImageScaleMode ScaleMode { get; set; } = ImageScaleMode.Fit;
    }
}
