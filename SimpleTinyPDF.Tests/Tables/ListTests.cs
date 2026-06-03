using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class ListTests
    {
        // ── Flat bullet list ───────────────────────────────────────

        [Fact]
        public void DrawList_Bullet_RendersItems()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: basic bullet list with round markers");
            var items = new[]
            {
                new ListItem("First item"),
                new ListItem("Second item"),
                new ListItem("Third item")
            };
            var (_, endY) = page.DrawList(items, 50, 50, 400);

            Assert.True(endY > 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/bullet-list-basic");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/bullet-list-basic");
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), TestHelper.PtToPx(itemY), TestHelper.PtToPx(itemY + 14)),
                    $"Expected visible text for bullet item {i + 1} at Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_Numbered_RendersWithNumbers()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: numbered list with sequential numbers");
            var items = new[]
            {
                new ListItem("First item"),
                new ListItem("Second item"),
                new ListItem("Third item")
            };
            var (_, endY) = page.DrawList(items, 50, 50, 400, style: ListStyle.Numbered);

            Assert.True(endY > 50);
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/numbered-list-basic");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/numbered-list-basic");
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(60), TestHelper.PtToPx(itemY), TestHelper.PtToPx(itemY + 14)),
                    $"Expected number marker for item {i + 1} at X=50, Y~{itemY}");
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(70), TestHelper.PtToPx(300), TestHelper.PtToPx(itemY), TestHelper.PtToPx(itemY + 14)),
                    $"Expected text for item {i + 1} at X=70, Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_Bullet_LongItems_WrapCorrectly()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: long bullet list items wrap correctly");
            var items = new[]
            {
                new ListItem("This is a very long list item that should wrap to multiple lines when rendered in a narrow column width"),
                new ListItem("Short item"),
                new ListItem("Another long item that tests word wrapping behavior within bulleted lists to ensure everything looks correct")
            };
            var (_, endY) = page.DrawList(items, 50, 50, 300);

            Assert.True(endY > 100, "Long items should take more vertical space");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/bullet-list-word-wrapped");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/bullet-list-word-wrapped");

            float lineHeight = 12 * 1.2f;
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(65), TestHelper.PtToPx(350), TestHelper.PtToPx(50), TestHelper.PtToPx(50 + 12)),
                "Expected text on line 1 of first (long) bullet item");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(65), TestHelper.PtToPx(350), TestHelper.PtToPx(50 + lineHeight), TestHelper.PtToPx(50 + lineHeight + 12)),
                "Expected wrapped text on line 2 of first (long) bullet item");

            float lastItemApproxY = endY - 12 * 1.2f - 12 * 0.3f;
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(350), TestHelper.PtToPx(lastItemApproxY - 20), TestHelper.PtToPx(endY)),
                "Expected visible text for the last bullet item");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_Numbered_CustomStartNumber()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: numbered list starts at specified number");
            var items = new[]
            {
                new ListItem("Item A"),
                new ListItem("Item B"),
                new ListItem("Item C")
            };
            var (_, _) = page.DrawList(items, 50, 50, 400, style: ListStyle.Numbered, startNumber: 5);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/numbered-list-start-at-5");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/numbered-list-start-at-5");
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), TestHelper.PtToPx(itemY), TestHelper.PtToPx(itemY + 14)),
                    $"Expected visible text for numbered item {5 + i} at Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_Bullet_ColoredText()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: bullet list renders in red color");
            var items = new[]
            {
                new ListItem("Red item one"),
                new ListItem("Red item two")
            };
            page.DrawList(items, 50, 50, 400, color: PdfColor.Red);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/bullet-list-red-color");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/bullet-list-red-color");

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
            TestHelper.AddDescription(page, "Verify: bullet and numbered lists on same page");
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
            TestHelper.SavePdf(bytes, "Tables/bullet-and-numbered-combined");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/bullet-and-numbered-combined");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), TestHelper.PtToPx(bulletStartY), TestHelper.PtToPx(bulletStartY + 14)),
                "Expected visible text in bullet list section");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), TestHelper.PtToPx(numberedLabelY), TestHelper.PtToPx(numberedLabelY + 14)),
                "Expected 'Numbered List:' label to be visible");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), TestHelper.PtToPx(numberedStartY), TestHelper.PtToPx(numberedStartY + 14)),
                "Expected visible text in numbered list section");
            bitmap.Dispose();
        }

        // ── Nested lists ───────────────────────────────────────────

        [Fact]
        public void DrawList_NestedBullet_RendersAllLevels()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: nested bullet lists with indentation");
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
            TestHelper.SavePdf(bytes, "Tables/nested-bullet-lists");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/nested-bullet-lists");

            // Level 1 item at y~50
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), TestHelper.PtToPx(50), TestHelper.PtToPx(64)),
                "Expected level-1 bullet item at top");
            // Level 2 item should appear below and indented
            float level2Y = 50 + 12 * 1.2f + 12 * 0.3f;
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(70), TestHelper.PtToPx(350), TestHelper.PtToPx(level2Y), TestHelper.PtToPx(level2Y + 28)),
                "Expected level-2 item visible below level-1");

            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_NestedNumbered_RendersAllLevels()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: nested numbered lists with indentation");
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
            TestHelper.SavePdf(bytes, "Tables/nested-numbered-lists");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/nested-numbered-lists");

            // Top-level number marker at x~50
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(65), TestHelper.PtToPx(50), TestHelper.PtToPx(64)),
                "Expected top-level number marker");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_MixedStyles_NumberedWithBulletChildren()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: nested lists mixing bullet and numbered styles");
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
            TestHelper.SavePdf(bytes, "Tables/nested-mixed-styles");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/nested-mixed-styles");

            // Top-level number marker at x~50
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(65), TestHelper.PtToPx(50), TestHelper.PtToPx(64)),
                "Expected numbered marker for top-level item");
            // Children are bullets — bullet marker should appear indented
            float childY = 50 + 12 * 1.2f + 12 * 0.3f;
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(70), TestHelper.PtToPx(350), TestHelper.PtToPx(childY), TestHelper.PtToPx(childY + 28)),
                "Expected bullet child items below the numbered parent");
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_TextWrapsAtAllNestingLevels()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: nested list items with long text wrap correctly");
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
            TestHelper.SavePdf(bytes, "Tables/nested-word-wrapped");
        }

        [Fact]
        public void DrawList_FlowsToNextPage()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: long nested lists span multiple pages");

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
            TestHelper.SavePdf(bytes, "Tables/nested-multipage-overflow");

            // Page 0: first page — should have content filling most of the page
            var page0Bitmap = TestHelper.RasterizePage(bytes, "Tables/nested-multipage-overflow", pageIndex: 0);
            Assert.True(TestHelper.HasDarkPixelsInRegion(page0Bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(50), TestHelper.PtToPx(750)),
                "Expected list content on the first page");
            page0Bitmap.Dispose();

            // Page 1: continuation page — should have content starting near the top
            var page1Bitmap = TestHelper.RasterizePage(bytes, "Tables/nested-multipage-overflow", pageIndex: 1);
            Assert.True(TestHelper.HasDarkPixelsInRegion(page1Bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(400), TestHelper.PtToPx(50), TestHelper.PtToPx(200)),
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
            TestHelper.AddDescription(page, "Verify: lists nested 5 levels deep render correctly");

            var (lastPage, endY) = page.DrawList(items,
                x: 50, y: 50, width: 500,
                bullet: new TextSpan("•", PdfFont.Helvetica),
                bottomMargin: 50, continuationY: 50);

            // Five levels of wrapped text should take significant vertical space
            Assert.True(endY > 200 || doc.PageCount > 1,
                "Five levels of wrapped items should produce significant vertical extent or overflow to page 2");

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/nested-5-levels-deep");
            for (int p = 0; p < doc.PageCount; p++)
            {
                var bmp = TestHelper.RasterizePage(bytes, "Tables/nested-5-levels-deep", pageIndex: p);
                Assert.True(TestHelper.HasDarkPixelsInRegion(bmp, TestHelper.PtToPx(50), TestHelper.PtToPx(500), TestHelper.PtToPx(50), TestHelper.PtToPx(700)),
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
            TestHelper.AddDescription(page, "Verify: list with lowercase Roman numeral markers (i, ii, iii)");
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
            TestHelper.SavePdf(bytes, "Tables/roman-numeral-lowercase");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/roman-numeral-lowercase");
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), TestHelper.PtToPx(itemY), TestHelper.PtToPx(itemY + 14)),
                    $"Expected visible content for roman-lower item {i + 1} at Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_RomanUpper_RendersItems()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: list with uppercase Roman numeral markers (I, II, III)");
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
            TestHelper.SavePdf(bytes, "Tables/roman-numeral-uppercase");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/roman-numeral-uppercase");
            float itemSpacing = 12 * 1.2f + 12 * 0.3f;
            for (int i = 0; i < items.Length; i++)
            {
                float itemY = 50 + i * itemSpacing;
                Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(300), TestHelper.PtToPx(itemY), TestHelper.PtToPx(itemY + 14)),
                    $"Expected visible content for roman-upper item {i + 1} at Y~{itemY}");
            }
            bitmap.Dispose();
        }

        [Fact]
        public void DrawList_AllFourStyles_Nested()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page, "Verify: all four list styles (bullet, numbered, roman lower, roman upper)");
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
            TestHelper.SavePdf(bytes, "Tables/all-four-list-styles");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/all-four-list-styles");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap, TestHelper.PtToPx(50), TestHelper.PtToPx(480), TestHelper.PtToPx(50), TestHelper.PtToPx(endY)),
                "Expected visible content across all four list styles");
            bitmap.Dispose();
        }
    }
}
