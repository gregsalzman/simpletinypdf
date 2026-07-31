using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    /// <summary>
    /// Tests for drawing (stamping) on pages imported from an existing PDF:
    /// content goes on top of the original page, resource names never collide,
    /// and non-zero MediaBox origins / page rotation are compensated.
    /// </summary>
    public class StampingTests
    {
        private static byte[] MakeSourceDocument()
        {
            // A page with a centered red rectangle and Helvetica text (which claims /F1
            // in the source page's resources — the classic name-collision hazard)
            var doc = new PdfDocument { Title = "Stamp Source" };
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: stamping source page (red rectangle + text)");
            page.DrawFilledRectangle(200, 300, 200, 200, PdfColor.Rgb(255, 0, 0));
            page.DrawText("Original content", 50, 100, PdfFont.Helvetica, 18);
            return doc.ToArray();
        }

        /// <summary>
        /// Builds a minimal single-page PDF by hand so tests can exercise geometry our
        /// own writer never produces (offset MediaBox origin, /Rotate).
        /// </summary>
        private static byte[] BuildPdfWithBoxAndRotation(string mediaBox, int? rotate, string contentOps)
        {
            var sb = new StringBuilder();
            var offsets = new List<int>();
            void BeginObj(int num)
            {
                offsets.Add(sb.Length);
                sb.Append(num).Append(" 0 obj\n");
            }

            sb.Append("%PDF-1.4\n");
            BeginObj(1);
            sb.Append("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            BeginObj(2);
            sb.Append("<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
            BeginObj(3);
            sb.Append($"<< /Type /Page /Parent 2 0 R /MediaBox {mediaBox} ");
            if (rotate.HasValue)
                sb.Append($"/Rotate {rotate.Value} ");
            sb.Append("/Contents 4 0 R /Resources << >> >>\nendobj\n");
            BeginObj(4);
            sb.Append($"<< /Length {contentOps.Length} >>\nstream\n{contentOps}\nendstream\nendobj\n");

            int xrefPos = sb.Length;
            sb.Append("xref\n0 5\n0000000000 65535 f \n");
            foreach (var off in offsets)
                sb.Append(off.ToString("D10")).Append(" 00000 n \n");
            sb.Append("trailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n")
              .Append(xrefPos).Append("\n%%EOF\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        // ── Basic stamping ──────────────────────────────────────────

        [Fact]
        public void Stamp_TextAndRect_OriginalAndStampBothVisible()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                TestHelper.AddDescription(page, "Verify: blue square + text stamped over red rectangle page");
                page.DrawFilledRectangle(50, 600, 100, 100, PdfColor.Rgb(0, 0, 255));
                page.DrawText("Stamped!", 300, 150, PdfFont.HelveticaBold, 24);

                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/stamp-basic");
                var bitmap = TestHelper.RasterizePage(bytes, "Reading/stamp-basic");

                // Original red rectangle still at page center
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(300), TestHelper.PtToPx(400), 255, 0, 0);
                // Stamped blue square
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(100), TestHelper.PtToPx(650), 0, 0, 255);
                // Stamped text drew something
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                    TestHelper.PtToPx(300), TestHelper.PtToPx(420),
                    TestHelper.PtToPx(140), TestHelper.PtToPx(175)));
            }
        }

        [Fact]
        public void Stamp_DrawsOnTopOfOriginalContent()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                TestHelper.AddDescription(page, "Verify: green square covers the center of the red rectangle");
                // Cover the center of the source's red rectangle
                page.DrawFilledRectangle(250, 350, 100, 100, PdfColor.Rgb(0, 170, 0));

                var bytes = dest.ToArray();
                var bitmap = TestHelper.RasterizePage(bytes, "Reading/stamp-on-top");

                // Center is now green (stamp wins), corner of the rectangle still red
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(300), TestHelper.PtToPx(400), 0, 170, 0);
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(220), TestHelper.PtToPx(320), 255, 0, 0);
            }
        }

        [Fact]
        public void Stamp_ResourceNames_DoNotCollideWithSourcePage()
        {
            // The source page uses /F1 for Helvetica; the stamp uses its own /F1 for
            // Times inside the Form XObject's private resources
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                TestHelper.AddDescription(page, "Verify: stamped Times text and original Helvetica text both render");
                page.DrawText("Stamped with Times", 50, 700, PdfFont.TimesRoman, 18);

                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/stamp-no-collision");
                var text = TestHelper.GetPdfText(bytes);
                Assert.Contains("/Stamp1 Do", text);

                var bitmap = TestHelper.RasterizePage(bytes, "Reading/stamp-no-collision");
                // Original text near top
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                    TestHelper.PtToPx(50), TestHelper.PtToPx(200),
                    TestHelper.PtToPx(95), TestHelper.PtToPx(120)));
                // Stamped text near bottom
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                    TestHelper.PtToPx(50), TestHelper.PtToPx(200),
                    TestHelper.PtToPx(695), TestHelper.PtToPx(720)));
            }
        }

        [Fact]
        public void Stamp_WithImage_RendersImage()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                TestHelper.AddDescription(page, "Verify: quadrant JPEG stamped onto imported page");
                var image = PdfImage.FromBytes(TestHelper.CreateQuadrantJpeg());
                page.DrawImage(image, 50, 600, 100, 100);

                var bytes = dest.ToArray();
                var bitmap = TestHelper.RasterizePage(bytes, "Reading/stamp-image");

                // Quadrant colors at the stamped location (top-left red, bottom-right yellow)
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(75), TestHelper.PtToPx(625), 255, 0, 0, 60);
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(125), TestHelper.PtToPx(675), 255, 255, 0, 60);
            }
        }

        [Fact]
        public void Stamp_MultiplePagesFromSameSource()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var p1 = dest.ImportPage(source, 1);
                var p2 = dest.ImportPage(source, 1);
                TestHelper.AddDescription(p1, "Verify: page 1 stamped blue");
                TestHelper.AddDescription(p2, "Verify: page 2 stamped green");
                p1.DrawFilledRectangle(50, 600, 100, 100, PdfColor.Rgb(0, 0, 255));
                p2.DrawFilledRectangle(50, 600, 100, 100, PdfColor.Rgb(0, 170, 0));

                var bytes = dest.ToArray();
                Assert.Equal(2, TestHelper.GetPageCount(bytes));
                var b1 = TestHelper.RasterizePage(bytes, "Reading/stamp-multi", 0);
                var b2 = TestHelper.RasterizePage(bytes, "Reading/stamp-multi", 1);
                TestHelper.AssertPixelColor(b1, TestHelper.PtToPx(100), TestHelper.PtToPx(650), 0, 0, 255);
                TestHelper.AssertPixelColor(b2, TestHelper.PtToPx(100), TestHelper.PtToPx(650), 0, 170, 0);
            }
        }

        [Fact]
        public void Stamp_SaveTwice_ProducesSameResult()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                TestHelper.AddDescription(page, "Verify: stamped page survives saving twice");
                page.DrawFilledRectangle(50, 600, 100, 100, PdfColor.Rgb(0, 0, 255));

                var first = dest.ToArray();
                var second = dest.ToArray();

                foreach (var bytes in new[] { first, second })
                {
                    Assert.Equal(1, TestHelper.GetPageCount(bytes));
                    var bitmap = TestHelper.RasterizePage(bytes, "Reading/stamp-save-twice");
                    TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(100), TestHelper.PtToPx(650), 0, 0, 255);
                    TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(300), TestHelper.PtToPx(400), 255, 0, 0);
                }
                // No stamp-name accumulation across saves
                Assert.DoesNotContain("/Stamp2", TestHelper.GetPdfText(second));
            }
        }

        // ── Real-world file ─────────────────────────────────────────

        [Fact]
        public void Stamp_OnGhentPage_Renders()
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "TestAssets", "Pdfs", "Ghent_PDF-Output-Test-V50_ALL_X4.pdf");
            using (var source = PdfReadDocument.Open(path))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                TestHelper.AddDescription(page, "Verify: magenta square + text stamped on Ghent page 1");
                page.DrawFilledRectangle(30, 30, 80, 80, PdfColor.Rgb(255, 0, 255));
                page.DrawText("STAMPED", 130, 60, PdfFont.HelveticaBold, 24, PdfColor.Rgb(255, 0, 255));

                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/stamp-ghent");
                var bitmap = TestHelper.RasterizePage(bytes, "Reading/stamp-ghent");
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(70), TestHelper.PtToPx(70), 255, 0, 255);

                // The original Ghent content must survive the stamp. Its /Contents is an
                // indirect reference to an ARRAY of streams — a regression here renders
                // the page blank except for the stamp.
                Assert.True(TestHelper.CountDarkPixelsInRegion(bitmap,
                    TestHelper.PtToPx(50), TestHelper.PtToPx(550),
                    TestHelper.PtToPx(300), TestHelper.PtToPx(700)) > 1000,
                    "Original Ghent page content disappeared after stamping");
            }
        }

        [Fact]
        public void Stamp_ContentsIsReferenceToArray_OriginalPreserved()
        {
            // /Contents -> indirect array of two streams (red square + green square)
            var sb = new StringBuilder();
            var offsets = new List<int>();
            void BeginObj(int num)
            {
                offsets.Add(sb.Length);
                sb.Append(num).Append(" 0 obj\n");
            }
            const string ops1 = "1 0 0 rg 50 600 100 100 re f";
            const string ops2 = "0 1 0 rg 200 600 100 100 re f";
            sb.Append("%PDF-1.4\n");
            BeginObj(1);
            sb.Append("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            BeginObj(2);
            sb.Append("<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
            BeginObj(3);
            sb.Append("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] " +
                "/Contents 4 0 R /Resources << >> >>\nendobj\n");
            BeginObj(4);
            sb.Append("[5 0 R 6 0 R]\nendobj\n");
            BeginObj(5);
            sb.Append($"<< /Length {ops1.Length} >>\nstream\n{ops1}\nendstream\nendobj\n");
            BeginObj(6);
            sb.Append($"<< /Length {ops2.Length} >>\nstream\n{ops2}\nendstream\nendobj\n");
            int xrefPos = sb.Length;
            sb.Append("xref\n0 7\n0000000000 65535 f \n");
            foreach (var off in offsets)
                sb.Append(off.ToString("D10")).Append(" 00000 n \n");
            sb.Append("trailer\n<< /Size 7 /Root 1 0 R >>\nstartxref\n")
              .Append(xrefPos).Append("\n%%EOF\n");
            var sourceBytes = Encoding.ASCII.GetBytes(sb.ToString());

            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                TestHelper.AddDescription(page, "Verify: red + green squares (source) and blue square (stamp)");
                page.DrawFilledRectangle(350, 92, 100, 100, PdfColor.Rgb(0, 0, 255));

                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/stamp-contents-array");
                var bitmap = TestHelper.RasterizePage(bytes, "Reading/stamp-contents-array");

                // Both original streams render (PDF-space y 600..700 = viewed y 92..192)
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(100), TestHelper.PtToPx(142), 255, 0, 0);
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(250), TestHelper.PtToPx(142), 0, 255, 0);
                // And the stamp
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(400), TestHelper.PtToPx(142), 0, 0, 255);
            }
        }

        // ── Geometry: offset MediaBox and /Rotate ───────────────────

        [Fact]
        public void Stamp_OffsetMediaBoxOrigin_PositionsCorrectly()
        {
            // MediaBox with a non-zero origin: viewed size is 295 x 421
            var sourceBytes = BuildPdfWithBoxAndRotation("[100 50 395 471]", null,
                "1 0 0 rg 100 50 50 50 re f");
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                Assert.Equal(295, page.Width, 1);
                Assert.Equal(421, page.Height, 1);
                TestHelper.AddDescription(page, "Verify: blue square at viewed top-left despite offset MediaBox");
                page.DrawFilledRectangle(10, 10, 50, 50, PdfColor.Rgb(0, 0, 255));

                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/stamp-offset-box");
                var bitmap = TestHelper.RasterizePage(bytes, "Reading/stamp-offset-box");

                // Stamped square at viewed (10..60, 10..60)
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(35), TestHelper.PtToPx(35), 0, 0, 255);
                // Original red square at the box origin corner = viewed bottom-left
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(25), TestHelper.PtToPx(421 - 25), 255, 0, 0);
            }
        }

        [Fact]
        public void Stamp_Rotate90_UprightAtExpectedPosition()
        {
            // Portrait Letter rotated 90° clockwise for display -> viewed landscape 792 x 612.
            // Source content: blue square at PDF-space origin corner.
            var sourceBytes = BuildPdfWithBoxAndRotation("[0 0 612 792]", 90,
                "0 0 1 rg 0 0 50 50 re f");
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                Assert.Equal(792, page.Width, 1);
                Assert.Equal(612, page.Height, 1);
                TestHelper.AddDescription(page, "Verify: rotated page - blue square top-left, red stamp at (200,100)");
                page.DrawFilledRectangle(200, 100, 100, 50, PdfColor.Rgb(255, 0, 0));

                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/stamp-rotate90");
                var bitmap = TestHelper.RasterizePage(bytes, "Reading/stamp-rotate90");

                // PDFium applies /Rotate: bitmap is landscape
                Assert.True(bitmap.Width > bitmap.Height);
                // Original blue square lands at the viewed top-left after rotation
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(25), TestHelper.PtToPx(25), 0, 0, 255);
                // Stamp at viewed (200..300, 100..150)
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(250), TestHelper.PtToPx(125), 255, 0, 0);
            }
        }

        [Fact]
        public void Stamp_Rotate270_UprightAtExpectedPosition()
        {
            var sourceBytes = BuildPdfWithBoxAndRotation("[0 0 612 792]", 270,
                "0 0 1 rg 0 0 50 50 re f");
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                Assert.Equal(792, page.Width, 1);
                TestHelper.AddDescription(page, "Verify: 270-rotated page - red stamp at (200,100)");
                page.DrawFilledRectangle(200, 100, 100, 50, PdfColor.Rgb(255, 0, 0));

                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/stamp-rotate270");
                var bitmap = TestHelper.RasterizePage(bytes, "Reading/stamp-rotate270");

                Assert.True(bitmap.Width > bitmap.Height);
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(250), TestHelper.PtToPx(125), 255, 0, 0);
                // Original blue square (PDF origin corner) lands at viewed bottom-right for 270
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(792 - 25), TestHelper.PtToPx(612 - 25), 0, 0, 255);
            }
        }

        // ── Annotations and form fields on imported pages ───────────

        [Fact]
        public void Annotations_OnImportedPage_Render()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                TestHelper.AddDescription(page, "Verify: highlight + note annotations added to imported page");
                page.AddMarkupAnnotation(200, 300, 200, 100,
                    MarkupAnnotationType.Highlight, PdfColor.Rgb(255, 255, 0));
                page.AddTextAnnotation(500, 50, "A note on an imported page", "Tester");

                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/stamp-annotations");
                var text = TestHelper.GetPdfText(bytes);
                Assert.Contains("/Highlight", text);
                Assert.Contains("/Text", text);

                var bitmap = TestHelper.RasterizePage(bytes, "Reading/stamp-annotations",
                    withAnnotations: true);
                // Highlight over the top part of the red rectangle: yellow tint over red
                // stays strongly red-dominant but no longer pure red; just require rendering
                // didn't fail and the note icon drew something near the top-right
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                    TestHelper.PtToPx(495), TestHelper.PtToPx(530),
                    TestHelper.PtToPx(45), TestHelper.PtToPx(80)));
            }
        }

        [Fact]
        public void FormField_OnImportedPage_RendersValue()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                TestHelper.AddDescription(page, "Verify: text field with value on imported page");
                page.AddTextField("name", 50, 600, 200, 30,
                    new TextFieldOptions { Value = "Hello imported" });

                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/stamp-formfield");
                var text = TestHelper.GetPdfText(bytes);
                Assert.Contains("/AcroForm", text);

                var bitmap = TestHelper.RasterizePage(bytes, "Reading/stamp-formfield",
                    withAnnotations: true, withFormFill: true);
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                    TestHelper.PtToPx(55), TestHelper.PtToPx(245),
                    TestHelper.PtToPx(605), TestHelper.PtToPx(625)));
            }
        }

        // ── Interop: encryption ─────────────────────────────────────

        [Fact]
        public void Stamp_ThenEncrypt_OpensAndShowsStamp()
        {
            var sourceBytes = MakeSourceDocument();
            using (var source = PdfReadDocument.Open(sourceBytes))
            {
                var dest = new PdfDocument();
                var page = dest.ImportPage(source, 1);
                TestHelper.AddDescription(page, "Verify: encrypted stamped page (password: secret)");
                page.DrawFilledRectangle(50, 600, 100, 100, PdfColor.Rgb(0, 0, 255));
                dest.Encryption = new PdfEncryptionOptions
                {
                    UserPassword = "secret",
                    Level = PdfEncryptionLevel.Aes128,
                };

                var bytes = dest.ToArray();
                TestHelper.SavePdf(bytes, "Reading/stamp-encrypted");
                var bitmap = PDFtoImage.Conversion.ToImage(bytes, page: 0, password: "secret",
                    options: new PDFtoImage.RenderOptions(Dpi: 150));
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(100), TestHelper.PtToPx(650), 0, 0, 255);
                TestHelper.AssertPixelColor(bitmap, TestHelper.PtToPx(300), TestHelper.PtToPx(400), 255, 0, 0);
            }
        }
    }
}
