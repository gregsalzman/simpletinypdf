using System;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Represents a color in RGB, CMYK, or spot (Separation) color space for use in PDF rendering.
    /// </summary>
    public readonly struct PdfColor
    {
        /// <summary>True if this color uses the CMYK color space.</summary>
        public bool IsCmyk { get; }

        /// <summary>Red component (0.0 - 1.0). Only meaningful for RGB colors.</summary>
        public float R { get; }
        /// <summary>Green component (0.0 - 1.0). Only meaningful for RGB colors.</summary>
        public float G { get; }
        /// <summary>Blue component (0.0 - 1.0). Only meaningful for RGB colors.</summary>
        public float B { get; }

        /// <summary>Cyan component (0.0 - 1.0). Only meaningful for CMYK and spot colors.</summary>
        public float C { get; }
        /// <summary>Magenta component (0.0 - 1.0). Only meaningful for CMYK and spot colors.</summary>
        public float M { get; }
        /// <summary>Yellow component (0.0 - 1.0). Only meaningful for CMYK and spot colors.</summary>
        public float Y { get; }
        /// <summary>Key/Black component (0.0 - 1.0). Only meaningful for CMYK and spot colors.</summary>
        public float K { get; }

        /// <summary>Spot color name (e.g. "PANTONE 185 C"). Null for RGB/CMYK colors.</summary>
        public string SpotColorName { get; }

        /// <summary>Tint value (0.0 = no ink, 1.0 = full ink). Only meaningful for spot colors.</summary>
        public float Tint { get; }

        /// <summary>True if this is a spot (Separation) color.</summary>
        public bool IsSpotColor => SpotColorName != null;

        private PdfColor(float r, float g, float b, float c, float m, float y, float k, bool isCmyk,
            string spotColorName, float tint)
        {
            R = r; G = g; B = b;
            C = c; M = m; Y = y; K = k;
            IsCmyk = isCmyk;
            SpotColorName = spotColorName;
            Tint = tint;
        }

        /// <summary>Creates an RGB color from 0-255 integer values.</summary>
        public static PdfColor Rgb(int r, int g, int b) =>
            new PdfColor(r / 255f, g / 255f, b / 255f, 0, 0, 0, 0, false, null, 1f);

        /// <summary>Creates an RGB color from 0.0-1.0 float values.</summary>
        public static PdfColor Rgb(float r, float g, float b) =>
            new PdfColor(r, g, b, 0, 0, 0, 0, false, null, 1f);

        /// <summary>Creates a CMYK color from 0.0-1.0 float values.</summary>
        public static PdfColor Cmyk(float c, float m, float y, float k) =>
            new PdfColor(0, 0, 0, c, m, y, k, true, null, 1f);

        /// <summary>Creates a grayscale color (RGB shorthand).</summary>
        public static PdfColor Gray(float brightness) =>
            new PdfColor(brightness, brightness, brightness, 0, 0, 0, 0, false, null, 1f);

        /// <summary>
        /// Creates a spot (Separation) color with a CMYK display fallback.
        /// </summary>
        /// <param name="name">Spot color name (e.g. "PANTONE 185 C").</param>
        /// <param name="c">Cyan fallback component (0.0 - 1.0).</param>
        /// <param name="m">Magenta fallback component (0.0 - 1.0).</param>
        /// <param name="y">Yellow fallback component (0.0 - 1.0).</param>
        /// <param name="k">Key/Black fallback component (0.0 - 1.0).</param>
        /// <param name="tint">Tint value (0.0 = no ink, 1.0 = full ink). Default: 1.0.</param>
        public static PdfColor Spot(string name, float c, float m, float y, float k, float tint = 1f)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            return new PdfColor(0, 0, 0, c, m, y, k, true, name, Math.Max(0f, Math.Min(1f, tint)));
        }

        /// <summary>
        /// Returns a new spot color with the same ink definition but a different tint.
        /// Only valid for spot colors.
        /// </summary>
        public PdfColor WithTint(float tint)
        {
            if (SpotColorName == null)
                throw new InvalidOperationException("WithTint can only be used on spot colors.");
            return new PdfColor(0, 0, 0, C, M, Y, K, true, SpotColorName, Math.Max(0f, Math.Min(1f, tint)));
        }

        /// <summary>Black (CMYK).</summary>
        public static readonly PdfColor Black = Cmyk(0f, 0f, 0f, 1f);
        /// <summary>White.</summary>
        public static readonly PdfColor White = Rgb(1f, 1f, 1f);
        /// <summary>Red.</summary>
        public static readonly PdfColor Red = Rgb(1f, 0f, 0f);
        /// <summary>Green.</summary>
        public static readonly PdfColor Green = Rgb(0f, 1f, 0f);
        /// <summary>Blue.</summary>
        public static readonly PdfColor Blue = Rgb(0f, 0f, 1f);
        /// <summary>Yellow (CMYK).</summary>
        public static readonly PdfColor Yellow = Cmyk(0f, 0f, 1f, 0f);
        /// <summary>Cyan (CMYK).</summary>
        public static readonly PdfColor Cyan = Cmyk(1f, 0f, 0f, 0f);
        /// <summary>Magenta (CMYK).</summary>
        public static readonly PdfColor Magenta = Cmyk(0f, 1f, 0f, 0f);
        /// <summary>Orange (255, 165, 0).</summary>
        public static readonly PdfColor Orange = Rgb(255, 165, 0);
        /// <summary>Purple (128, 0, 128).</summary>
        public static readonly PdfColor Purple = Rgb(128, 0, 128);
        /// <summary>Pink (255, 192, 203).</summary>
        public static readonly PdfColor Pink = Rgb(255, 192, 203);
        /// <summary>Brown (139, 69, 19).</summary>
        public static readonly PdfColor Brown = Rgb(139, 69, 19);
        /// <summary>Gold (255, 215, 0).</summary>
        public static readonly PdfColor Gold = Rgb(255, 215, 0);
        /// <summary>Navy (0, 0, 128).</summary>
        public static readonly PdfColor Navy = Rgb(0, 0, 128);
        /// <summary>Teal (0, 128, 128).</summary>
        public static readonly PdfColor Teal = Rgb(0, 128, 128);
        /// <summary>Maroon (128, 0, 0).</summary>
        public static readonly PdfColor Maroon = Rgb(128, 0, 0);
        /// <summary>Olive (128, 128, 0).</summary>
        public static readonly PdfColor Olive = Rgb(128, 128, 0);
        /// <summary>Coral (255, 127, 80).</summary>
        public static readonly PdfColor Coral = Rgb(255, 127, 80);
        /// <summary>Crimson (220, 20, 60).</summary>
        public static readonly PdfColor Crimson = Rgb(220, 20, 60);
        /// <summary>Indigo (75, 0, 130).</summary>
        public static readonly PdfColor Indigo = Rgb(75, 0, 130);
        /// <summary>Silver — light metallic gray (192, 192, 192).</summary>
        public static readonly PdfColor Silver = Gray(0.753f);
        /// <summary>Medium gray (0.50).</summary>
        public static readonly PdfColor MediumGray = Gray(0.5f);
        /// <summary>Light gray (0.83).</summary>
        public static readonly PdfColor LightGray = Gray(0.83f);
        /// <summary>Dark gray (0.33).</summary>
        public static readonly PdfColor DarkGray = Gray(0.33f);
    }
}
