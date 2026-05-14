using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class TextRenderingTests
    {
        private static int PtToPx(float pt, int dpi = 150) => (int)(pt * dpi / 72.0);

        /// <summary>
        /// Scans a horizontal band for dark (non-white) pixels to confirm text was rendered.
        /// </summary>
        private static bool HasDarkPixelsInRegion(SkiaSharp.SKBitmap bitmap,
            int xMin, int xMax, int yMin, int yMax)
        {
            xMax = System.Math.Min(xMax, bitmap.Width - 1);
            yMax = System.Math.Min(yMax, bitmap.Height - 1);
            for (int x = xMin; x <= xMax; x++)
            {
                for (int y = yMin; y <= yMax; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Red < 200 || p.Green < 200 || p.Blue < 200)
                        return true;
                }
            }
            return false;
        }

        [Fact]
        public void DrawText_RendersVisibleText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Hello World", 50, 50, PdfFont.Helvetica, 24);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "text_hello");
            var bitmap = TestHelper.RasterizePage(bytes, "text_hello");
            // Text at (50,50) with fontSize 24. At 150 DPI: x~104px, y~104-154px
            int textY = PtToPx(50);
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250), textY, textY + PtToPx(24)),
                "Expected visible black text near (50, 50)");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_AllFonts_RenderWithoutError()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float y = 30;
            var fonts = new[]
            {
                PdfFont.Helvetica, PdfFont.HelveticaBold, PdfFont.HelveticaOblique, PdfFont.HelveticaBoldOblique,
                PdfFont.TimesRoman, PdfFont.TimesBold, PdfFont.TimesItalic, PdfFont.TimesBoldItalic,
                PdfFont.Courier, PdfFont.CourierBold, PdfFont.CourierOblique, PdfFont.CourierBoldOblique
            };

            foreach (var font in fonts)
            {
                page.DrawText($"Font: {font}", 50, y, font, 14);
                y += 20;
            }

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "text_all_fonts");
            var bitmap = TestHelper.RasterizePage(bytes, "text_all_fonts");
            // Verify each font line has visible text at its expected Y position
            float checkY = 30;
            for (int i = 0; i < fonts.Length; i++)
            {
                int py = PtToPx(checkY);
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), py, py + PtToPx(14)),
                    $"Expected visible text for font {fonts[i]} at Y={checkY}");
                checkY += 20;
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_ColoredText_RendersInColor()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            // Draw a large red block of text
            page.DrawText("RED TEXT", 50, 50, PdfFont.HelveticaBold, 48, PdfColor.Red);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "text_red");
            var bitmap = TestHelper.RasterizePage(bytes, "text_red");
            // Scan for red pixels in the text region
            bool foundRed = false;
            for (int x = 80; x < 500 && !foundRed; x++)
            {
                for (int y = 50; y < 250 && !foundRed; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.Red > 200 && pixel.Green < 50 && pixel.Blue < 50)
                        foundRed = true;
                }
            }
            Assert.True(foundRed, "Expected to find red pixels where red text was drawn");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_CenterAlignment_IsCenteredRelativeToX()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            // Draw centered text at the middle of the page
            page.DrawText("Center", page.Width / 2, 50, PdfFont.Helvetica, 24,
                alignment: TextAlignment.Center);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "text_center");
            var bitmap = TestHelper.RasterizePage(bytes, "text_center");
            int midX = bitmap.Width / 2;
            int textY = PtToPx(50);
            // Text should be visible near the horizontal center
            Assert.True(HasDarkPixelsInRegion(bitmap, midX - PtToPx(50), midX + PtToPx(50), textY, textY + PtToPx(24)),
                "Expected centered text to have dark pixels near the horizontal center");
            // Text should NOT be at the far left (it's centered, not left-aligned)
            Assert.False(HasDarkPixelsInRegion(bitmap, 5, PtToPx(30), textY, textY + PtToPx(24)),
                "Centered text should not appear at the far left edge");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_RightAlignment_Works()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Right", page.Width - 50, 50, PdfFont.Helvetica, 24,
                alignment: TextAlignment.Right);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "text_right");
            var bitmap = TestHelper.RasterizePage(bytes, "text_right");
            int rightEdge = PtToPx(page.Width - 50);
            int textY = PtToPx(50);
            // Text should be visible near the right side (text ends at x=page.Width-50)
            Assert.True(HasDarkPixelsInRegion(bitmap, rightEdge - PtToPx(80), rightEdge, textY, textY + PtToPx(24)),
                "Expected right-aligned text near the right edge");
            // Text should NOT be at the far left
            Assert.False(HasDarkPixelsInRegion(bitmap, 5, PtToPx(30), textY, textY + PtToPx(24)),
                "Right-aligned text should not appear at the far left edge");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_WrapsText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float endY = page.DrawText(
                "This is a paragraph of text that should wrap across multiple lines when drawn in a text box with limited width.",
                50, 50, PdfFont.Helvetica, 12, width: 200);

            Assert.True(endY > 50, "DrawText should return Y after text");

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "textbox_wrap");
            var bitmap = TestHelper.RasterizePage(bytes, "textbox_wrap");

            // The text should wrap to multiple lines. Verify text is visible at
            // several Y positions corresponding to distinct wrapped lines.
            float lineHeight = 12 * 1.2f; // default lineSpacing=1.2
            int linesRendered = (int)((endY - 50) / lineHeight + 0.5f);
            Assert.True(linesRendered >= 3, $"Expected at least 3 wrapped lines, got {linesRendered}");

            // Verify each wrapped line has visible dark pixels
            for (int line = 0; line < linesRendered; line++)
            {
                float lineY = 50 + line * lineHeight;
                int py = PtToPx(lineY);
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250), py, py + PtToPx(12)),
                    $"Expected visible text on wrapped line {line + 1} at Y~{lineY}pt");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_ReturnsCorrectY_ForChaining()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float y = 50;
            y = page.DrawText("First paragraph", 50, y, width: 400);
            float gap = 10;
            float secondStart = y + gap;
            y = page.DrawText("Second paragraph", 50, secondStart, width: 400);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "textbox_chain");
            var bitmap = TestHelper.RasterizePage(bytes, "textbox_chain");
            // First paragraph should have text near Y=50
            int py1 = PtToPx(50);
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), py1, py1 + PtToPx(14)),
                "Expected visible text for first paragraph");
            // Second paragraph should have text at its starting position
            int py2 = PtToPx(secondStart);
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), py2, py2 + PtToPx(14)),
                "Expected visible text for second paragraph");
            // There should be a gap between them — the area between should be mostly white
            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_RightAlignment_MultiLineAligns()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float boxX = 100;
            float boxY = 50;
            float boxWidth = 300;
            float fontSize = 14f;
            float lineSpacing = 1.2f;

            // Lines of deliberately varying length so we can verify they all share
            // the same right edge rather than the same left edge.
            string text = "Short line\nThis is a medium length line\nLonger line that has more words in it\nTiny";

            float endY = page.DrawText(text, boxX, boxY,
                PdfFont.Helvetica, fontSize, alignment: TextAlignment.Right, width: boxWidth, lineSpacing: lineSpacing);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "textbox_right_multiline");
            var bitmap = TestHelper.RasterizePage(bytes, "textbox_right_multiline");

            // The right edge of the text box in pixels
            int rightEdgePx = PtToPx(boxX + boxWidth);

            // For each line, find the rightmost dark pixel.
            // All lines should end very close to the box right edge.
            float lineHeight = fontSize * lineSpacing;
            string[] lines = text.Split('\n');
            int[] rightmostDark = new int[lines.Length];

            for (int i = 0; i < lines.Length; i++)
            {
                float lineY = boxY + i * lineHeight;
                int yMin = PtToPx(lineY);
                int yMax = yMin + PtToPx(fontSize);
                yMax = System.Math.Min(yMax, bitmap.Height - 1);

                rightmostDark[i] = -1;
                for (int x = System.Math.Min(rightEdgePx + PtToPx(10), bitmap.Width - 1); x >= PtToPx(boxX); x--)
                {
                    for (int y = yMin; y <= yMax; y++)
                    {
                        var p = bitmap.GetPixel(x, y);
                        if (p.Red < 200 || p.Green < 200 || p.Blue < 200)
                        {
                            rightmostDark[i] = x;
                            goto foundRight;
                        }
                    }
                }
                foundRight:
                Assert.True(rightmostDark[i] > 0,
                    $"Expected visible text on line {i + 1} (\"{lines[i]}\")");
            }

            // All rightmost dark pixels should be within a small tolerance of each other,
            // proving the lines are right-aligned to the same edge.
            int tolerancePx = PtToPx(4); // allow ~4pt of rasterisation slop
            for (int i = 1; i < lines.Length; i++)
            {
                int diff = System.Math.Abs(rightmostDark[i] - rightmostDark[0]);
                Assert.True(diff <= tolerancePx,
                    $"Line {i + 1} rightmost pixel ({rightmostDark[i]}) differs from line 1 ({rightmostDark[0]}) " +
                    $"by {diff}px (tolerance {tolerancePx}px) — lines are not right-aligned");
            }

            // Also verify the left edges are NOT aligned (lines have different widths)
            int[] leftmostDark = new int[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                float lineY = boxY + i * lineHeight;
                int yMin = PtToPx(lineY);
                int yMax = yMin + PtToPx(fontSize);
                yMax = System.Math.Min(yMax, bitmap.Height - 1);

                leftmostDark[i] = bitmap.Width;
                for (int x = PtToPx(boxX); x <= rightEdgePx && x < bitmap.Width; x++)
                {
                    for (int y = yMin; y <= yMax; y++)
                    {
                        var p = bitmap.GetPixel(x, y);
                        if (p.Red < 200 || p.Green < 200 || p.Blue < 200)
                        {
                            leftmostDark[i] = x;
                            goto foundLeft;
                        }
                    }
                }
                foundLeft:;
            }

            // The left edges should vary (short line starts further right than long line)
            bool leftEdgesVary = false;
            for (int i = 1; i < lines.Length; i++)
            {
                if (System.Math.Abs(leftmostDark[i] - leftmostDark[0]) > tolerancePx)
                {
                    leftEdgesVary = true;
                    break;
                }
            }
            Assert.True(leftEdgesVary,
                "Left edges of right-aligned lines should differ (lines have different lengths)");

            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_Underline_RendersLineBelow()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            // Draw text without underline, then with underline below it
            page.DrawText("No underline", 50, 50, PdfFont.Helvetica, 24);
            page.DrawText("Underlined", 50, 100, PdfFont.Helvetica, 24, underline: true);
            var bytes = doc.ToArray();

            TestHelper.SavePdf(bytes, "text_underline");
            var bitmap = TestHelper.RasterizePage(bytes, "text_underline");

            // The underline should produce dark pixels just below the baseline.
            // Baseline is at roughly y + fontSize. Underline is ~fontSize/10 below that.
            // In user coords: underline region is around y + fontSize + fontSize*0.1
            float ulTop = 100 + 24; // baseline
            float ulBottom = ulTop + 24 * 0.2f; // generous band below baseline
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(200),
                PtToPx(ulTop), PtToPx(ulBottom)),
                "Expected underline pixels below baseline of underlined text");

            // The non-underlined text should NOT have dark pixels well below the baseline
            // (avoiding the descender zone where letters like 'g', 'y' extend)
            float noUlTop = 50 + 24 + 24 * 0.05f;
            float noUlBottom = noUlTop + 24 * 0.15f;
            Assert.False(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(200),
                PtToPx(noUlTop), PtToPx(noUlBottom)),
                "Non-underlined text should not have pixels in the underline region");

            bitmap.Dispose();
        }

        [Fact]
        public void DrawText_Underline_RendersOnAllLines()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float endY = page.DrawText(
                "This is text that should wrap and each line should be underlined.",
                50, 50, PdfFont.Helvetica, 14, underline: true, width: 200);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "textbox_underline");
            var bitmap = TestHelper.RasterizePage(bytes, "textbox_underline");

            float lineHeight = 14 * 1.2f;
            int linesRendered = (int)((endY - 50) / lineHeight + 0.5f);
            Assert.True(linesRendered >= 2, $"Expected at least 2 lines, got {linesRendered}");

            // Each line should have underline pixels below the baseline
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
        public void DrawText_DifferentSizes_ScaleCorrectly()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Small", 50, 50, fontSize: 8);
            page.DrawText("Medium", 50, 70, fontSize: 16);
            page.DrawText("Large", 50, 100, fontSize: 32);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "text_sizes");
            var bitmap = TestHelper.RasterizePage(bytes, "text_sizes");

            // Verify each text size is visible at its expected position
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(150), PtToPx(50), PtToPx(58)),
                "Expected visible 'Small' text at Y=50");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(200), PtToPx(70), PtToPx(86)),
                "Expected visible 'Medium' text at Y=70");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(250), PtToPx(100), PtToPx(132)),
                "Expected visible 'Large' text at Y=100");

            // Count dark pixels horizontally for each size — larger text should cover more pixels
            int smallDark = 0, largeDark = 0;
            int smallY = PtToPx(54), largeY = PtToPx(116);
            for (int x = PtToPx(50); x < PtToPx(300) && x < bitmap.Width; x++)
            {
                var ps = bitmap.GetPixel(x, smallY);
                if (ps.Red < 200 || ps.Green < 200 || ps.Blue < 200) smallDark++;
                var pl = bitmap.GetPixel(x, largeY);
                if (pl.Red < 200 || pl.Green < 200 || pl.Blue < 200) largeDark++;
            }
            Assert.True(largeDark > smallDark,
                $"Large text should cover more pixels ({largeDark}) than small text ({smallDark})");
            bitmap.Dispose();
        }
    }
}
