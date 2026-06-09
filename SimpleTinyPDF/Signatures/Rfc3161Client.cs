using System;
using System.Net.Http;
using System.Security.Cryptography;

namespace SimpleTinyPDF
{
    /// <summary>
    /// RFC 3161 Time-Stamp Protocol client for obtaining trusted timestamps.
    /// </summary>
    internal static class Rfc3161Client
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// Resolves the TSA URL from enum or custom URL.
        /// Returns null if no timestamping is configured.
        /// </summary>
        internal static string ResolveUrl(TimestampServer server, string customUrl)
        {
            switch (server)
            {
                case TimestampServer.DigiCert:
                    return "http://timestamp.digicert.com";
                case TimestampServer.Sectigo:
                    return "http://timestamp.sectigo.com";
                case TimestampServer.FreeTSA:
                    return "https://freetsa.org/tsr";
                default:
                    return string.IsNullOrEmpty(customUrl) ? null : customUrl;
            }
        }

        /// <summary>
        /// Obtains an RFC 3161 timestamp token for the given signature bytes.
        /// </summary>
        /// <param name="signatureBytes">The PKCS#1 signature value to timestamp.</param>
        /// <param name="hashAlgorithm">Hash algorithm for the message imprint.</param>
        /// <param name="tsaUrl">TSA server URL.</param>
        /// <returns>DER-encoded TimeStampToken (CMS SignedData) to embed as unsigned attribute.</returns>
        internal static byte[] GetTimestamp(byte[] signatureBytes, HashAlgorithmName hashAlgorithm, string tsaUrl)
        {
            // Hash the signature value for the message imprint
            byte[] digest;
            using (var hash = HashAlgorithm.Create(hashAlgorithm.Name))
                digest = hash.ComputeHash(signatureBytes);

            // Build and send TSA request
            var request = BuildTimeStampReq(digest, hashAlgorithm);
            var response = SendRequest(request, tsaUrl);
            return ParseTimeStampResp(response);
        }

        /// <summary>
        /// Builds an RFC 3161 TimeStampReq DER structure.
        /// </summary>
        internal static byte[] BuildTimeStampReq(byte[] digest, HashAlgorithmName hashAlgorithm)
        {
            string hashOid = GetHashOid(hashAlgorithm);

            // MessageImprint ::= SEQUENCE { hashAlgorithm, hashedMessage }
            var messageImprint = DerEncoder.Sequence(
                DerEncoder.Sequence(
                    DerEncoder.ObjectIdentifier(hashOid),
                    DerEncoder.Null()),
                DerEncoder.OctetString(digest));

            // TimeStampReq ::= SEQUENCE { version, messageImprint, certReq }
            return DerEncoder.Sequence(
                DerEncoder.IntegerFromInt(1),     // version
                messageImprint,
                DerEncoder.Boolean(true)           // certReq — ask TSA to include its cert
            );
        }

        /// <summary>
        /// Parses an RFC 3161 TimeStampResp, validates status, and extracts the TimeStampToken.
        /// </summary>
        internal static byte[] ParseTimeStampResp(byte[] response)
        {
            int pos = 0;

            // Outer SEQUENCE (TimeStampResp)
            byte outerTag = DerEncoder.ReadTag(response, ref pos);
            if (outerTag != DerEncoder.TagSequence)
                throw new InvalidOperationException(
                    $"Invalid TSA response: expected SEQUENCE but got 0x{outerTag:X2}.");
            int outerLen = DerEncoder.ReadLength(response, ref pos);
            int outerEnd = pos + outerLen;

            // PKIStatusInfo SEQUENCE
            byte statusSeqTag = DerEncoder.ReadTag(response, ref pos);
            if (statusSeqTag != DerEncoder.TagSequence)
                throw new InvalidOperationException("Invalid PKIStatusInfo in TSA response.");
            int statusSeqLen = DerEncoder.ReadLength(response, ref pos);
            int statusSeqEnd = pos + statusSeqLen;

            // PKIStatus INTEGER
            int status = DerEncoder.ReadIntegerValue(response, ref pos);
            if (status != 0 && status != 1) // 0 = granted, 1 = grantedWithMods
                throw new InvalidOperationException(
                    $"TSA request rejected with status {status}.");

            // Skip remaining PKIStatusInfo fields (statusString, failInfo)
            pos = statusSeqEnd;

            // TimeStampToken is the remaining data in the outer SEQUENCE
            if (pos >= outerEnd)
                throw new InvalidOperationException("TSA response contains no TimeStampToken.");

            int tokenLen = outerEnd - pos;
            var token = new byte[tokenLen];
            Buffer.BlockCopy(response, pos, token, 0, tokenLen);
            return token;
        }

        private static byte[] SendRequest(byte[] tsaRequest, string url)
        {
            var content = new ByteArrayContent(tsaRequest);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/timestamp-query");

            var httpResponse = Http.PostAsync(url, content).GetAwaiter().GetResult();
            httpResponse.EnsureSuccessStatusCode();
            return httpResponse.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        }

        private static string GetHashOid(HashAlgorithmName alg)
        {
            if (alg == HashAlgorithmName.SHA256) return "2.16.840.1.101.3.4.2.1";
            if (alg == HashAlgorithmName.SHA384) return "2.16.840.1.101.3.4.2.2";
            if (alg == HashAlgorithmName.SHA512) return "2.16.840.1.101.3.4.2.3";
            throw new ArgumentException($"Unsupported hash algorithm: {alg.Name}", nameof(alg));
        }
    }
}
