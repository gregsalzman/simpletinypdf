using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Builds PKCS#7 SignedData structures (RFC 2315/5652) for PDF digital signatures.
    /// </summary>
    internal static class Pkcs7Builder
    {
        // Well-known OIDs
        private const string OidData = "1.2.840.113549.1.7.1";
        private const string OidSignedData = "1.2.840.113549.1.7.2";
        private const string OidContentType = "1.2.840.113549.1.9.3";
        private const string OidMessageDigest = "1.2.840.113549.1.9.4";
        private const string OidSigningTime = "1.2.840.113549.1.9.5";
        private const string OidRsaEncryption = "1.2.840.113549.1.1.1";
        private const string OidSha256WithRsa = "1.2.840.113549.1.1.11";
        private const string OidSha384WithRsa = "1.2.840.113549.1.1.12";
        private const string OidSha512WithRsa = "1.2.840.113549.1.1.13";
        private const string OidSha256 = "2.16.840.1.101.3.4.2.1";
        private const string OidSha384 = "2.16.840.1.101.3.4.2.2";
        private const string OidSha512 = "2.16.840.1.101.3.4.2.3";
        private const string OidTimeStampToken = "1.2.840.113549.1.9.16.2.14";

        /// <summary>
        /// Builds a complete PKCS#7 SignedData structure for a PDF detached signature.
        /// </summary>
        internal static byte[] BuildSignedData(
            byte[] messageDigest,
            X509Certificate2 certificate,
            X509Certificate2[] additionalCerts,
            HashAlgorithmName hashAlgorithm,
            DateTime signingTime,
            Func<byte[], byte[]> customSigner,
            byte[] timestampToken = null,
            byte[] precomputedSignature = null)
        {
            string hashOid = GetHashOid(hashAlgorithm);

            // 1. Build authenticated attributes
            var contentTypeAttr = DerEncoder.Sequence(
                DerEncoder.ObjectIdentifier(OidContentType),
                DerEncoder.Set(DerEncoder.ObjectIdentifier(OidData)));

            var signingTimeAttr = DerEncoder.Sequence(
                DerEncoder.ObjectIdentifier(OidSigningTime),
                DerEncoder.Set(DerEncoder.UtcTime(signingTime)));

            var messageDigestAttr = DerEncoder.Sequence(
                DerEncoder.ObjectIdentifier(OidMessageDigest),
                DerEncoder.Set(DerEncoder.OctetString(messageDigest)));

            // Sort attributes by DER encoding (DER SET OF requirement)
            var attrs = new List<byte[]> { contentTypeAttr, signingTimeAttr, messageDigestAttr };
            attrs.Sort(CompareDer);

            var authenticatedAttrsContent = DerEncoder.Concat(attrs.ToArray());

            // 2. Sign the authenticated attributes (or use precomputed signature for timestamp rebuild)
            byte[] signature;
            if (precomputedSignature != null)
            {
                signature = precomputedSignature;
            }
            else
            {
                // For signing, attributes are wrapped as SET (tag 0x31)
                var attrsForSigning = DerEncoder.Set(attrs.ToArray());

                if (customSigner != null)
                {
                    signature = customSigner(attrsForSigning);
                }
                else
                {
                    using (var rsa = certificate.GetRSAPrivateKey())
                    {
                        if (rsa == null)
                            throw new InvalidOperationException(
                                "Certificate does not contain an RSA private key. " +
                                "Provide a certificate with a private key or use CustomSigner.");
                        signature = rsa.SignData(attrsForSigning, hashAlgorithm, RSASignaturePadding.Pkcs1);
                    }
                }
            }

            // 3. Build SignerInfo
            var issuerAndSerial = BuildIssuerAndSerialNumber(certificate);
            var digestAlgId = BuildAlgorithmIdentifier(hashOid);
            var sigEncAlgId = BuildAlgorithmIdentifier(OidRsaEncryption);

            // Authenticated attrs in SignerInfo use context [0] IMPLICIT tag
            var authAttrsTagged = DerEncoder.ContextImplicit(0, authenticatedAttrsContent);

            byte[] signerInfo;
            if (timestampToken != null)
            {
                // Build unauthenticatedAttributes [1] IMPLICIT with timestamp token
                var tsAttr = DerEncoder.Sequence(
                    DerEncoder.ObjectIdentifier(OidTimeStampToken),
                    DerEncoder.Set(new[] { timestampToken }));
                var unauthAttrs = DerEncoder.ContextImplicit(1, tsAttr);

                signerInfo = DerEncoder.Sequence(
                    DerEncoder.IntegerFromInt(1),     // version
                    issuerAndSerial,
                    digestAlgId,
                    authAttrsTagged,                   // authenticatedAttributes [0]
                    sigEncAlgId,
                    DerEncoder.OctetString(signature), // encryptedDigest
                    unauthAttrs                        // unauthenticatedAttributes [1]
                );
            }
            else
            {
                signerInfo = DerEncoder.Sequence(
                    DerEncoder.IntegerFromInt(1),     // version
                    issuerAndSerial,
                    digestAlgId,
                    authAttrsTagged,                   // authenticatedAttributes [0]
                    sigEncAlgId,
                    DerEncoder.OctetString(signature)  // encryptedDigest
                );
            }

            // 4. Build certificates [0] IMPLICIT
            var certBytes = BuildCertificates(certificate, additionalCerts);

            // 5. Build SignedData
            var signedData = DerEncoder.Sequence(
                DerEncoder.IntegerFromInt(1),                      // version
                DerEncoder.Set(digestAlgId),                        // digestAlgorithms
                DerEncoder.Sequence(DerEncoder.ObjectIdentifier(OidData)), // contentInfo (detached)
                DerEncoder.ContextImplicit(0, certBytes),           // certificates [0]
                DerEncoder.Set(signerInfo)                          // signerInfos
            );

            // 6. Wrap in ContentInfo
            return DerEncoder.Sequence(
                DerEncoder.ObjectIdentifier(OidSignedData),
                DerEncoder.ContextExplicit(0, signedData)
            );
        }

        private static byte[] BuildIssuerAndSerialNumber(X509Certificate2 cert)
        {
            // Issuer: DER-encoded Name from certificate
            byte[] issuerDer = cert.IssuerName.RawData;

            // Serial: GetSerialNumber() returns little-endian in .NET, reverse to big-endian
            byte[] serialLE = cert.GetSerialNumber();
            byte[] serialBE = new byte[serialLE.Length];
            for (int i = 0; i < serialLE.Length; i++)
                serialBE[i] = serialLE[serialLE.Length - 1 - i];

            return DerEncoder.Sequence(
                WrapRawDer(issuerDer),      // issuer Name already DER-encoded
                DerEncoder.Integer(serialBE) // serialNumber
            );
        }

        private static byte[] BuildCertificates(X509Certificate2 cert, X509Certificate2[] additionalCerts)
        {
            // Concatenate raw DER certificates
            var parts = new List<byte[]>();
            parts.Add(cert.RawData);
            if (additionalCerts != null)
            {
                for (int i = 0; i < additionalCerts.Length; i++)
                    parts.Add(additionalCerts[i].RawData);
            }
            return DerEncoder.Concat(parts.ToArray());
        }

        private static byte[] BuildAlgorithmIdentifier(string oid)
        {
            return DerEncoder.Sequence(
                DerEncoder.ObjectIdentifier(oid),
                DerEncoder.Null()
            );
        }

        /// <summary>
        /// Wraps already-DER-encoded bytes as-is (no re-encoding).
        /// Used for issuer Name which is already DER from the certificate.
        /// </summary>
        private static byte[] WrapRawDer(byte[] der) => der;

        private static string GetHashOid(HashAlgorithmName alg)
        {
            if (alg == HashAlgorithmName.SHA256) return OidSha256;
            if (alg == HashAlgorithmName.SHA384) return OidSha384;
            if (alg == HashAlgorithmName.SHA512) return OidSha512;
            throw new ArgumentException($"Unsupported hash algorithm: {alg.Name}", nameof(alg));
        }

        private static int CompareDer(byte[] a, byte[] b)
        {
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                if (a[i] != b[i]) return a[i].CompareTo(b[i]);
            }
            return a.Length.CompareTo(b.Length);
        }
    }
}
