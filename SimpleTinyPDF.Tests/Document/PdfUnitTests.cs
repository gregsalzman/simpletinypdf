using System;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class PdfUnitTests
    {
        // --- Inches ---

        [Fact]
        public void InchesToPoints_OneInch_Returns72()
        {
            Assert.Equal(72f, PdfUnit.InchesToPoints(1f));
        }

        [Fact]
        public void PointsToInches_72Points_Returns1()
        {
            Assert.Equal(1f, PdfUnit.PointsToInches(72f));
        }

        [Fact]
        public void Inches_RoundTrip()
        {
            float original = 3.5f;
            Assert.Equal(original, PdfUnit.PointsToInches(PdfUnit.InchesToPoints(original)), 4);
        }

        // --- Centimeters ---

        [Fact]
        public void CmToPoints_OneCm()
        {
            Assert.Equal(72f / 2.54f, PdfUnit.CmToPoints(1f), 4);
        }

        [Fact]
        public void CmToPoints_254Cm_Returns72()
        {
            Assert.Equal(72f, PdfUnit.CmToPoints(2.54f), 4);
        }

        [Fact]
        public void Cm_RoundTrip()
        {
            float original = 10f;
            Assert.Equal(original, PdfUnit.PointsToCm(PdfUnit.CmToPoints(original)), 4);
        }

        // --- Millimeters ---

        [Fact]
        public void MmToPoints_254Mm_Returns72()
        {
            Assert.Equal(72f, PdfUnit.MmToPoints(25.4f), 4);
        }

        [Fact]
        public void Mm_RoundTrip()
        {
            float original = 100f;
            Assert.Equal(original, PdfUnit.PointsToMm(PdfUnit.MmToPoints(original)), 4);
        }

        // --- Fractional inches (parameters) ---

        [Fact]
        public void InchesToPoints_Fraction_1_1_8_Returns81()
        {
            Assert.Equal(81f, PdfUnit.InchesToPoints(1, 1, 8));
        }

        [Fact]
        public void InchesToPoints_Fraction_0_3_4_Returns54()
        {
            Assert.Equal(54f, PdfUnit.InchesToPoints(0, 3, 4));
        }

        [Fact]
        public void InchesToPoints_Fraction_ZeroDenominator_Throws()
        {
            Assert.Throws<ArgumentException>(() => PdfUnit.InchesToPoints(1, 1, 0));
        }

        // --- Fractional inches (string parsing) ---

        [Fact]
        public void InchesToPoints_String_MixedWithHyphen()
        {
            Assert.Equal(81f, PdfUnit.InchesToPoints("1-1/8"));
        }

        [Fact]
        public void InchesToPoints_String_MixedWithSpace()
        {
            Assert.Equal(81f, PdfUnit.InchesToPoints("1 1/8"));
        }

        [Fact]
        public void InchesToPoints_String_FractionOnly()
        {
            Assert.Equal(54f, PdfUnit.InchesToPoints("3/4"));
        }

        [Fact]
        public void InchesToPoints_String_WholeNumber()
        {
            Assert.Equal(144f, PdfUnit.InchesToPoints("2"));
        }

        [Fact]
        public void ParseInches_MixedWithHyphen()
        {
            Assert.Equal(1.125f, PdfUnit.ParseInches("1-1/8"), 4);
        }

        [Fact]
        public void ParseInches_MixedWithSpace()
        {
            Assert.Equal(1.125f, PdfUnit.ParseInches("1 1/8"), 4);
        }

        [Fact]
        public void ParseInches_FractionOnly()
        {
            Assert.Equal(0.75f, PdfUnit.ParseInches("3/4"), 4);
        }

        [Fact]
        public void ParseInches_WholeNumber()
        {
            Assert.Equal(2f, PdfUnit.ParseInches("2"));
        }

        [Fact]
        public void ParseInches_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => PdfUnit.ParseInches(null));
        }

        [Fact]
        public void ParseInches_Empty_Throws()
        {
            Assert.Throws<FormatException>(() => PdfUnit.ParseInches(""));
        }

        [Fact]
        public void ParseInches_Invalid_Throws()
        {
            Assert.Throws<FormatException>(() => PdfUnit.ParseInches("abc"));
        }

        [Fact]
        public void ParseInches_ZeroDenominator_Throws()
        {
            Assert.Throws<FormatException>(() => PdfUnit.ParseInches("1/0"));
        }
    }
}
