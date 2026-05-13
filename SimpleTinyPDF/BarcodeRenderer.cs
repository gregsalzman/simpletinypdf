using System;
using System.Globalization;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Orchestrates barcode encoding and PDF content stream generation.
    /// </summary>
    internal static class BarcodeRenderer
    {
        private static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        internal static void Render(PdfPage page, string data, BarcodeType type,
            float x, float y, float width, float height, BarcodeOptions options)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (width <= 0) throw new ArgumentException("Width must be positive.", nameof(width));
            if (height <= 0) throw new ArgumentException("Height must be positive.", nameof(height));

            options = options ?? new BarcodeOptions();

            switch (type)
            {
                case BarcodeType.QrCode:
                    RenderQrCode(page, data, x, y, width, height, options);
                    break;
                default:
                    Render1D(page, data, type, x, y, width, height, options);
                    break;
            }
        }

        private static void Render1D(PdfPage page, string data, BarcodeType type,
            float x, float y, float width, float height, BarcodeOptions options)
        {
            bool[] modules;
            string displayText;

            switch (type)
            {
                case BarcodeType.Code39:
                    modules = Code39Encoder.Encode(data, out displayText);
                    break;
                case BarcodeType.Ean13:
                    modules = Ean13Encoder.Encode(data, out displayText);
                    break;
                case BarcodeType.UpcA:
                    modules = Ean13Encoder.EncodeUpcA(data, out displayText);
                    break;
                case BarcodeType.Code128:
                    modules = Code128Encoder.Encode(data, out displayText);
                    break;
                default:
                    throw new ArgumentException($"Unsupported barcode type: {type}", nameof(type));
            }

            // Calculate quiet zone
            int quietZoneModules = 0;
            if (options.IncludeQuietZone)
            {
                quietZoneModules = type == BarcodeType.Code39 ? 10
                    : (type == BarcodeType.Ean13 || type == BarcodeType.UpcA) ? 11
                    : 10; // Code128
            }

            int totalModules = modules.Length + 2 * quietZoneModules;
            float moduleWidth = width / totalModules;

            // Reserve space for text if needed
            float barHeight = height;
            float textHeight = 0;
            if (options.ShowText)
            {
                textHeight = options.TextFontSize * 1.2f;
                barHeight = height - textHeight;
                if (barHeight < 1) barHeight = height; // fall back if too small
            }

            bool topDown = page.CoordinateOrigin == CoordinateOrigin.TopDown;
            float pdfY = topDown ? page.Height - y - height : y;
            float barPdfY = pdfY + textHeight; // bars sit above the text area

            var sb = page.GetContentBuilder();

            sb.Append("q\n");
            page.ApplyOpacity(options.Opacity);
            page.ApplyRotation(options.Rotation, x, topDown ? page.Height - y : y);

            // Background
            if (options.DrawBackground)
            {
                AppendColorFill(sb, options.BackgroundColor);
                sb.AppendFormat("{0} {1} {2} {3} re f\n", F(x), F(pdfY), F(width), F(height));
            }

            // Bars - batch adjacent dark modules into single rectangles
            AppendColorFill(sb, options.ForegroundColor);
            float barsLeft = x + quietZoneModules * moduleWidth;
            int runStart = -1;

            for (int i = 0; i <= modules.Length; i++)
            {
                bool isDark = i < modules.Length && modules[i];
                if (isDark && runStart < 0)
                {
                    runStart = i;
                }
                else if (!isDark && runStart >= 0)
                {
                    float barX = barsLeft + runStart * moduleWidth;
                    float barW = (i - runStart) * moduleWidth;
                    sb.AppendFormat("{0} {1} {2} {3} re\n",
                        F(barX), F(barPdfY), F(barW), F(barHeight));
                    runStart = -1;
                }
            }
            sb.Append("f\n");

            sb.Append("Q\n");

            // Human-readable text (uses PdfPage.DrawText which manages its own q/Q)
            if (options.ShowText && textHeight > 0 && barHeight < height)
            {
                float textY = topDown ? y + barHeight : y; // text is below bars in user coords
                float textWidth = page.MeasureText(displayText, options.TextFont, options.TextFontSize);
                float textX = x + (width - textWidth) / 2f;
                page.DrawText(displayText, textX, textY, options.TextFont, options.TextFontSize,
                    options.ForegroundColor);
            }
        }

        private static void RenderQrCode(PdfPage page, string data,
            float x, float y, float width, float height, BarcodeOptions options)
        {
            var result = QrCodeEncoder.Encode(data, options.QrErrorCorrectionLevel);
            int size = result.Size;
            bool[,] grid = result.Modules;

            // QR codes are square — use the smaller dimension
            float side = Math.Min(width, height);
            float offsetX = x + (width - side) / 2f;
            float offsetY = y + (height - side) / 2f;

            // Quiet zone: 4 modules on each side per spec
            int quietZoneModules = options.IncludeQuietZone ? 4 : 0;
            int totalModules = size + 2 * quietZoneModules;
            float moduleSize = side / totalModules;

            bool topDown = page.CoordinateOrigin == CoordinateOrigin.TopDown;
            float pdfY = topDown ? page.Height - offsetY - side : offsetY;

            var sb = page.GetContentBuilder();

            sb.Append("q\n");
            page.ApplyOpacity(options.Opacity);
            page.ApplyRotation(options.Rotation, x, topDown ? page.Height - y : y);

            // Background
            if (options.DrawBackground)
            {
                AppendColorFill(sb, options.BackgroundColor);
                sb.AppendFormat("{0} {1} {2} {3} re f\n",
                    F(offsetX), F(pdfY), F(side), F(side));
            }

            // Dark modules — row-run-length optimized
            AppendColorFill(sb, options.ForegroundColor);
            float gridLeft = offsetX + quietZoneModules * moduleSize;
            float gridBottom = pdfY + quietZoneModules * moduleSize;

            for (int row = 0; row < size; row++)
            {
                // PDF Y: bottom of this row (PDF coordinates go up)
                float rowPdfY = gridBottom + (size - 1 - row) * moduleSize;
                int runStart = -1;

                for (int col = 0; col <= size; col++)
                {
                    bool isDark = col < size && grid[row, col];
                    if (isDark && runStart < 0)
                    {
                        runStart = col;
                    }
                    else if (!isDark && runStart >= 0)
                    {
                        float modX = gridLeft + runStart * moduleSize;
                        float modW = (col - runStart) * moduleSize;
                        sb.AppendFormat("{0} {1} {2} {3} re\n",
                            F(modX), F(rowPdfY), F(modW), F(moduleSize));
                        runStart = -1;
                    }
                }
            }
            sb.Append("f\n");

            sb.Append("Q\n");
        }

        private static void AppendColorFill(StringBuilder sb, PdfColor color)
        {
            if (color.IsCmyk)
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0} {1} {2} {3} k\n", F(color.C), F(color.M), F(color.Y), F(color.K));
            else
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0} {1} {2} rg\n", F(color.R), F(color.G), F(color.B));
        }
    }
}
