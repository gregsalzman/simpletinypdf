using System;
using System.Linq;
using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class EncryptionTests
    {
        // ── AES-128 (V4/R4) ─────────────────────────────────────────────

        [Fact]
        public void Aes128_UserPassword_ProducesEncryptedPdf()
        {
            var doc = new PdfDocument();
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "test123",
                OwnerPassword = "owner456",
                Level = PdfEncryptionLevel.Aes128
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: AES-128 encrypted PDF with user password");
            page.DrawText("Hello Encrypted World", 50, 50, PdfFont.Helvetica, 24);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/encrypted-aes128-user-password");

            // Verify PDF header is 1.6
            var header = Encoding.ASCII.GetString(bytes, 0, 10);
            Assert.StartsWith("%PDF-1.6", header);

            // Verify /Encrypt dictionary is present
            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.Contains("/Encrypt", pdfText);
            Assert.Contains("/AESV2", pdfText);
            Assert.Contains("/V 4", pdfText);
            Assert.Contains("/R 4", pdfText);
        }

        [Fact]
        public void Aes128_OwnerPasswordOnly_OpensWithoutPassword()
        {
            var doc = new PdfDocument();
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "",
                OwnerPassword = "owner",
                Level = PdfEncryptionLevel.Aes128,
                Permissions = PdfPermissions.Print
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: AES-128 encrypted PDF opens without password when only owner password set");
            page.DrawText("Owner-only restriction", 50, 50, PdfFont.Helvetica, 18);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/encrypted-aes128-owner-only");

            // Should open without password (empty user password)
            var bitmap = TestHelper.RasterizePage(bytes, "Document/encrypted-aes128-owner-only");
            Assert.True(bitmap.Width > 100);
            bitmap.Dispose();
        }

        [Fact]
        public void Aes128_WithPassword_RasterizesCorrectly()
        {
            var doc = new PdfDocument();
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "secret",
                OwnerPassword = "owner",
                Level = PdfEncryptionLevel.Aes128
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: AES-128 encrypted PDF renders text content correctly");
            page.DrawText("Test content", 100, 100, PdfFont.Helvetica, 24);
            page.DrawFilledRectangle(50, 200, 200, 100, PdfColor.Red);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/encrypted-aes128-with-content");

            // Rasterize with the user password
            var bitmap = PDFtoImage.Conversion.ToImage(bytes, page: 0, password: "secret",
                options: new PDFtoImage.RenderOptions(Dpi: 150));
            Assert.True(bitmap.Width > 100);

            // Verify the red rectangle is visible (not a blank/corrupt page)
            int px = (int)(150 * 150f / 72f);
            int py = (int)(250 * 150f / 72f);
            if (px < bitmap.Width && py < bitmap.Height)
            {
                var pixel = bitmap.GetPixel(px, py);
                Assert.True(pixel.Red > 200, $"Expected red channel > 200, got {pixel.Red}");
            }
            bitmap.Dispose();
        }

        // ── AES-256 (V5/R6) ─────────────────────────────────────────────

        [Fact]
        public void Aes256_UserPassword_ProducesEncryptedPdf()
        {
            var doc = new PdfDocument();
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "test123",
                OwnerPassword = "owner456",
                Level = PdfEncryptionLevel.Aes256
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: AES-256 encrypted PDF with user password");
            page.DrawText("AES-256 Encrypted", 50, 50, PdfFont.Helvetica, 24);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/encrypted-aes256-user-password");

            // Verify PDF header is 2.0
            var header = Encoding.ASCII.GetString(bytes, 0, 10);
            Assert.StartsWith("%PDF-2.0", header);

            // Verify /Encrypt dictionary entries
            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.Contains("/Encrypt", pdfText);
            Assert.Contains("/AESV3", pdfText);
            Assert.Contains("/V 5", pdfText);
            Assert.Contains("/R 6", pdfText);
            Assert.Contains("/OE", pdfText);
            Assert.Contains("/UE", pdfText);
            Assert.Contains("/Perms", pdfText);
        }

        [Fact]
        public void Aes256_OwnerPasswordOnly_OpensWithoutPassword()
        {
            var doc = new PdfDocument();
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "",
                OwnerPassword = "owner",
                Level = PdfEncryptionLevel.Aes256,
                Permissions = PdfPermissions.Print | PdfPermissions.ExtractText
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: AES-256 encrypted PDF opens without password when only owner password set");
            page.DrawText("AES-256 owner-only", 50, 50, PdfFont.Helvetica, 18);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/encrypted-aes256-owner-only");

            // Should open without password
            var bitmap = TestHelper.RasterizePage(bytes, "Document/encrypted-aes256-owner-only");
            Assert.True(bitmap.Width > 100);
            bitmap.Dispose();
        }

        [Fact]
        public void Aes256_WithPassword_RasterizesCorrectly()
        {
            var doc = new PdfDocument();
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "secret256",
                OwnerPassword = "owner256",
                Level = PdfEncryptionLevel.Aes256
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: AES-256 encrypted PDF renders text content correctly");
            page.DrawFilledRectangle(50, 50, 200, 100, PdfColor.Blue);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/encrypted-aes256-with-content");

            var bitmap = PDFtoImage.Conversion.ToImage(bytes, page: 0, password: "secret256",
                options: new PDFtoImage.RenderOptions(Dpi: 150));
            Assert.True(bitmap.Width > 100);

            // Verify blue rectangle is visible
            int px = (int)(150 * 150f / 72f);
            int py = (int)(100 * 150f / 72f);
            if (px < bitmap.Width && py < bitmap.Height)
            {
                var pixel = bitmap.GetPixel(px, py);
                Assert.True(pixel.Blue > 200, $"Expected blue channel > 200, got {pixel.Blue}");
            }
            bitmap.Dispose();
        }

        // ── Permission flags ─────────────────────────────────────────────

        [Fact]
        public void PValue_AllPermissions_HasCorrectBits()
        {
            int p = PdfEncryptor.ComputePValue(PdfPermissions.All);
            // All user permission bits set + reserved bits 7-8 and 13-32
            Assert.True(p < 0, "P value should be negative (bit 32 set)");
            // Check bits 3-6 and 9-12 are set
            Assert.Equal(0x0F3C, p & 0x0F3C);
        }

        [Fact]
        public void PValue_NoPermissions_HasOnlyReservedBits()
        {
            int p = PdfEncryptor.ComputePValue(PdfPermissions.None);
            // Only reserved bits set
            Assert.Equal(0, p & 0x0F3C);
            // Bits 7-8 set
            Assert.Equal(0xC0, p & 0xC0);
        }

        [Fact]
        public void PValue_PrintOnly()
        {
            int p = PdfEncryptor.ComputePValue(PdfPermissions.Print);
            Assert.NotEqual(0, p & (1 << 2)); // bit 3 set
            Assert.Equal(0, p & (1 << 3));     // bit 4 not set
        }

        // ── Content encryption ───────────────────────────────────────────

        [Fact]
        public void EncryptedPdf_StreamContentIsNotPlaintext()
        {
            var doc = new PdfDocument();
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "test",
                OwnerPassword = "test",
                Level = PdfEncryptionLevel.Aes128
            };
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("UNIQUE_MARKER_TEXT", 50, 50, PdfFont.Helvetica, 24);
            var bytes = doc.ToArray();

            // The plaintext marker should NOT appear in the raw PDF bytes
            // (content streams are encrypted)
            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.DoesNotContain("UNIQUE_MARKER_TEXT", pdfText);
        }

        [Fact]
        public void EncryptedPdf_Aes256_StreamContentIsNotPlaintext()
        {
            var doc = new PdfDocument();
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "test",
                OwnerPassword = "test",
                Level = PdfEncryptionLevel.Aes256
            };
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("SECRET_DATA_HERE", 50, 50, PdfFont.Helvetica, 24);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.DoesNotContain("SECRET_DATA_HERE", pdfText);
        }

        // ── Backward compatibility ───────────────────────────────────────

        [Fact]
        public void NoEncryption_ProducesStandardPdf()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("No encryption", 50, 50, PdfFont.Helvetica, 18);
            var bytes = doc.ToArray();

            var header = Encoding.ASCII.GetString(bytes, 0, 10);
            Assert.StartsWith("%PDF-1.4", header);

            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.DoesNotContain("/Encrypt", pdfText);

            // Should rasterize without any password
            var bitmap = TestHelper.RasterizePage(bytes, "no_encryption_compat");
            Assert.True(bitmap.Width > 100);
            bitmap.Dispose();
        }

        // ── RC4 internal tests ───────────────────────────────────────────

        [Fact]
        public void Rc4_KnownVector_MatchesExpected()
        {
            // RFC 6229 test vector: Key = "Key", Plaintext = "Plaintext"
            var key = Encoding.ASCII.GetBytes("Key");
            var plaintext = Encoding.ASCII.GetBytes("Plaintext");
            var encrypted = Rc4.Transform(key, plaintext);

            // RC4("Key", "Plaintext") = BBF316E8D940AF0AD3
            var expected = new byte[] { 0xBB, 0xF3, 0x16, 0xE8, 0xD9, 0x40, 0xAF, 0x0A, 0xD3 };
            Assert.Equal(expected, encrypted);
        }

        [Fact]
        public void Rc4_EncryptDecrypt_Roundtrip()
        {
            var key = Encoding.ASCII.GetBytes("TestKey");
            var plaintext = Encoding.ASCII.GetBytes("Hello World!");
            var encrypted = Rc4.Transform(key, plaintext);
            var decrypted = Rc4.Transform(key, encrypted);
            Assert.Equal(plaintext, decrypted);
        }

        // ── Multi-page encrypted documents ───────────────────────────────

        [Fact]
        public void Aes128_MultiPage_AllPagesAccessible()
        {
            var doc = new PdfDocument();
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "multi",
                OwnerPassword = "multi",
                Level = PdfEncryptionLevel.Aes128
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: AES-128 encrypted multi-page PDF has all pages accessible");
            page.DrawText("Page 1", 50, 50, PdfFont.Helvetica, 18);
            doc.AddPage(PageSize.A4).DrawText("Page 2", 50, 50, PdfFont.Helvetica, 18);
            doc.AddPage(PageSize.A4).DrawText("Page 3", 50, 50, PdfFont.Helvetica, 18);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/encrypted-multipage");

            // Should have 3 pages accessible with the password
            var count = PDFtoImage.Conversion.GetPageCount(bytes, password: "multi");
            Assert.Equal(3, count);
        }

        // ── Encrypted with images ────────────────────────────────────────

        [Fact]
        public void Aes128_WithImage_ProducesValidPdf()
        {
            var doc = new PdfDocument();
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "imgtest",
                OwnerPassword = "imgtest",
                Level = PdfEncryptionLevel.Aes128
            };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: AES-128 encrypted PDF with embedded image renders correctly");
            var jpeg = TestHelper.CreateTestJpeg();
            var img = doc.AddImage(PdfImage.FromBytes(jpeg));
            page.DrawImage(img, 50, 50, 100, 100);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Document/encrypted-with-image");

            var bitmap = PDFtoImage.Conversion.ToImage(bytes, page: 0, password: "imgtest",
                options: new PDFtoImage.RenderOptions(Dpi: 150));
            Assert.True(bitmap.Width > 100);
            bitmap.Dispose();
        }
    }
}
