using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class PageImportTests
    {
        private static byte[] MakeSourceDocument()
        {
            // Three visually distinct pages: filled colored rectangles at page center
            var doc = new PdfDocument { Title = "Import Source" };

            var p1 = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(p1, "Verify: import source page 1 (red rectangle)");
            p1.DrawFilledRectangle(200, 300, 200, 200, PdfColor.Rgb(255, 0, 0));

            var p2 = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(p2, "Verify: import source page 2 (green rectangle)");
            p2.DrawFilledRectangle(200, 300, 200, 200, PdfColor.Rgb(0, 170, 0));

            var p3 = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(p3, "Verify: import source page 3 (blue rectangle)");
            p3.DrawFilledRectangle(200, 300, 200, 200, PdfColor.Rgb(0, 0, 255));

            return doc.ToArray();
        }

        private static void AssertCenterColor(byte[] pdfBytes, string testName, int pageIndex,
            byte r, byte g, byte b)
        {
            var bitmap = TestHelper.RasterizePage(pdfBytes, testName, pageIndex);
            // Rectangle spans x 200-400, y 300-500 (top-down) -> sample its center
            int px = TestHelper.PtToPx(300);
            int py = TestHelper.PtToPx(400);
            TestHelper.AssertPixelColor(bitmap, px, py, r, g, b);
        }

        // ── Basic import (extract) ──────────────────────────────────

        [Fact]
        public void ImportPage_ExtractSinglePage_RendersSameContent()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                dest.ImportPage(source, 2);
                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/import-extract-page2");

                Assert.Equal(1, TestHelper.GetPageCount(bytes));
                AssertCenterColor(bytes, "Reading/import-extract-page2", 0, 0, 170, 0);
            }
        }

        [Fact]
        public void ImportPage_SourceDisposedBeforeSave_StillWorks()
        {
            var sourceBytes = MakeSourceDocument();
            var dest = new PdfDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                dest.ImportPage(source, 1);
            } // disposed here

            var bytes = dest.ToArray();
            Assert.Equal(1, TestHelper.GetPageCount(bytes));
            AssertCenterColor(bytes, "Reading/import-after-dispose", 0, 255, 0, 0);
        }

        [Fact]
        public void ImportPage_InsertAtPosition()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var gen1 = dest.AddPage(PageSize.A4);
                TestHelper.AddDescription(gen1, "Verify: generated page before inserted import");
                gen1.DrawText("Generated A", 50, 100, PdfFont.Helvetica, 24);
                var gen2 = dest.AddPage(PageSize.A4);
                TestHelper.AddDescription(gen2, "Verify: generated page after inserted import");
                gen2.DrawText("Generated B", 50, 100, PdfFont.Helvetica, 24);

                var imported = dest.ImportPage(source, 3, insertAt: 2);
                Assert.True(imported.IsImported);
                Assert.Equal(2, dest.GetPageNumber(imported));

                var bytes = dest.ToArray();
                Assert.Equal(3, TestHelper.GetPageCount(bytes));
                // Page 2 (index 1) is the imported blue page
                AssertCenterColor(bytes, "Reading/import-insert-at", 1, 0, 0, 255);
            }
        }

        // ── Merge ───────────────────────────────────────────────────

        [Fact]
        public void Merge_TwoDocuments_AllPagesInOrder()
        {
            var sourceBytes = MakeSourceDocument();
            using (var a = PdfReadDocument.Open(sourceBytes))
            using (var b = PdfReadDocument.Open(sourceBytes))
            {
                var merged = PdfDocument.Merge(a, b);
                Assert.Equal(6, merged.PageCount);
                var bytes = merged.ToArray();
                TestHelper.SavePdf(bytes, "Reading/merge-two-sources");

                Assert.Equal(6, TestHelper.GetPageCount(bytes));
                AssertCenterColor(bytes, "Reading/merge-two-sources", 0, 255, 0, 0);  // first: red
                AssertCenterColor(bytes, "Reading/merge-two-sources", 5, 0, 0, 255);  // last: blue
            }
        }

        [Fact]
        public void Merge_FromFilePaths()
        {
            var sourceBytes = MakeSourceDocument();
            var path1 = TestHelper.SavePdf(sourceBytes, "Reading/merge-input-1");
            var path2 = TestHelper.SavePdf(sourceBytes, "Reading/merge-input-2");

            var merged = PdfDocument.Merge(path1, path2);
            Assert.Equal(6, merged.PageCount);
            var bytes = merged.ToArray();
            Assert.Equal(6, TestHelper.GetPageCount(bytes));
        }

        // ── Interleaving generated and imported pages ───────────────

        [Fact]
        public void Interleave_GeneratedAndImported_KidsOrderCorrect()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var gen1 = dest.AddPage(PageSize.A4);
                TestHelper.AddDescription(gen1, "Verify: interleave page 1 is generated (black rect)");
                gen1.DrawFilledRectangle(200, 300, 200, 200, PdfColor.Rgb(0, 0, 0));

                dest.ImportPage(source, 2); // green

                var gen2 = dest.AddPage(PageSize.A4);
                TestHelper.AddDescription(gen2, "Verify: interleave page 3 is generated (gray rect)");
                gen2.DrawFilledRectangle(200, 300, 200, 200, PdfColor.Rgb(128, 128, 128));

                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/interleave");

                Assert.Equal(3, TestHelper.GetPageCount(bytes));
                AssertCenterColor(bytes, "Reading/interleave", 0, 0, 0, 0);
                AssertCenterColor(bytes, "Reading/interleave", 1, 0, 170, 0);
                AssertCenterColor(bytes, "Reading/interleave", 2, 128, 128, 128);
            }
        }

        // ── Remove / Move ───────────────────────────────────────────

        [Fact]
        public void RemoveAndMovePages_MixedDocument()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                dest.ImportPages(source, 1, 3); // red green blue

                dest.RemovePage(2);             // red blue
                Assert.Equal(2, dest.PageCount);

                dest.MovePage(2, 1);            // blue red
                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/remove-move");

                AssertCenterColor(bytes, "Reading/remove-move", 0, 0, 0, 255);
                AssertCenterColor(bytes, "Reading/remove-move", 1, 255, 0, 0);
            }
        }

        [Fact]
        public void RemovePage_OutOfRange_Throws()
        {
            var doc = new PdfDocument();
            doc.AddPage(PageSize.A4);
            Assert.Throws<ArgumentOutOfRangeException>(() => doc.RemovePage(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => doc.RemovePage(2));
        }

        // ── Split ───────────────────────────────────────────────────

        [Fact]
        public void Split_IntoSinglePageDocuments()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                for (int i = 1; i <= source.PageCount; i++)
                {
                    var single = new PdfDocument();
                    single.ImportPage(source, i);
                    var bytes = single.ToArray();
                    Assert.Equal(1, TestHelper.GetPageCount(bytes));
                    using (var reopened = PdfReadDocument.Open(bytes))
                        Assert.Equal(1, reopened.PageCount);
                }
            }
        }

        // ── Resource sharing across imports from the same source ────

        [Fact]
        public void ImportSamePageTwice_SharesResourceClosure()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var one = new PdfDocument();
                one.ImportPage(source, 1);
                int singleSize = one.ToArray().Length;

                var two = new PdfDocument();
                two.ImportPage(source, 1);
                two.ImportPage(source, 1);
                var twoBytes = two.ToArray();

                Assert.Equal(2, TestHelper.GetPageCount(twoBytes));
                // The shared closure (content stream, fonts) is emitted once, so the
                // two-page file must be well below twice the single-page size
                Assert.True(twoBytes.Length < singleSize * 1.8,
                    $"Two imports of the same page ({twoBytes.Length}) should share objects (single: {singleSize})");
            }
        }

        // ── Destination features on merged documents ────────────────

        [Fact]
        public void MergedDocument_EncryptedDestination_Rasterizes()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                dest.ImportPage(source, 1);
                dest.Encryption = new PdfEncryptionOptions
                {
                    UserPassword = "secret",
                    OwnerPassword = "owner",
                    Level = PdfEncryptionLevel.Aes128,
                };
                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/merged-encrypted");

                var bitmap = PDFtoImage.Conversion.ToImage(bytes, page: 0, password: "secret",
                    options: new PDFtoImage.RenderOptions(Dpi: 150));
                int px = TestHelper.PtToPx(300);
                int py = TestHelper.PtToPx(400);
                TestHelper.AssertPixelColor(bitmap, px, py, 255, 0, 0);
            }
        }

        [Fact]
        public void MergedDocument_EncryptedDestination_SavedTwiceIsStable()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                dest.ImportPage(source, 1);
                dest.Encryption = new PdfEncryptionOptions
                {
                    UserPassword = "secret",
                    OwnerPassword = "owner",
                    Level = PdfEncryptionLevel.Aes256,
                };
                var first = dest.ToArray();
                var second = dest.ToArray(); // must not double-encrypt the shared closure

                foreach (var bytes in new[] { first, second })
                {
                    var bitmap = PDFtoImage.Conversion.ToImage(bytes, page: 0, password: "secret",
                        options: new PDFtoImage.RenderOptions(Dpi: 150));
                    TestHelper.AssertPixelColor(bitmap,
                        TestHelper.PtToPx(300), TestHelper.PtToPx(400), 255, 0, 0);
                }
            }
        }

        [Fact]
        public void MergedDocument_Signed_HasSignatureStructure()
        {
            var sourceBytes = MakeSourceDocument();
            using (var rsa = RSA.Create(2048))
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var request = new CertificateRequest(
                    "CN=SimpleTinyPDF Test, O=Test", rsa,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var cert = request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddMinutes(-5),
                    DateTimeOffset.UtcNow.AddYears(1));

                var dest = new PdfDocument();
                dest.ImportPage(source, 1);
                dest.Signature = new PdfSignatureOptions { Certificate = cert };
                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/merged-signed");

                var text = TestHelper.GetPdfText(bytes);
                Assert.Contains("/Type /Sig", text);
                Assert.Contains("/ByteRange", text);
                Assert.Equal(1, TestHelper.GetPageCount(bytes));
            }
        }

        // ── Guards ──────────────────────────────────────────────────

        [Fact]
        public void DrawingOnImportedPage_ThrowsAtSave()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var imported = dest.ImportPage(source, 1);
                imported.DrawText("Stamp attempt", 50, 50, PdfFont.Helvetica, 12);

                Assert.Throws<InvalidOperationException>(() => dest.ToArray());
            }
        }

        [Fact]
        public void ImportPage_LinkAnnotationSurvives_WidgetsAndDestsDropped()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: URI link survives import, widgets dropped");
            page.DrawText("Visit example.com", 50, 100, PdfFont.Helvetica, 14, link: "https://example.com");
            page.AddTextField("name", 50, 200, 200, 24);
            var sourceBytes = doc.ToArray();

            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                dest.ImportPage(source, 1);
                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/import-annots");

                var text = TestHelper.GetPdfText(bytes);
                Assert.Contains("example.com", text);       // URI link kept
                Assert.DoesNotContain("/Widget", text);      // form widget dropped
                Assert.DoesNotContain("/AcroForm", text);    // no orphaned form
            }
        }

        // ── Ghent Workgroup real-world file ─────────────────────────

        [Fact]
        public void Ghent_ImportFirstPage_RendersLikeSource()
        {
            var ghentBytes = File.ReadAllBytes(PdfReadDocumentTests.GhentPath);
            using (var source = PdfReadDocument.Open(ghentBytes))
            {
                var dest = new PdfDocument();
                dest.ImportPage(source, 1);
                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/ghent-page1");

                var expected = TestHelper.RasterizePage(ghentBytes, "Reading/ghent-page1-source", 0, dpi: 72);
                var actual = TestHelper.RasterizePage(bytes, "Reading/ghent-page1-imported", 0, dpi: 72);

                Assert.Equal(expected.Width, actual.Width);
                Assert.Equal(expected.Height, actual.Height);

                // Sample a grid of pixels; the imported page must render nearly identically
                int checkedPixels = 0, matches = 0;
                for (int y = 10; y < expected.Height - 10; y += expected.Height / 20)
                {
                    for (int x = 10; x < expected.Width - 10; x += expected.Width / 20)
                    {
                        var e = expected.GetPixel(x, y);
                        var a = actual.GetPixel(x, y);
                        checkedPixels++;
                        if (Math.Abs(e.Red - a.Red) <= 8 &&
                            Math.Abs(e.Green - a.Green) <= 8 &&
                            Math.Abs(e.Blue - a.Blue) <= 8)
                            matches++;
                    }
                }
                Assert.True(matches >= checkedPixels * 0.98,
                    $"Only {matches}/{checkedPixels} sampled pixels match the source rendering");
            }
        }

        [Fact]
        public void Ghent_MergeWithGeneratedDocument()
        {
            using (var source = PdfReadDocument.Open(PdfReadDocumentTests.GhentPath))
            {
                var dest = new PdfDocument();
                var cover = dest.AddPage(PageSize.A4);
                TestHelper.AddDescription(cover, "Verify: generated cover page before imported Ghent pages");
                cover.DrawText("Merged Document Cover", 50, 100, PdfFont.HelveticaBold, 28);

                int importCount = source.PageCount;
                dest.ImportPages(source, 1, importCount);

                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/ghent-merged");

                Assert.Equal(1 + importCount, TestHelper.GetPageCount(bytes));
                // Cover renders with text
                var coverBitmap = TestHelper.RasterizePage(bytes, "Reading/ghent-merged", 0);
                Assert.True(TestHelper.HasDarkPixelsInRegion(coverBitmap,
                    TestHelper.PtToPx(50), TestHelper.PtToPx(400),
                    TestHelper.PtToPx(80), TestHelper.PtToPx(120)));
                // First and last imported pages render non-blank
                var importedBitmap = TestHelper.RasterizePage(bytes, "Reading/ghent-merged", 1);
                Assert.True(TestHelper.CountDarkPixelsInRegion(importedBitmap,
                    0, importedBitmap.Width - 1, 0, importedBitmap.Height - 1) > 100,
                    "First imported Ghent page rendered blank");
                var lastBitmap = TestHelper.RasterizePage(bytes, "Reading/ghent-merged", importCount);
                Assert.True(TestHelper.CountDarkPixelsInRegion(lastBitmap,
                    0, lastBitmap.Width - 1, 0, lastBitmap.Height - 1) > 100,
                    "Last imported Ghent page rendered blank");
            }
        }

        [Fact]
        public void Ghent_MergedOutput_PreservesPdfxOutputIntentAndMetadata()
        {
            // The Ghent file is PDF/X-4. Its /OutputIntents (/S /GTS_PDFX) and XMP metadata
            // must survive the merge — Acrobat only auto-enables overprint simulation for
            // PDF/X files, and the GWG overprint patches render wrong without it.
            using (var source = PdfReadDocument.Open(PdfReadDocumentTests.GhentPath))
            {
                var dest = new PdfDocument();
                dest.ImportPage(source, 1);
                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/ghent-outputintent");

                var text = TestHelper.GetPdfText(bytes);
                Assert.Contains("/OutputIntents", text);
                Assert.Contains("/GTS_PDFX", text);
                Assert.Contains("/Metadata", text);
            }
        }

        [Fact]
        public void Ghent_ReopenMergedOutput_WithOwnReader()
        {
            using (var source = PdfReadDocument.Open(PdfReadDocumentTests.GhentPath))
            {
                var dest = new PdfDocument();
                int importCount = Math.Min(2, source.PageCount);
                dest.ImportPages(source, 1, importCount);
                var bytes = dest.ToArray();

                using (var reopened = PdfReadDocument.Open(bytes))
                {
                    Assert.Equal(importCount, reopened.PageCount);
                    var originalSize = source.GetPageSize(1);
                    var reopenedSize = reopened.GetPageSize(1);
                    Assert.Equal(originalSize.Width, reopenedSize.Width, 1);
                    Assert.Equal(originalSize.Height, reopenedSize.Height, 1);
                }
            }
        }
    }
}
