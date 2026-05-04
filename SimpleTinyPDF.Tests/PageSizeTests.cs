using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class PageSizeTests
    {
        [Fact]
        public void A4_HasCorrectDimensions()
        {
            Assert.Equal(595f, PageSize.A4.Width);
            Assert.Equal(842f, PageSize.A4.Height);
        }

        [Fact]
        public void A3_HasCorrectDimensions()
        {
            Assert.Equal(842f, PageSize.A3.Width);
            Assert.Equal(1191f, PageSize.A3.Height);
        }

        [Fact]
        public void A5_HasCorrectDimensions()
        {
            Assert.Equal(420f, PageSize.A5.Width);
            Assert.Equal(595f, PageSize.A5.Height);
        }

        [Fact]
        public void Letter_HasCorrectDimensions()
        {
            Assert.Equal(612f, PageSize.Letter.Width);
            Assert.Equal(792f, PageSize.Letter.Height);
        }

        [Fact]
        public void Legal_HasCorrectDimensions()
        {
            Assert.Equal(612f, PageSize.Legal.Width);
            Assert.Equal(1008f, PageSize.Legal.Height);
        }

        [Fact]
        public void Landscape_SwapsWidthAndHeight()
        {
            var landscape = PageSize.A4.Landscape();
            Assert.Equal(842f, landscape.Width);
            Assert.Equal(595f, landscape.Height);
        }

        [Fact]
        public void CustomSize_StoresCorrectly()
        {
            var custom = new PageSize(300f, 400f);
            Assert.Equal(300f, custom.Width);
            Assert.Equal(400f, custom.Height);
        }

        [Fact]
        public void Landscape_OfLandscape_ReturnsPortrait()
        {
            var original = PageSize.Letter;
            var back = original.Landscape().Landscape();
            Assert.Equal(original.Width, back.Width);
            Assert.Equal(original.Height, back.Height);
        }
    }
}
