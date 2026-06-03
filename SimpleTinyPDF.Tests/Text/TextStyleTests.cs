using System.IO;
using System.Linq;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class TextStyleTests
    {
        // ── TextSpan property tests ──────────────────────────────────

        [Fact]
        public void TextSpan_Defaults_AreCorrect()
        {
            var span = new TextSpan("Hello");
            Assert.False(span.Bold);
            Assert.False(span.Italic);
            Assert.Equal(0f, span.CharacterSpacing);
        }

        [Fact]
        public void TextSpan_Bold_CanBeSet()
        {
            var span = new TextSpan("Hello", bold: true);
            Assert.True(span.Bold);
        }

        [Fact]
        public void TextSpan_Italic_CanBeSet()
        {
            var span = new TextSpan("Hello", italic: true);
            Assert.True(span.Italic);
        }

        [Fact]
        public void TextSpan_CharacterSpacing_CanBeSet()
        {
            var span = new TextSpan("Hello", characterSpacing: 2.5f);
            Assert.Equal(2.5f, span.CharacterSpacing);
        }

        [Fact]
        public void TextSpan_BackwardCompatibility_ExistingNamedParams()
        {
            // Verify existing usage patterns still work
            var span = new TextSpan("Hello", underline: true, opacity: 0.5f);
            Assert.True(span.Underline);
            Assert.Equal(0.5f, span.Opacity);
            Assert.False(span.Bold);
            Assert.False(span.Italic);
        }

        // ── Character Spacing content stream tests ───────────────────

        [Fact]
        public void DrawText_CharacterSpacing_EmitsTcOperator()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Spaced", 50, 50, characterSpacing: 2f);
            var stream = page.GetContentStream();
            Assert.Contains("2 Tc", stream);
            Assert.Contains("0 Tc", stream); // reset
        }

        [Fact]
        public void DrawText_NoCharacterSpacing_NoTcOperator()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Normal", 50, 50);
            var stream = page.GetContentStream();
            Assert.DoesNotContain("Tc", stream);
        }

        [Fact]
        public void DrawText_CharacterSpacing_WrappedText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float endY = page.DrawText("This text has character spacing and wraps.",
                50, 50, characterSpacing: 1.5f, width: 200);
            Assert.True(endY > 50);
            var stream = page.GetContentStream();
            Assert.Contains("Tc", stream);
        }

        [Fact]
        public void MeasureText_CharacterSpacing_IncreasesWidth()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float normalW = page.MeasureText("Hello", PdfFont.Helvetica, 12);
            float spacedW = page.MeasureText("Hello", PdfFont.Helvetica, 12, characterSpacing: 2f);
            Assert.True(spacedW > normalW,
                $"Spaced width ({spacedW}) should be greater than normal ({normalW})");
        }

        [Fact]
        public void RichText_CharacterSpacing_PerSpan()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Normal "),
                new TextSpan("Spaced", characterSpacing: 3f),
                new TextSpan(" Normal")
            }, 50, 50);
            var stream = page.GetContentStream();
            Assert.Contains("3 Tc", stream);
            Assert.Contains("0 Tc", stream);
        }

        // ── Character Spacing visual tests ───────────────────────────

        [Fact]
        public void DrawText_CharacterSpacing_RendersWider()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: character spacing makes text wider than default");
            page.DrawText("HELLO", 50, 50, PdfFont.Helvetica, 24);
            page.DrawText("HELLO", 50, 90, PdfFont.Helvetica, 24, characterSpacing: 5f);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/char-spacing-wider-builtin");
            var bitmap = TestHelper.RasterizePage(bytes, "Text/char-spacing-wider-builtin");

            // Spaced text should extend further right
            int normalRight = 0;
            int spacedRight = 0;
            int yNormal = TestHelper.PtToPx(50);
            int ySpaced = TestHelper.PtToPx(90);
            for (int px = bitmap.Width - 1; px >= 0; px--)
            {
                if (normalRight == 0 && TestHelper.HasDarkPixelsInRegion(bitmap, px, px, yNormal, yNormal + TestHelper.PtToPx(24)))
                    normalRight = px;
                if (spacedRight == 0 && TestHelper.HasDarkPixelsInRegion(bitmap, px, px, ySpaced, ySpaced + TestHelper.PtToPx(24)))
                    spacedRight = px;
                if (normalRight > 0 && spacedRight > 0) break;
            }
            Assert.True(spacedRight > normalRight,
                $"Spaced text right edge ({spacedRight}) should exceed normal ({normalRight})");
            bitmap.Dispose();
        }

        // ── Faux Bold content stream tests ───────────────────────────

        [Fact]
        public void DrawText_Bold_EmitsFillStrokeMode()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Bold", 50, 50, bold: true);
            var stream = page.GetContentStream();
            Assert.Contains("2 Tr", stream); // fill+stroke mode
            Assert.Contains("0 Tr", stream); // reset
        }

        [Fact]
        public void DrawText_NoBold_NoTrOperator()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Normal", 50, 50);
            var stream = page.GetContentStream();
            Assert.DoesNotContain("Tr", stream);
        }

        [Fact]
        public void DrawText_Bold_StrokeWidthProportionalToFontSize()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Bold", 50, 50, fontSize: 20f, bold: true);
            var stream = page.GetContentStream();
            // 20 * 0.025 = 0.5
            Assert.Contains("0.5 w", stream);
        }

        // ── Faux Bold visual tests ───────────────────────────────────

        [Fact]
        public void DrawText_FauxBold_RendersThickerThanNormal()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: faux bold text appears thicker via fill+stroke rendering");
            page.DrawText("Hello World", 50, 50, PdfFont.Helvetica, 24);
            page.DrawText("Hello World", 50, 90, PdfFont.Helvetica, 24, bold: true);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/faux-bold-thicker-stroke");
            var bitmap = TestHelper.RasterizePage(bytes, "Text/faux-bold-thicker-stroke");

            int normalDark = TestHelper.CountDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(250), TestHelper.PtToPx(50), TestHelper.PtToPx(50) + TestHelper.PtToPx(24));
            int boldDark = TestHelper.CountDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(250), TestHelper.PtToPx(90), TestHelper.PtToPx(90) + TestHelper.PtToPx(24));
            Assert.True(boldDark > normalDark,
                $"Faux bold ({boldDark} dark px) should be thicker than normal ({normalDark})");
            bitmap.Dispose();
        }

        // ── Faux Italic content stream tests ─────────────────────────

        [Fact]
        public void DrawText_Italic_EmitsShearMatrix()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Italic", 50, 50, italic: true);
            var stream = page.GetContentStream();
            Assert.Contains("0.2126", stream); // italic shear factor
            Assert.Contains("Tm", stream);
        }

        [Fact]
        public void DrawText_NoItalic_NoShear()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Normal", 50, 50);
            var stream = page.GetContentStream();
            Assert.DoesNotContain("0.2126", stream);
        }

        // ── Faux Italic visual tests ─────────────────────────────────

        [Fact]
        public void DrawText_FauxItalic_RendersSlanted()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: faux italic text has 12-degree shear");
            page.DrawText("Italic Text", 50, 50, PdfFont.Helvetica, 36, italic: true);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/faux-italic-12deg-shear");
            var bitmap = TestHelper.RasterizePage(bytes, "Text/faux-italic-12deg-shear");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(350), TestHelper.PtToPx(50), TestHelper.PtToPx(50) + TestHelper.PtToPx(36)));
            bitmap.Dispose();
        }

        // ── Bold+Italic combined ─────────────────────────────────────

        [Fact]
        public void DrawText_BoldItalic_Combined()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: faux bold and italic applied together");
            page.DrawText("Bold Italic", 50, 50, PdfFont.Helvetica, 24,
                bold: true, italic: true);
            var stream = page.GetContentStream();
            Assert.Contains("2 Tr", stream);
            Assert.Contains("0.2126", stream);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/faux-bold-and-italic-combined");
        }

        // ── Justification content stream tests ───────────────────────

        [Fact]
        public void DrawText_Justify_BuiltInFont_UsesTwOperator()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("This is text that needs to wrap across multiple lines so that the first line gets justified properly with the Tw operator.",
                50, 50, PdfFont.Helvetica, 12,
                alignment: TextAlignment.Justify, width: 250);
            var stream = page.GetContentStream();
            Assert.Contains("Tw", stream);
        }

        [Fact]
        public void DrawText_Justify_LastLineNotJustified()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            // Short text that fits on one line — should not justify (it's the last line)
            page.DrawText("Short text.", 50, 50, PdfFont.Helvetica, 12,
                alignment: TextAlignment.Justify, width: 400);
            var stream = page.GetContentStream();
            Assert.DoesNotContain("Tw", stream);
        }

        [Fact]
        public void DrawText_Justify_CustomFont_UsesPerWordTm()
        {
            var font = PdfFontSource.FromFile(Path.Combine("TestAssets", "Roboto-Regular.ttf"));
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("This is text that should wrap and be justified nicely across the full width of the box.",
                50, 50, font, 12, alignment: TextAlignment.Justify, width: 300);
            var stream = page.GetContentStream();
            // Custom font justification uses per-word Tm — should have multiple Tm entries
            int tmCount = stream.Split("Tm").Length - 1;
            Assert.True(tmCount > 2, $"Expected multiple Tm entries for per-word positioning, got {tmCount}");
        }

        // ── Justification visual tests ───────────────────────────────

        [Fact]
        public void DrawText_Justify_RendersFullWidth()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: justified text fills full textbox width");
            string longText = "This is a longer paragraph of text that should be fully justified. " +
                "Each line except the last should stretch to fill the entire available width, " +
                "creating clean left and right edges for a professional typeset appearance.";
            page.DrawText(longText, 50, 50, PdfFont.Helvetica, 12,
                alignment: TextAlignment.Justify, width: 400);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/justify-full-width-textbox");
            var bitmap = TestHelper.RasterizePage(bytes, "Text/justify-full-width-textbox");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(450), TestHelper.PtToPx(50), TestHelper.PtToPx(50) + TestHelper.PtToPx(60)));
            bitmap.Dispose();
        }

        [Fact]
        public void RichText_Justify_DistributesSpace()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: justified rich text spans fill textbox width");
            page.DrawText(new[]
            {
                new TextSpan("This is "),
                new TextSpan("justified ", bold: true),
                new TextSpan("rich text that wraps across multiple lines to test the justification algorithm.")
            }, 50, 50, alignment: TextAlignment.Justify, width: 350);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/richtext-justified-spans");
            var bitmap = TestHelper.RasterizePage(bytes, "Text/richtext-justified-spans");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(50), TestHelper.PtToPx(50) + TestHelper.PtToPx(40)));
            bitmap.Dispose();
        }

        // ── Custom font tests ────────────────────────────────────────

        [Fact]
        public void DrawText_CustomFont_FauxBold()
        {
            var font = PdfFontSource.FromFile(Path.Combine("TestAssets", "Roboto-Regular.ttf"));
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: faux bold works with custom TrueType font");
            page.DrawText("Custom font bold", 50, 50, font, 24, bold: true);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/custom-font-faux-bold");
            var stream = page.GetContentStream();
            Assert.Contains("2 Tr", stream);
            var bitmap = TestHelper.RasterizePage(bytes, "Text/custom-font-faux-bold");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(350), TestHelper.PtToPx(50), TestHelper.PtToPx(50) + TestHelper.PtToPx(24)));
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_CustomFont_FauxItalic()
        {
            var font = PdfFontSource.FromFile(Path.Combine("TestAssets", "Roboto-Regular.ttf"));
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: faux italic works with custom TrueType font");
            page.DrawText("Custom font italic", 50, 50, font, 24, italic: true);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/custom-font-faux-italic");
            var stream = page.GetContentStream();
            Assert.Contains("0.2126", stream);
            var bitmap = TestHelper.RasterizePage(bytes, "Text/custom-font-faux-italic");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(50), TestHelper.PtToPx(50) + TestHelper.PtToPx(24)));
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_CustomFont_CharacterSpacing()
        {
            var font = PdfFontSource.FromFile(Path.Combine("TestAssets", "Roboto-Regular.ttf"));
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: character spacing works with custom TrueType font");
            page.DrawText("Spaced", 50, 50, font, 24, characterSpacing: 3f);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/custom-font-char-spacing");
            var stream = page.GetContentStream();
            Assert.Contains("3 Tc", stream);
        }

        // ── Wrapped text with bold/italic ────────────────────────────

        [Fact]
        public void DrawText_Bold_WrappedText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: faux bold text wraps correctly in textbox");
            float endY = page.DrawText("This bold text wraps across multiple lines for testing.",
                50, 50, bold: true, width: 150);
            Assert.True(endY > 50);
            var stream = page.GetContentStream();
            Assert.Contains("2 Tr", stream);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/faux-bold-wrapped-textbox");
        }

        [Fact]
        public void DrawText_Italic_WrappedText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: faux italic text wraps correctly in textbox");
            float endY = page.DrawText("This italic text wraps across multiple lines for testing.",
                50, 50, italic: true, width: 150);
            Assert.True(endY > 50);
            var stream = page.GetContentStream();
            Assert.Contains("0.2126", stream);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/faux-italic-wrapped-textbox");
        }

        // ── Rich text with mixed styles ──────────────────────────────

        [Fact]
        public void RichText_MixedBoldNormal()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: mix of bold and normal spans inline");
            page.DrawText(new[]
            {
                new TextSpan("Normal "),
                new TextSpan("Bold", bold: true),
                new TextSpan(" Normal")
            }, 50, 50);
            var stream = page.GetContentStream();
            Assert.Contains("2 Tr", stream);
            Assert.Contains("0 Tr", stream);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/richtext-mixed-bold-spans");
        }

        [Fact]
        public void RichText_MixedItalicNormal()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: mix of italic and normal spans inline");
            page.DrawText(new[]
            {
                new TextSpan("Normal "),
                new TextSpan("Italic", italic: true),
                new TextSpan(" Normal")
            }, 50, 50);
            var stream = page.GetContentStream();
            Assert.Contains("0.2126", stream);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/richtext-mixed-italic-spans");
        }

        [Fact]
        public void RichText_WrappedMixedStyles()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: mixed bold/italic rich text wraps in textbox");
            page.DrawText(new[]
            {
                new TextSpan("Normal text "),
                new TextSpan("bold text ", bold: true),
                new TextSpan("italic text ", italic: true),
                new TextSpan("spaced text ", characterSpacing: 2f),
                new TextSpan("and back to normal.")
            }, 50, 50, width: 300);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/richtext-wrapped-mixed-styles");
            var bitmap = TestHelper.RasterizePage(bytes, "Text/richtext-wrapped-mixed-styles");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(350), TestHelper.PtToPx(50), TestHelper.PtToPx(50) + TestHelper.PtToPx(40)));
            bitmap.Dispose();
        }

        // ── All features combined ────────────────────────────────────

        [Fact]
        public void DrawText_AllFeaturesCombined()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: bold, italic, spacing, and justification all combined");
            page.DrawText("Bold Italic Spaced", 50, 50, PdfFont.Helvetica, 24,
                bold: true, italic: true, characterSpacing: 1.5f);
            var stream = page.GetContentStream();
            Assert.Contains("2 Tr", stream);     // bold
            Assert.Contains("0.2126", stream);    // italic
            Assert.Contains("1.5 Tc", stream); // character spacing
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/all-styles-combined-showcase");
        }

        [Fact]
        public void DrawText_JustifiedBoldItalic_WrappedText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: justified text with bold and italic formatting");
            page.DrawText(
                "This text is bold, italic, and justified. It wraps across lines to demonstrate all features working together.",
                50, 50, PdfFont.Helvetica, 14,
                alignment: TextAlignment.Justify, width: 350,
                bold: true, italic: true);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/justified-bold-italic-combined");
            var bitmap = TestHelper.RasterizePage(bytes, "Text/justified-bold-italic-combined");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(50), TestHelper.PtToPx(50) + TestHelper.PtToPx(50)));
            bitmap.Dispose();
        }

        // ── Comprehensive visual showcase ────────────────────────────

        [Fact]
        public void Showcase_AllNewFeatures()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: full showcase of all text styling features");
            float y = 50;

            // Header
            page.DrawText("Text Style Features Showcase", 50, y, PdfFont.HelveticaBold, 18);
            y += 30;

            // Character spacing
            page.DrawText("Normal spacing", 50, y, PdfFont.Helvetica, 14); y += 20;
            page.DrawText("Character spacing = 2", 50, y, PdfFont.Helvetica, 14, characterSpacing: 2f); y += 20;
            page.DrawText("Character spacing = 5", 50, y, PdfFont.Helvetica, 14, characterSpacing: 5f); y += 30;

            // Faux bold
            page.DrawText("Normal weight", 50, y, PdfFont.Helvetica, 14); y += 20;
            page.DrawText("Faux bold", 50, y, PdfFont.Helvetica, 14, bold: true); y += 20;
            page.DrawText("Actual bold (HelveticaBold)", 50, y, PdfFont.HelveticaBold, 14); y += 30;

            // Faux italic
            page.DrawText("Normal style", 50, y, PdfFont.Helvetica, 14); y += 20;
            page.DrawText("Faux italic", 50, y, PdfFont.Helvetica, 14, italic: true); y += 20;
            page.DrawText("Actual oblique (HelveticaOblique)", 50, y, PdfFont.HelveticaOblique, 14); y += 30;

            // Combined
            page.DrawText("Faux bold + italic", 50, y, PdfFont.Helvetica, 14,
                bold: true, italic: true); y += 30;

            // Justified
            string loremIpsum = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. " +
                "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris.";
            page.DrawText("Left aligned:", 50, y, PdfFont.HelveticaBold, 12); y += 18;
            y = page.DrawText(loremIpsum, 50, y, PdfFont.Helvetica, 11,
                alignment: TextAlignment.Left, width: 400); y += 10;

            page.DrawText("Justified:", 50, y, PdfFont.HelveticaBold, 12); y += 18;
            y = page.DrawText(loremIpsum, 50, y, PdfFont.Helvetica, 11,
                alignment: TextAlignment.Justify, width: 400); y += 10;

            // Custom font with faux styles
            var customFont = PdfFontSource.FromFile(Path.Combine("TestAssets", "Roboto-Regular.ttf"));
            page.DrawText("Custom font normal", 50, y, customFont, 14); y += 20;
            page.DrawText("Custom font faux bold", 50, y, customFont, 14, bold: true); y += 20;
            page.DrawText("Custom font faux italic", 50, y, customFont, 14, italic: true); y += 20;
            page.DrawText("Custom font faux bold+italic", 50, y, customFont, 14,
                bold: true, italic: true); y += 30;

            // Rich text with mixed styles
            page.DrawText(new[]
            {
                new TextSpan("Mixed: "),
                new TextSpan("bold ", bold: true),
                new TextSpan("italic ", italic: true),
                new TextSpan("spaced ", characterSpacing: 3f),
                new TextSpan("underline ", underline: true),
                new TextSpan("bold+italic", bold: true, italic: true)
            }, 50, y);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/text-styles-full-showcase");
            var bitmap = TestHelper.RasterizePage(bytes, "Text/text-styles-full-showcase");
            // Verify page has content throughout
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(450), TestHelper.PtToPx(50), TestHelper.PtToPx(200)));
            bitmap.Dispose();
        }
    }
}
