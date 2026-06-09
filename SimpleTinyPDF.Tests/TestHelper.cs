using System;
using System.IO;
using System.Text;
using PDFtoImage;
using SkiaSharp;

namespace SimpleTinyPDF.Tests
{
    public static class TestHelper
    {
        private static readonly string OutputDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TestOutput");

        static TestHelper()
        {
            Directory.CreateDirectory(OutputDir);
        }

        /// <summary>
        /// Saves a PDF byte array to TestOutput and returns the file path.
        /// testName may contain slashes for subdirectory output (e.g. "Text/hello-world").
        /// </summary>
        public static string SavePdf(byte[] pdfBytes, string testName)
        {
            var path = Path.Combine(OutputDir, testName + ".pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, pdfBytes);
            return path;
        }

        /// <summary>
        /// Rasterizes a PDF page to a bitmap. Returns the bitmap for assertions.
        /// Also saves it as a PNG to TestOutput.
        /// </summary>
        public static SKBitmap RasterizePage(byte[] pdfBytes, string testName, int pageIndex = 0, int dpi = 150,
            bool withAnnotations = false, bool withFormFill = false)
        {
            var pngPath = Path.Combine(OutputDir, $"{testName}_page{pageIndex}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);
            var options = new RenderOptions(Dpi: dpi, WithAnnotations: withAnnotations, WithFormFill: withFormFill);

            var bitmap = Conversion.ToImage(pdfBytes, page: pageIndex, options: options);

            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var fs = File.Create(pngPath))
            {
                data.SaveTo(fs);
            }

            return bitmap;
        }

        /// <summary>
        /// Gets the number of pages in a PDF.
        /// </summary>
        public static int GetPageCount(byte[] pdfBytes)
        {
            return Conversion.GetPageCount(pdfBytes);
        }

        /// <summary>
        /// Asserts that the pixel at (x, y) is approximately the expected RGB color.
        /// </summary>
        public static void AssertPixelColor(SKBitmap bitmap, int x, int y,
            byte expectedR, byte expectedG, byte expectedB, int tolerance = 30)
        {
            var pixel = bitmap.GetPixel(x, y);
            var dr = Math.Abs(pixel.Red - expectedR);
            var dg = Math.Abs(pixel.Green - expectedG);
            var db = Math.Abs(pixel.Blue - expectedB);

            if (dr > tolerance || dg > tolerance || db > tolerance)
                throw new Exception(
                    $"Pixel at ({x},{y}): expected approx ({expectedR},{expectedG},{expectedB}) " +
                    $"but got ({pixel.Red},{pixel.Green},{pixel.Blue}), tolerance={tolerance}");
        }

        /// <summary>
        /// Asserts that the pixel at (x, y) is NOT white (i.e., something was drawn there).
        /// </summary>
        public static void AssertPixelNotWhite(SKBitmap bitmap, int x, int y, int threshold = 250)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Red > threshold && pixel.Green > threshold && pixel.Blue > threshold)
                throw new Exception(
                    $"Pixel at ({x},{y}) is white ({pixel.Red},{pixel.Green},{pixel.Blue}) " +
                    $"but expected non-white content.");
        }

        /// <summary>
        /// Generates a minimal valid JPEG image (solid color) as a byte array.
        /// Uses a raw JFIF construction.
        /// </summary>
        public static byte[] CreateTestJpeg(int width = 8, int height = 8)
        {
            // Create a minimal valid JPEG using raw JFIF bytes.
            // This creates a tiny 8x8 red image.
            using (var surface = SKSurface.Create(new SKImageInfo(width, height)))
            {
                surface.Canvas.Clear(SKColors.Red);
                using (var img = surface.Snapshot())
                using (var data = img.Encode(SKEncodedImageFormat.Jpeg, 90))
                {
                    return data.ToArray();
                }
            }
        }

        /// <summary>
        /// Creates a test JPEG with a specific color.
        /// </summary>
        public static byte[] CreateTestJpeg(SKColor color, int width = 8, int height = 8)
        {
            using (var surface = SKSurface.Create(new SKImageInfo(width, height)))
            {
                surface.Canvas.Clear(color);
                using (var img = surface.Snapshot())
                using (var data = img.Encode(SKEncodedImageFormat.Jpeg, 90))
                {
                    return data.ToArray();
                }
            }
        }

        /// <summary>
        /// Creates a test JPEG with four distinct colored quadrants for verifying
        /// orientation, rotation, and aspect ratio. Colors:
        ///   Top-left: Red,  Top-right: Green,
        ///   Bottom-left: Blue, Bottom-right: Yellow
        /// </summary>
        public static byte[] CreateQuadrantJpeg(int width = 100, int height = 100)
        {
            using (var surface = SKSurface.Create(new SKImageInfo(width, height)))
            {
                var canvas = surface.Canvas;
                int hw = width / 2;
                int hh = height / 2;
                canvas.DrawRect(new SKRect(0, 0, hw, hh), new SKPaint { Color = SKColors.Red });          // top-left
                canvas.DrawRect(new SKRect(hw, 0, width, hh), new SKPaint { Color = SKColors.Green });     // top-right
                canvas.DrawRect(new SKRect(0, hh, hw, height), new SKPaint { Color = SKColors.Blue });     // bottom-left
                canvas.DrawRect(new SKRect(hw, hh, width, height), new SKPaint { Color = SKColors.Yellow });// bottom-right
                using (var img = surface.Snapshot())
                using (var data = img.Encode(SKEncodedImageFormat.Jpeg, 95))
                {
                    return data.ToArray();
                }
            }
        }
        /// <summary>
        /// Converts PDF points to pixels at a given DPI.
        /// </summary>
        public static int PtToPx(float pt, int dpi = 150) => (int)(pt * dpi / 72.0);

        /// <summary>
        /// Returns true if any pixel in the region is non-white (dark).
        /// </summary>
        public static bool HasDarkPixelsInRegion(SKBitmap bitmap,
            int xMin, int xMax, int yMin, int yMax)
        {
            xMax = Math.Min(xMax, bitmap.Width - 1);
            yMax = Math.Min(yMax, bitmap.Height - 1);
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Red < 200 || p.Green < 200 || p.Blue < 200)
                        return true;
                }
            return false;
        }

        /// <summary>
        /// Counts dark (non-white) pixels in the region.
        /// </summary>
        public static int CountDarkPixelsInRegion(SKBitmap bitmap,
            int xMin, int xMax, int yMin, int yMax)
        {
            xMax = Math.Min(xMax, bitmap.Width - 1);
            yMax = Math.Min(yMax, bitmap.Height - 1);
            int count = 0;
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Red < 200 || p.Green < 200 || p.Blue < 200)
                        count++;
                }
            return count;
        }

        /// <summary>
        /// Returns the raw ASCII text of a PDF byte array for content inspection.
        /// </summary>
        public static string GetPdfText(byte[] bytes) =>
            Encoding.ASCII.GetString(bytes);

        /// <summary>
        /// Draws a small description label at the top of the page to help humans
        /// understand what the PDF is demonstrating during manual review.
        /// </summary>
        public static void AddDescription(PdfPage page, string description)
        {
            page.DrawText(description, 10, 10, PdfFont.Helvetica, 8,
                PdfColor.Rgb(180, 180, 180));
        }
    }
}
