namespace SimpleTinyPDF
{
    /// <summary>
    /// Optional settings that control barcode rendering.
    /// </summary>
    public sealed class BarcodeOptions
    {
        /// <summary>Foreground (bar/module) color. Default: Black.</summary>
        public PdfColor ForegroundColor { get; set; } = PdfColor.Black;

        /// <summary>Background color. Default: White.</summary>
        public PdfColor BackgroundColor { get; set; } = PdfColor.White;

        /// <summary>Whether to draw the background rectangle. Default: true.</summary>
        public bool DrawBackground { get; set; } = true;

        /// <summary>
        /// Whether to include a quiet zone (required white space around the barcode).
        /// Default: true. The quiet zone is included within the specified width/height.
        /// </summary>
        public bool IncludeQuietZone { get; set; } = true;

        /// <summary>Whether to render human-readable text below 1D barcodes. Default: false.</summary>
        public bool ShowText { get; set; }

        /// <summary>Font for the human-readable text. Default: Courier.</summary>
        public PdfFontSource TextFont { get; set; } = PdfFont.Courier;

        /// <summary>Font size for the human-readable text. Default: 8.</summary>
        public float TextFontSize { get; set; } = 8f;

        /// <summary>Error correction level for QR codes. Ignored for 1D barcodes. Default: Medium.</summary>
        public QrErrorCorrection QrErrorCorrectionLevel { get; set; } = QrErrorCorrection.Medium;

        /// <summary>Rotation angle in degrees (clockwise). Default: 0.</summary>
        public float Rotation { get; set; }

        /// <summary>Opacity (0.0 = fully transparent, 1.0 = fully opaque). Default: 1.0.</summary>
        public float Opacity { get; set; } = 1f;
    }
}
