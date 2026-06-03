using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class CoordinateOriginTests
    {
        [Fact]
        public void DefaultCoordinateOrigin_IsTopDown()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage(PageSize.A4);
            Assert.Equal(CoordinateOrigin.TopDown, page.CoordinateOrigin);
        }

        [Fact]
        public void BottomUp_Text_RendersAtExpectedPosition()
        {
            // Draw text at y=400 in BottomUp mode (400pt from page bottom, baseline).
            // A4 height is 842pt, so equivalent TopDown y = 842 - 400 - 24 = 418.
            var docBU = new PdfDocument();
            var pageBU = docBU.AddPage(PageSize.A4);
            TestHelper.AddDescription(pageBU, "Verify: BottomUp coordinate system places text correctly");
            pageBU.CoordinateOrigin = CoordinateOrigin.BottomUp;
            pageBU.DrawText("Hello", 50, 400, PdfFont.Helvetica, 24);

            var docTD = new PdfDocument();
            var pageTD = docTD.AddPage(PageSize.A4);
            TestHelper.AddDescription(pageTD, "Verify: TopDown coordinate system places text at equivalent position");
            pageTD.DrawText("Hello", 50, 418, PdfFont.Helvetica, 24);

            var bytesBU = docBU.ToArray();
            var bytesTD = docTD.ToArray();
            TestHelper.SavePdf(bytesBU, "Document/coord-bottomup-text");
            TestHelper.SavePdf(bytesTD, "Document/coord-topdown-text-equiv");
            var bitmapBU = TestHelper.RasterizePage(bytesBU, "Document/coord-bottomup-text");
            var bitmapTD = TestHelper.RasterizePage(bytesTD, "Document/coord-topdown-text-equiv");

            // Both should have text in the same region (around 418-442pt from top)
            int textY1 = TestHelper.PtToPx(418);
            int textY2 = TestHelper.PtToPx(442);
            int textX1 = TestHelper.PtToPx(50);
            int textX2 = TestHelper.PtToPx(120);

            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmapBU, textX1, textX2, textY1, textY2),
                "BottomUp text should have visible content in expected region");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmapTD, textX1, textX2, textY1, textY2),
                "TopDown equivalent text should have visible content in same region");

            bitmapBU.Dispose();
            bitmapTD.Dispose();
        }

        [Fact]
        public void BottomUp_Line_RendersAtExpectedPosition()
        {
            // Draw a horizontal line at y=400 (middle of A4 page) in BottomUp mode.
            // A4 height = 842pt. y=400 from bottom = 442 from top in TopDown.
            var docBU = new PdfDocument();
            var pageBU = docBU.AddPage(PageSize.A4);
            TestHelper.AddDescription(pageBU, "Verify: BottomUp coordinate system places line correctly");
            pageBU.CoordinateOrigin = CoordinateOrigin.BottomUp;
            pageBU.DrawLine(50, 400, 500, 400, PdfColor.Black, 2f);

            var docTD = new PdfDocument();
            var pageTD = docTD.AddPage(PageSize.A4);
            TestHelper.AddDescription(pageTD, "Verify: TopDown coordinate system places line at equivalent position");
            pageTD.DrawLine(50, 442, 500, 442, PdfColor.Black, 2f);

            var bytesBU = docBU.ToArray();
            var bytesTD = docTD.ToArray();
            TestHelper.SavePdf(bytesBU, "Document/coord-bottomup-line");
            TestHelper.SavePdf(bytesTD, "Document/coord-topdown-line-equiv");
            var bitmapBU = TestHelper.RasterizePage(bytesBU, "Document/coord-bottomup-line");
            var bitmapTD = TestHelper.RasterizePage(bytesTD, "Document/coord-topdown-line-equiv");

            // Both should have a line at the same vertical position
            int checkX = TestHelper.PtToPx(200);
            int checkY = TestHelper.PtToPx(442);
            TestHelper.AssertPixelNotWhite(bitmapBU, checkX, checkY);
            TestHelper.AssertPixelNotWhite(bitmapTD, checkX, checkY);

            bitmapBU.Dispose();
            bitmapTD.Dispose();
        }

        [Fact]
        public void BottomUp_Rectangle_RendersAtExpectedPosition()
        {
            // Draw a filled rectangle in BottomUp mode.
            // Box: x=100, y=300 (bottom-left in PDF), width=200, height=100
            // So the box spans y=300 to y=400 from bottom.
            // In TopDown: top of box = 842-400 = 442, so y=442 height=100.
            var docBU = new PdfDocument();
            var pageBU = docBU.AddPage(PageSize.A4);
            TestHelper.AddDescription(pageBU, "Verify: BottomUp coordinate system places rectangle correctly");
            pageBU.CoordinateOrigin = CoordinateOrigin.BottomUp;
            pageBU.DrawFilledRectangle(100, 300, 200, 100, PdfColor.Red);

            var docTD = new PdfDocument();
            var pageTD = docTD.AddPage(PageSize.A4);
            TestHelper.AddDescription(pageTD, "Verify: TopDown coordinate system places rectangle at equivalent position");
            pageTD.DrawFilledRectangle(100, 442, 200, 100, PdfColor.Red);

            var bytesBU = docBU.ToArray();
            var bytesTD = docTD.ToArray();
            TestHelper.SavePdf(bytesBU, "Document/coord-bottomup-rect");
            TestHelper.SavePdf(bytesTD, "Document/coord-topdown-rect-equiv");
            var bitmapBU = TestHelper.RasterizePage(bytesBU, "Document/coord-bottomup-rect");
            var bitmapTD = TestHelper.RasterizePage(bytesTD, "Document/coord-topdown-rect-equiv");

            // Center of the rectangle in top-down pixel coords
            int centerX = TestHelper.PtToPx(200);
            int centerY = TestHelper.PtToPx(492); // 442 + 100/2

            TestHelper.AssertPixelColor(bitmapBU, centerX, centerY, 255, 0, 0);
            TestHelper.AssertPixelColor(bitmapTD, centerX, centerY, 255, 0, 0);

            bitmapBU.Dispose();
            bitmapTD.Dispose();
        }

        [Fact]
        public void BottomUp_Image_RendersAtExpectedPosition()
        {
            // Draw an image in BottomUp mode.
            // Image at x=100, y=500 (bottom-left in PDF), width=100, height=100.
            // In TopDown: top of image = 842-600 = 242, so y=242.
            var docBU = new PdfDocument();
            var pageBU = docBU.AddPage(PageSize.A4);
            TestHelper.AddDescription(pageBU, "Verify: BottomUp coordinate system places image correctly");
            pageBU.CoordinateOrigin = CoordinateOrigin.BottomUp;
            var jpeg = TestHelper.CreateTestJpeg(100, 100);
            var imageBU = PdfImage.FromBytes(jpeg);
            pageBU.DrawImage(imageBU, 100, 500, 100, 100);

            var docTD = new PdfDocument();
            var pageTD = docTD.AddPage(PageSize.A4);
            TestHelper.AddDescription(pageTD, "Verify: TopDown coordinate system places image at equivalent position");
            var imageTD = PdfImage.FromBytes(jpeg);
            pageTD.DrawImage(imageTD, 100, 242, 100, 100);

            var bytesBU = docBU.ToArray();
            var bytesTD = docTD.ToArray();
            TestHelper.SavePdf(bytesBU, "Document/coord-bottomup-image");
            TestHelper.SavePdf(bytesTD, "Document/coord-topdown-image-equiv");
            var bitmapBU = TestHelper.RasterizePage(bytesBU, "Document/coord-bottomup-image");
            var bitmapTD = TestHelper.RasterizePage(bytesTD, "Document/coord-topdown-image-equiv");

            // Center of image in top-down pixel coords: y = 242 + 50 = 292
            int centerX = TestHelper.PtToPx(150);
            int centerY = TestHelper.PtToPx(292);

            TestHelper.AssertPixelColor(bitmapBU, centerX, centerY, 255, 0, 0);
            TestHelper.AssertPixelColor(bitmapTD, centerX, centerY, 255, 0, 0);

            bitmapBU.Dispose();
            bitmapTD.Dispose();
        }

        [Fact]
        public void TopDown_BehaviorUnchanged()
        {
            // Verify that explicitly setting TopDown produces the same result as default.
            var docDefault = new PdfDocument();
            var pageDefault = docDefault.AddPage(PageSize.A4);
            TestHelper.AddDescription(pageDefault, "Verify: default TopDown coordinate system behavior is unchanged");
            pageDefault.DrawText("Hello", 50, 50, PdfFont.Helvetica, 24);
            pageDefault.DrawFilledRectangle(50, 100, 100, 50, PdfColor.Blue);

            var docExplicit = new PdfDocument();
            var pageExplicit = docExplicit.AddPage(PageSize.A4);
            TestHelper.AddDescription(pageExplicit, "Verify: explicit TopDown coordinate system matches default behavior");
            pageExplicit.CoordinateOrigin = CoordinateOrigin.TopDown;
            pageExplicit.DrawText("Hello", 50, 50, PdfFont.Helvetica, 24);
            pageExplicit.DrawFilledRectangle(50, 100, 100, 50, PdfColor.Blue);

            var bytesDefault = docDefault.ToArray();
            var bytesExplicit = docExplicit.ToArray();
            TestHelper.SavePdf(bytesDefault, "Document/coord-topdown-default");
            TestHelper.SavePdf(bytesExplicit, "Document/coord-topdown-explicit");
            var bitmapDefault = TestHelper.RasterizePage(bytesDefault, "Document/coord-topdown-default");
            var bitmapExplicit = TestHelper.RasterizePage(bytesExplicit, "Document/coord-topdown-explicit");

            // Text area (24pt font at y=50, scan region)
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmapDefault, TestHelper.PtToPx(50), TestHelper.PtToPx(120), TestHelper.PtToPx(50), TestHelper.PtToPx(74)),
                "Default page should have visible text");
            Assert.True(TestHelper.HasDarkPixelsInRegion(bitmapExplicit, TestHelper.PtToPx(50), TestHelper.PtToPx(120), TestHelper.PtToPx(50), TestHelper.PtToPx(74)),
                "Explicit TopDown page should have visible text");

            // Rectangle center
            TestHelper.AssertPixelColor(bitmapDefault, TestHelper.PtToPx(100), TestHelper.PtToPx(125), 0, 0, 255);
            TestHelper.AssertPixelColor(bitmapExplicit, TestHelper.PtToPx(100), TestHelper.PtToPx(125), 0, 0, 255);

            bitmapDefault.Dispose();
            bitmapExplicit.Dispose();
        }
    }
}
