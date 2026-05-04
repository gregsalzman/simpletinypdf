using System.Linq;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class FontMetricsTests
    {
        [Fact]
        public void MeasureString_EmptyString_ReturnsZero()
        {
            Assert.Equal(0f, FontMetrics.MeasureString("", PdfFont.Helvetica, 12));
            Assert.Equal(0f, FontMetrics.MeasureString(null, PdfFont.Helvetica, 12));
        }

        [Fact]
        public void MeasureString_Courier_IsFixedWidth()
        {
            // All Courier characters are 600 units wide
            float wA = FontMetrics.MeasureString("A", PdfFont.Courier, 12);
            float wI = FontMetrics.MeasureString("i", PdfFont.Courier, 12);
            float wM = FontMetrics.MeasureString("M", PdfFont.Courier, 12);
            Assert.Equal(wA, wI);
            Assert.Equal(wA, wM);
        }

        [Fact]
        public void MeasureString_Helvetica_IsProportional()
        {
            // 'M' should be wider than 'i' in Helvetica
            float wM = FontMetrics.MeasureString("M", PdfFont.Helvetica, 12);
            float wI = FontMetrics.MeasureString("i", PdfFont.Helvetica, 12);
            Assert.True(wM > wI);
        }

        [Theory]
        [InlineData(PdfFont.Helvetica)]
        [InlineData(PdfFont.HelveticaBold)]
        [InlineData(PdfFont.HelveticaOblique)]
        [InlineData(PdfFont.HelveticaBoldOblique)]
        [InlineData(PdfFont.TimesRoman)]
        [InlineData(PdfFont.TimesBold)]
        [InlineData(PdfFont.TimesItalic)]
        [InlineData(PdfFont.TimesBoldItalic)]
        [InlineData(PdfFont.Courier)]
        [InlineData(PdfFont.CourierBold)]
        [InlineData(PdfFont.CourierOblique)]
        [InlineData(PdfFont.CourierBoldOblique)]
        public void AllFonts_ReturnNonzeroWidth_ForAsciiLetters(PdfFont font)
        {
            float w = FontMetrics.MeasureString("Hello", font, 12);
            Assert.True(w > 0, $"Font {font} returned zero width for 'Hello'");
        }

        [Fact]
        public void MeasureString_ScalesWithFontSize()
        {
            float w12 = FontMetrics.MeasureString("Test", PdfFont.Helvetica, 12);
            float w24 = FontMetrics.MeasureString("Test", PdfFont.Helvetica, 24);
            Assert.Equal(w12 * 2, w24, 2);
        }

        [Fact]
        public void WrapText_ShortText_SingleLine()
        {
            var lines = FontMetrics.WrapText("Hello", PdfFont.Helvetica, 12, 500);
            Assert.Single(lines);
            Assert.Equal("Hello", lines[0]);
        }

        [Fact]
        public void WrapText_LongText_WrapsToMultipleLines()
        {
            var text = "This is a fairly long line of text that should be wrapped across multiple lines when rendered";
            var lines = FontMetrics.WrapText(text, PdfFont.Helvetica, 12, 200);
            Assert.True(lines.Count > 1, "Expected text to wrap to multiple lines");
        }

        [Fact]
        public void WrapText_ExplicitNewlines_SplitsCorrectly()
        {
            var lines = FontMetrics.WrapText("Line one\nLine two\nLine three", PdfFont.Helvetica, 12, 500);
            Assert.Equal(3, lines.Count);
            Assert.Equal("Line one", lines[0]);
            Assert.Equal("Line two", lines[1]);
            Assert.Equal("Line three", lines[2]);
        }

        [Fact]
        public void WrapText_EmptyString_ReturnsSingleEmptyLine()
        {
            var lines = FontMetrics.WrapText("", PdfFont.Helvetica, 12, 500);
            Assert.Single(lines);
        }

        [Fact]
        public void Helvetica_SpaceWidth_IsKnown()
        {
            // Helvetica space = 278 units. At 10pt: 278*10/1000 = 2.78
            float w = FontMetrics.MeasureString(" ", PdfFont.Helvetica, 10);
            Assert.Equal(2.78f, w, 2);
        }

        [Fact]
        public void Helvetica_And_HelveticaOblique_HaveSameWidths()
        {
            float w1 = FontMetrics.MeasureString("Testing widths", PdfFont.Helvetica, 12);
            float w2 = FontMetrics.MeasureString("Testing widths", PdfFont.HelveticaOblique, 12);
            Assert.Equal(w1, w2);
        }
    }
}
