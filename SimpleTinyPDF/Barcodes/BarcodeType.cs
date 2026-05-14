namespace SimpleTinyPDF
{
    /// <summary>
    /// Identifies the barcode symbology to encode.
    /// </summary>
    public enum BarcodeType
    {
        /// <summary>Code 128 – variable-length, high-density barcode supporting all 128 ASCII characters.</summary>
        Code128,

        /// <summary>Code 39 – alphanumeric barcode (uppercase A-Z, 0-9, and symbols - . $ / + % SPACE).</summary>
        Code39,

        /// <summary>EAN-13 – 13-digit numeric barcode used in international retail.</summary>
        Ean13,

        /// <summary>UPC-A – 12-digit numeric barcode used in North American retail.</summary>
        UpcA,

        /// <summary>QR Code – 2D matrix barcode capable of encoding text, URLs, and binary data.</summary>
        QrCode
    }
}
