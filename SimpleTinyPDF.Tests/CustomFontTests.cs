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

        private static int PtToPx(float pt, int dpi = 150) => (int)(pt * dpi / 72.0);

        private static bool HasDarkPixelsInRegion(SkiaSharp.SKBitmap bitmap,
            int xMin, int xMax, int yMin, int yMax)
        {
            xMax = System.Math.Min(xMax, bitmap.Width - 1);
            yMax = System.Math.Min(yMax, bitmap.Height - 1);
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Red < 200 || p.Green < 200 || p.Blue < 200)
                        return true;
                }
            return false;
        }

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
            page.DrawText("Hello Custom Font", 50, 50, font, 24);
            var bytes = doc.ToArray();

            Assert.True(bytes.Length > 100, "PDF should have content");
            // Verify PDF header
            var header = Encoding.ASCII.GetString(bytes, 0, 5);
            Assert.Equal("%PDF-", header);

            TestHelper.SavePdf(bytes, "custom_font_hello");
        }

        [Fact]
        public void DrawText_CustomFont_RendersVisibleText()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Hello Custom Font", 50, 50, font, 24);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "custom_font_visible");
            var bitmap = TestHelper.RasterizePage(bytes, "custom_font_visible");
            int textY = PtToPx(50);
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), textY, textY + PtToPx(24)),
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
            page.DrawText("Built-in Helvetica", 50, 50, PdfFont.Helvetica, 18);
            page.DrawText("Custom Roboto", 50, 80, customFont, 18);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "custom_font_mixed");
            var bitmap = TestHelper.RasterizePage(bytes, "custom_font_mixed");

            int y1 = PtToPx(50);
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), y1, y1 + PtToPx(18)),
                "Expected visible built-in font text");

            int y2 = PtToPx(80);
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), y2, y2 + PtToPx(18)),
                "Expected visible custom font text");
            bitmap.Dispose();
        }

        [Fact]
        public void TextSpan_CustomFont_RichTextRenders()
        {
            var customFont = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            var spans = new[]
            {
                new TextSpan("Hello ", PdfFont.HelveticaBold, 14f, PdfColor.Red),
                new TextSpan("World", customFont, 14f, PdfColor.Blue),
            };
            page.DrawText(spans, 50, 50);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "custom_font_richtext");
            var bitmap = TestHelper.RasterizePage(bytes, "custom_font_richtext");
            int textY = PtToPx(50);
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), textY, textY + PtToPx(14)),
                "Expected visible rich text with custom font");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_CustomFont_WrappedText()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(
                "The quick brown fox jumps over the lazy dog. This is a longer text to test word wrapping.",
                50, 50, font, 12, width: 200);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "custom_font_wrap");
            var bitmap = TestHelper.RasterizePage(bytes, "custom_font_wrap");
            // Should have multiple lines
            int y1 = PtToPx(50);
            int y2 = PtToPx(70); // Second line area
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250), y1, y1 + PtToPx(12)),
                "Expected visible wrapped text line 1");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250), y2, y2 + PtToPx(12)),
                "Expected visible wrapped text line 2+");
            bitmap.Dispose();
        }

        [Fact]
        public void PdfTable_CustomFont_Works()
        {
            var font = PdfFontSource.FromFile(FontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            var table = new PdfTable(100, 200);
            table.HeaderFont = font;
            table.CellFont = font;
            table.SetHeaders("Name", "Value");
            table.AddRow("Key", "123");
            page.DrawTable(table, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "custom_font_table");
            var bitmap = TestHelper.RasterizePage(bytes, "custom_font_table");
            int textY = PtToPx(50);
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(350), textY, textY + PtToPx(40)),
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
            page.DrawText($"Hello from {filename}", 50, 50, font, 18);
            var bytes = doc.ToArray();

            var testName = $"ttf_{Path.GetFileNameWithoutExtension(filename)}";
            TestHelper.SavePdf(bytes, testName);
            var bitmap = TestHelper.RasterizePage(bytes, testName);
            int textY = PtToPx(50);
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(350), textY, textY + PtToPx(18)),
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
            Assert.Contains("/" + font.CustomFont.PostScriptName, pdfText);
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
            page.DrawText($"Hello from {filename}", 50, 50, font, 18);
            var bytes = doc.ToArray();

            var testName = $"otf_{Path.GetFileNameWithoutExtension(filename)}";
            TestHelper.SavePdf(bytes, testName);
            var bitmap = TestHelper.RasterizePage(bytes, testName);
            int textY = PtToPx(50);
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(350), textY, textY + PtToPx(18)),
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
            float y = 40;

            foreach (var (file, label) in fonts)
            {
                var font = PdfFontSource.FromFile(Path.Combine("TestAssets", file));
                page.DrawText($"{label}: The quick brown fox jumps", 50, y, font, 14);
                y += 24;
            }

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "all_custom_fonts");
            var bitmap = TestHelper.RasterizePage(bytes, "all_custom_fonts");

            // Check each line rendered
            float checkY = 40;
            for (int i = 0; i < fonts.Length; i++)
            {
                int py = PtToPx(checkY);
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(500), py, py + PtToPx(14)),
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
            var spans = new[]
            {
                new TextSpan("Sans: Hello ", openSans, 14f, PdfColor.Black),
                new TextSpan("Mono: World ", sourceCode, 14f, PdfColor.Blue),
                new TextSpan("Serif: End", sourceSerif, 14f, PdfColor.Red),
            };
            page.DrawText(spans, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtext_multi_custom");
            var bitmap = TestHelper.RasterizePage(bytes, "richtext_multi_custom");
            int textY = PtToPx(50);
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(450), textY, textY + PtToPx(14)),
                "Expected visible rich text with multiple custom fonts");
            bitmap.Dispose();
        }
    }
}
