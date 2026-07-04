using System.IO;
using SimpleTinyPDF.Text;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class RtlTextTests
    {
        private static readonly string DejaVuFontPath =
            Path.Combine("TestAssets", "DejaVuSans.ttf");

        // ── TextShaper unit tests (string transforms, no PDF) ─────────

        [Fact]
        public void Process_PlainAscii_ReturnsSameInstance()
        {
            var text = "Hello, world! 123 (test)";
            Assert.Same(text, TextShaper.Process(text));
        }

        [Fact]
        public void NeedsProcessing_DetectsRtlRanges()
        {
            Assert.False(TextShaper.NeedsProcessing("plain latin"));
            Assert.False(TextShaper.NeedsProcessing(""));
            Assert.True(TextShaper.NeedsProcessing("ש"));   // Hebrew shin
            Assert.True(TextShaper.NeedsProcessing("س"));   // Arabic seen
            Assert.True(TextShaper.NeedsProcessing("a‏b")); // RLM
            Assert.True(TextShaper.NeedsProcessing("ﺳ"));   // presentation form
        }

        [Fact]
        public void Process_Hebrew_ReversesToVisualOrder()
        {
            // "שלום" (shalom) — Hebrew has no shaping, only reordering
            Assert.Equal("םולש", TextShaper.Process("שלום"));
        }

        [Fact]
        public void Process_Arabic_ShapesAndReorders()
        {
            // "سلام" (salaam) = seen, lam, alef, meem.
            // Expected: seen initial + lam-alef final ligature + meem isolated,
            // then reversed to visual order.
            Assert.Equal("ﻡﻼﺳ", TextShaper.Process("سلام"));
        }

        [Fact]
        public void Process_MixedLtrRtl_KeepsLatinInPlace()
        {
            // Latin base direction: Hebrew word reversed, Latin words stay put
            Assert.Equal("abc םולש xyz",
                TextShaper.Process("abc שלום xyz"));
        }

        [Fact]
        public void Process_ArabicWithNumber_KeepsDigitOrder()
        {
            // "صفحة 12" (page 12) — digits must not be reversed.
            // sad initial, fa medial, hah medial, teh-marbuta final; RTL base
            // puts the number run on the left, digits in logical order.
            Assert.Equal("12 ﺔﺤﻔﺻ",
                TextShaper.Process("صفحة 12"));
        }

        [Fact]
        public void Process_HebrewInParentheses_MirrorsBrackets()
        {
            // Brackets in an RTL run are mirrored (rule L4) so they still
            // enclose the text after reversal.
            Assert.Equal("(םולש)", TextShaper.Process("(שלום)"));
        }

        [Fact]
        public void Process_ArabicWithTashkeel_KeepsMarkAfterBase()
        {
            // ba + fatha + alef: the fatha must stay attached to (directly after)
            // its base letter when the run is reversed (rule L3).
            // Logical: ba(initial due to alef) fatha alef(final) → visual:
            // alef-final, ba-initial, fatha
            Assert.Equal("ﺎﺑَ", TextShaper.Process("بَا"));
        }

        [Fact]
        public void Process_LamAlefIsolated_UsesIsolatedLigature()
        {
            // Word-initial lam+alef with nothing joining before it → isolated form
            Assert.Equal("ﻻ", TextShaper.Process("لا"));
        }

        [Fact]
        public void Process_RemovesBidiControlCharacters()
        {
            // RLM around a Hebrew word must not survive into the output
            var result = TextShaper.Process("‏של‏");
            Assert.Equal("לש", result);
        }

        [Fact]
        public void Shape_IsStableForPresentationForms()
        {
            // Already-shaped text contains no shapeable base letters, so a
            // second shaping pass must not change it.
            var shaped = ArabicShaper.Shape("سلام");
            Assert.Equal(shaped, ArabicShaper.Shape(shaped));
        }

        [Fact]
        public void Shape_CombinesShaddaVowel_WhenFontHasLigature()
        {
            // meem + shadda + fatha + dal — both mark orders must combine into
            // the precomposed shadda+fatha form (U+FC60) when the font has it
            var canonical = ArabicShaper.Shape("مَّد", cp => true);
            Assert.Contains('ﱠ', canonical);
            var keyboard = ArabicShaper.Shape("مَّد", cp => true);
            Assert.Contains('ﱠ', keyboard);

            // fonts without the ligature glyph keep the separate marks
            var without = ArabicShaper.Shape("مَّد", cp => cp < 0xFC00);
            Assert.DoesNotContain('ﱠ', without);
            Assert.Contains('ّ', without);
            Assert.Contains('َ', without);
        }

        [Fact]
        public void Process_WidthIsPreservedByReordering()
        {
            // Measurement processes text internally; drawing processes it before
            // measuring. Both must agree, which relies on reordering being
            // width-neutral.
            var font = PdfFontSource.FromFile(DejaVuFontPath);
            var logical = "abc سلام 123";
            var processed = TextShaper.Process(logical);
            var w1 = FontMetrics.MeasureString(logical, font, 12f);
            var w2 = FontMetrics.MeasureString(processed, font, 12f);
            Assert.Equal(w1, w2, 3);
        }

        // ── Font coverage sanity ───────────────────────────────────────

        [Fact]
        public void DejaVuSans_HasPresentationFormGlyphs()
        {
            var font = PdfFontSource.FromFile(DejaVuFontPath);
            // isolated meem, lam-alef final ligature, initial seen, Hebrew shin
            Assert.NotEqual(0, font.CustomFont.GetGlyphId(0xFEE1));
            Assert.NotEqual(0, font.CustomFont.GetGlyphId(0xFEFC));
            Assert.NotEqual(0, font.CustomFont.GetGlyphId(0xFEB3));
            Assert.NotEqual(0, font.CustomFont.GetGlyphId(0x05E9));
        }

        // ── Visual rendering tests ─────────────────────────────────────

        [Fact]
        public void ArabicText_RendersVisiblePixels()
        {
            var font = PdfFontSource.FromFile(DejaVuFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page,
                "Verify: Arabic renders joined (connected letters, lam-alef ligature), right-aligned: السلام عليكم");
            // "السلام عليكم" (as-salaamu alaykum)
            page.DrawText("السلام عليكم",
                545, 50, font, 24, alignment: TextAlignment.Right);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/rtl-arabic-basic");

            var bitmap = TestHelper.RasterizePage(bytes, "Text/rtl-arabic-basic");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(350), TestHelper.PtToPx(550), TestHelper.PtToPx(35), TestHelper.PtToPx(70)),
                "Arabic text should render visible pixels near the right margin");
            bitmap.Dispose();
        }

        [Fact]
        public void HebrewText_RendersVisiblePixels()
        {
            var font = PdfFontSource.FromFile(DejaVuFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page,
                "Verify: Hebrew reads right-to-left, right-aligned: שלום עולם");
            // "שלום עולם" (hello world)
            page.DrawText("שלום עולם",
                545, 50, font, 24, alignment: TextAlignment.Right);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/rtl-hebrew-basic");

            var bitmap = TestHelper.RasterizePage(bytes, "Text/rtl-hebrew-basic");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(400), TestHelper.PtToPx(550), TestHelper.PtToPx(35), TestHelper.PtToPx(70)),
                "Hebrew text should render visible pixels near the right margin");
            bitmap.Dispose();
        }

        [Fact]
        public void MixedDirectionText_RendersVisiblePixels()
        {
            var font = PdfFontSource.FromFile(DejaVuFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page,
                "Verify: mixed line reads: Invoice 42, then (2026) and Arabic word as one RTL segment. Digits stay LTR, parens stay paired");
            page.DrawText("Invoice 42 فاتورة (2026)",
                50, 50, font, 18);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/rtl-mixed-direction");

            var bitmap = TestHelper.RasterizePage(bytes, "Text/rtl-mixed-direction");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(300), TestHelper.PtToPx(35), TestHelper.PtToPx(60)),
                "Mixed-direction text should render visible pixels");
            bitmap.Dispose();
        }

        [Fact]
        public void ArabicTextBox_WrapsAndRendersOnMultipleLines()
        {
            var font = PdfFontSource.FromFile(DejaVuFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page,
                "Verify: Arabic paragraph wraps in a 200pt box, right-aligned, letters joined on every line");
            // Repeated phrase to force wrapping
            var phrase = "السلام عليكم ورحمة الله وبركاته";
            var text = phrase + " " + phrase + " " + phrase;
            page.DrawText(text, 50, 50, font, 14, alignment: TextAlignment.Right, width: 200);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/rtl-arabic-textbox");

            var bitmap = TestHelper.RasterizePage(bytes, "Text/rtl-arabic-textbox");
            // Expect pixels on at least the first and a following wrapped line
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(250), TestHelper.PtToPx(48), TestHelper.PtToPx(66)),
                "First wrapped Arabic line should render");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(250), TestHelper.PtToPx(67), TestHelper.PtToPx(120)),
                "Later wrapped Arabic lines should render");
            bitmap.Dispose();
        }

        [Fact]
        public void ArabicTextWithTashkeel_RendersVisiblePixels()
        {
            var font = PdfFontSource.FromFile(DejaVuFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page,
                "Verify: Arabic with tashkeel (vowel marks) renders marks near their letters: مُحَمَّد");
            // "Muhammad" with full diacritics
            page.DrawText("مُحَمَّد", 545, 50, font, 36, alignment: TextAlignment.Right);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/rtl-arabic-tashkeel");

            var bitmap = TestHelper.RasterizePage(bytes, "Text/rtl-arabic-tashkeel");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(430), TestHelper.PtToPx(560), TestHelper.PtToPx(35), TestHelper.PtToPx(90)),
                "Arabic text with tashkeel should render visible pixels");
            bitmap.Dispose();
        }

        [Fact]
        public void ArabicText_SubsettedFontKeepsPresentationFormGlyphs()
        {
            // Presentation forms are recorded as used characters, so the
            // subsetter must retain their glyphs — a blank render would mean
            // they were dropped.
            var font = PdfFontSource.FromFile(DejaVuFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page,
                "Verify: Arabic still renders after font subsetting (glyphs retained): سلام");
            page.DrawText("سلام", 545, 50, font, 36, alignment: TextAlignment.Right);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/rtl-arabic-subset");

            var bitmap = TestHelper.RasterizePage(bytes, "Text/rtl-arabic-subset");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(430), TestHelper.PtToPx(560), TestHelper.PtToPx(35), TestHelper.PtToPx(80)),
                "Subsetted Arabic text should render visible pixels");
            bitmap.Dispose();
        }
    }
}
