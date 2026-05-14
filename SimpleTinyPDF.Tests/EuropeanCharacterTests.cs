using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class EuropeanCharacterTests
    {
        // ── GlyphMapping coverage ──────────────────────────────────

        [Fact]
        public void GlyphMapping_AllGlyphNames_DeriveToBasicLatin()
        {
            foreach (var kv in GlyphMapping.UnicodeToGlyphName)
            {
                char baseChar = (kv.Value == "dotlessi") ? 'i' : kv.Value[0];
                Assert.True(baseChar >= 'A' && baseChar <= 'z',
                    $"Derived base char for U+{(int)kv.Key:X4} ({kv.Value}) is '{baseChar}' — expected basic Latin letter");
            }
        }

        // ── EncodingExtension ──────────────────────────────────────

        [Fact]
        public void EncodingExtension_InitialState_HasNoExtensions()
        {
            var ext = new EncodingExtension();
            Assert.False(ext.HasExtensions);
            Assert.Equal(0, ext.UsedSlots);
        }

        [Fact]
        public void EncodingExtension_TryEncode_AssignsSlot()
        {
            var ext = new EncodingExtension();
            Assert.True(ext.TryEncode('\u0105', out byte code)); // ą
            Assert.True(ext.HasExtensions);
            Assert.Equal(1, ext.UsedSlots);
            Assert.Equal(1, code); // First available slot is byte 1
        }

        [Fact]
        public void EncodingExtension_SameChar_ReturnsSameSlot()
        {
            var ext = new EncodingExtension();
            ext.TryEncode('\u0105', out byte code1);
            ext.TryEncode('\u0105', out byte code2);
            Assert.Equal(code1, code2);
            Assert.Equal(1, ext.UsedSlots);
        }

        [Fact]
        public void EncodingExtension_DifferentChars_GetDifferentSlots()
        {
            var ext = new EncodingExtension();
            ext.TryEncode('\u0105', out byte code1); // ą
            ext.TryEncode('\u0107', out byte code2); // ć
            Assert.NotEqual(code1, code2);
            Assert.Equal(2, ext.UsedSlots);
        }

        [Fact]
        public void EncodingExtension_Capacity_Is37()
        {
            var ext = new EncodingExtension();
            Assert.Equal(37, ext.Capacity);
        }

        [Fact]
        public void EncodingExtension_ExceedCapacity_ReturnsFalse()
        {
            var ext = new EncodingExtension();
            // Fill all slots
            for (int i = 0; i < ext.Capacity; i++)
            {
                char c = (char)(0x0400 + i); // Use Cyrillic range as dummy chars
                Assert.True(ext.TryEncode(c, out _), $"Slot {i} should be available");
            }
            // Next one should fail
            Assert.False(ext.TryEncode((char)0x0500, out _));
        }

        [Fact]
        public void EncodingExtension_GetEncodingDict_ContainsGlyphNames()
        {
            var ext = new EncodingExtension();
            ext.TryEncode('\u0105', out _); // ą → aogonek
            ext.TryEncode('\u0142', out _); // ł → lslash

            string dict = ext.GetEncodingDict();
            Assert.Contains("/BaseEncoding /WinAnsiEncoding", dict);
            Assert.Contains("/Differences", dict);
            Assert.Contains("/aogonek", dict);
            Assert.Contains("/lslash", dict);
        }

        [Fact]
        public void EncodingExtension_NoExtensions_ReturnsWinAnsi()
        {
            var ext = new EncodingExtension();
            Assert.Equal("/WinAnsiEncoding", ext.GetEncodingDict());
        }

        // ── PdfStringHelper.Escape with encoding extension ────────

        [Fact]
        public void Escape_ExtendedChar_EncodesAsOctal()
        {
            var ext = new EncodingExtension();
            string result = PdfStringHelper.Escape("\u0105", ext); // ą
            // Should contain an octal escape for byte 1 (first slot): \001
            Assert.Contains("\\001", result);
        }

        [Fact]
        public void Escape_MixedAsciiAndExtended_PreservesBoth()
        {
            var ext = new EncodingExtension();
            string result = PdfStringHelper.Escape("za\u017C\u00F3\u0142\u0107", ext); // zażółć
            // ASCII 'z' and 'a' should be literal
            Assert.StartsWith("(za", result);
            // Extended chars should produce octal escapes
            Assert.True(result.Length > "(zażółć)".Length, "Extended chars should be escaped to octal");
        }

        [Fact]
        public void Escape_CapacityExceeded_ThrowsNotSupportedException()
        {
            var ext = new EncodingExtension();
            // Fill all slots
            for (int i = 0; i < ext.Capacity; i++)
            {
                char c = (char)(0x0400 + i);
                // Use a char that has a glyph mapping — but we need real mapped chars
                // Instead, fill slots directly
                ext.TryEncode(c, out _);
            }
            // Now try to escape a character that needs a new slot
            // Use a char that IS in GlyphMapping so the code path reaches TryEncode
            Assert.Throws<NotSupportedException>(() =>
                PdfStringHelper.Escape("\u0105", ext)); // ą
        }

        [Fact]
        public void Escape_WithoutExtension_DropsExtendedChars()
        {
            // Original behavior: no extension → extended chars silently dropped
            string result = PdfStringHelper.Escape("a\u0105b");
            Assert.Equal("(ab)", result);
        }

        // ── FontMetrics for extended characters ────────────────────

        [Fact]
        public void GetCharWidth_ExtendedChar_UsesBaseCharWidth()
        {
            // ą should have the same width as 'a'
            int widthA = FontMetrics.GetCharWidth(PdfFont.Helvetica, 'a');
            int widthAogonek = FontMetrics.GetCharWidth(PdfFont.Helvetica, '\u0105');
            Assert.Equal(widthA, widthAogonek);
        }

        [Fact]
        public void GetCharWidth_ExtendedChar_NotFallback500()
        {
            // Extended European chars should NOT return the 500 fallback
            int width = FontMetrics.GetCharWidth(PdfFont.Helvetica, '\u0105'); // ą
            Assert.NotEqual(500, width);
        }

        [Theory]
        [InlineData('\u0141', 'L')] // Ł → L
        [InlineData('\u0142', 'l')] // ł → l
        [InlineData('\u010C', 'C')] // Č → C
        [InlineData('\u0159', 'r')] // ř → r
        [InlineData('\u0150', 'O')] // Ő → O
        [InlineData('\u0111', 'd')] // đ → d
        public void GetCharWidth_VariousExtendedChars_MatchBaseWidth(char extended, char baseChar)
        {
            int extWidth = FontMetrics.GetCharWidth(PdfFont.Helvetica, extended);
            int baseWidth = FontMetrics.GetCharWidth(PdfFont.Helvetica, baseChar);
            Assert.Equal(baseWidth, extWidth);
        }

        [Fact]
        public void MeasureString_PolishText_ReturnsNonzero()
        {
            float width = FontMetrics.MeasureString("Zażółć gęślą jaźń", PdfFont.Helvetica, 12);
            Assert.True(width > 0);
        }

        // ── Integration: PDF generation with European text ─────────

        [Fact]
        public void GeneratePdf_PolishText_ContainsDifferencesArray()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            page.DrawText("Zażółć gęślą jaźń", 50, 50);

            using var ms = new MemoryStream();
            doc.Save(ms);
            string pdfContent = System.Text.Encoding.ASCII.GetString(ms.ToArray());

            Assert.Contains("/Differences", pdfContent);
            Assert.Contains("/aogonek", pdfContent);
            Assert.Contains("/zdotaccent", pdfContent);
            Assert.Contains("/lslash", pdfContent);
            Assert.Contains("/cacute", pdfContent);
            Assert.Contains("/nacute", pdfContent);
        }

        [Fact]
        public void GeneratePdf_AsciiOnly_NoDifferencesArray()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            page.DrawText("Hello World", 50, 50);

            using var ms = new MemoryStream();
            doc.Save(ms);
            string pdfContent = System.Text.Encoding.ASCII.GetString(ms.ToArray());

            Assert.DoesNotContain("/Differences", pdfContent);
            Assert.Contains("/WinAnsiEncoding", pdfContent);
        }

        [Fact]
        public void GeneratePdf_CzechText_ContainsCorrectGlyphs()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            page.DrawText("Příliš žluťoučký kůň", 50, 50);

            using var ms = new MemoryStream();
            doc.Save(ms);
            string pdfContent = System.Text.Encoding.ASCII.GetString(ms.ToArray());

            Assert.Contains("/Differences", pdfContent);
            Assert.Contains("/rcaron", pdfContent);  // ř
            Assert.Contains("/tcaron", pdfContent);  // ť
            Assert.Contains("/uring", pdfContent);   // ů
            Assert.Contains("/ccaron", pdfContent);  // č
            Assert.Contains("/ncaron", pdfContent);  // ň
        }

        [Fact]
        public void GeneratePdf_MultipleLanguages_WorksTogether()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            // Polish
            page.DrawText("Zażółć gęślą jaźń", 50, 50);
            // Czech
            page.DrawText("Příliš žluťoučký kůň", 50, 70);
            // Hungarian
            page.DrawText("Árvíztűrő tükörfúrógép", 50, 90);

            using var ms = new MemoryStream();
            doc.Save(ms);
            string pdfContent = System.Text.Encoding.ASCII.GetString(ms.ToArray());

            // Should contain glyph names from multiple languages
            Assert.Contains("/aogonek", pdfContent);     // Polish
            Assert.Contains("/rcaron", pdfContent);       // Czech
            Assert.Contains("/uhungarumlaut", pdfContent); // Hungarian
        }

        [Fact]
        public void GeneratePdf_TextBox_WrapsExtendedCharsCorrectly()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            page.DrawText("Zażółć gęślą jaźń — to jest pangram języka polskiego.",
                50, 50, width: 200);

            using var ms = new MemoryStream();
            doc.Save(ms);
            string pdfContent = System.Text.Encoding.ASCII.GetString(ms.ToArray());

            Assert.Contains("/Differences", pdfContent);
        }

        [Fact]
        public void GeneratePdf_WesternEuropean_NoDifferencesNeeded()
        {
            // French/German characters are already in WinAnsiEncoding
            var doc = new PdfDocument();
            var page = doc.AddPage();
            page.DrawText("Ärger über größe Dürre — café résumé", 50, 50);

            using var ms = new MemoryStream();
            doc.Save(ms);
            string pdfContent = System.Text.Encoding.ASCII.GetString(ms.ToArray());

            // These are all in WinAnsiEncoding, no Differences needed
            Assert.DoesNotContain("/Differences", pdfContent);
        }

        [Fact]
        public void GeneratePdf_EuropeanLanguages_SaveToFile()
        {
            var doc = new PdfDocument();
            doc.Title = "European Language Test";
            var page = doc.AddPage(PageSize.A4);
            float y = 50;
            float lineHeight = 20;

            page.DrawText("European Language Support", 50, y, PdfFont.HelveticaBold, 16);
            y += 30;

            // Western European (WinAnsiEncoding — no Differences needed)
            page.DrawText("French: Les élèves français étudient à l'université", 50, y);
            y += lineHeight;
            page.DrawText("German: Ärger über größe Dürre — Straße", 50, y);
            y += lineHeight;
            page.DrawText("Spanish: El niño comió piña y bebió café", 50, y);
            y += lineHeight;
            page.DrawText("Portuguese: Ação, coração, não, água", 50, y);
            y += lineHeight;
            page.DrawText("Italian: Perché è così difficile capirà", 50, y);
            y += lineHeight;
            page.DrawText("Swedish/Danish/Norwegian: Blåbær, ødegård, smörgås", 50, y);
            y += lineHeight * 1.5f;

            // Central/Eastern European (requires Differences array)
            page.DrawText("Central & Eastern European", 50, y, PdfFont.HelveticaBold, 14);
            y += 25;
            page.DrawText("Polish: Zażółć gęślą jaźń", 50, y);
            y += lineHeight;
            page.DrawText("Czech: Příliš žluťoučký kůň úpěl ďábelské ódy", 50, y);
            y += lineHeight;
            page.DrawText("Slovak: Kôň žerie šťavnatú trávu", 50, y);
            y += lineHeight;
            page.DrawText("Hungarian: Árvíztűrő tükörfúrógép", 50, y);
            y += lineHeight;
            page.DrawText("Romanian: Îți mulțumesc foarte mult", 50, y);
            y += lineHeight;
            page.DrawText("Croatian: Đurđevdan je lijep dan", 50, y);
            y += lineHeight;
            page.DrawText("Turkish: Güneşli günler çığ gibi geçiyor", 50, y);
            y += lineHeight;
            page.DrawText("Lithuanian: Ąžuolų ūksmėje įlinkę", 50, y);
            y += lineHeight;
            page.DrawText("Latvian: Ķīmija, ģeogrāfija, šūšana", 50, y);
            y += lineHeight * 1.5f;

            // TextBox wrapping test
            page.DrawText("Text Box Wrapping", 50, y, PdfFont.HelveticaBold, 14);
            y += 25;
            page.DrawText(
                "Zażółć gęślą jaźń — to jest pangram języka polskiego. " +
                "Příliš žluťoučký kůň úpěl ďábelské ódy — to je český pangram. " +
                "Árvíztűrő tükörfúrógép — ez a magyar pangram.",
                50, y, width: 300);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "european_languages");
            var bitmap = TestHelper.RasterizePage(bytes, "european_languages");
            Assert.True(bitmap.Width > 0);
        }
    }
}
