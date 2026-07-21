using System;
using System.IO;
using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class PdfReadDocumentTests
    {
        internal static readonly string GhentPath =
            Path.Combine("TestAssets", "Pdfs", "Ghent_PDF-Output-Test-V50_ALL_X4.pdf");

        private static byte[] MakeSimpleDocument(int pages = 3, string title = "Read Test")
        {
            var doc = new PdfDocument { Title = title, Author = "Unit Test" };
            for (int i = 1; i <= pages; i++)
            {
                var page = doc.AddPage(PageSize.A4);
                page.DrawText($"Page {i}", 50, 50, PdfFont.Helvetica, 24);
            }
            return doc.ToArray();
        }

        // ── Opening self-generated files (classic xref) ─────────────

        [Fact]
        public void Open_SelfGenerated_PageCountAndMetadata()
        {
            var bytes = MakeSimpleDocument(3, "My Title");
            using (var read = PdfReadDocument.Open(bytes))
            {
                Assert.Equal(3, read.PageCount);
                Assert.Equal("My Title", read.Title);
                Assert.Equal("Unit Test", read.Author);
            }
        }

        [Fact]
        public void Open_SelfGenerated_PageSizeMatches()
        {
            var bytes = MakeSimpleDocument(1);
            using (var read = PdfReadDocument.Open(bytes))
            {
                var size = read.GetPageSize(1);
                Assert.Equal(PageSize.A4.Width, size.Width, 1);
                Assert.Equal(PageSize.A4.Height, size.Height, 1);
            }
        }

        [Fact]
        public void Open_FromStreamAndFile_Work()
        {
            var bytes = MakeSimpleDocument(2);
            using (var ms = new MemoryStream(bytes))
            using (var read = PdfReadDocument.Open(ms))
            {
                Assert.Equal(2, read.PageCount);
            }

            var path = TestHelper.SavePdf(bytes, "Reading/open-from-file");
            using (var read = PdfReadDocument.Open(path))
            {
                Assert.Equal(2, read.PageCount);
            }
        }

        // ── Opening the Ghent Workgroup test file (xref stream) ─────

        [Fact]
        public void Open_Ghent_PageCountMatchesPdfium()
        {
            var bytes = File.ReadAllBytes(GhentPath);
            int expected = TestHelper.GetPageCount(bytes);
            using (var read = PdfReadDocument.Open(bytes))
            {
                Assert.Equal(expected, read.PageCount);
            }
        }

        [Fact]
        public void Open_Ghent_PageSizesArePositive()
        {
            using (var read = PdfReadDocument.Open(GhentPath))
            {
                for (int i = 1; i <= read.PageCount; i++)
                {
                    var size = read.GetPageSize(i);
                    Assert.True(size.Width > 100, $"Page {i} width {size.Width} looks wrong");
                    Assert.True(size.Height > 100, $"Page {i} height {size.Height} looks wrong");
                }
            }
        }

        // ── Error handling ──────────────────────────────────────────

        [Fact]
        public void Open_Encrypted_ThrowsNotSupported()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Secret", 50, 50, PdfFont.Helvetica, 12);
            doc.Encryption = new PdfEncryptionOptions
            {
                UserPassword = "user",
                OwnerPassword = "owner",
                Level = PdfEncryptionLevel.Aes128,
            };
            var bytes = doc.ToArray();

            var ex = Assert.Throws<NotSupportedException>(() => PdfReadDocument.Open(bytes));
            Assert.Contains("password", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Open_CorruptedStartxref_RepairedByScan()
        {
            var bytes = MakeSimpleDocument(3);
            // Break the startxref offset so the normal path fails
            string text = Encoding.ASCII.GetString(bytes);
            int startxref = text.LastIndexOf("startxref", StringComparison.Ordinal);
            int digitPos = startxref + "startxref".Length;
            while (!char.IsDigit(text[digitPos])) digitPos++;
            bytes[digitPos] = (byte)'9';
            bytes[digitPos + 1] = (byte)'9';

            using (var read = PdfReadDocument.Open(bytes))
            {
                Assert.Equal(3, read.PageCount);
            }
        }

        [Fact]
        public void Open_Garbage_ThrowsParseException()
        {
            var garbage = Encoding.ASCII.GetBytes("%PDF-1.4\nthis is not a real pdf at all");
            Assert.Throws<PdfParseException>(() => PdfReadDocument.Open(garbage));
        }

        [Fact]
        public void Open_NotAPdf_ThrowsParseException()
        {
            var notPdf = Encoding.ASCII.GetBytes("hello world, definitely not a pdf");
            Assert.Throws<PdfParseException>(() => PdfReadDocument.Open(notPdf));
        }

        [Fact]
        public void Disposed_AccessThrows()
        {
            var read = PdfReadDocument.Open(MakeSimpleDocument(1));
            read.Dispose();
            Assert.Throws<ObjectDisposedException>(() => read.PageCount);
        }

        // ── Predictor decoding (used by xref streams) ───────────────

        [Fact]
        public void PngUpPredictor_Decoded()
        {
            // Two rows of 3 columns, filter type 2 (Up) on both rows
            var encoded = new byte[]
            {
                2, 10, 20, 30,   // row 0: Up with zero previous row -> 10 20 30
                2, 1, 2, 3,      // row 1: adds previous row -> 11 22 33
            };
            var parms = new CosDict();
            parms.Set("Predictor", new CosInteger(12));
            parms.Set("Columns", new CosInteger(3));
            var decoded = FlateFilter.ApplyPredictor(encoded, parms);
            Assert.Equal(new byte[] { 10, 20, 30, 11, 22, 33 }, decoded);
        }

        [Fact]
        public void PngPaethAndSubPredictors_Decoded()
        {
            var encoded = new byte[]
            {
                1, 5, 5, 5,      // row 0: Sub -> 5 10 15
                4, 1, 1, 1,      // row 1: Paeth
            };
            var parms = new CosDict();
            parms.Set("Predictor", new CosInteger(15));
            parms.Set("Columns", new CosInteger(3));
            var decoded = FlateFilter.ApplyPredictor(encoded, parms);
            Assert.Equal(new byte[] { 5, 10, 15 }, new[] { decoded[0], decoded[1], decoded[2] });
            // Paeth for col 0: a=0, b=5(up), c=0 -> predictor=5 -> 1+5=6
            Assert.Equal(6, decoded[3]);
        }

        [Fact]
        public void TiffPredictor_Decoded()
        {
            var encoded = new byte[] { 10, 5, 5 }; // one row: 10, +5, +5
            var parms = new CosDict();
            parms.Set("Predictor", new CosInteger(2));
            parms.Set("Columns", new CosInteger(3));
            var decoded = FlateFilter.ApplyPredictor(encoded, parms);
            Assert.Equal(new byte[] { 10, 15, 20 }, decoded);
        }
    }
}
