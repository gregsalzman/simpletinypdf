using System.IO;
using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class UnicodeFontTests
    {
        private static readonly string CjkFontPath =
            Path.Combine("TestAssets", "NotoSansJP-Regular.ttf");

        private static readonly string RobotoFontPath =
            Path.Combine("TestAssets", "Roboto-Regular.ttf");

        // ── CJK rendering ──────────────────────────────────────────

        [Fact]
        public void CjkText_Japanese_RendersVisiblePixels()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: Japanese CJK characters render with NotoSansJP font");
            // "こんにちは" = Konnichiwa in Hiragana
            page.DrawText("\u3053\u3093\u306B\u3061\u306F", 50, 50, font, 24);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-cjk-japanese");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-cjk-japanese");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50) - 5, TestHelper.PtToPx(200), TestHelper.PtToPx(35), TestHelper.PtToPx(60)),
                "CJK Japanese text should render visible pixels");
            bitmap.Dispose();
        }

        [Fact]
        public void CjkText_Chinese_RendersVisiblePixels()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: Chinese CJK characters render correctly");
            // "世界" = World in Chinese
            page.DrawText("\u4E16\u754C", 50, 50, font, 24);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-cjk-chinese");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-cjk-chinese");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50) - 5, TestHelper.PtToPx(120), TestHelper.PtToPx(35), TestHelper.PtToPx(60)),
                "CJK Chinese text should render visible pixels");
            bitmap.Dispose();
        }

        // ── Cyrillic rendering ──────────────────────────────────────

        [Fact]
        public void CyrillicText_RendersVisiblePixels()
        {
            var font = PdfFontSource.FromFile(RobotoFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: Cyrillic characters render correctly");
            // "Привет" = Hello in Russian
            page.DrawText("\u041F\u0440\u0438\u0432\u0435\u0442", 50, 50, font, 24);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-cyrillic-text");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-cyrillic-text");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50) - 5, TestHelper.PtToPx(200), TestHelper.PtToPx(35), TestHelper.PtToPx(60)),
                "Cyrillic text should render visible pixels");
            bitmap.Dispose();
        }

        // ── Mixed scripts ───────────────────────────────────────────

        [Fact]
        public void MixedLatinAndCjk_BothRender()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: Latin and CJK text render together on same line");
            // "Hello 世界" = Hello World
            page.DrawText("Hello \u4E16\u754C", 50, 50, font, 24);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-mixed-latin-cjk");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-mixed-latin-cjk");
            // Latin part
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50) - 5, TestHelper.PtToPx(120), TestHelper.PtToPx(35), TestHelper.PtToPx(60)),
                "Latin part should render");
            // CJK part (to the right)
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(120), TestHelper.PtToPx(250), TestHelper.PtToPx(35), TestHelper.PtToPx(60)),
                "CJK part should render");
            bitmap.Dispose();
        }

        [Fact]
        public void RichText_MixedBuiltInAndCjk_BothRender()
        {
            var cjkFont = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: Unicode text works in rich text spans");
            page.DrawText(new[]
            {
                new TextSpan("Hello ", PdfFont.HelveticaBold, 20),
                new TextSpan("\u4E16\u754C", cjkFont, 20)
            }, 50, 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-richtext-mixed");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-richtext-mixed");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50) - 5, TestHelper.PtToPx(250), TestHelper.PtToPx(35), TestHelper.PtToPx(60)),
                "Mixed rich text should render");
            bitmap.Dispose();
        }

        // ── PDF structure assertions ────────────────────────────────

        [Fact]
        public void CjkPdf_ContainsType0FontStructure()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("\u4E16\u754C", 50, 50, font, 12);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.Contains("/Subtype /Type0", pdfText);
            Assert.Contains("/Encoding /Identity-H", pdfText);
            Assert.Contains("/CIDFontType2", pdfText);
            Assert.Contains("/FontFile2", pdfText);
            Assert.Contains("begincmap", pdfText);
            Assert.Contains("beginbfchar", pdfText);
            Assert.Contains("endcmap", pdfText);
        }

        [Fact]
        public void CjkPdf_DoesNotContainWinAnsiForCustomFonts()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("\u4E16\u754C", 50, 50, font, 12);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.DoesNotContain("/Encoding /WinAnsiEncoding", pdfText);
            Assert.DoesNotContain("/Subtype /TrueType\n", pdfText);
        }

        [Fact]
        public void ContentStream_UsesHexEncoding_ForCustomFonts()
        {
            var font = PdfFontSource.FromFile(RobotoFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Hi", 50, 50, font, 12);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            // Content stream should contain hex glyph IDs (angle brackets), not (Hi)
            Assert.Contains("<", pdfText);
            Assert.Contains("> Tj", pdfText);
        }

        [Fact]
        public void BuiltInFonts_StillUseType1()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Hello", 50, 50, PdfFont.Helvetica, 12);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.Contains("/Subtype /Type1", pdfText);
            Assert.DoesNotContain("/Subtype /Type0", pdfText);
            Assert.DoesNotContain("/Identity-H", pdfText);
        }

        // ── Font deduplication ──────────────────────────────────────

        [Fact]
        public void CjkFont_DeduplicatedAcrossPages()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page1 = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page1, "Verify: same custom font is deduplicated across pages");
            page1.DrawText("\u3053\u3093", 50, 50, font, 12); // first two chars
            var page2 = doc.AddPage(PageSize.A4);
            page2.DrawText("\u306B\u3061\u306F", 50, 50, font, 12); // next three chars
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-font-deduplication");

            var pdfText = Encoding.ASCII.GetString(bytes);

            // Should have only one /FontFile2 (font stream deduplicated)
            int fontFileCount = 0;
            int idx = 0;
            while ((idx = pdfText.IndexOf("/FontFile2", idx)) >= 0)
            {
                fontFileCount++;
                idx += 10;
            }
            Assert.Equal(1, fontFileCount);

            // ToUnicode CMap should include glyphs from both pages
            Assert.Contains("beginbfchar", pdfText);
        }

        // ── Measurement ─────────────────────────────────────────────

        [Fact]
        public void MeasureText_CjkCharacters_ReturnsPositiveWidth()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            float width = page.MeasureText("\u4E16\u754C", font, 12);
            Assert.True(width > 0, "CJK character width should be positive");
        }

        [Fact]
        public void MeasureText_CyrillicCharacters_ReturnsPositiveWidth()
        {
            var font = PdfFontSource.FromFile(RobotoFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            float width = page.MeasureText("\u041F\u0440\u0438\u0432\u0435\u0442", font, 12);
            Assert.True(width > 0, "Cyrillic character width should be positive");
        }

        // ── Text wrapping ───────────────────────────────────────────

        [Fact]
        public void WrapText_CjkText_WrapsAtNarrowWidth()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            TestHelper.AddDescription(page, "Verify: CJK text wraps correctly in textbox");
            // Draw CJK words separated by spaces in a narrow box
            string longText = "\u3053\u3093\u306B\u3061\u306F \u4E16\u754C \u3053\u3093\u306B\u3061\u306F \u4E16\u754C";
            float endY = page.DrawText(longText, 50, 50, font, 14, width: 100);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-wrapped-cjk-text");

            // endY should be below 50 (text wrapped to multiple lines)
            Assert.True(endY > 70, $"CJK text should wrap; endY={endY}");
        }

        // ── Table with CJK ─────────────────────────────────────────

        [Fact]
        public void Table_WithCjkFont_Renders()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: CJK text renders correctly in table cells");

            var table = new PdfTable(200, 200);
            table.SetHeaders("\u540D\u524D", "\u5024"); // 名前, 値
            table.AddRow("\u7530\u4E2D", "\u6771\u4EAC"); // 田中, 東京
            table.HeaderFont = font;
            table.CellFont = font;

            page.DrawTable(table, 50, 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-cjk-in-table");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-cjk-in-table");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(50), TestHelper.PtToPx(120)),
                "CJK table should render visible content");
            bitmap.Dispose();
        }

        // ── OTF CFF with CID ───────────────────────────────────────

        [Fact]
        public void OtfCffFont_UsesCIDFontType0()
        {
            var fontPath = Path.Combine("TestAssets", "SourceCodePro-Regular.otf");
            var font = PdfFontSource.FromFile(fontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Hello", 50, 50, font, 12);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.Contains("/Subtype /Type0", pdfText);
            Assert.Contains("/CIDFontType0", pdfText);
            Assert.Contains("/FontFile3", pdfText);
            Assert.Contains("/Identity-H", pdfText);
            Assert.DoesNotContain("/CIDToGIDMap", pdfText); // CFF fonts don't use CIDToGIDMap
        }

        [Fact]
        public void TtfFont_UsesCIDFontType2WithCIDToGIDMap()
        {
            var font = PdfFontSource.FromFile(RobotoFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            page.DrawText("Hello", 50, 50, font, 12);
            var bytes = doc.ToArray();

            var pdfText = Encoding.ASCII.GetString(bytes);
            Assert.Contains("/Subtype /Type0", pdfText);
            Assert.Contains("/CIDFontType2", pdfText);
            Assert.Contains("/CIDToGIDMap /Identity", pdfText);
            Assert.Contains("/FontFile2", pdfText);
        }

        // ── Nested lists with Unicode ────────────────────────────────

        [Fact]
        public void NestedList_CjkBulletList_Renders()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: CJK text in nested bullet lists");

            var items = new[]
            {
                new ListItem("\u65E5\u672C\u8A9E",                      // 日本語
                    new ListItem("\u6771\u4EAC"),                        // 東京
                    new ListItem("\u5927\u962A"),                        // 大阪
                    new ListItem("\u4EAC\u90FD")),                       // 京都
                new ListItem("\u4E2D\u6587",                             // 中文
                    new ListItem("\u5317\u4EAC"),                        // 北京
                    new ListItem("\u4E0A\u6D77")),                       // 上海
                new ListItem("\u5E83\u6771\u8A9E",                        // 広東語 (Cantonese)
                    new ListItem("\u9999\u6E2F"),                        // 香港
                    new ListItem("\u6FB3\u9580"))                        // 澳門
            };

            var (lastPage, endY) = page.DrawList(items, 50, 50, 400, font: font, fontSize: 14);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-nested-list-cjk");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-nested-list-cjk");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(40), TestHelper.PtToPx(200)),
                "CJK nested bullet list should render visible content");
            Assert.True(endY > 100, $"List should span multiple lines; endY={endY}");
            bitmap.Dispose();
        }

        [Fact]
        public void NestedList_CjkNumberedList_Renders()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: CJK text in nested numbered lists");

            var items = new[]
            {
                new ListItem("\u5B66\u6821", ListStyle.RomanLower,      // 学校 → children use i, ii, iii
                    new ListItem("\u5C0F\u5B66\u6821"),                  // 小学校
                    new ListItem("\u4E2D\u5B66\u6821"),                  // 中学校
                    new ListItem("\u9AD8\u7B49\u5B66\u6821")),           // 高等学校
                new ListItem("\u56F3\u66F8\u9928", ListStyle.RomanLower, // 図書館
                    new ListItem("\u516C\u5171"),                         // 公共
                    new ListItem("\u5927\u5B66"))                         // 大学
            };

            var (lastPage, endY) = page.DrawList(items, 50, 50, 400,
                style: ListStyle.Numbered, font: font, fontSize: 14);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-nested-list-numbered");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-nested-list-numbered");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(40), TestHelper.PtToPx(200)),
                "CJK numbered nested list should render visible content");
            bitmap.Dispose();
        }

        [Fact]
        public void NestedList_CyrillicWithCustomFont_Renders()
        {
            var font = PdfFontSource.FromFile(RobotoFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: Cyrillic text in nested lists");

            var items = new[]
            {
                new ListItem("\u0420\u043E\u0441\u0441\u0438\u044F",     // Россия
                    new ListItem("\u041C\u043E\u0441\u043A\u0432\u0430"), // Москва
                    new ListItem("\u0421\u0430\u043D\u043A\u0442-\u041F\u0435\u0442\u0435\u0440\u0431\u0443\u0440\u0433")), // Санкт-Петербург
                new ListItem("\u0423\u043A\u0440\u0430\u0457\u043D\u0430", // Україна
                    new ListItem("\u041A\u0438\u0457\u0432"),              // Київ
                    new ListItem("\u041B\u044C\u0432\u0456\u0432"))        // Львів
            };

            var (lastPage, endY) = page.DrawList(items, 50, 50, 400, font: font, fontSize: 12);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-nested-list-cyrillic");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-nested-list-cyrillic");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(40), TestHelper.PtToPx(180)),
                "Cyrillic nested list should render visible content");
            bitmap.Dispose();
        }

        [Fact]
        public void NestedList_MixedScriptsAndStyles_Renders()
        {
            var cjkFont = PdfFontSource.FromFile(CjkFontPath);
            var roboto = PdfFontSource.FromFile(RobotoFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: mixed Unicode scripts in nested lists");

            // Top-level uses CJK font with numbered style
            // Mix: draw the list with CJK font, then a second with Cyrillic
            var cjkItems = new[]
            {
                new ListItem("\u98DF\u3079\u7269", ListStyle.Bullet,     // 食べ物 (Food)
                    new ListItem("\u5BFF\u53F8"),                          // 寿司
                    new ListItem("\u30E9\u30FC\u30E1\u30F3"),              // ラーメン
                    new ListItem("\u5929\u3077\u3089"))                    // 天ぷら
            };

            var (nextPage, nextY) = page.DrawList(cjkItems, 50, 50, 400,
                style: ListStyle.Numbered, font: cjkFont, fontSize: 13);

            // Draw a second list in Cyrillic below
            var cyrillicItems = new[]
            {
                new ListItem("\u0415\u0434\u0430",                        // Еда (Food)
                    new ListItem("\u0411\u043E\u0440\u0449"),              // Борщ
                    new ListItem("\u041F\u0435\u043B\u044C\u043C\u0435\u043D\u0438")) // Пельмени
            };

            var (finalPage, finalY) = nextPage.DrawList(cyrillicItems, 50, nextY + 20, 400,
                style: ListStyle.Numbered, font: roboto, fontSize: 13);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-nested-list-mixed");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-nested-list-mixed");
            // CJK section
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(40), TestHelper.PtToPx(130)),
                "CJK list section should render");
            // Cyrillic section (below CJK)
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(140), TestHelper.PtToPx(250)),
                "Cyrillic list section should render");
            bitmap.Dispose();
        }

        [Fact]
        public void NestedList_CjkWithTextWrapping_Wraps()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: Unicode nested list text wraps correctly");

            // Long CJK text items that should wrap in a narrow width
            var items = new[]
            {
                new ListItem("\u3053\u308C\u306F \u3068\u3066\u3082 \u9577\u3044 \u65E5\u672C\u8A9E\u306E \u6587\u7AE0\u3067\u3059", // これは とても 長い 日本語の 文章です
                    new ListItem("\u5B50\u4F9B\u306E \u9805\u76EE\u3082 \u9577\u3044\u3067\u3059")), // 子供の 項目も 長いです
                new ListItem("\u4E8C\u756A\u76EE\u306E \u9805\u76EE")                                 // 二番目の 項目
            };

            var (lastPage, endY) = page.DrawList(items, 50, 50, 150, font: font, fontSize: 14);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-nested-list-wrapped");

            // With 150pt width and 14pt CJK text, items should wrap
            Assert.True(endY > 120, $"CJK list items should wrap in narrow width; endY={endY}");
        }

        // ── Supplementary Plane (U+10000+) ───────────────────────────

        [Fact]
        public void SupplementaryPlane_CjkExtB_Renders()
        {
            // U+2000B, U+20089, U+200A2 are CJK Extension B characters in NotoSansJP
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: supplementary plane CJK Extension B characters render");
            string text = "\U0002000B\U00020089\U000200A2"; // 3 CJK Ext B chars
            page.DrawText(text, 50, 50, font: font, fontSize: 24);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-supplementary-cjk-ext-b");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-supplementary-cjk-ext-b");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(200), TestHelper.PtToPx(35), TestHelper.PtToPx(65)),
                "CJK Extension B characters should render visible content");
            bitmap.Dispose();
        }

        [Fact]
        public void SupplementaryPlane_MeasureText_ReturnsPositiveWidth()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            // U+2000B is a CJK Extension B character
            string text = "\U0002000B";
            float width = FontMetrics.MeasureString(text, font, 12f);
            Assert.True(width > 0, $"Supplementary char U+2000B should have positive width; got {width}");
        }

        [Fact]
        public void SupplementaryPlane_MixedBmpAndSupplementary()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: mix of BMP and supplementary plane characters");
            // Mix BMP CJK (日本) + supplementary CJK Ext B (U+2000B) + BMP Latin (ABC)
            string text = "\u65E5\u672C\U0002000BABC";
            page.DrawText(text, 50, 50, font: font, fontSize: 18);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-supplementary-mixed-bmp");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-supplementary-mixed-bmp");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(250), TestHelper.PtToPx(35), TestHelper.PtToPx(65)),
                "Mixed BMP + supplementary text should render");
            bitmap.Dispose();
        }

        [Fact]
        public void SupplementaryPlane_ToUnicodeCMap_ContainsSurrogatePairs()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            // U+2000B → UTF-16 surrogates: D840 DC0B
            page.DrawText("\U0002000B", 50, 50, font: font, fontSize: 12);

            var bytes = doc.ToArray();
            var pdfText = Encoding.ASCII.GetString(bytes);

            // ToUnicode CMap should contain surrogate pair mapping
            Assert.Contains("beginbfchar", pdfText);
            Assert.Contains("<D840DC0B>", pdfText.Replace(" ", ""));
        }

        [Fact]
        public void SupplementaryPlane_InTable_Renders()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: supplementary plane characters in table cells");

            var table = new PdfTable(200, 200);
            table.HeaderFont = font;
            table.CellFont = font;
            table.SetHeaders("\u5B57 (BMP)", "\U0002000B (Ext B)");
            table.AddRow("\u6F22\u5B57", "\U00020089\U000200A2");

            page.DrawTable(table, 50, 50, 400);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-supplementary-table");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-supplementary-table");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(40), TestHelper.PtToPx(120)),
                "Table with supplementary characters should render");
            bitmap.Dispose();
        }

        [Fact]
        public void SupplementaryPlane_InList_Renders()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: supplementary plane characters in lists");

            var items = new[]
            {
                new ListItem("\U0002000B\U00020089 (CJK Ext B)"),
                new ListItem("\u65E5\u672C\u8A9E (BMP)",
                    new ListItem("\U000200A2\U000200A4 (more Ext B)"))
            };

            var (lastPage, endY) = page.DrawList(items, 50, 50, 400, font: font, fontSize: 14);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-supplementary-list");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-supplementary-list");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(40), TestHelper.PtToPx(150)),
                "List with supplementary characters should render");
            bitmap.Dispose();
        }

        [Fact]
        public void SupplementaryPlane_HexEncoding_UsesFourDigitGlyphIds()
        {
            var font = PdfFontSource.FromFile(CjkFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            // One supplementary char should produce one 4-digit hex glyph ID, not two
            page.DrawText("\U0002000B", 50, 50, font: font, fontSize: 12);

            var bytes = doc.ToArray();
            var pdfText = Encoding.ASCII.GetString(bytes);

            // Content stream should have a hex string with exactly 4 hex digits (1 glyph)
            // followed by Tj, not 8 digits (which would mean surrogates encoded separately)
            Assert.Matches(@"<[0-9A-F]{4}> Tj", pdfText);
        }

        // ── Showcase ────────────────────────────────────────────────

        [Fact]
        public void Showcase_MultiScriptDocument()
        {
            var cjkFont = PdfFontSource.FromFile(CjkFontPath);
            var roboto = PdfFontSource.FromFile(RobotoFontPath);
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: comprehensive showcase of Unicode support across all features");

            float y = 50;
            page.DrawText("Unicode Font Support Showcase", 50, y, PdfFont.HelveticaBold, 20);
            y += 40;

            page.DrawText("English: The quick brown fox jumps over the lazy dog", 50, y, roboto, 12);
            y += 25;

            // Cyrillic
            page.DrawText("\u041A\u0438\u0440\u0438\u043B\u043B\u0438\u0446\u0430: \u041F\u0440\u0438\u0432\u0435\u0442 \u043C\u0438\u0440", 50, y, roboto, 12);
            y += 25;

            // Japanese
            page.DrawText("\u65E5\u672C\u8A9E: \u3053\u3093\u306B\u3061\u306F\u4E16\u754C", 50, y, cjkFont, 12);
            y += 25;

            // Chinese
            page.DrawText("\u4E2D\u6587: \u4F60\u597D\u4E16\u754C", 50, y, cjkFont, 12);
            y += 25;

            // Korean (Noto Sans JP may not have Korean — but it has CJK Unified Ideographs)
            page.DrawText("CJK Ideographs: \u5B66\u6821 \u56F3\u66F8\u9928 \u96FB\u8A71", 50, y, cjkFont, 12);
            y += 40;

            // Mixed rich text
            page.DrawText(new[]
            {
                new TextSpan("Built-in: ", PdfFont.HelveticaBold, 14),
                new TextSpan("Hello ", roboto, 14),
                new TextSpan("\u4E16\u754C", cjkFont, 14, PdfColor.Red),
                new TextSpan(" \u041F\u0440\u0438\u0432\u0435\u0442", roboto, 14, PdfColor.Blue)
            }, 50, y);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Fonts/unicode-full-showcase");

            var bitmap = TestHelper.RasterizePage(bytes, "Fonts/unicode-full-showcase");
            // Verify multiple regions have content
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(40), TestHelper.PtToPx(60)),
                "Title should render");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(80), TestHelper.PtToPx(200)),
                "Multi-script text should render");
            bitmap.Dispose();
        }
    }
}
