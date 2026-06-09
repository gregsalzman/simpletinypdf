using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Configuration for digitally signing a PDF document.
    /// </summary>
    public sealed class PdfSignatureOptions
    {
        /// <summary>
        /// Certificate with private key for signing. Required.
        /// When using <see cref="CustomSigner"/>, the certificate is still needed
        /// for embedding the public key in the PKCS#7 structure, but its private key is not used.
        /// </summary>
        public X509Certificate2 Certificate { get; set; }

        /// <summary>
        /// Alternative to <see cref="Certificate"/>: path to a PKCS#12 (.pfx/.p12) file.
        /// </summary>
        public string CertificatePath { get; set; }

        /// <summary>
        /// Password for the PKCS#12 file specified by <see cref="CertificatePath"/>.
        /// </summary>
        public string CertificatePassword { get; set; }

        /// <summary>
        /// Optional external signing function for HSM, smart card, or cloud KMS scenarios.
        /// Receives the DER-encoded authenticated attributes to sign (already hashed internally),
        /// and must return the PKCS#1 v1.5 RSA signature bytes.
        /// When set, the certificate's private key is not used.
        /// </summary>
        public Func<byte[], byte[]> CustomSigner { get; set; }

        /// <summary>Reason for signing the document.</summary>
        public string Reason { get; set; }

        /// <summary>Location where the document was signed.</summary>
        public string Location { get; set; }

        /// <summary>Contact information of the signer.</summary>
        public string ContactInfo { get; set; }

        /// <summary>Hash algorithm. Defaults to SHA-256.</summary>
        public HashAlgorithmName HashAlgorithm { get; set; } = HashAlgorithmName.SHA256;

        /// <summary>
        /// Page for visible signature placement.
        /// When null, the signature is invisible (no visual appearance).
        /// </summary>
        public PdfPage Page { get; set; }

        /// <summary>X position of the visible signature rectangle (in page coordinates).</summary>
        public float X { get; set; }

        /// <summary>Y position of the visible signature rectangle (in page coordinates).</summary>
        public float Y { get; set; }

        /// <summary>Width of the visible signature rectangle. Defaults to 150.</summary>
        public float Width { get; set; } = 150;

        /// <summary>Height of the visible signature rectangle. Defaults to 50.</summary>
        public float Height { get; set; } = 50;

        /// <summary>
        /// Additional certificates to include in the PKCS#7 structure (e.g., intermediate CA certs).
        /// </summary>
        public X509Certificate2[] AdditionalCertificates { get; set; }

        /// <summary>
        /// Well-known RFC 3161 timestamp server. Defaults to <see cref="SimpleTinyPDF.TimestampServer.None"/>.
        /// When set, the signature includes a trusted timestamp proving when the document was signed.
        /// </summary>
        public TimestampServer TimestampServer { get; set; }

        /// <summary>
        /// Custom RFC 3161 timestamp server URL. Used when <see cref="TimestampServer"/> is <see cref="SimpleTinyPDF.TimestampServer.None"/>.
        /// </summary>
        public string TimestampServerUrl { get; set; }
    }
}
