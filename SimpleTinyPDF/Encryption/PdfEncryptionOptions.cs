namespace SimpleTinyPDF
{
    /// <summary>
    /// Configures PDF document encryption. Set on <see cref="PdfDocument.Encryption"/>.
    /// </summary>
    public sealed class PdfEncryptionOptions
    {
        /// <summary>
        /// Password required to open the document.
        /// An empty string means no password is needed to open (but restrictions still apply).
        /// </summary>
        public string UserPassword { get; set; } = "";

        /// <summary>
        /// Password required for full owner access (changing permissions, unrestricted printing/copying, etc.).
        /// </summary>
        public string OwnerPassword { get; set; } = "";

        /// <summary>
        /// Encryption algorithm strength. Default: <see cref="PdfEncryptionLevel.Aes128"/>.
        /// </summary>
        public PdfEncryptionLevel Level { get; set; } = PdfEncryptionLevel.Aes128;

        /// <summary>
        /// User access permissions when the document is opened with the user password.
        /// Default: <see cref="PdfPermissions.All"/>.
        /// </summary>
        public PdfPermissions Permissions { get; set; } = PdfPermissions.All;
    }
}
