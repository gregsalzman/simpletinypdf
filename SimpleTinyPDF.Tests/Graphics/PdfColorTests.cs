using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class PdfColorTests
    {
        [Fact]
        public void Rgb_Int_NormalizesTo01()
        {
            var c = PdfColor.Rgb(255, 128, 0);
            Assert.Equal(1f, c.R, 2);
            Assert.Equal(128f / 255f, c.G, 2);
            Assert.Equal(0f, c.B, 2);
            Assert.False(c.IsCmyk);
        }

        [Fact]
        public void Rgb_Float_StoresDirectly()
        {
            var c = PdfColor.Rgb(0.5f, 0.3f, 0.1f);
            Assert.Equal(0.5f, c.R, 4);
            Assert.Equal(0.3f, c.G, 4);
            Assert.Equal(0.1f, c.B, 4);
            Assert.False(c.IsCmyk);
        }

        [Fact]
        public void Cmyk_StoresCorrectly()
        {
            var c = PdfColor.Cmyk(0.1f, 0.2f, 0.3f, 0.4f);
            Assert.Equal(0.1f, c.C, 4);
            Assert.Equal(0.2f, c.M, 4);
            Assert.Equal(0.3f, c.Y, 4);
            Assert.Equal(0.4f, c.K, 4);
            Assert.True(c.IsCmyk);
        }

        [Fact]
        public void Gray_SetsAllRgbChannelsEqual()
        {
            var c = PdfColor.Gray(0.5f);
            Assert.Equal(0.5f, c.R, 4);
            Assert.Equal(0.5f, c.G, 4);
            Assert.Equal(0.5f, c.B, 4);
            Assert.False(c.IsCmyk);
        }

        [Fact]
        public void Black_IsCmyk()
        {
            Assert.True(PdfColor.Black.IsCmyk);
            Assert.Equal(0f, PdfColor.Black.C);
            Assert.Equal(0f, PdfColor.Black.M);
            Assert.Equal(0f, PdfColor.Black.Y);
            Assert.Equal(1f, PdfColor.Black.K);
        }

        [Fact]
        public void White_IsOneRgb()
        {
            Assert.Equal(1f, PdfColor.White.R);
            Assert.Equal(1f, PdfColor.White.G);
            Assert.Equal(1f, PdfColor.White.B);
        }

        [Fact]
        public void Red_IsCorrect()
        {
            Assert.Equal(1f, PdfColor.Red.R);
            Assert.Equal(0f, PdfColor.Red.G);
            Assert.Equal(0f, PdfColor.Red.B);
        }

        [Fact]
        public void CmykPrimaries_AreCmyk()
        {
            Assert.True(PdfColor.Black.IsCmyk);
            Assert.True(PdfColor.Cyan.IsCmyk);
            Assert.True(PdfColor.Magenta.IsCmyk);
            Assert.True(PdfColor.Yellow.IsCmyk);
        }

        [Fact]
        public void RgbNamedColors_AreNotCmyk()
        {
            Assert.False(PdfColor.White.IsCmyk);
            Assert.False(PdfColor.Red.IsCmyk);
            Assert.False(PdfColor.Green.IsCmyk);
            Assert.False(PdfColor.Blue.IsCmyk);
            Assert.False(PdfColor.Orange.IsCmyk);
            Assert.False(PdfColor.Purple.IsCmyk);
            Assert.False(PdfColor.Pink.IsCmyk);
            Assert.False(PdfColor.Brown.IsCmyk);
            Assert.False(PdfColor.Gold.IsCmyk);
            Assert.False(PdfColor.Navy.IsCmyk);
            Assert.False(PdfColor.Teal.IsCmyk);
            Assert.False(PdfColor.Maroon.IsCmyk);
            Assert.False(PdfColor.Olive.IsCmyk);
            Assert.False(PdfColor.Coral.IsCmyk);
            Assert.False(PdfColor.Crimson.IsCmyk);
            Assert.False(PdfColor.Indigo.IsCmyk);
            Assert.False(PdfColor.Silver.IsCmyk);
            Assert.False(PdfColor.MediumGray.IsCmyk);
            Assert.False(PdfColor.LightGray.IsCmyk);
            Assert.False(PdfColor.DarkGray.IsCmyk);
        }

        [Fact]
        public void Yellow_IsCorrect()
        {
            Assert.Equal(0f, PdfColor.Yellow.C);
            Assert.Equal(0f, PdfColor.Yellow.M);
            Assert.Equal(1f, PdfColor.Yellow.Y);
            Assert.Equal(0f, PdfColor.Yellow.K);
        }

        [Fact]
        public void Cyan_IsCorrect()
        {
            Assert.Equal(1f, PdfColor.Cyan.C);
            Assert.Equal(0f, PdfColor.Cyan.M);
            Assert.Equal(0f, PdfColor.Cyan.Y);
            Assert.Equal(0f, PdfColor.Cyan.K);
        }

        [Fact]
        public void Magenta_IsCorrect()
        {
            Assert.Equal(0f, PdfColor.Magenta.C);
            Assert.Equal(1f, PdfColor.Magenta.M);
            Assert.Equal(0f, PdfColor.Magenta.Y);
            Assert.Equal(0f, PdfColor.Magenta.K);
        }

        [Fact]
        public void Orange_IsCorrect()
        {
            Assert.Equal(1f, PdfColor.Orange.R, 2);
            Assert.Equal(165f / 255f, PdfColor.Orange.G, 2);
            Assert.Equal(0f, PdfColor.Orange.B, 2);
        }

        [Fact]
        public void Navy_IsCorrect()
        {
            Assert.Equal(0f, PdfColor.Navy.R, 2);
            Assert.Equal(0f, PdfColor.Navy.G, 2);
            Assert.Equal(128f / 255f, PdfColor.Navy.B, 2);
        }
    }
}
