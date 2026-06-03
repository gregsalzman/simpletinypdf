using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class CustomFontTests
    {
        private static readonly string FontPath =
            Path.Combine("TestAssets", "Roboto-Regular.ttf");

        // ── Font loading ─────────────────────────────────────────────

        [Fact]
        public void FromFile_LoadsFont()
        {
            var font = PdfFontSource.FromFile(FontPath);
            Assert.NotNull(font);
            Assert.False(font.IsBuiltIn);
        }

        [Fact]
        public void FromBytes_LoadsFont()
        {
            var data = File.ReadAllBytes(FontPath);
            var font = PdfFontSource.FromBytes(data);
            Assert.NotNull(font);
            Assert.False(font.IsBuiltIn);
        }

        [Fact]
        public void FromStream_LoadsFont()
        {
            using (var stream = File.OpenRead(FontPath))
            {
                var font = PdfFontSource.FromStream(stream);
                Assert.NotNull(font);
                Assert.False(font.IsBuiltIn);
            }
        }

        // ── Parser ───────────────────────────────────────────────────

        [Fact]
        public void Parser_ExtractsPostScriptName()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var name = font.CustomFont.PostScriptName;
            Assert.False(string.IsNullOrEmpty(name));
            // Roboto variable font PostScript name contains "Roboto"
            Assert.Contains("Roboto", name, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Parser_ExtractsUnitsPerEm()
        {
            var font = PdfFontSource.FromFile(FontPath);
            // Common values are 1000 or 2048
            Assert.True(font.CustomFont.UnitsPerEm == 1000 || font.CustomFont.UnitsPerEm == 2048,
                $"UnitsPerEm={font.CustomFont.UnitsPerEm}");
        }

        [Fact]
        public void Parser_ExtractsAscenderAndDescender()
        {
            var font = PdfFontSource.FromFile(FontPath);
            Assert.True(font.CustomFont.Ascender > 0, "Ascender should be positive");
            Assert.True(font.CustomFont.Descender < 0 || font.CustomFont.Descender == 0,
                "Descender should be negative or zero");
        }

        [Fact]
        public void Parser_CmapMapsAsciiCharacters()
        {
            var font = PdfFontSource.FromFile(FontPath);
            // 'A' (65) should map to a non-zero glyph ID
            int gid = font.CustomFont.GetGlyphId('A');
            Assert.True(gid > 0, "Glyph ID for 'A' should be non-zero");
        }

        [Fact]
        public void Parser_HmtxReturnsNonZeroWidths()
        {
            var font = PdfFontSource.FromFile(FontPath);
            int width = font.CustomFont.GetCharWidth('A');
            Assert.True(width > 0, $"Width for 'A' should be positive, got {width}");
        }

        [Fact]
        public void Parser_SpaceHasPositiveWidth()
        {
            var font = PdfFontSource.FromFile(FontPath);
            int width = font.CustomFont.GetCharWidth(' ');
            Assert.True(width > 0, $"Width for space should be positive, got {width}");
        }

        // ── Text measurement ────────────────────────────────────────

        [Fact]
        public void MeasureString_ReturnsPlausibleWidth()
        {
            var font = PdfFontSource.FromFile(FontPath);
            float width = FontMetrics.MeasureString("Hello World", font, 12f);
            Assert.True(width > 20f && width < 200f,
                $"MeasureString width={width} should be plausible for 'Hello World' at 12pt");
        }

        [Fact]
        public void MeasureString_ScalesWithFontSize()
        {
            var font = PdfFontSource.FromFile(FontPath);
            float w12 = FontMetrics.MeasureString("Test", font, 12f);
            float w24 = FontMetrics.MeasureString("Test", font, 24f);
            Assert.InRange(w24 / w12, 1.9f, 2.1f);
        }

        [Fact]
        public void WrapText_WrapsCorrectly()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var lines = FontMetrics.WrapText("The quick brown fox jumps over the lazy dog",
                font, 12f, 100f);
            Assert.True(lines.Count > 1, "Text should wrap into multiple lines at 100pt width");
        }

        // ── PdfFontSource equality ──────────────────────────────────

        [Fact]
        public void BuiltInFonts_EqualByValue()
        {
            PdfFontSource a = PdfFont.Helvetica;
            PdfFontSource b = PdfFont.Helvetica;
            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void CustomFonts_EqualByReference()
        {
            var font = PdfFontSource.FromFile(FontPath);
            Assert.Equal(font, font);

            // Different load = different instance = not equal
            var font2 = PdfFontSource.FromFile(FontPath);
            Assert.NotEqual(font, font2);
        }

        [Fact]
        public void ImplicitConversion_Works()
        {
            PdfFontSource fs = PdfFont.TimesRoman;
            Assert.True(fs.IsBuiltIn);
            Assert.Equal(PdfFont.TimesRoman, fs.BuiltInFont);
        }

        // ── PDF generation ──────────────────────────────────────────

        [Fact]
        public void DrawText_CustomFont_ProducesValidPdf()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: custom Roboto TTF font renders 'Hello, Custom Font!'");
            page.DrawText("Hello Custom Font", 50, 50, font, 24);
            var bytes = doc.ToArray();

            Assert.True(bytes.Length > 100, "PDF should have content");
            // Verify PDF header
            var header = Encoding.ASCII.GetString(bytes, 0, 5);
            Assert.Equal("%PDF-", header);

            TestHelper.SavePdf(bytes, "Fonts/custom-hello-world-roboto");
        }

        [Fact]
        public void DrawText_CustomFont_RendersVisibleText()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: custom font text is visually present on page");
            page.DrawText("Hello Custom Font", 50, 50, font, 24);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Fonts/custom-font-visible-text");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/custom-font-visible-text");
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), textY, textY + TestHelper.PtToPx(24)),
                "Expected visible text rendered with custom font");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_CustomFont_EmbedsFontFile()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Test", 50, 50, font, 12);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.Contains("/Subtype /Type0", pdfText);
            Assert.Contains("/FontDescriptor", pdfText);
            Assert.Contains("/FontFile2", pdfText);
        }

        [Fact]
        public void DrawText_MixedBuiltInAndCustom_BothRender()
        {
            var customFont = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: custom and built-in fonts render together on same page");
            page.DrawText("Built-in Helvetica", 50, 50, PdfFont.Helvetica, 18);
            page.DrawText("Custom Roboto", 50, 80, customFont, 18);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Fonts/custom-and-builtin-mixed");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/custom-and-builtin-mixed");

            int y1 = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), y1, y1 + TestHelper.PtToPx(18)),
                "Expected visible built-in font text");

            int y2 = TestHelper.PtToPx(80);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), y2, y2 + TestHelper.PtToPx(18)),
                "Expected visible custom font text");
            bitmap.Dispose();
        }

        [Fact]
        public void TextSpan_CustomFont_RichTextRenders()
        {
            var customFont = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: custom font works in TextSpan rich text");

            var spans = new[]
            {
                new TextSpan("Hello ", PdfFont.HelveticaBold, 14f, PdfColor.Red),
                new TextSpan("World", customFont, 14f, PdfColor.Blue),
            };
            page.DrawText(spans, 50, 50);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Fonts/custom-font-richtext-spans");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/custom-font-richtext-spans");
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), textY, textY + TestHelper.PtToPx(14)),
                "Expected visible rich text with custom font");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_CustomFont_WrappedText()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: custom font text wraps correctly in textbox");
            page.DrawText(
                "The quick brown fox jumps over the lazy dog. This is a longer text to test word wrapping.",
                50, 50, font, 12, width: 200);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Fonts/custom-font-wrapped-text");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/custom-font-wrapped-text");
            // Should have multiple lines
            int y1 = TestHelper.PtToPx(50);
            int y2 = TestHelper.PtToPx(70); // Second line area
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(250), y1, y1 + TestHelper.PtToPx(12)),
                "Expected visible wrapped text line 1");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(250), y2, y2 + TestHelper.PtToPx(12)),
                "Expected visible wrapped text line 2+");
            bitmap.Dispose();
        }

        [Fact]
        public void PdfTable_CustomFont_Works()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: custom font renders correctly in table cells");

            var table = new PdfTable(100, 200);
            table.HeaderFont = font;
            table.CellFont = font;
            table.SetHeaders("Name", "Value");
            table.AddRow("Key", "123");
            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/custom-font-in-table");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/custom-font-in-table");
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(350), textY, textY + TestHelper.PtToPx(40)),
                "Expected visible table with custom font");
            bitmap.Dispose();
        }

        [Fact]
        public void CustomFont_SameInstanceAcrossPages_DeduplicatesFontStream()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page1 = doc.AddPage(PageSize.A4);
            page1.DrawText("Page 1", 50, 50, font, 12);
            var page2 = doc.AddPage(PageSize.A4);
            page2.DrawText("Page 2", 50, 50, font, 12);

            var bytes = doc.ToArray();
            var pdfText = Encoding.ASCII.GetString(bytes);

            // Should have only one /FontFile2 entry (deduplicated)
            int fontFileCount = 0;
            int idx = 0;
            while ((idx = pdfText.IndexOf("/FontFile2", idx)) >= 0)
            {
                fontFileCount++;
                idx += 10;
            }
            Assert.Equal(1, fontFileCount);
        }

        [Fact]
        public void MeasureText_CustomFont_ViaPage()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float width = page.MeasureText("Hello", font, 12f);
            Assert.True(width > 10f && width < 100f,
                $"MeasureText via PdfPage should return plausible width, got {width}");
        }

        // ── Backward compatibility ──────────────────────────────────

        [Fact]
        public void ExistingApi_EnumStillWorks()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            // All these should compile and work with implicit conversion
            page.DrawText("Hello", 50, 50, PdfFont.Helvetica, 12f);
            page.DrawText("Bold", 50, 70, PdfFont.HelveticaBold, 12f);
            float w = page.MeasureText("test", PdfFont.Helvetica, 12f);
            Assert.True(w > 0);

            var span = new TextSpan("span text", PdfFont.TimesRoman, 10f);
            Assert.Equal(PdfFont.TimesRoman, span.Font.BuiltInFont);

            var bytes = doc.ToArray();
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void TextSpan_DefaultFont_IsHelvetica()
        {
            var span = new TextSpan("test");
            Assert.True(span.Font.IsBuiltIn);
            Assert.Equal(PdfFont.Helvetica, span.Font.BuiltInFont);
        }

        // ── Multiple TTF fonts ──────────────────────────────────────

        [Theory]
        [InlineData("Roboto-Regular.ttf")]
        [InlineData("OpenSans-Regular.ttf")]
        [InlineData("OpenSans-Bold.ttf")]
        [InlineData("OpenSans-Italic.ttf")]
        [InlineData("Inconsolata-Regular.ttf")]
        public void TtfFont_LoadsAndParsesCorrectly(string filename)
        {
            var path = Path.Combine("TestAssets", filename);
            var font = PdfFontSource.FromFile(path);
            var ttf = font.CustomFont;

            Assert.False(font.IsBuiltIn);
            Assert.False(ttf.IsCff);
            Assert.False(string.IsNullOrEmpty(ttf.PostScriptName));
            Assert.True(ttf.UnitsPerEm > 0, $"UnitsPerEm={ttf.UnitsPerEm}");
            Assert.True(ttf.Ascender > 0, $"Ascender={ttf.Ascender}");
            Assert.True(ttf.GetGlyphId('A') > 0, "Glyph ID for 'A' should be non-zero");
            Assert.True(ttf.GetCharWidth('A') > 0, "Width for 'A' should be positive");
            Assert.True(ttf.GetCharWidth(' ') > 0, "Width for space should be positive");
        }

        [Theory]
        [InlineData("Roboto-Regular.ttf")]
        [InlineData("OpenSans-Regular.ttf")]
        [InlineData("OpenSans-Bold.ttf")]
        [InlineData("OpenSans-Italic.ttf")]
        [InlineData("Inconsolata-Regular.ttf")]
        public void TtfFont_RendersVisibleText(string filename)
        {
            var path = Path.Combine("TestAssets", filename);
            var font = PdfFontSource.FromFile(path);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var name = Path.GetFileNameWithoutExtension(filename);
            TestHelper.AddDescription(page, $"Verify: TTF font {name} renders visible text");
            page.DrawText($"Hello from {filename}", 50, 50, font, 18);
            var bytes = doc.ToArray();

            var testName = $"Fonts/ttf-{name}-hello";
            TestHelper.SavePdf(bytes, testName);
            var bitmap = TestHelper.RasterizePage(bytes, testName);
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(350), textY, textY + TestHelper.PtToPx(18)),
                $"Expected visible text for {filename}");
            bitmap.Dispose();
        }

        [Theory]
        [InlineData("Roboto-Regular.ttf")]
        [InlineData("OpenSans-Regular.ttf")]
        [InlineData("Inconsolata-Regular.ttf")]
        public void TtfFont_EmbedsFontFile2(string filename)
        {
            var path = Path.Combine("TestAssets", filename);
            var font = PdfFontSource.FromFile(path);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Test", 50, 50, font, 12);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.Contains("/Subtype /Type0", pdfText);
            Assert.Contains("/FontFile2", pdfText);
            // Subset fonts use a tag prefix (e.g. ABCDEF+FontName)
            Assert.Contains(font.CustomFont.PostScriptName, pdfText);
        }

        // ── OTF (CFF) fonts ────────────────────────────────────────

        [Theory]
        [InlineData("SourceCodePro-Regular.otf")]
        [InlineData("SourceCodePro-Bold.otf")]
        [InlineData("SourceSerifPro-Regular.otf")]
        public void OtfFont_LoadsAndParsesCorrectly(string filename)
        {
            var path = Path.Combine("TestAssets", filename);
            var font = PdfFontSource.FromFile(path);
            var ttf = font.CustomFont;

            Assert.False(font.IsBuiltIn);
            Assert.True(ttf.IsCff, $"{filename} should be detected as CFF/OpenType");
            Assert.False(string.IsNullOrEmpty(ttf.PostScriptName));
            Assert.True(ttf.UnitsPerEm > 0, $"UnitsPerEm={ttf.UnitsPerEm}");
            Assert.True(ttf.Ascender > 0, $"Ascender={ttf.Ascender}");
            Assert.True(ttf.GetGlyphId('A') > 0, "Glyph ID for 'A' should be non-zero");
            Assert.True(ttf.GetCharWidth('A') > 0, "Width for 'A' should be positive");
        }

        [Theory]
        [InlineData("SourceCodePro-Regular.otf")]
        [InlineData("SourceCodePro-Bold.otf")]
        [InlineData("SourceSerifPro-Regular.otf")]
        public void OtfFont_RendersVisibleText(string filename)
        {
            var path = Path.Combine("TestAssets", filename);
            var font = PdfFontSource.FromFile(path);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var name = Path.GetFileNameWithoutExtension(filename);
            TestHelper.AddDescription(page, $"Verify: OTF font {name} renders visible text");
            page.DrawText($"Hello from {filename}", 50, 50, font, 18);
            var bytes = doc.ToArray();

            var testName = $"Fonts/otf-{name}-hello";
            TestHelper.SavePdf(bytes, testName);
            var bitmap = TestHelper.RasterizePage(bytes, testName);
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(350), textY, textY + TestHelper.PtToPx(18)),
                $"Expected visible text for {filename}");
            bitmap.Dispose();
        }

        [Theory]
        [InlineData("SourceCodePro-Regular.otf")]
        [InlineData("SourceSerifPro-Regular.otf")]
        public void OtfFont_EmbedsFontFile3(string filename)
        {
            var path = Path.Combine("TestAssets", filename);
            var font = PdfFontSource.FromFile(path);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Test", 50, 50, font, 12);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.Contains("/FontFile3", pdfText);
            Assert.Contains("/Subtype /OpenType", pdfText);
            Assert.Contains("/" + font.CustomFont.PostScriptName, pdfText);
        }

        // ── Monospace detection ─────────────────────────────────────

        [Fact]
        public void Inconsolata_IsFixedPitch()
        {
            var font = PdfFontSource.FromFile(Path.Combine("TestAssets", "Inconsolata-Regular.ttf"));
            Assert.True(font.CustomFont.IsFixedPitch, "Inconsolata should be detected as fixed-pitch");
            // All printable ASCII should have the same width
            int wA = font.CustomFont.GetCharWidth('A');
            int wM = font.CustomFont.GetCharWidth('M');
            int wi = font.CustomFont.GetCharWidth('i');
            Assert.Equal(wA, wM);
            Assert.Equal(wA, wi);
        }

        [Fact]
        public void SourceCodePro_IsFixedPitch()
        {
            var font = PdfFontSource.FromFile(Path.Combine("TestAssets", "SourceCodePro-Regular.otf"));
            Assert.True(font.CustomFont.IsFixedPitch, "Source Code Pro should be detected as fixed-pitch");
        }

        [Fact]
        public void OpenSans_IsNotFixedPitch()
        {
            var font = PdfFontSource.FromFile(Path.Combine("TestAssets", "OpenSans-Regular.ttf"));
            Assert.False(font.CustomFont.IsFixedPitch, "Open Sans should not be fixed-pitch");
        }

        // ── Bold vs regular width differences ───────────────────────

        [Fact]
        public void OpenSans_BoldIsWiderThanRegular()
        {
            var regular = PdfFontSource.FromFile(Path.Combine("TestAssets", "OpenSans-Regular.ttf"));
            var bold = PdfFontSource.FromFile(Path.Combine("TestAssets", "OpenSans-Bold.ttf"));
            float wRegular = FontMetrics.MeasureString("Hello World", regular, 12f);
            float wBold = FontMetrics.MeasureString("Hello World", bold, 12f);
            Assert.True(wBold > wRegular,
                $"Bold ({wBold:F1}) should be wider than regular ({wRegular:F1})");
        }

        // ── Underline with custom fonts ─────────────────────────────

        [Fact]
        public void DrawText_CustomFont_Underline_ProducesUnderlineRect()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: underline renders under custom font text");
            page.DrawText("Underlined custom font", 50, 50, font, 14, underline: true);
            var bytes = doc.ToArray();

            // Underline is rendered as a filled rectangle (re f) in the content stream
            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.Contains("re f", pdfText);

            TestHelper.SavePdf(bytes, "Fonts/custom-font-underline");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/custom-font-underline");
            Assert.True(bitmap.Width > 0);
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_CustomFont_Underline_RendersVisibleUnderline()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: custom font underline is visually present");
            page.DrawText("Underlined text", 50, 50, font, 18, underline: true);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Fonts/custom-font-underline-visible");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/custom-font-underline-visible");

            // Check text area has dark pixels
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(250), textY, textY + TestHelper.PtToPx(18)),
                "Expected visible underlined text");

            // Check underline region (just below the text baseline)
            int underlineY = TestHelper.PtToPx(50) + TestHelper.PtToPx(18);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(200), underlineY, underlineY + TestHelper.PtToPx(4)),
                "Expected visible underline below text");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_CustomFont_Underline_WithColor()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: custom font underline renders in blue color");
            page.DrawText("Blue underline", 50, 50, font, 14, PdfColor.Blue, underline: true);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Fonts/custom-font-underline-blue");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/custom-font-underline-blue");
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(200), textY, textY + TestHelper.PtToPx(18)),
                "Expected visible blue underlined text");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_CustomFont_WrappedUnderline()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: underlined custom font text wraps correctly");
            page.DrawText(
                "This is a longer underlined text that should wrap across multiple lines in the box.",
                50, 50, font, 12, underline: true, width: 150);
            var bytes = doc.ToArray();

            // Multiple underline rectangles for wrapped lines
            var pdfText = Encoding.ASCII.GetString(bytes);
            int reCount = 0;
            int idx = 0;
            while ((idx = pdfText.IndexOf("re f", idx)) >= 0) { reCount++; idx += 4; }
            Assert.True(reCount > 1, $"Expected multiple underline rects for wrapped text, got {reCount}");

            TestHelper.SavePdf(bytes, "Fonts/custom-font-underline-wrapped");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/custom-font-underline-wrapped");
            Assert.True(bitmap.Width > 0);
            bitmap.Dispose();
        }

        [Fact]
        public void RichText_CustomFont_Underline_SpanLevel()
        {
            var customFont = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: underline works with custom font in rich text");
            page.DrawText(new[]
            {
                new TextSpan("Normal ", PdfFont.Helvetica, 14),
                new TextSpan("Underlined custom", customFont, 14, PdfColor.Red, underline: true),
                new TextSpan(" not underlined", customFont, 14),
            }, 50, 50);
            var bytes = doc.ToArray();

            // Only the middle span should produce an underline rect
            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.Contains("re f", pdfText);

            TestHelper.SavePdf(bytes, "Fonts/custom-font-underline-richtext");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/custom-font-underline-richtext");
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(400), textY, textY + TestHelper.PtToPx(14)),
                "Expected visible rich text with underlined custom font span");
            bitmap.Dispose();
        }

        [Fact]
        public void RichText_CustomFont_WrappedUnderline()
        {
            var customFont = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: underlined custom font rich text wraps correctly");
            page.DrawText(new[]
            {
                new TextSpan("This underlined custom font text should wrap across lines in the box",
                    customFont, 12, PdfColor.Navy, underline: true),
            }, 50, 50, width: 180);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "Fonts/custom-font-underline-richtext-wrap");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/custom-font-underline-richtext-wrap");
            // Multiple lines should be visible
            int y1 = TestHelper.PtToPx(50);
            int y2 = TestHelper.PtToPx(65);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(230), y1, y1 + TestHelper.PtToPx(12)),
                "Expected visible wrapped underlined text line 1");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(230), y2, y2 + TestHelper.PtToPx(12)),
                "Expected visible wrapped underlined text line 2");
            bitmap.Dispose();
        }

        [Theory]
        [InlineData("OpenSans-Bold.ttf")]
        [InlineData("OpenSans-Italic.ttf")]
        [InlineData("SourceCodePro-Regular.otf")]
        [InlineData("SourceSerifPro-Regular.otf")]
        public void Underline_MultipleCustomFonts_AllRender(string filename)
        {
            var path = Path.Combine("TestAssets", filename);
            var font = PdfFontSource.FromFile(path);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var name = Path.GetFileNameWithoutExtension(filename);
            TestHelper.AddDescription(page, $"Verify: underline renders for font {name}");
            page.DrawText($"Underlined {filename}", 50, 50, font, 16, underline: true);
            var bytes = doc.ToArray();

            var testName = $"Fonts/underline-{name}";
            TestHelper.SavePdf(bytes, testName);
            var bitmap = TestHelper.RasterizePage(bytes, testName);
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(350), textY, textY + TestHelper.PtToPx(16)),
                $"Expected visible underlined text for {filename}");
            bitmap.Dispose();
        }

        // ── Multi-font PDF showcase ─────────────────────────────────

        [Fact]
        public void AllCustomFonts_OnOnePage_RenderCorrectly()
        {
            var fonts = new (string file, string label)[]
            {
                ("Roboto-Regular.ttf", "Roboto Regular"),
                ("OpenSans-Regular.ttf", "Open Sans Regular"),
                ("OpenSans-Bold.ttf", "Open Sans Bold"),
                ("OpenSans-Italic.ttf", "Open Sans Italic"),
                ("Inconsolata-Regular.ttf", "Inconsolata Regular"),
                ("SourceCodePro-Regular.otf", "Source Code Pro Regular"),
                ("SourceCodePro-Bold.otf", "Source Code Pro Bold"),
                ("SourceSerifPro-Regular.otf", "Source Serif 4 Regular"),
            };

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: all custom TTF and OTF fonts render correctly");
            float y = 40;

            foreach (var (file, label) in fonts)
            {
                var font = PdfFontSource.FromFile(Path.Combine("TestAssets", file));
                page.DrawText($"{label}: The quick brown fox jumps", 50, y, font, 14);
                y += 24;
            }

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/all-custom-fonts-showcase");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/all-custom-fonts-showcase");

            // Check each line rendered
            float checkY = 40;
            for (int i = 0; i < fonts.Length; i++)
            {
                int py = TestHelper.PtToPx(checkY);
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(500), py, py + TestHelper.PtToPx(14)),
                    $"Expected visible text for {fonts[i].label} at Y={checkY}");
                checkY += 24;
            }
            bitmap.Dispose();
        }

        [Fact]
        public void RichText_MixedCustomFonts()
        {
            var openSans = PdfFontSource.FromFile(Path.Combine("TestAssets", "OpenSans-Regular.ttf"));
            var sourceCode = PdfFontSource.FromFile(Path.Combine("TestAssets", "SourceCodePro-Regular.otf"));
            var sourceSerif = PdfFontSource.FromFile(Path.Combine("TestAssets", "SourceSerifPro-Regular.otf"));

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: multiple custom fonts render inline in rich text");
            var spans = new[]
            {
                new TextSpan("Sans: Hello ", openSans, 14f, PdfColor.Black),
                new TextSpan("Mono: World ", sourceCode, 14f, PdfColor.Blue),
                new TextSpan("Serif: End", sourceSerif, 14f, PdfColor.Red),
            };
            page.DrawText(spans, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/richtext-multiple-custom-fonts");
            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/richtext-multiple-custom-fonts");
            int textY = TestHelper.PtToPx(50);
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(450), textY, textY + TestHelper.PtToPx(14)),
                "Expected visible rich text with multiple custom fonts");
            bitmap.Dispose();
        }
    }
}
