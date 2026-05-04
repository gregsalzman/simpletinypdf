using System;
using System.IO;
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
        /// </summary>
        public static string SavePdf(byte[] pdfBytes, string testName)
        {
            var path = Path.Combine(OutputDir, testName + ".pdf");
            File.WriteAllBytes(path, pdfBytes);
            return path;
        }

        /// <summary>
        /// Rasterizes a PDF page to a bitmap. Returns the bitmap for assertions.
        /// Also saves it as a PNG to TestOutput.
        /// </summary>
        public static SKBitmap RasterizePage(byte[] pdfBytes, string testName, int pageIndex = 0, int dpi = 150)
        {
            var pngPath = Path.Combine(OutputDir, $"{testName}_page{pageIndex}.png");
            var options = new RenderOptions(Dpi: dpi);

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
    }
}
