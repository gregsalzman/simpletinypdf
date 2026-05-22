namespace SimpleTinyPDF
{
    /// <summary>
    /// Specifies the encryption algorithm strength for PDF document protection.
    /// </summary>
    public enum PdfEncryptionLevel
    {
        /// <summary>AES-128 encryption (PDF 1.6, V4/R4). Widely compatible.</summary>
        Aes128,

        /// <summary>AES-256 encryption (PDF 2.0, V5/R6). Strongest protection.</summary>
        Aes256
    }
}
