using System.IO;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class AdvancedTextTests
    {
        private static readonly PdfFontSource Roboto =
            PdfFontSource.FromFile(Path.Combine("TestAssets", "Roboto-Regular.ttf"));

        private static readonly PdfFontSource OpenSans =
            PdfFontSource.FromFile(Path.Combine("TestAssets", "OpenSans-Regular.ttf"));

        private static readonly PdfFontSource OpenSansBold =
            PdfFontSource.FromFile(Path.Combine("TestAssets", "OpenSans-Bold.ttf"));

        private static readonly PdfFontSource SourceSerif =
            PdfFontSource.FromFile(Path.Combine("TestAssets", "SourceSerifPro-Regular.otf"));

        // ── Test 1: Full-page lorem ipsum with four alignment modes ──

        [Fact]
        public void FullPage_LoremIpsum_FourParagraphs_AllAlignments()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page,
                "Verify: four paragraphs with Left/Center/Right/Justify alignment, " +
                "each mixing custom fonts, faux bold, faux italic, and underline");

            float x = 50;
            float width = PageSize.A4.Width - 100;
            float y = 30;
            float fontSize = 10f;
            float lineSpacing = 1.4f;

            // ── Paragraph 1: Left-aligned ──
            var p1Label = new TextSpan("Paragraph 1 — Left Aligned\n",
                font: OpenSansBold, fontSize: 12f, bold: true,
                color: PdfColor.Rgb(40, 40, 120));
            var p1Spans = new[]
            {
                p1Label,
                new TextSpan("Lorem ipsum dolor sit amet, ", font: Roboto, fontSize: fontSize),
                new TextSpan("consectetur adipiscing elit, ", font: OpenSans, fontSize: fontSize, bold: true),
                new TextSpan("sed do eiusmod tempor ", font: SourceSerif, fontSize: fontSize, italic: true),
                new TextSpan("incididunt ut labore ", font: Roboto, fontSize: fontSize, underline: true),
                new TextSpan("et dolore magna aliqua. ", font: OpenSans, fontSize: fontSize, bold: true, italic: true),
                new TextSpan("Ut enim ad minim veniam, ", font: SourceSerif, fontSize: fontSize, underline: true, bold: true),
                new TextSpan("quis nostrud exercitation ", font: Roboto, fontSize: fontSize, italic: true, underline: true),
                new TextSpan("ullamco laboris nisi ut aliquip ex ea commodo consequat. ", font: OpenSans, fontSize: fontSize),
                new TextSpan("Duis aute irure dolor ", font: SourceSerif, fontSize: fontSize, bold: true),
                new TextSpan("in reprehenderit in voluptate ", font: Roboto, fontSize: fontSize, italic: true),
                new TextSpan("velit esse cillum dolore eu fugiat nulla pariatur.", font: OpenSans, fontSize: fontSize, underline: true),
            };
            y = page.DrawText(p1Spans, x, y, TextAlignment.Left, width: width, lineSpacing: lineSpacing);

            y += 16;

            // ── Paragraph 2: Center-aligned ──
            var p2Label = new TextSpan("Paragraph 2 — Center Aligned\n",
                font: OpenSansBold, fontSize: 12f, bold: true,
                color: PdfColor.Rgb(120, 40, 40));
            var p2Spans = new[]
            {
                p2Label,
                new TextSpan("Sed ut perspiciatis ", font: OpenSans, fontSize: fontSize, bold: true),
                new TextSpan("unde omnis iste natus\n", font: Roboto, fontSize: fontSize, italic: true),
                new TextSpan("error sit voluptatem ", font: SourceSerif, fontSize: fontSize, underline: true),
                new TextSpan("accusantium doloremque\n", font: OpenSans, fontSize: fontSize, bold: true, underline: true),
                new TextSpan("totam rem aperiam, ", font: Roboto, fontSize: fontSize),
                new TextSpan("eaque ipsa\n", font: SourceSerif, fontSize: fontSize, italic: true, bold: true),
                new TextSpan("inventore veritatis et quasi ", font: OpenSans, fontSize: fontSize, italic: true, underline: true),
                new TextSpan("architecto beatae\n", font: Roboto, fontSize: fontSize, bold: true),
                new TextSpan("Nemo enim ipsam voluptatem\n", font: SourceSerif, fontSize: fontSize, underline: true),
                new TextSpan("quia voluptas sit ", font: OpenSans, fontSize: fontSize, italic: true),
                new TextSpan("aspernatur aut odit\n", font: Roboto, fontSize: fontSize, bold: true, italic: true),
                new TextSpan("sed quia consequuntur ", font: SourceSerif, fontSize: fontSize, bold: true, underline: true),
                new TextSpan("magni dolores.\n", font: OpenSans, fontSize: fontSize, italic: true, underline: true),
                new TextSpan("Qui ratione voluptatem ", font: Roboto, fontSize: fontSize),
                new TextSpan("sequi nesciunt.", font: SourceSerif, fontSize: fontSize, bold: true, italic: true),
            };
            y = page.DrawText(p2Spans, x, y, TextAlignment.Center, width: width, lineSpacing: lineSpacing);

            y += 16;

            // ── Paragraph 3: Right-aligned ──
            var p3Label = new TextSpan("Paragraph 3 — Right Aligned\n",
                font: OpenSansBold, fontSize: 12f, bold: true,
                color: PdfColor.Rgb(40, 100, 40));
            var p3Spans = new[]
            {
                p3Label,
                new TextSpan("At vero eos et accusamus ", font: SourceSerif, fontSize: fontSize, italic: true),
                new TextSpan("et iusto odio dignissimos ", font: OpenSans, fontSize: fontSize, bold: true),
                new TextSpan("ducimus qui blanditiis ", font: Roboto, fontSize: fontSize, underline: true),
                new TextSpan("praesentium voluptatum deleniti ", font: SourceSerif, fontSize: fontSize, bold: true, italic: true),
                new TextSpan("atque corrupti quos dolores ", font: OpenSans, fontSize: fontSize),
                new TextSpan("et quas molestias ", font: Roboto, fontSize: fontSize, italic: true, underline: true),
                new TextSpan("excepturi sint occaecati ", font: SourceSerif, fontSize: fontSize, bold: true, underline: true),
                new TextSpan("cupiditate non provident, ", font: OpenSans, fontSize: fontSize, italic: true),
                new TextSpan("similique sunt in culpa qui officia ", font: Roboto, fontSize: fontSize, bold: true),
                new TextSpan("deserunt mollitia animi est laborum et dolorum fuga.", font: SourceSerif, fontSize: fontSize, underline: true, italic: true),
            };
            y = page.DrawText(p3Spans, x, y, TextAlignment.Right, width: width, lineSpacing: lineSpacing);

            y += 16;

            // ── Paragraph 4: Fully justified ──
            var p4Label = new TextSpan("Paragraph 4 — Fully Justified\n",
                font: OpenSansBold, fontSize: 12f, bold: true,
                color: PdfColor.Rgb(120, 80, 0));
            var p4Spans = new[]
            {
                p4Label,
                new TextSpan("Temporibus autem quibusdam ", font: Roboto, fontSize: fontSize, bold: true),
                new TextSpan("et aut officiis debitis aut rerum ", font: OpenSans, fontSize: fontSize, italic: true),
                new TextSpan("necessitatibus saepe eveniet ", font: SourceSerif, fontSize: fontSize, underline: true),
                new TextSpan("ut et voluptates repudiandae sint ", font: Roboto, fontSize: fontSize, bold: true, italic: true),
                new TextSpan("et molestiae non recusandae. ", font: OpenSans, fontSize: fontSize, underline: true, bold: true),
                new TextSpan("Itaque earum rerum hic tenetur ", font: SourceSerif, fontSize: fontSize),
                new TextSpan("a sapiente delectus, ", font: Roboto, fontSize: fontSize, italic: true, underline: true),
                new TextSpan("ut aut reiciendis voluptatibus ", font: OpenSans, fontSize: fontSize, bold: true),
                new TextSpan("maiores alias consequatur aut perferendis ", font: SourceSerif, fontSize: fontSize, italic: true),
                new TextSpan("doloribus asperiores repellat. ", font: Roboto, fontSize: fontSize, underline: true),
                new TextSpan("Nam libero tempore cum soluta nobis ", font: OpenSans, fontSize: fontSize, bold: true, italic: true, underline: true),
                new TextSpan("est eligendi optio cumque nihil impedit.", font: SourceSerif, fontSize: fontSize, bold: true),
            };
            y = page.DrawText(p4Spans, x, y, TextAlignment.Justify, width: width, lineSpacing: lineSpacing);

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Text/lorem-ipsum-four-alignments-richtext");
            var bitmap = TestHelper.RasterizePage(bytes, "Text/lorem-ipsum-four-alignments-richtext");

            // Verify all four paragraphs have visible content
            // Paragraph 1 starts around y=30
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(x), TestHelper.PtToPx(x + width),
                TestHelper.PtToPx(30), TestHelper.PtToPx(150)),
                "Paragraph 1 (left-aligned) should have visible text");

            // Paragraph 4 (justified) should have visible content in the lower portion
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(x), TestHelper.PtToPx(x + width),
                TestHelper.PtToPx(250), TestHelper.PtToPx(370)),
                "Paragraph 4 (justified) should have visible text");

            bitmap.Dispose();
        }

        // ── Test 2: Nested bulleted list with custom fonts and styling ──

        [Fact]
        public void NestedList_CustomFonts_BoldItalicUnderline()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            TestHelper.AddDescription(page,
                "Verify: nested bullet list with custom fonts, faux bold, faux italic, " +
                "and underline in various combinations per item");

            float x = 50;
            float width = PageSize.A4.Width - 100;
            float fontSize = 11f;
            float lineSpacing = 1.3f;
            float indent = 20f;
            float bulletGap = 14f;
            float itemGap = 4f;
            var bulletColor = PdfColor.Rgb(80, 80, 80);

            float y = 30;

            // Helper: draw a bullet marker + rich text for one item
            void DrawBulletItem(ref float cy, int level, string bullet, TextSpan[] spans)
            {
                float ix = x + level * indent;
                float textX = ix + bulletGap;
                float textW = width - level * indent - bulletGap;

                page.DrawText(bullet, ix, cy, OpenSans, fontSize, bulletColor);
                cy = page.DrawText(spans, textX, cy, TextAlignment.Left,
                    width: textW, lineSpacing: lineSpacing);
                cy += itemGap;
            }

            // ── Level 0 item 1: Normal Roboto ──
            DrawBulletItem(ref y, 0, "\u2022", new[]
            {
                new TextSpan("Project overview and introduction — ", font: Roboto, fontSize: fontSize),
                new TextSpan("this section describes the goals of the project ",
                    font: Roboto, fontSize: fontSize, italic: true),
                new TextSpan("and key deliverables.", font: Roboto, fontSize: fontSize, underline: true),
            });

            //   Level 1 item 1a: Bold OpenSans
            DrawBulletItem(ref y, 1, "–", new[]
            {
                new TextSpan("Requirements gathering phase ", font: OpenSans, fontSize: fontSize, bold: true),
                new TextSpan("with stakeholder interviews and ", font: OpenSans, fontSize: fontSize),
                new TextSpan("documented outcomes.", font: OpenSans, fontSize: fontSize, italic: true, underline: true),
            });

            //     Level 2 item 1a-i: Italic SourceSerif
            DrawBulletItem(ref y, 2, "›", new[]
            {
                new TextSpan("User research findings ", font: SourceSerif, fontSize: fontSize, italic: true),
                new TextSpan("revealed critical usability gaps ", font: SourceSerif, fontSize: fontSize, bold: true),
                new TextSpan("that need addressing in the next sprint.", font: SourceSerif, fontSize: fontSize),
            });

            //     Level 2 item 1a-ii: Bold+Italic+Underline mixed
            DrawBulletItem(ref y, 2, "›", new[]
            {
                new TextSpan("Technical constraints ", font: Roboto, fontSize: fontSize, bold: true, italic: true),
                new TextSpan("include limited API throughput ", font: OpenSans, fontSize: fontSize, underline: true),
                new TextSpan("and legacy system compatibility.", font: SourceSerif, fontSize: fontSize, bold: true, underline: true),
            });

            //   Level 1 item 1b: Underlined Roboto
            DrawBulletItem(ref y, 1, "–", new[]
            {
                new TextSpan("Design and prototyping ", font: Roboto, fontSize: fontSize, underline: true),
                new TextSpan("using iterative feedback loops ", font: Roboto, fontSize: fontSize, bold: true),
                new TextSpan("across multiple design sprints.", font: Roboto, fontSize: fontSize, italic: true),
            });

            // ── Level 0 item 2: Bold OpenSans ──
            DrawBulletItem(ref y, 0, "\u2022", new[]
            {
                new TextSpan("Implementation milestones ", font: OpenSans, fontSize: fontSize, bold: true),
                new TextSpan("and delivery schedule — ", font: OpenSans, fontSize: fontSize),
                new TextSpan("all dates are tentative ", font: OpenSans, fontSize: fontSize, italic: true),
                new TextSpan("pending final review.", font: OpenSans, fontSize: fontSize, bold: true, underline: true),
            });

            //   Level 1 item 2a: Italic SourceSerif
            DrawBulletItem(ref y, 1, "–", new[]
            {
                new TextSpan("Phase 1: Core infrastructure ", font: SourceSerif, fontSize: fontSize, italic: true),
                new TextSpan("setup and deployment pipeline ", font: Roboto, fontSize: fontSize, bold: true),
                new TextSpan("with automated testing.", font: OpenSans, fontSize: fontSize, underline: true),
            });

            //   Level 1 item 2b: Bold+Underline Roboto
            DrawBulletItem(ref y, 1, "–", new[]
            {
                new TextSpan("Phase 2: Feature development ", font: Roboto, fontSize: fontSize, bold: true, underline: true),
                new TextSpan("covering user authentication, ", font: SourceSerif, fontSize: fontSize, italic: true),
                new TextSpan("data visualization, ", font: OpenSans, fontSize: fontSize),
                new TextSpan("and reporting modules.", font: Roboto, fontSize: fontSize, italic: true, underline: true),
            });

            //     Level 2 item 2b-i: Normal mixed
            DrawBulletItem(ref y, 2, "›", new[]
            {
                new TextSpan("Authentication module ", font: OpenSans, fontSize: fontSize, bold: true),
                new TextSpan("supports OAuth 2.0 and SAML ", font: SourceSerif, fontSize: fontSize),
                new TextSpan("with multi-factor enforcement.", font: Roboto, fontSize: fontSize, underline: true),
            });

            //     Level 2 item 2b-ii: All styles combined
            DrawBulletItem(ref y, 2, "›", new[]
            {
                new TextSpan("Reporting engine ", font: Roboto, fontSize: fontSize, bold: true, italic: true, underline: true),
                new TextSpan("generates PDF, CSV, and Excel ", font: OpenSans, fontSize: fontSize, bold: true),
                new TextSpan("exports with custom templates.", font: SourceSerif, fontSize: fontSize, italic: true),
            });

            //   Level 1 item 2c: Italic+Underline OpenSans
            DrawBulletItem(ref y, 1, "–", new[]
            {
                new TextSpan("Phase 3: Integration testing ", font: OpenSans, fontSize: fontSize, italic: true, underline: true),
                new TextSpan("and performance optimization ", font: Roboto, fontSize: fontSize, bold: true, italic: true),
                new TextSpan("across all environments.", font: SourceSerif, fontSize: fontSize),
            });

            // ── Level 0 item 3: Italic SourceSerif ──
            DrawBulletItem(ref y, 0, "\u2022", new[]
            {
                new TextSpan("Quality assurance and release criteria ", font: SourceSerif, fontSize: fontSize, italic: true),
                new TextSpan("must be met before go-live, ", font: OpenSans, fontSize: fontSize, bold: true),
                new TextSpan("including load testing ", font: Roboto, fontSize: fontSize, underline: true),
                new TextSpan("and security audit completion.", font: SourceSerif, fontSize: fontSize, bold: true, italic: true),
            });

            //   Level 1 item 3a: Bold+Italic+Underline mixed
            DrawBulletItem(ref y, 1, "–", new[]
            {
                new TextSpan("Automated test coverage ", font: Roboto, fontSize: fontSize, bold: true),
                new TextSpan("must exceed 80% for all modules, ", font: OpenSans, fontSize: fontSize, italic: true, underline: true),
                new TextSpan("with zero critical defects open.", font: SourceSerif, fontSize: fontSize, bold: true),
            });

            //   Level 1 item 3b: All styles
            DrawBulletItem(ref y, 1, "–", new[]
            {
                new TextSpan("Security penetration testing ", font: SourceSerif, fontSize: fontSize, underline: true),
                new TextSpan("completed by external auditor ", font: Roboto, fontSize: fontSize, bold: true, italic: true),
                new TextSpan("with full remediation of findings.", font: OpenSans, fontSize: fontSize, italic: true, underline: true),
            });

            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Tables/nested-list-custom-fonts-styled");
            var bitmap = TestHelper.RasterizePage(bytes, "Tables/nested-list-custom-fonts-styled");

            // Verify top-level bullets are visible
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(x), TestHelper.PtToPx(x + 20),
                TestHelper.PtToPx(30), TestHelper.PtToPx(50)),
                "First top-level bullet marker should be visible");

            // Verify indented level-1 content is visible
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(x + indent), TestHelper.PtToPx(x + width),
                TestHelper.PtToPx(60), TestHelper.PtToPx(120)),
                "Level-1 indented content should be visible");

            // Verify level-2 content is visible further down
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(x + indent * 2), TestHelper.PtToPx(x + width),
                TestHelper.PtToPx(100), TestHelper.PtToPx(200)),
                "Level-2 indented content should be visible");

            // Verify content extends into the lower third of content area
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmap,
                TestHelper.PtToPx(x), TestHelper.PtToPx(x + width),
                TestHelper.PtToPx(250), TestHelper.PtToPx(360)),
                "Content should extend past the midpoint of the list");

            bitmap.Dispose();
        }

    }
}
