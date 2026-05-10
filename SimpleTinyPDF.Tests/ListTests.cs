using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class ListTests
    {
        private static int PtToPx(float pt) => (int)(pt * 150 / 72.0);

        private static bool HasDarkPixelsInRegion(SkiaSharp.SKBitmap bitmap,
            int xMin, int xMax, int yMin, int yMax)
        {
            xMax = System.Math.Min(xMax, bitmap.Width - 1);
            yMax = System.Math.Min(yMax, bitmap.Height - 1);
            for (int x = System.Math.Max(0, xMin); x <= xMax; x++)
                for (int y = System.Math.Max(0, yMin); y <= yMax; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Red < 200 || p.Green < 200 || p.Blue < 200) return true;
                }
            return false;
        }

        // ── Flat bullet list ───────────────────────────────────────

        [Fact]
        public void DrawList_Bullet_RendersItems()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[]
            {
                new ListItem("First item"),
                new ListItem("Second item"),
                new ListItem("Third item")
            };
            var (_, endY) = page.DrawList(items, 50, 50, 400);

            Assert.True(endY > 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_bullet");
            var bitmap = TestHelper.RasterizePage(bytes, "list_bullet");
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(itemY), PtToPx(itemY + 14)),
                    $"Expected visible text for bullet item {i + 1} at Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_Numbered_RendersWithNumbers()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[]
            {
                new ListItem("First item"),
                new ListItem("Second item"),
                new ListItem("Third item")
            };
            var (_, endY) = page.DrawList(items, 50, 50, 400, style: ListStyle.Numbered);

            Assert.True(endY > 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_numbered");
            var bitmap = TestHelper.RasterizePage(bytes, "list_numbered");
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(60), PtToPx(itemY), PtToPx(itemY + 14)),
                    $"Expected number marker for item {i + 1} at X=50, Y~{itemY}");
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(70), PtToPx(300), PtToPx(itemY), PtToPx(itemY + 14)),
                    $"Expected text for item {i + 1} at X=70, Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_Bullet_LongItems_WrapCorrectly()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[]
            {
                new ListItem("This is a very long list item that should wrap to multiple lines when rendered in a narrow column width"),
                new ListItem("Short item"),
                new ListItem("Another long item that tests word wrapping behavior within bulleted lists to ensure everything looks correct")
            };
            var (_, endY) = page.DrawList(items, 50, 50, 300);

            Assert.True(endY > 100, "Long items should take more vertical space");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_bullet_wrap");
            var bitmap = TestHelper.RasterizePage(bytes, "list_bullet_wrap");

            float lineHeight = 12 * 1.2f;
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(65), PtToPx(350), PtToPx(50), PtToPx(50 + 12)),
                "Expected text on line 1 of first (long) bullet item");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(65), PtToPx(350), PtToPx(50 + lineHeight), PtToPx(50 + lineHeight + 12)),
                "Expected wrapped text on line 2 of first (long) bullet item");

            float lastItemApproxY = endY - 12 * 1.2f - 12 * 0.3f;
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(350), PtToPx(lastItemApproxY - 20), PtToPx(endY)),
                "Expected visible text for the last bullet item");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_Numbered_CustomStartNumber()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[]
            {
                new ListItem("Item A"),
                new ListItem("Item B"),
                new ListItem("Item C")
            };
            var (_, _) = page.DrawList(items, 50, 50, 400, style: ListStyle.Numbered, startNumber: 5);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_numbered_start5");
            var bitmap = TestHelper.RasterizePage(bytes, "list_numbered_start5");
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(itemY), PtToPx(itemY + 14)),
                    $"Expected visible text for numbered item {5 + i} at Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_Bullet_ColoredText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[]
            {
                new ListItem("Red item one"),
                new ListItem("Red item two")
            };
            page.DrawList(items, 50, 50, 400, color: PdfColor.Red);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_bullet_red");
            var bitmap = TestHelper.RasterizePage(bytes, "list_bullet_red");

            bool foundRed = false;
            for (int x = 80; x < 400 && !foundRed; x++)
                for (int y = 70; y < 200 && !foundRed; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.Red > 200 && pixel.Green < 50 && pixel.Blue < 50)
                        foundRed = true;
                }
            Assert.True(foundRed, "Expected red text in bullet list");
            bitmap.Dispose();
        }

        [Fact]
        public void BulletAndNumberedLists_Together()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            float y = 50;
            page.DrawText("Bullet List:", 50, y, PdfFont.HelveticaBold, 14);
            y += 20;
            float bulletStartY = y;
            (_, y) = page.DrawList(new[]
            {
                new ListItem("Apples"),
                new ListItem("Bananas"),
                new ListItem("Cherries")
            }, 50, y, 400);
            y += 10;
            page.DrawText("Numbered List:", 50, y, PdfFont.HelveticaBold, 14);
            float numberedLabelY = y;
            y += 20;
            float numberedStartY = y;
            page.DrawList(new[]
            {
                new ListItem("Step one"),
                new ListItem("Step two"),
                new ListItem("Step three")
            }, 50, y, 400, style: ListStyle.Numbered);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_both");
            var bitmap = TestHelper.RasterizePage(bytes, "list_both");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(bulletStartY), PtToPx(bulletStartY + 14)),
                "Expected visible text in bullet list section");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(numberedLabelY), PtToPx(numberedLabelY + 14)),
                "Expected 'Numbered List:' label to be visible");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(numberedStartY), PtToPx(numberedStartY + 14)),
                "Expected visible text in numbered list section");
            bitmap.Dispose();
        }

        // ── Nested lists ───────────────────────────────────────────

        [Fact]
        public void DrawList_NestedBullet_RendersAllLevels()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[]
            {
                new ListItem("Level 1 item A",
                    new ListItem("Level 2 item A1"),
                    new ListItem("Level 2 item A2",
                        new ListItem("Level 3 item A2a"),
                        new ListItem("Level 3 item A2b"))),
                new ListItem("Level 1 item B",
                    new ListItem("Level 2 item B1"))
            };
            var (lastPage, endY) = page.DrawList(items, 50, 50, 450);

            Assert.Same(page, lastPage);
            Assert.True(endY > 100, "Nested items should produce significant vertical extent");

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "nested_bullet");
            var bitmap = TestHelper.RasterizePage(bytes, "nested_bullet");

            // Level 1 item at y~50
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(50), PtToPx(64)),
                "Expected level-1 bullet item at top");
            // Level 2 item should appear below and indented
            float level2Y = 50 + 12 * 1.2f + 12 * 0.3f;
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(70), PtToPx(350), PtToPx(level2Y), PtToPx(level2Y + 28)),
                "Expected level-2 item visible below level-1");

            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_NestedNumbered_RendersAllLevels()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[]
            {
                new ListItem("First top-level item",
                    new ListItem("First sub-item"),
                    new ListItem("Second sub-item")),
                new ListItem("Second top-level item")
            };
            var (_, endY) = page.DrawList(items, 50, 50, 450, style: ListStyle.Numbered);

            Assert.True(endY > 80);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "nested_numbered");
            var bitmap = TestHelper.RasterizePage(bytes, "nested_numbered");

            // Top-level number marker at x~50
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(65), PtToPx(50), PtToPx(64)),
                "Expected top-level number marker");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_MixedStyles_NumberedWithBulletChildren()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[]
            {
                new ListItem("Numbered item 1", ListStyle.Bullet,
                    new ListItem("Bullet child A"),
                    new ListItem("Bullet child B")),
                new ListItem("Numbered item 2")
            };
            var (_, endY) = page.DrawList(items, 50, 50, 450, style: ListStyle.Numbered);

            Assert.True(endY > 80);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "nested_mixed");
            var bitmap = TestHelper.RasterizePage(bytes, "nested_mixed");

            // Top-level number marker at x~50
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(65), PtToPx(50), PtToPx(64)),
                "Expected numbered marker for top-level item");
            // Children are bullets — bullet marker should appear indented
            float childY = 50 + 12 * 1.2f + 12 * 0.3f;
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(70), PtToPx(350), PtToPx(childY), PtToPx(childY + 28)),
                "Expected bullet child items below the numbered parent");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_TextWrapsAtAllNestingLevels()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            const string longText = "This text is intentionally very long so that it must wrap to multiple lines at this nesting level within the available width";
            var items = new[]
            {
                new ListItem(longText,
                    new ListItem(longText,
                        new ListItem(longText)))
            };
            var (_, endY) = page.DrawList(items, 50, 50, 400);

            // Three long items should each span multiple lines (3 single-line items would only reach ~104pt)
            Assert.True(endY > 120, "Multiple wrapped items across three levels should use significant vertical space");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "nested_wrap");
        }

        [Fact]
        public void DrawList_FlowsToNextPage()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            // 50 items × ~18pt each = ~900pt starting at y=50, exceeds 842-50=792pt available
            var items = new ListItem[50];
            for (int i = 0; i < items.Length; i++)
                items[i] = new ListItem($"Item {i + 1} with enough text to ensure we eventually overflow the page");

            var (lastPage, endY) = page.DrawList(items, 50, 50, 450,
                bottomMargin: 50, continuationY: 50);

            Assert.True(doc.PageCount > 1, "List should have created a continuation page");
            Assert.NotSame(page, lastPage);
            Assert.True(endY > 50 && endY < 400, "End Y should be near the top of the last page");

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "nested_multipage");

            // Page 0: first page — should have content filling most of the page
            var page0Bitmap = TestHelper.RasterizePage(bytes, "nested_multipage", pageIndex: 0);
            Assert.True(HasDarkPixelsInRegion(page0Bitmap, PtToPx(50), PtToPx(400), PtToPx(50), PtToPx(750)),
                "Expected list content on the first page");
            page0Bitmap.Dispose();

            // Page 1: continuation page — should have content starting near the top
            var page1Bitmap = TestHelper.RasterizePage(bytes, "nested_multipage", pageIndex: 1);
            Assert.True(HasDarkPixelsInRegion(page1Bitmap, PtToPx(50), PtToPx(400), PtToPx(50), PtToPx(200)),
                "Expected list content near the top of the continuation page");
            page1Bitmap.Dispose();
        }

        [Fact]
        public void DrawList_FiveDeepNesting_WrapsAndCustomSymbols()
        {
            // Each ListItem's ChildrenBullet controls the symbol used at the NEXT level down.
            // Level 0: •  (set on DrawList call)
            // Level 1: »  (set on level-0 item's ChildrenBullet)
            // Level 2: –  (set on level-1 item's ChildrenBullet)
            // Level 3: ›  (set on level-2 item's ChildrenBullet)
            // Level 4: ·  (set on level-3 item's ChildrenBullet)

            const string wrap = "This text is intentionally long enough to wrap across at least two lines at this nesting level within the available list width";

            var items = new[]
            {
                new ListItem($"Level 0 — {wrap}",
                    ListStyle.Bullet, new TextSpan("»", PdfFont.Helvetica),
                    new ListItem($"Level 1 — {wrap}",
                        ListStyle.Bullet, new TextSpan("–", PdfFont.HelveticaBold),
                        new ListItem($"Level 2 — {wrap}",
                            ListStyle.Bullet, new TextSpan("›", PdfFont.Helvetica),
                            new ListItem($"Level 3 — {wrap}",
                                ListStyle.Bullet, new TextSpan("·", PdfFont.Helvetica),
                                new ListItem($"Level 4 — {wrap}"))))),
                new ListItem($"Second level-0 item — {wrap}",
                    ListStyle.Bullet, new TextSpan("»", PdfFont.Helvetica),
                    new ListItem($"Second level-1 item — {wrap}"))
            };

            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);

            var (lastPage, endY) = page.DrawList(items,
                x: 50, y: 50, width: 500,
                bullet: new TextSpan("•", PdfFont.Helvetica),
                bottomMargin: 50, continuationY: 50);

            // Five levels of wrapped text should take significant vertical space
            Assert.True(endY > 200 || doc.PageCount > 1,
                "Five levels of wrapped items should produce significant vertical extent or overflow to page 2");

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "nested_deep5");
            for (int p = 0; p < doc.PageCount; p++)
            {
                var bmp = TestHelper.RasterizePage(bytes, "nested_deep5", pageIndex: p);
                Assert.True(HasDarkPixelsInRegion(bmp, PtToPx(50), PtToPx(500), PtToPx(50), PtToPx(700)),
                    $"Expected visible content on page {p}");
                bmp.Dispose();
            }
        }
        // ── Roman numeral lists ────────────────────────────────────

        [Fact]
        public void DrawList_RomanLower_RendersItems()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[]
            {
                new ListItem("First item"),
                new ListItem("Second item"),
                new ListItem("Third item"),
                new ListItem("Fourth item"),
                new ListItem("Fifth item")
            };
            var (_, endY) = page.DrawList(items, 50, 50, 400, style: ListStyle.RomanLower);

            Assert.True(endY > 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_roman_lower");
            var bitmap = TestHelper.RasterizePage(bytes, "list_roman_lower");
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(itemY), PtToPx(itemY + 14)),
                    $"Expected visible content for roman-lower item {i + 1} at Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_RomanUpper_RendersItems()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            var items = new[]
            {
                new ListItem("First item"),
                new ListItem("Second item"),
                new ListItem("Third item"),
                new ListItem("Fourth item"),
                new ListItem("Fifth item")
            };
            var (_, endY) = page.DrawList(items, 50, 50, 400, style: ListStyle.RomanUpper);

            Assert.True(endY > 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_roman_upper");
            var bitmap = TestHelper.RasterizePage(bytes, "list_roman_upper");
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(300), PtToPx(itemY), PtToPx(itemY + 14)),
                    $"Expected visible content for roman-upper item {i + 1} at Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_AllFourStyles_Nested()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            // Level 0: Bullet, Level 1: Numbered, Level 2: RomanUpper, Level 3: RomanLower
            var items = new[]
            {
                new ListItem("Bullet item one", ListStyle.Numbered,
                    new ListItem("Numbered sub-item", ListStyle.RomanUpper,
                        new ListItem("Roman-upper sub-sub-item", ListStyle.RomanLower,
                            new ListItem("Roman-lower deepest item"))),
                    new ListItem("Another numbered sub-item")),
                new ListItem("Bullet item two")
            };
            var (_, endY) = page.DrawList(items, 50, 50, 480);

            Assert.True(endY > 100);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "list_allfourstyles");
            var bitmap = TestHelper.RasterizePage(bytes, "list_allfourstyles");
            Assert.True(HasDarkPixelsInRegion(bitmap, PtToPx(50), PtToPx(480), PtToPx(50), PtToPx(endY)),
                "Expected visible content across all four list styles");
            bitmap.Dispose();
        }
    }
}
