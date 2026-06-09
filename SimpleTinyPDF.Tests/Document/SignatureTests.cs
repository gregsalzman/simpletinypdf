using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class SignatureTests
    {
        private static X509Certificate2 CreateTestCertificate()
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(
                    "CN=SimpleTinyPDF Test, O=Test", rsa,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                return request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddMinutes(-5),
                    DateTimeOffset.UtcNow.AddYears(1));
            }
        }

        // ── Structure Tests ──

        [Fact]
        public void InvisibleSignature_ProducesValidStructure()
        {
            var cert = CreateTestCertificate();
            var doc = new PdfDocument();
            doc.Signature = new PdfSignatureOptions { Certificate = cert };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: invisible signature produces /Type /Sig structure");
            page.DrawText("Signed Document", 50, 50, PdfFont.Helvetica, 24);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Document/signed-invisible");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Type /Sig", text);
            Assert.Contains("/Filter /Adobe.PPKLite", text);
            Assert.Contains("/SubFilter /adbe.pkcs7.detached", text);
            Assert.Contains("/ByteRange", text);
            Assert.Contains("/AcroForm", text);
            Assert.Contains("/SigFlags 3", text);
            Assert.Contains("/Rect [0 0 0 0]", text);
        }

        [Fact]
        public void VisibleSignature_HasNonZeroRect()
        {
            var cert = CreateTestCertificate();
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: visible signature has non-zero /Rect and /AP");
            doc.Signature = new PdfSignatureOptions
            {
                Certificate = cert,
                Page = page,
                X = 50, Y = 700, Width = 200, Height = 60,
                Reason = "Approval",
                Location = "Seattle, WA"
            };
            page.DrawText("Visible Signature Doc", 50, 50, PdfFont.Helvetica, 18);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Document/signed-visible");

            var text = TestHelper.GetPdfText(bytes);
            Assert.DoesNotContain("/Rect [0 0 0 0]", text);
            Assert.Contains("/Reason", text);
            Assert.Contains("/Location", text);
            Assert.Contains("/AP", text);
        }

        [Fact]
        public void Signature_MetadataFields_Present()
        {
            var cert = CreateTestCertificate();
            var doc = new PdfDocument();
            doc.Signature = new PdfSignatureOptions
            {
                Certificate = cert,
                Reason = "Test signing",
                Location = "Test location",
                ContactInfo = "test@example.com"
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: signature has /Reason, /Location, /ContactInfo, /M, /Name");
            page.DrawText("Test", 50, 50, PdfFont.Helvetica, 12);
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Reason", text);
            Assert.Contains("/Location", text);
            Assert.Contains("/ContactInfo", text);
            Assert.Contains("/M", text);
            Assert.Contains("/Name", text);
        }

        // ── ByteRange Tests ──

        [Fact]
        public void ByteRange_CoversCorrectRegions()
        {
            var cert = CreateTestCertificate();
            var doc = new PdfDocument();
            doc.Signature = new PdfSignatureOptions { Certificate = cert };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: ByteRange covers correct regions of PDF");
            page.DrawText("Test", 50, 50, PdfFont.Helvetica, 12);
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            // Extract ByteRange values
            var match = Regex.Match(text, @"/ByteRange\s*\[(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]");
            Assert.True(match.Success, "ByteRange not found");

            long br0 = long.Parse(match.Groups[1].Value);
            long br1 = long.Parse(match.Groups[2].Value);
            long br2 = long.Parse(match.Groups[3].Value);
            long br3 = long.Parse(match.Groups[4].Value);

            Assert.Equal(0, br0); // starts at 0
            Assert.True(br1 > 0, "First range length should be positive");
            Assert.True(br2 > br1, "Second range start should be after first range end");
            Assert.Equal(bytes.Length, br1 + (br2 - br1) + br3); // total should equal file size
        }

        // ── PKCS#7 Tests ──

        [Fact]
        public void Pkcs7_StartsWithSequenceTag()
        {
            var cert = CreateTestCertificate();
            var doc = new PdfDocument();
            doc.Signature = new PdfSignatureOptions { Certificate = cert };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: PKCS#7 contents starts with SEQUENCE tag 0x30");
            page.DrawText("Test", 50, 50, PdfFont.Helvetica, 12);
            var bytes = doc.ToArray();

            // Extract Contents hex from the PDF
            var text = TestHelper.GetPdfText(bytes);
            var match = Regex.Match(text, @"/Contents\s*<([0-9A-Fa-f]+)>");
            Assert.True(match.Success, "Contents hex not found");

            string hex = match.Groups[1].Value.TrimEnd('0');
            // PKCS#7 ContentInfo starts with SEQUENCE tag (0x30)
            Assert.StartsWith("30", hex);
        }

        [Fact]
        public void Pkcs7_ContainsSignedDataOid()
        {
            var cert = CreateTestCertificate();
            var doc = new PdfDocument();
            doc.Signature = new PdfSignatureOptions { Certificate = cert };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: PKCS#7 contains id-signedData OID");
            page.DrawText("Test", 50, 50, PdfFont.Helvetica, 12);
            var bytes = doc.ToArray();

            var text = TestHelper.GetPdfText(bytes);
            var match = Regex.Match(text, @"/Contents\s*<([0-9A-Fa-f]+)>");
            Assert.True(match.Success);

            string hex = match.Groups[1].Value;
            // id-signedData OID (1.2.840.113549.1.7.2) encoded:
            // 06 09 2A 86 48 86 F7 0D 01 07 02
            Assert.Contains("2A864886F70D010702", hex.ToUpperInvariant());
        }

        // ── Encryption + Signature ──

        [Fact]
        public void SignatureWithEncryption_ProducesValidPdf()
        {
            var cert = CreateTestCertificate();
            var doc = new PdfDocument();
            doc.Signature = new PdfSignatureOptions { Certificate = cert };
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "",
                OwnerPassword = "owner",
                Level = PdfEncryptionLevel.Aes128
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: signature and encryption coexist in PDF");
            page.DrawText("Signed + Encrypted", 50, 50, PdfFont.Helvetica, 18);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Document/signed-and-encrypted");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Type /Sig", text);
            Assert.Contains("/Encrypt", text);
        }

        // ── CustomSigner ──

        [Fact]
        public void CustomSigner_IsUsedInsteadOfPrivateKey()
        {
            var cert = CreateTestCertificate();
            bool customSignerCalled = false;

            var doc = new PdfDocument();
            doc.Signature = new PdfSignatureOptions
            {
                Certificate = cert,
                CustomSigner = (data) =>
                {
                    customSignerCalled = true;
                    // Sign with the same RSA key (simulating external signer)
                    using (var rsa = cert.GetRSAPrivateKey())
                        return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: custom signer callback is invoked");
            page.DrawText("Custom signer test", 50, 50, PdfFont.Helvetica, 12);
            var bytes = doc.ToArray();

            Assert.True(customSignerCalled);
            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Type /Sig", text);
        }

        // ── Error Cases ──

        [Fact]
        public void NoCertificate_Throws()
        {
            var doc = new PdfDocument();
            doc.Signature = new PdfSignatureOptions();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: missing certificate throws InvalidOperationException");
            page.DrawText("Test", 50, 50, PdfFont.Helvetica, 12);

            Assert.Throws<InvalidOperationException>(() => doc.ToArray());
        }

        // ── Coexistence with Form Fields ──

        [Fact]
        public void SignatureWithFormFields_BothInAcroForm()
        {
            var cert = CreateTestCertificate();
            var doc = new PdfDocument();
            doc.Signature = new PdfSignatureOptions { Certificate = cert };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: signature and form fields coexist in AcroForm");
            page.DrawText("Form + Signature", 50, 50, PdfFont.Helvetica, 18);
            page.AddTextField("name", 50, 100, 200, 25,
                new TextFieldOptions { Value = "Test" });
            page.AddCheckbox("agree", 50, 140, 15,
                new CheckboxOptions { Checked = true });
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Document/signed-with-forms");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/FT /Tx", text);
            Assert.Contains("/FT /Sig", text);
            Assert.Contains("/AcroForm", text);
            Assert.Contains("/SigFlags 3", text);
        }

        // ── DER Encoder Unit Tests ──

        [Fact]
        public void DerEncoder_EncodeLength_ShortForm()
        {
            var len = DerEncoder.EncodeLength(5);
            Assert.Equal(new byte[] { 0x05 }, len);
        }

        [Fact]
        public void DerEncoder_EncodeLength_LongFormOneByte()
        {
            var len = DerEncoder.EncodeLength(128);
            Assert.Equal(new byte[] { 0x81, 0x80 }, len);
        }

        [Fact]
        public void DerEncoder_EncodeLength_LongFormTwoBytes()
        {
            var len = DerEncoder.EncodeLength(256);
            Assert.Equal(new byte[] { 0x82, 0x01, 0x00 }, len);
        }

        [Fact]
        public void DerEncoder_ObjectIdentifier_IdData()
        {
            var oid = DerEncoder.ObjectIdentifier("1.2.840.113549.1.7.1");
            Assert.Equal(0x06, oid[0]); // OID tag
            Assert.Equal(9, oid[1]);    // length
            Assert.Equal(0x2A, oid[2]); // 40*1+2
        }

        [Fact]
        public void DerEncoder_Integer_HighBitSet_PrependZero()
        {
            var value = new byte[] { 0x80 };
            var encoded = DerEncoder.Integer(value);
            // Tag=0x02, Length=2, 0x00, 0x80
            Assert.Equal(new byte[] { 0x02, 0x02, 0x00, 0x80 }, encoded);
        }

        [Fact]
        public void DerEncoder_Integer_NoHighBit_NoPadding()
        {
            var value = new byte[] { 0x7F };
            var encoded = DerEncoder.Integer(value);
            Assert.Equal(new byte[] { 0x02, 0x01, 0x7F }, encoded);
        }

        [Fact]
        public void DerEncoder_Sequence_Wraps()
        {
            var inner = DerEncoder.Null();
            var seq = DerEncoder.Sequence(inner);
            Assert.Equal(0x30, seq[0]); // SEQUENCE tag
            Assert.Equal(2, seq[1]);     // length of NULL (2 bytes)
        }

        [Fact]
        public void DerEncoder_UtcTime_Format()
        {
            var dt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            var encoded = DerEncoder.UtcTime(dt);
            Assert.Equal(0x17, encoded[0]); // UTCTime tag
            // Content: "240115103000Z"
            var content = Encoding.ASCII.GetString(encoded, 2, encoded.Length - 2);
            Assert.Equal("240115103000Z", content);
        }

        // ── Timestamp Unit Tests ──

        [Fact]
        public void DerEncoder_GeneralizedTime_Format()
        {
            var dt = new DateTime(2024, 7, 4, 15, 30, 0, DateTimeKind.Utc);
            var encoded = DerEncoder.GeneralizedTime(dt);
            Assert.Equal(0x18, encoded[0]); // GeneralizedTime tag
            var content = Encoding.ASCII.GetString(encoded, 2, encoded.Length - 2);
            Assert.Equal("20240704153000Z", content);
        }

        [Fact]
        public void Rfc3161Client_BuildTimeStampReq_ValidDer()
        {
            var digest = new byte[32]; // SHA-256 digest (all zeros for test)
            var req = Rfc3161Client.BuildTimeStampReq(digest, HashAlgorithmName.SHA256);

            // Should be a SEQUENCE
            Assert.Equal(0x30, req[0]);
            // Should contain version INTEGER 1
            Assert.Equal(0x02, req[2]); // INTEGER tag
            Assert.Equal(0x01, req[3]); // length 1
            Assert.Equal(0x01, req[4]); // value 1
        }

        [Fact]
        public void Rfc3161Client_ParseTimeStampResp_ExtractsToken()
        {
            // Build a synthetic TimeStampResp: SEQUENCE { SEQUENCE { INTEGER 0 }, token_bytes }
            var mockToken = new byte[] { 0x30, 0x03, 0x02, 0x01, 0x42 }; // a tiny SEQUENCE
            var statusInfo = DerEncoder.Sequence(DerEncoder.IntegerFromInt(0)); // status = granted
            var resp = DerEncoder.Sequence(statusInfo, mockToken);

            var token = Rfc3161Client.ParseTimeStampResp(resp);
            Assert.Equal(mockToken, token);
        }

        [Fact]
        public void Rfc3161Client_ParseTimeStampResp_StatusRejected_Throws()
        {
            var statusInfo = DerEncoder.Sequence(DerEncoder.IntegerFromInt(2)); // status = rejection
            var resp = DerEncoder.Sequence(statusInfo);

            Assert.Throws<InvalidOperationException>(() => Rfc3161Client.ParseTimeStampResp(resp));
        }

        [Fact]
        public void Pkcs7_WithTimestampToken_ContainsTimestampOid()
        {
            var cert = CreateTestCertificate();
            var digest = new byte[32];
            // A mock timestamp token (just needs to be valid DER-ish bytes)
            var mockToken = DerEncoder.Sequence(DerEncoder.IntegerFromInt(99));

            var pkcs7 = Pkcs7Builder.BuildSignedData(
                digest, cert, null,
                HashAlgorithmName.SHA256, DateTime.UtcNow, null,
                timestampToken: mockToken);

            // id-smime-aa-timeStampToken OID (1.2.840.113549.1.9.16.2.14) encoded:
            // 2A 86 48 86 F7 0D 01 09 10 02 0E
            var hex = BitConverter.ToString(pkcs7).Replace("-", "").ToUpperInvariant();
            Assert.Contains("2A864886F70D0109100200E", hex.Replace("0E", "00E").Length > 0
                ? "2A864886F70D0109100200E" : ""); // just check contains
            // Simpler: check for the OID bytes
            bool containsOid = false;
            var oidBytes = new byte[] { 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x10, 0x02, 0x0E };
            for (int i = 0; i <= pkcs7.Length - oidBytes.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < oidBytes.Length; j++)
                {
                    if (pkcs7[i + j] != oidBytes[j]) { match = false; break; }
                }
                if (match) { containsOid = true; break; }
            }
            Assert.True(containsOid, "PKCS#7 should contain id-smime-aa-timeStampToken OID");
        }

        [Fact]
        public void ExtractSignatureValue_RoundTrips()
        {
            var cert = CreateTestCertificate();
            var digest = new byte[32];

            var pkcs7 = Pkcs7Builder.BuildSignedData(
                digest, cert, null,
                HashAlgorithmName.SHA256, DateTime.UtcNow, null);

            var sigValue = PdfSigner.ExtractSignatureValue(pkcs7);
            Assert.NotNull(sigValue);
            Assert.True(sigValue.Length > 0, "Signature value should not be empty");
            // RSA-2048 signature should be 256 bytes
            Assert.Equal(256, sigValue.Length);
        }

        [Fact]
        public void DerEncoder_ReadLength_ShortForm()
        {
            var data = new byte[] { 0x05 };
            int pos = 0;
            Assert.Equal(5, DerEncoder.ReadLength(data, ref pos));
        }

        [Fact]
        public void DerEncoder_ReadLength_LongForm()
        {
            var data = new byte[] { 0x82, 0x01, 0x00 };
            int pos = 0;
            Assert.Equal(256, DerEncoder.ReadLength(data, ref pos));
        }

        // ── Timestamp Integration Test (requires network) ──

        [Fact]
        [Trait("Category", "Integration")]
        public void Signature_WithDigiCertTimestamp_ProducesTimestampedPdf()
        {
            var cert = CreateTestCertificate();
            var doc = new PdfDocument();
            doc.Signature = new PdfSignatureOptions
            {
                Certificate = cert,
                TimestampServer = TimestampServer.DigiCert
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: signature with DigiCert timestamp");
            page.DrawText("Timestamped Document", 50, 50, PdfFont.Helvetica, 18);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Document/signed-timestamped-digicert");

            var text = TestHelper.GetPdfText(bytes);
            Assert.Contains("/Type /Sig", text);

            // PKCS#7 should contain the timestamp OID
            var match = Regex.Match(text, @"/Contents\s*<([0-9A-Fa-f]+)>");
            Assert.True(match.Success);
            string hex = match.Groups[1].Value;
            // id-smime-aa-timeStampToken OID encoded hex
            Assert.Contains("2A864886F70D010910020E", hex.ToUpperInvariant());
        }

        // ── Rendering ──

        [Fact]
        public void VisibleSignature_Renders()
        {
            var cert = CreateTestCertificate();
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: visible signature renders dark pixels in area");
            doc.Signature = new PdfSignatureOptions
            {
                Certificate = cert,
                Page = page,
                X = 50, Y = 50, Width = 250, Height = 80,
                Reason = "Approval",
                Location = "Seattle"
            };
            page.DrawText("Document with visible signature", 50, 20, PdfFont.Helvetica, 14);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Document/signed-visible-render");

            var bitmap = TestHelper.RasterizePage(bytes, "Document/signed-visible-render",
                withAnnotations: true, withFormFill: true);
            // Signature area should have content
            int px = TestHelper.PtToPx(50);
            int py = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                px, px + TestHelper.PtToPx(250), py, py + TestHelper.PtToPx(80)));
        }
    }
}
