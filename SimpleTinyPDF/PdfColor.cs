namespace SimpleTinyPDF
{
    /// <summary>
    /// Represents a color in either RGB or CMYK color space for use in PDF rendering.
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

        /// <summary>Cyan component (0.0 - 1.0). Only meaningful for CMYK colors.</summary>
        public float C { get; }
        /// <summary>Magenta component (0.0 - 1.0). Only meaningful for CMYK colors.</summary>
        public float M { get; }
        /// <summary>Yellow component (0.0 - 1.0). Only meaningful for CMYK colors.</summary>
        public float Y { get; }
        /// <summary>Key/Black component (0.0 - 1.0). Only meaningful for CMYK colors.</summary>
        public float K { get; }

        private PdfColor(float r, float g, float b, float c, float m, float y, float k, bool isCmyk)
        {
            R = r; G = g; B = b;
            C = c; M = m; Y = y; K = k;
            IsCmyk = isCmyk;
        }

        /// <summary>Creates an RGB color from 0-255 integer values.</summary>
        public static PdfColor Rgb(int r, int g, int b) =>
            new PdfColor(r / 255f, g / 255f, b / 255f, 0, 0, 0, 0, false);

        /// <summary>Creates an RGB color from 0.0-1.0 float values.</summary>
        public static PdfColor Rgb(float r, float g, float b) =>
            new PdfColor(r, g, b, 0, 0, 0, 0, false);

        /// <summary>Creates a CMYK color from 0.0-1.0 float values.</summary>
        public static PdfColor Cmyk(float c, float m, float y, float k) =>
            new PdfColor(0, 0, 0, c, m, y, k, true);

        /// <summary>Creates a grayscale color (RGB shorthand).</summary>
        public static PdfColor Gray(float brightness) =>
            new PdfColor(brightness, brightness, brightness, 0, 0, 0, 0, false);

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
