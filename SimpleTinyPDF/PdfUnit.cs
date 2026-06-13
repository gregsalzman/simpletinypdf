using System;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Converts between PDF points and common measurement units (inches, centimeters, millimeters).
    /// PDF uses points as its native unit: 72 points = 1 inch.
    /// </summary>
    public static class PdfUnit
    {
        private const float PointsPerInch = 72f;
        private const float CmPerInch = 2.54f;
        private const float MmPerInch = 25.4f;

        /// <summary>Converts inches to PDF points.</summary>
        public static float InchesToPoints(float inches) => inches * PointsPerInch;

        /// <summary>Converts PDF points to inches.</summary>
        public static float PointsToInches(float points) => points / PointsPerInch;

        /// <summary>Converts centimeters to PDF points.</summary>
        public static float CmToPoints(float cm) => cm * PointsPerInch / CmPerInch;

        /// <summary>Converts PDF points to centimeters.</summary>
        public static float PointsToCm(float points) => points * CmPerInch / PointsPerInch;

        /// <summary>Converts millimeters to PDF points.</summary>
        public static float MmToPoints(float mm) => mm * PointsPerInch / MmPerInch;

        /// <summary>Converts PDF points to millimeters.</summary>
        public static float PointsToMm(float points) => points * MmPerInch / PointsPerInch;

        /// <summary>
        /// Converts fractional inches to PDF points.
        /// </summary>
        /// <param name="whole">Whole inches (e.g. 1).</param>
        /// <param name="numerator">Fraction numerator (e.g. 1).</param>
        /// <param name="denominator">Fraction denominator (e.g. 8). Must be greater than zero.</param>
        /// <returns>The value in PDF points. For example, (1, 1, 8) returns 81 (1-1/8 inches).</returns>
        public static float InchesToPoints(int whole, int numerator, int denominator)
        {
            if (denominator == 0) throw new ArgumentException("Denominator cannot be zero.", nameof(denominator));
            return (whole + (float)numerator / denominator) * PointsPerInch;
        }

        /// <summary>
        /// Parses a fractional inch string and converts to PDF points.
        /// Supported formats: "1-1/8", "1 1/8", "3/4", "2".
        /// </summary>
        public static float InchesToPoints(string fractionalInches)
        {
            return ParseInches(fractionalInches) * PointsPerInch;
        }

        /// <summary>
        /// Parses a fractional inch string and returns the value in inches as a float.
        /// Supported formats: "1-1/8", "1 1/8", "3/4", "2".
        /// </summary>
        public static float ParseInches(string fractionalInches)
        {
            if (fractionalInches == null) throw new ArgumentNullException(nameof(fractionalInches));
            string s = fractionalInches.Trim();
            if (s.Length == 0) throw new FormatException("Input string is empty.");

            int slashIndex = s.IndexOf('/');

            // No fraction part — whole number or decimal
            if (slashIndex < 0)
            {
                if (float.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float whole))
                    return whole;
                throw new FormatException($"Cannot parse \"{fractionalInches}\" as inches.");
            }

            // Find separator between whole part and fraction (hyphen or space)
            int separatorIndex = -1;
            for (int i = slashIndex - 1; i >= 0; i--)
            {
                if (s[i] == '-' || s[i] == ' ')
                {
                    separatorIndex = i;
                    break;
                }
            }

            float wholePart = 0f;
            string fractionStr;

            if (separatorIndex >= 0)
            {
                // Has whole part + fraction
                string wholeStr = s.Substring(0, separatorIndex).Trim();
                fractionStr = s.Substring(separatorIndex + 1).Trim();
                if (!float.TryParse(wholeStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out wholePart))
                    throw new FormatException($"Cannot parse \"{fractionalInches}\" as inches.");
            }
            else
            {
                // Fraction only (e.g. "3/4")
                fractionStr = s;
            }

            // Parse numerator/denominator
            string[] parts = fractionStr.Split('/');
            if (parts.Length != 2
                || !int.TryParse(parts[0].Trim(), out int num)
                || !int.TryParse(parts[1].Trim(), out int den)
                || den == 0)
                throw new FormatException($"Cannot parse \"{fractionalInches}\" as inches.");

            return wholePart + (float)num / den;
        }
    }
}
