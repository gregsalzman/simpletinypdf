using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Orchestrates the two-pass PDF signing process.
    /// </summary>
    internal static class PdfSigner
    {
        // Maximum PKCS#7 signature size in bytes (8192 bytes = 16384 hex chars).
        // Typical RSA-2048 signatures are ~1500-2000 bytes; this allows room for
        // larger keys and certificate chains.
        internal const int MaxSignatureBytes = 8192;
        internal const int MaxSignatureHexChars = MaxSignatureBytes * 2;

        /// <summary>
        /// Resolves the signing certificate from options (either direct or from PFX file).
        /// </summary>
        internal static X509Certificate2 ResolveCertificate(PdfSignatureOptions options)
        {
            if (options.Certificate != null)
                return options.Certificate;

            if (!string.IsNullOrEmpty(options.CertificatePath))
                return new X509Certificate2(options.CertificatePath, options.CertificatePassword);

            throw new InvalidOperationException(
                "No certificate specified. Set either Certificate or CertificatePath.");
        }

        /// <summary>
        /// Applies the digital signature to the PDF byte array that was written with placeholders.
        /// </summary>
        /// <param name="pdfBytes">Mutable PDF byte array with ByteRange and Contents placeholders.</param>
        /// <param name="contentsValueStart">Byte offset of the '&lt;' character starting the hex Contents value.</param>
        /// <param name="contentsValueEnd">Byte offset after the '&gt;' character ending the hex Contents value.</param>
        /// <param name="byteRangeValueStart">Byte offset where the ByteRange array value starts (the '[').</param>
        /// <param name="byteRangeValueEnd">Byte offset after the ByteRange value ends (after the ']').</param>
        /// <param name="options">Signature configuration.</param>
        internal static void ApplySignature(
            byte[] pdfBytes,
            long contentsValueStart,
            long contentsValueEnd,
            long byteRangeValueStart,
            long byteRangeValueEnd,
            PdfSignatureOptions options)
        {
            // Step 1: Calculate actual ByteRange
            // ByteRange = [0, beforeContents, afterContentsStart, afterContentsLen]
            // "beforeContents" = offset of '<' in Contents
            // "afterContentsStart" = offset after '>' in Contents
            long beforeContentsLen = contentsValueStart;
            long afterContentsStart = contentsValueEnd;
            long afterContentsLen = pdfBytes.Length - contentsValueEnd;

            // Step 2: Patch ByteRange value in the byte array
            // Format: [0 NNNNNNNNNN NNNNNNNNNN NNNNNNNNNN]
            // Padded to fill the exact placeholder space
            string byteRangeStr = string.Format("[0 {0} {1} {2}]",
                beforeContentsLen.ToString().PadRight(10),
                afterContentsStart.ToString().PadRight(10),
                afterContentsLen.ToString().PadRight(10));

            var brBytes = Encoding.ASCII.GetBytes(byteRangeStr);
            int brPlaceholderLen = (int)(byteRangeValueEnd - byteRangeValueStart);
            // Pad with spaces if needed
            for (int i = 0; i < brPlaceholderLen; i++)
                pdfBytes[byteRangeValueStart + i] = i < brBytes.Length ? brBytes[i] : (byte)' ';

            // Step 3: Compute hash over the ByteRange regions
            byte[] digest;
            using (var hash = IncrementalHash.CreateHash(options.HashAlgorithm))
            {
                hash.AppendData(pdfBytes, 0, (int)beforeContentsLen);
                hash.AppendData(pdfBytes, (int)afterContentsStart, (int)afterContentsLen);
                digest = hash.GetHashAndReset();
            }

            // Step 4: Build PKCS#7 SignedData (with optional RFC 3161 timestamp)
            var cert = ResolveCertificate(options);
            var signingTime = DateTime.UtcNow;
            var tsaUrl = Rfc3161Client.ResolveUrl(options.TimestampServer, options.TimestampServerUrl);

            byte[] pkcs7;
            if (tsaUrl != null)
            {
                // Two-phase: build PKCS#7 to get signature, timestamp it, rebuild with token
                var pkcs7Initial = Pkcs7Builder.BuildSignedData(
                    digest, cert, options.AdditionalCertificates,
                    options.HashAlgorithm, signingTime, options.CustomSigner);

                var signatureValue = ExtractSignatureValue(pkcs7Initial);
                var timestampToken = Rfc3161Client.GetTimestamp(
                    signatureValue, options.HashAlgorithm, tsaUrl);

                pkcs7 = Pkcs7Builder.BuildSignedData(
                    digest, cert, options.AdditionalCertificates,
                    options.HashAlgorithm, signingTime, options.CustomSigner,
                    timestampToken, signatureValue);
            }
            else
            {
                pkcs7 = Pkcs7Builder.BuildSignedData(
                    digest, cert, options.AdditionalCertificates,
                    options.HashAlgorithm, signingTime, options.CustomSigner);
            }

            // Step 5: Hex-encode and pad into Contents placeholder
            var hex = BitConverter.ToString(pkcs7).Replace("-", "").ToUpperInvariant();
            if (hex.Length > MaxSignatureHexChars)
                throw new InvalidOperationException(
                    $"PKCS#7 signature ({hex.Length / 2} bytes) exceeds maximum size ({MaxSignatureBytes} bytes).");

            // Write hex chars between '<' and '>'
            // contentsValueStart points to '<', so hex goes at +1
            long hexStart = contentsValueStart + 1;
            long hexEnd = contentsValueEnd - 1; // before '>'
            int hexSpaceLen = (int)(hexEnd - hexStart);

            var hexBytes = Encoding.ASCII.GetBytes(hex);
            for (int i = 0; i < hexSpaceLen; i++)
                pdfBytes[hexStart + i] = (byte)(i < hexBytes.Length ? hexBytes[i] : (byte)'0');
        }

        /// <summary>
        /// Extracts the encryptedDigest (RSA signature value) from a PKCS#7 ContentInfo DER structure.
        /// Walks: ContentInfo → SignedData → SignerInfos → SignerInfo → encryptedDigest (OCTET STRING).
        /// </summary>
        internal static byte[] ExtractSignatureValue(byte[] pkcs7)
        {
            int pos = 0;
            // ContentInfo SEQUENCE
            DerEncoder.ReadTag(pkcs7, ref pos);
            DerEncoder.ReadLength(pkcs7, ref pos);
            // OID (id-signedData)
            DerEncoder.SkipTlv(pkcs7, ref pos);
            // [0] EXPLICIT SignedData
            DerEncoder.ReadTag(pkcs7, ref pos);
            DerEncoder.ReadLength(pkcs7, ref pos);
            // SignedData SEQUENCE
            DerEncoder.ReadTag(pkcs7, ref pos);
            DerEncoder.ReadLength(pkcs7, ref pos);
            // version INTEGER
            DerEncoder.SkipTlv(pkcs7, ref pos);
            // digestAlgorithms SET
            DerEncoder.SkipTlv(pkcs7, ref pos);
            // contentInfo SEQUENCE
            DerEncoder.SkipTlv(pkcs7, ref pos);
            // certificates [0] IMPLICIT
            if (pos < pkcs7.Length && pkcs7[pos] == 0xA0)
                DerEncoder.SkipTlv(pkcs7, ref pos);
            // crls [1] IMPLICIT (optional, skip if present)
            if (pos < pkcs7.Length && pkcs7[pos] == 0xA1)
                DerEncoder.SkipTlv(pkcs7, ref pos);
            // signerInfos SET
            DerEncoder.ReadTag(pkcs7, ref pos); // SET tag
            DerEncoder.ReadLength(pkcs7, ref pos);
            // SignerInfo SEQUENCE
            DerEncoder.ReadTag(pkcs7, ref pos); // SEQUENCE tag
            DerEncoder.ReadLength(pkcs7, ref pos);
            // version INTEGER
            DerEncoder.SkipTlv(pkcs7, ref pos);
            // issuerAndSerialNumber SEQUENCE
            DerEncoder.SkipTlv(pkcs7, ref pos);
            // digestAlgorithm SEQUENCE
            DerEncoder.SkipTlv(pkcs7, ref pos);
            // authenticatedAttributes [0] IMPLICIT (optional)
            if (pos < pkcs7.Length && pkcs7[pos] == 0xA0)
                DerEncoder.SkipTlv(pkcs7, ref pos);
            // digestEncryptionAlgorithm SEQUENCE
            DerEncoder.SkipTlv(pkcs7, ref pos);
            // encryptedDigest OCTET STRING — this is what we want
            return DerEncoder.ReadContents(pkcs7, ref pos, DerEncoder.TagOctetString);
        }
    }
}
