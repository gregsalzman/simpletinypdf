using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class RichTextTests
    {
        private static int PtToPx(float pt, int dpi = 150) => (int)(pt * dpi / 72.0);

        private static bool HasDarkPixelsInRegion(SkiaSharp.SKBitmap bitmap,
            int xMin, int xMax, int yMin, int yMax)
        {
            xMax = System.Math.Min(xMax, bitmap.Width - 1);
            yMax = System.Math.Min(yMax, bitmap.Height - 1);
            for (int x = System.Math.Max(0, xMin); x <= xMax; x++)
                for (int y = System.Math.Max(0, yMin); y <= yMax; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Red < 200 || p.Green < 200 || p.Blue < 200)
                        return true;
                }
            return false;
        }

        // ── TextSpan Construction ────────────────────────────────

        [Fact]
        public void TextSpan_Defaults_AreCorrect()
        {
            var span = new TextSpan("Hello");
            Assert.Equal("Hello", span.Text);
            Assert.Equal(PdfFont.Helvetica, span.Font);
            Assert.Equal(12f, span.FontSize);
            Assert.Equal(PdfColor.Black, span.Color);
        }

        [Fact]
        public void TextSpan_NullText_BecomesEmptyString()
        {
            var span = new TextSpan(null);
            Assert.Equal(string.Empty, span.Text);
        }

        [Fact]
        public void TextSpan_CustomValues_ArePreserved()
        {
            var span = new TextSpan("test", PdfFont.CourierBold, 24f, PdfColor.Red);
            Assert.Equal("test", span.Text);
            Assert.Equal(PdfFont.CourierBold, span.Font);
            Assert.Equal(24f, span.FontSize);
            Assert.Equal(PdfColor.Red, span.Color);
        }

        // ── DrawText (single line, from spans) ──────────────────────

        [Fact]
        public void DrawRichText_SingleSpan_RendersText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Hello World", PdfFont.Helvetica, 24)
            }, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtext_single_span");
            var bitmap = TestHelper.RasterizePage(bytes, "richtext_single_span");
            int textY = PtToPx(50);
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250),
                textY, textY + PtToPx(24)),
                "Expected visible text for single-span rich text");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichText_MultipleSpans_MixedFonts()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Normal ", PdfFont.Helvetica, 14),
                new TextSpan("Bold ", PdfFont.HelveticaBold, 14),
                new TextSpan("Italic", PdfFont.HelveticaOblique, 14)
            }, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtext_mixed_fonts");
            var bitmap = TestHelper.RasterizePage(bytes, "richtext_mixed_fonts");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(350),
                PtToPx(50), PtToPx(64)),
                "Expected visible mixed-font text");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichText_MixedColors_ShowsDifferentColors()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Red ", PdfFont.HelveticaBold, 36, PdfColor.Red),
                new TextSpan("Blue", PdfFont.HelveticaBold, 36, PdfColor.Blue)
            }, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtext_mixed_colors");
            var bitmap = TestHelper.RasterizePage(bytes, "richtext_mixed_colors");

            bool foundRed = false;
            for (int px = PtToPx(50); px < PtToPx(150) && !foundRed; px++)
                for (int py = PtToPx(50); py < PtToPx(86) && !foundRed; py++)
                {
                    var p = bitmap.GetPixel(px, py);
                    if (p.Red > 200 && p.Green < 50 && p.Blue < 50)
                        foundRed = true;
                }
            Assert.True(foundRed, "Expected red pixels in first span");

            bool foundBlue = false;
            for (int px = PtToPx(100); px < PtToPx(300) && !foundBlue; px++)
                for (int py = PtToPx(50); py < PtToPx(86) && !foundBlue; py++)
                {
                    var p = bitmap.GetPixel(px, py);
                    if (p.Blue > 200 && p.Red < 50 && p.Green < 50)
                        foundBlue = true;
                }
            Assert.True(foundBlue, "Expected blue pixels in second span");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichText_MixedSizes_RendersText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Big ", PdfFont.Helvetica, 36),
                new TextSpan("small", PdfFont.Helvetica, 10)
            }, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtext_mixed_sizes");
            var bitmap = TestHelper.RasterizePage(bytes, "richtext_mixed_sizes");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300),
                PtToPx(50), PtToPx(86)),
                "Expected visible text with mixed sizes");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichText_CenterAlignment()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Center ", PdfFont.Helvetica, 20),
                new TextSpan("Text", PdfFont.HelveticaBold, 20)
            }, page.Width / 2, 50, TextAlignment.Center);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtext_center");
            var bitmap = TestHelper.RasterizePage(bytes, "richtext_center");
            int midX = bitmap.Width / 2;
            Assert.True(HasDarkPixelsInRegion(bitmap, midX - PtToPx(60), midX + PtToPx(60),
                PtToPx(50), PtToPx(70)),
                "Expected centered rich text near horizontal center");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichText_EmptySpans_NoError()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new TextSpan[] { }, 50, 50);
            page.DrawText(new[] { new TextSpan("") }, 50, 50);
            page.DrawText((TextSpan[])null, 50, 50);
            var bytes = doc.ToArray();
            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
        }

        // ── DrawText with width (multi-line, wrapped) ────────────

        [Fact]
        public void DrawRichTextBox_SingleSpan_WrapsText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            string text = "This is a paragraph of text that should wrap across multiple lines when drawn in a text box.";
            float endY = page.DrawText(new[]
            {
                new TextSpan(text, PdfFont.Helvetica, 12)
            }, 50, 50, width: 200);

            Assert.True(endY > 50, "DrawText should return Y after text");

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtextbox_single_span");
            var bitmap = TestHelper.RasterizePage(bytes, "richtextbox_single_span");

            float lineHeight = 12 * 1.2f;
            int linesRendered = (int)((endY - 50) / lineHeight + 0.5f);
            Assert.True(linesRendered >= 3, $"Expected at least 3 wrapped lines, got {linesRendered}");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichTextBox_MixedSpans_WrapsCorrectly()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float endY = page.DrawText(new[]
            {
                new TextSpan("This is ", PdfFont.Helvetica, 12),
                new TextSpan("bold text ", PdfFont.HelveticaBold, 12),
                new TextSpan("followed by normal text that should wrap across multiple lines.", PdfFont.Helvetica, 12)
            }, 50, 50, width: 200);

            Assert.True(endY > 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtextbox_mixed");
            var bitmap = TestHelper.RasterizePage(bytes, "richtextbox_mixed");

            float lineHeight = 12 * 1.2f;
            int linesRendered = (int)((endY - 50) / lineHeight + 0.5f);
            Assert.True(linesRendered >= 2, $"Expected multiple wrapped lines, got {linesRendered}");

            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250),
                PtToPx(50), PtToPx(62)),
                "Expected text on first line");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichTextBox_MixedSizes_LineHeightAdjusts()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float endY = page.DrawText(new[]
            {
                new TextSpan("Big ", PdfFont.Helvetica, 30),
                new TextSpan("and small text that wraps.", PdfFont.Helvetica, 10)
            }, 50, 50, width: 200);

            Assert.True(endY > 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtextbox_mixed_sizes");
            var bitmap = TestHelper.RasterizePage(bytes, "richtextbox_mixed_sizes");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250),
                PtToPx(50), PtToPx(80)),
                "Expected visible text with mixed sizes");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichTextBox_MixedColors_RendersInColor()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Red text ", PdfFont.HelveticaBold, 24, PdfColor.Red),
                new TextSpan("and blue text", PdfFont.HelveticaBold, 24, PdfColor.Blue)
            }, 50, 50, width: 400);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtextbox_colors");
            var bitmap = TestHelper.RasterizePage(bytes, "richtextbox_colors");

            bool foundRed = false;
            for (int px = PtToPx(50); px < PtToPx(200) && !foundRed; px++)
                for (int py = PtToPx(50); py < PtToPx(74) && !foundRed; py++)
                {
                    var p = bitmap.GetPixel(px, py);
                    if (p.Red > 200 && p.Green < 50 && p.Blue < 50)
                        foundRed = true;
                }
            Assert.True(foundRed, "Expected red pixels in rich text box");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichTextBox_NewlinesInSpans_CreateLineBreaks()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float endY = page.DrawText(new[]
            {
                new TextSpan("Line one\nLine two\nLine three", PdfFont.Helvetica, 12)
            }, 50, 50, width: 400);

            float lineHeight = 12 * 1.2f;
            int linesRendered = (int)((endY - 50) / lineHeight + 0.5f);
            Assert.Equal(3, linesRendered);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtextbox_newlines");
            var bitmap = TestHelper.RasterizePage(bytes, "richtextbox_newlines");

            for (int line = 0; line < 3; line++)
            {
                float lineY = 50 + line * lineHeight;
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250),
                    PtToPx(lineY), PtToPx(lineY + 12)),
                    $"Expected text on line {line + 1}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichTextBox_RightAlignment()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float boxX = 100, boxWidth = 300;
            page.DrawText(new[]
            {
                new TextSpan("Short\n", PdfFont.Helvetica, 14),
                new TextSpan("A longer line of text", PdfFont.Helvetica, 14)
            }, boxX, 50, TextAlignment.Right, width: boxWidth);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtextbox_right");
            var bitmap = TestHelper.RasterizePage(bytes, "richtextbox_right");

            int rightEdgePx = PtToPx(boxX + boxWidth);
            float lineHeight = 14 * 1.2f;
            for (int i = 0; i < 2; i++)
            {
                float lineY = 50 + i * lineHeight;
                Assert.True(HasDarkPixelsInRegion(bitmap,
                    rightEdgePx - PtToPx(100), rightEdgePx,
                    PtToPx(lineY), PtToPx(lineY + 14)),
                    $"Expected right-aligned text on line {i + 1}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichTextBox_ReturnsCorrectY_ForChaining()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float y = 50;
            y = page.DrawText(new[]
            {
                new TextSpan("First ", PdfFont.Helvetica, 12),
                new TextSpan("paragraph", PdfFont.HelveticaBold, 12)
            }, 50, y, width: 400);

            float gap = 10;
            y += gap;
            y = page.DrawText(new[]
            {
                new TextSpan("Second paragraph", PdfFont.Helvetica, 12)
            }, 50, y, width: 400);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtextbox_chain");
            var bitmap = TestHelper.RasterizePage(bytes, "richtextbox_chain");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300),
                PtToPx(50), PtToPx(62)),
                "Expected first paragraph text");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichTextBox_EmptyInput_ReturnsStartY()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float result = page.DrawText(new TextSpan[] { }, 50, 100, width: 400);
            Assert.Equal(100, result);

            result = page.DrawText((TextSpan[])null, 50, 100, width: 400);
            Assert.Equal(100, result);
        }

        // ── The user's exact example ─────────────────────────────

        [Fact]
        public void DrawRichText_UsersExample_MixedFontsSizesColors()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("It's a ", PdfFont.Helvetica, 12),
                new TextSpan("pleasure ", PdfFont.CourierBold, 7, PdfColor.Red),
                new TextSpan("to meet you.", PdfFont.TimesRoman, 16, PdfColor.Rgb(1f, 1f, 0f))
            }, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtext_users_example");
            var bitmap = TestHelper.RasterizePage(bytes, "richtext_users_example");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300),
                PtToPx(50), PtToPx(66)),
                "Expected visible text for the user's mixed-format example");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichTextBox_UsersExample_InBox()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float endY = page.DrawText(new[]
            {
                new TextSpan("It's a ", PdfFont.Helvetica, 12),
                new TextSpan("pleasure ", PdfFont.CourierBold, 7, PdfColor.Red),
                new TextSpan("to meet you.", PdfFont.TimesRoman, 16, PdfColor.Rgb(1f, 1f, 0f))
            }, 50, 50, width: 300);

            Assert.True(endY > 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtextbox_users_example");
            var bitmap = TestHelper.RasterizePage(bytes, "richtextbox_users_example");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300),
                PtToPx(50), PtToPx(66)),
                "Expected visible text for the user's mixed-format example in a box");
            bitmap.Dispose();
        }

        // ── Underline tests ───────────────────────────────────────

        [Fact]
        public void TextSpan_Underline_DefaultIsFalse()
        {
            var span = new TextSpan("Hello");
            Assert.False(span.Underline);
        }

        [Fact]
        public void TextSpan_Underline_CanBeSetTrue()
        {
            var span = new TextSpan("Hello", underline: true);
            Assert.True(span.Underline);
        }

        [Fact]
        public void DrawRichText_Underline_RendersLineBelow()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Normal ", PdfFont.Helvetica, 24),
                new TextSpan("Underlined", PdfFont.Helvetica, 24, underline: true)
            }, 50, 50);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtext_underline");
            var bitmap = TestHelper.RasterizePage(bytes, "richtext_underline");

            // The underlined span starts after "Normal " which is ~7 chars wide
            // Check for underline pixels below the text baseline
            float ulTop = 50 + 24; // baseline
            float ulBottom = ulTop + 24 * 0.2f;
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(100), PtToPx(300),
                PtToPx(ulTop), PtToPx(ulBottom)),
                "Expected underline pixels below the underlined span");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichTextBox_Underline_RendersOnWrappedLines()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float endY = page.DrawText(new[]
            {
                new TextSpan("This underlined text should wrap across lines.", PdfFont.Helvetica, 14, underline: true)
            }, 50, 50, width: 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtextbox_underline");
            var bitmap = TestHelper.RasterizePage(bytes, "richtextbox_underline");

            float lineHeight = 14 * 1.2f;
            int linesRendered = (int)((endY - 50) / lineHeight + 0.5f);
            Assert.True(linesRendered >= 2, $"Expected at least 2 lines, got {linesRendered}");

            for (int line = 0; line < linesRendered; line++)
            {
                float lineY = 50 + line * lineHeight;
                float ulTop = lineY + 14;
                float ulBottom = ulTop + 14 * 0.2f;
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250),
                    PtToPx(ulTop), PtToPx(ulBottom)),
                    $"Expected underline on wrapped line {line + 1}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawRichTextBox_PartialUnderline_OnlyUnderlinedSpansHaveLine()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText(new[]
            {
                new TextSpan("Normal ", PdfFont.Helvetica, 24),
                new TextSpan("Underlined", PdfFont.Helvetica, 24, underline: true)
            }, 50, 50, width: 500);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "richtextbox_partial_underline");
            var bitmap = TestHelper.RasterizePage(bytes, "richtextbox_partial_underline");

            // Both text areas should be visible
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(400),
                PtToPx(50), PtToPx(74)),
                "Expected visible text");
            bitmap.Dispose();
        }

        // ── WrapRichText unit tests ──────────────────────────────

        [Fact]
        public void WrapRichText_SingleSpan_MatchesWrapTextLineCount()
        {
            string text = "Hello world this is a test of wrapping";
            float maxWidth = 100;
            var font = PdfFont.Helvetica;
            float fontSize = 12;

            var plainLines = FontMetrics.WrapText(text, font, fontSize, maxWidth);
            var richLines = FontMetrics.WrapRichText(
                new[] { new TextSpan(text, font, fontSize) }, maxWidth);

            Assert.Equal(plainLines.Count, richLines.Count);
        }

        [Fact]
        public void WrapRichText_SpanBoundaryPreservesSpaces()
        {
            var lines = FontMetrics.WrapRichText(new[]
            {
                new TextSpan("Hello ", PdfFont.Helvetica, 12),
                new TextSpan("world", PdfFont.Helvetica, 12)
            }, 500);

            Assert.Single(lines);
            Assert.Equal(2, lines[0].Words.Count);
            Assert.Equal("Hello", lines[0].Words[0].Text);
            Assert.Equal("world", lines[0].Words[1].Text);
            Assert.True(lines[0].Words[1].HasLeadingSpace,
                "Space between spans should be preserved");
        }

        [Fact]
        public void WrapRichText_NoSpaceBetweenSpans_AbutsWords()
        {
            var lines = FontMetrics.WrapRichText(new[]
            {
                new TextSpan("Hello", PdfFont.Helvetica, 12),
                new TextSpan("world", PdfFont.Helvetica, 12)
            }, 500);

            Assert.Single(lines);
            Assert.Equal(2, lines[0].Words.Count);
            Assert.False(lines[0].Words[1].HasLeadingSpace,
                "No space between abutted spans");
        }

        [Fact]
        public void WrapRichText_LeadingSpaceOnSecondSpan_PreservesSpace()
        {
            var lines = FontMetrics.WrapRichText(new[]
            {
                new TextSpan("Hello", PdfFont.Helvetica, 12),
                new TextSpan(" world", PdfFont.CourierBold, 14)
            }, 500);

            Assert.Single(lines);
            Assert.Equal(2, lines[0].Words.Count);
            Assert.True(lines[0].Words[1].HasLeadingSpace,
                "Leading space on second span should create space");
        }
    }
}
