using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class FontSubsettingTests
    {
        private static readonly string FontPath =
            Path.Combine("TestAssets", "Roboto-Regular.ttf");

        // ── Size reduction ──────────────────────────────────────────

        [Fact]
        public void SubsetFont_ProducesSmallerPdf()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Hi", 50, 50, font, 12);
            var pdfBytes = doc.ToArray();

            var fontFileSize = new FileInfo(FontPath).Length;
            Assert.True(pdfBytes.Length < fontFileSize,
                $"Subset PDF ({pdfBytes.Length}) should be smaller than full font file ({fontFileSize})");
        }

        [Fact]
        public void SubsetFont_CjkFont_DramaticSizeReduction()
        {
            var cjkPath = Path.Combine("TestAssets", "NotoSansJP-Regular.ttf");
            if (!File.Exists(cjkPath)) return;

            var font = PdfFontSource.FromFile(cjkPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("\u3053\u3093\u306B\u3061\u306F", 50, 50, font, 24);
            var bytes = doc.ToArray();

            // NotoSansJP is ~16MB; subset PDF with 5 chars should be much smaller
            Assert.True(bytes.Length < 1_000_000,
                $"CJK subset PDF ({bytes.Length} bytes) should be well under 1MB");
        }

        [Fact]
        public void SubsetFont_CjkFont_RendersAndIsSmall()
        {
            var cjkPath = Path.Combine("TestAssets", "NotoSansJP-Regular.ttf");
            if (!File.Exists(cjkPath)) return;

            var fullFontSize = new FileInfo(cjkPath).Length;
            var font = PdfFontSource.FromFile(cjkPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: CJK font subsetting dramatically reduces PDF size");

            // Mix of hiragana, katakana, kanji, and Latin
            page.DrawText("\u6771\u4EAC\u90FD Tokyo", 50, 50, font, 28);          // 東京都 Tokyo
            page.DrawText("\u304A\u306F\u3088\u3046\u3054\u3056\u3044\u307E\u3059", 50, 90, font, 20); // おはようございます
            page.DrawText("\u30B3\u30FC\u30D2\u30FC", 50, 120, font, 20);          // コーヒー

            var bytes = doc.ToArray();

            // Verify dramatic size reduction vs full 16MB font
            Assert.True(bytes.Length < fullFontSize / 10,
                $"Subset PDF ({bytes.Length}) should be <10% of full font ({fullFontSize})");

            // Verify all three lines render
            TestHelper.SavePdf(bytes, "Fonts/subset-cjk-large-reduction");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/subset-cjk-large-reduction");

            int y1 = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), y1, y1 + TestHelper.PtToPx(28)),
                "Kanji/Latin line should render");

            int y2 = TestHelper.PtToPx(90);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), y2, y2 + TestHelper.PtToPx(20)),
                "Hiragana line should render");

            int y3 = TestHelper.PtToPx(120);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(200), y3, y3 + TestHelper.PtToPx(20)),
                "Katakana line should render");

            bitmap.Dispose();
        }

        // ── Subset tag naming ───────────────────────────────────────

        [Fact]
        public void SubsetTag_IsSixUppercaseLetters()
        {
            var tag = TrueTypeSubsetter.GenerateSubsetTag();
            Assert.Equal(6, tag.Length);
            Assert.Matches("^[A-Z]{6}$", tag);
        }

        [Fact]
        public void SubsetFont_PdfContainsSubsetTagPrefix()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Test", 50, 50, font, 12);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            // Should contain a subset tag like /ABCDEF+FontName
            Assert.Matches(new Regex(@"/[A-Z]{6}\+"), pdfText);
        }

        [Fact]
        public void SubsetFont_CffFont_NoSubsetTag()
        {
            var font = PdfFontSource.FromFile(Path.Combine("TestAssets", "SourceCodePro-Regular.otf"));
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Test", 50, 50, font, 12);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            // CFF fonts should not be subsetted — no tag prefix
            Assert.Contains("/FontFile3", pdfText);
            Assert.Contains("/" + font.CustomFont.PostScriptName, pdfText);
        }

        // ── Visual regression ───────────────────────────────────────

        [Theory]
        [InlineData("Roboto-Regular.ttf")]
        [InlineData("OpenSans-Regular.ttf")]
        [InlineData("Inconsolata-Regular.ttf")]
        public void SubsetFont_RendersVisibleText(string filename)
        {
            var path = Path.Combine("TestAssets", filename);
            var font = PdfFontSource.FromFile(path);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var name = Path.GetFileNameWithoutExtension(filename);
            TestHelper.AddDescription(page, $"Verify: subset font {name} renders visible text");
            page.DrawText("Hello World!", 50, 50, font, 18);
            var bytes = doc.ToArray();

            var testName = $"Fonts/subset-{name}";
            TestHelper.SavePdf(bytes, testName);
            var bitmap = TestHelper.RasterizePage(bytes, testName);
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), textY, textY + TestHelper.PtToPx(18)),
                $"Subset font {filename} should render visible text");
            bitmap.Dispose();
        }

        [Fact]
        public void SubsetFont_AccentedCharacters_CompositeGlyphsPreserved()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: composite glyphs (accented chars) preserved after subsetting");
            page.DrawText("\u00E9\u00E8\u00EA\u00EB", 50, 50, font, 24);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Fonts/subset-composite-glyphs");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/subset-composite-glyphs");
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(200), textY, textY + TestHelper.PtToPx(24)),
                "Composite (accented) characters should render correctly with subsetting");
            bitmap.Dispose();
        }

        // ── Deduplication ───────────────────────────────────────────

        [Fact]
        public void SubsetFont_MultiPage_SingleFontStream()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page1 = doc.AddPage(PageSize.A4);
            page1.DrawText("Page 1", 50, 50, font, 12);
            var page2 = doc.AddPage(PageSize.A4);
            page2.DrawText("Page 2", 50, 50, font, 12);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            int fontFileCount = 0;
            int idx = 0;
            while ((idx = pdfText.IndexOf("/FontFile2", idx)) >= 0)
            {
                fontFileCount++;
                idx += 10;
            }
            Assert.Equal(1, fontFileCount);
        }

        // ── Opt-out ───────────────────────────────────────────────────

        [Fact]
        public void SubsetFont_DisabledViaProperty_EmbedsFullFont()
        {
            var font = PdfFontSource.FromFile(FontPath);
            font.Subset = false;

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Hi", 50, 50, font, 12);
            var fullBytes = doc.ToArray();

            // Compare with subsetted version
            var fontSubset = PdfFontSource.FromFile(FontPath);
            var doc2 = new PdfDocument();
            var page2 = doc2.AddPage(PageSize.A4);
            page2.DrawText("Hi", 50, 50, fontSubset, 12);
            var subsetBytes = doc2.ToArray();

            Assert.True(fullBytes.Length > subsetBytes.Length,
                $"Full embed ({fullBytes.Length}) should be larger than subset ({subsetBytes.Length})");

            // Full embed should not have a subset tag prefix
            var pdfText = System.Text.Encoding.ASCII.GetString(fullBytes);
            Assert.Contains("/" + font.CustomFont.PostScriptName, pdfText);
            Assert.DoesNotMatch(new System.Text.RegularExpressions.Regex(@"/[A-Z]{6}\+"), pdfText);
        }

        [Fact]
        public void SubsetFont_DisabledViaProperty_StillRendersCorrectly()
        {
            var font = PdfFontSource.FromFile(FontPath);
            font.Subset = false;

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: full font embedded when subsetting disabled");
            page.DrawText("Hello World!", 50, 50, font, 18);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Fonts/no-subset-full-font");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/no-subset-full-font");
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), textY, textY + TestHelper.PtToPx(18)),
                "Full-embed font should render visible text");
            bitmap.Dispose();
        }

        // ── Encryption combination ──────────────────────────────────

        [Fact]
        public void SubsetFont_WithEncryption_ProducesValidPdf()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            doc.Encryption = new PdfEncryptionOptions { UserPassword = "test" };
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Encrypted subset", 50, 50, font, 18);
            var bytes = doc.ToArray();
            Assert.True(bytes.Length > 100);
        }
    }
}
