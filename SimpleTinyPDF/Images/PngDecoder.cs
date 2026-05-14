using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace SimpleTinyPDF
{
    internal struct PngResult
    {
        internal byte[] ColorData;  // Deflate-compressed color pixel data (with PNG filter bytes)
        internal byte[] AlphaData;  // Deflate-compressed alpha data (with PNG filter bytes), null if no alpha
        internal int Width;
        internal int Height;
        internal int BitDepth;
        internal int Components;    // 1=gray, 3=RGB (after alpha separation)
    }

    internal static class PngDecoder
    {
        internal static PngResult Decode(byte[] data)
        {
            // Validate PNG signature
            if (data.Length < 8 ||
                data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47 ||
                data[4] != 0x0D || data[5] != 0x0A || data[6] != 0x1A || data[7] != 0x0A)
                throw new ArgumentException("Invalid PNG signature.");

            // Parse IHDR
            int pos = 8;
            if (pos + 8 > data.Length)
                throw new ArgumentException("Truncated PNG file.");

            int ihdrLen = ReadInt32BE(data, pos);
            pos += 4;
            string ihdrType = "" + (char)data[pos] + (char)data[pos + 1] + (char)data[pos + 2] + (char)data[pos + 3];
            if (ihdrType != "IHDR" || ihdrLen != 13)
                throw new ArgumentException("Invalid PNG: expected IHDR chunk.");
            pos += 4;

            int width = ReadInt32BE(data, pos);
            int height = ReadInt32BE(data, pos + 4);
            int bitDepth = data[pos + 8];
            int colorType = data[pos + 9];
            // compression=data[pos+10], filter=data[pos+11], interlace=data[pos+12]
            int interlace = data[pos + 12];
            if (interlace != 0)
                throw new ArgumentException("Interlaced PNG images are not supported.");

            pos += ihdrLen + 4; // skip IHDR data + CRC

            if (width <= 0 || height <= 0)
                throw new ArgumentException("PNG has invalid dimensions.");

            // Collect IDAT chunks and PLTE
            var idatChunks = new List<byte[]>();
            byte[] palette = null;
            byte[] trns = null;

            while (pos + 8 <= data.Length)
            {
                int chunkLen = ReadInt32BE(data, pos);
                pos += 4;
                if (pos + 4 > data.Length) break;
                string chunkType = "" + (char)data[pos] + (char)data[pos + 1] + (char)data[pos + 2] + (char)data[pos + 3];
                pos += 4;

                if (chunkLen < 0 || pos + chunkLen > data.Length)
                    throw new ArgumentException("Truncated PNG chunk.");

                if (chunkType == "IDAT")
                {
                    var chunk = new byte[chunkLen];
                    Buffer.BlockCopy(data, pos, chunk, 0, chunkLen);
                    idatChunks.Add(chunk);
                }
                else if (chunkType == "PLTE")
                {
                    palette = new byte[chunkLen];
                    Buffer.BlockCopy(data, pos, palette, 0, chunkLen);
                }
                else if (chunkType == "tRNS")
                {
                    trns = new byte[chunkLen];
                    Buffer.BlockCopy(data, pos, trns, 0, chunkLen);
                }
                else if (chunkType == "IEND")
                {
                    break;
                }

                pos += chunkLen + 4; // skip data + CRC
            }

            if (idatChunks.Count == 0)
                throw new ArgumentException("PNG has no IDAT chunks.");

            // Concatenate IDAT data
            int totalLen = 0;
            foreach (var c in idatChunks) totalLen += c.Length;
            var compressedData = new byte[totalLen];
            int offset = 0;
            foreach (var c in idatChunks)
            {
                Buffer.BlockCopy(c, 0, compressedData, offset, c.Length);
                offset += c.Length;
            }

            // Decompress (zlib = 2-byte header + deflate stream)
            byte[] rawScanlines;
            using (var ms = new MemoryStream(compressedData, 2, compressedData.Length - 2)) // skip zlib header
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                ds.CopyTo(output);
                rawScanlines = output.ToArray();
            }

            // Calculate bytes per pixel and scanline
            int srcComponents = GetComponentCount(colorType);
            int bytesPerPixel = srcComponents * (bitDepth / 8);
            if (bytesPerPixel == 0) bytesPerPixel = 1; // sub-byte (1/2/4-bit)
            int scanlineBytes = CalculateScanlineBytes(width, srcComponents, bitDepth);
            int expectedLen = height * (1 + scanlineBytes); // 1 filter byte per row

            if (rawScanlines.Length < expectedLen)
                throw new ArgumentException($"PNG decoded data too short: expected {expectedLen}, got {rawScanlines.Length}.");

            // Undo PNG filtering
            var unfiltered = new byte[height * scanlineBytes];
            UndoFilters(rawScanlines, unfiltered, height, scanlineBytes, bytesPerPixel);

            // Handle different color types
            bool hasAlpha = (colorType == 4 || colorType == 6);
            bool isIndexed = (colorType == 3);

            if (isIndexed)
            {
                if (palette == null)
                    throw new ArgumentException("Indexed PNG missing PLTE chunk.");
                return ExpandIndexed(unfiltered, width, height, bitDepth, palette, trns);
            }

            if (!hasAlpha)
            {
                // Color types 0 (gray) and 2 (RGB) — no alpha separation needed
                int components = (colorType == 0) ? 1 : 3;
                var colorCompressed = CompressWithPngFilter(unfiltered, width, height, components, bitDepth);
                return new PngResult
                {
                    ColorData = colorCompressed,
                    AlphaData = null,
                    Width = width,
                    Height = height,
                    BitDepth = bitDepth,
                    Components = components
                };
            }

            // Separate alpha from color
            return SeparateAlpha(unfiltered, width, height, bitDepth, colorType);
        }

        private static PngResult ExpandIndexed(byte[] unfiltered, int width, int height,
            int bitDepth, byte[] palette, byte[] trns)
        {
            bool hasAlpha = false;
            if (trns != null)
            {
                for (int i = 0; i < trns.Length; i++)
                    if (trns[i] != 255) { hasAlpha = true; break; }
            }

            int rgbRowBytes = width * 3;
            var colorPixels = new byte[height * rgbRowBytes];
            byte[] alphaPixels = hasAlpha ? new byte[height * width] : null;

            for (int row = 0; row < height; row++)
            {
                int srcRowStart = row * CalculateScanlineBytes(width, 1, bitDepth);
                int dstColorStart = row * rgbRowBytes;
                int dstAlphaStart = hasAlpha ? row * width : 0;

                for (int col = 0; col < width; col++)
                {
                    int index = GetIndexedValue(unfiltered, srcRowStart, col, bitDepth);
                    int palIdx = index * 3;
                    if (palIdx + 2 < palette.Length)
                    {
                        colorPixels[dstColorStart + col * 3] = palette[palIdx];
                        colorPixels[dstColorStart + col * 3 + 1] = palette[palIdx + 1];
                        colorPixels[dstColorStart + col * 3 + 2] = palette[palIdx + 2];
                    }
                    if (hasAlpha)
                    {
                        alphaPixels[dstAlphaStart + col] = (index < trns.Length) ? trns[index] : (byte)255;
                    }
                }
            }

            var colorCompressed = CompressWithPngFilter(colorPixels, width, height, 3, 8);
            byte[] alphaCompressed = hasAlpha ? CompressWithPngFilter(alphaPixels, width, height, 1, 8) : null;

            return new PngResult
            {
                ColorData = colorCompressed,
                AlphaData = alphaCompressed,
                Width = width,
                Height = height,
                BitDepth = 8,
                Components = 3
            };
        }

        private static int GetIndexedValue(byte[] data, int rowStart, int col, int bitDepth)
        {
            if (bitDepth == 8) return data[rowStart + col];
            if (bitDepth == 4)
            {
                int byteIdx = rowStart + col / 2;
                return (col % 2 == 0) ? (data[byteIdx] >> 4) & 0x0F : data[byteIdx] & 0x0F;
            }
            if (bitDepth == 2)
            {
                int byteIdx = rowStart + col / 4;
                int shift = 6 - (col % 4) * 2;
                return (data[byteIdx] >> shift) & 0x03;
            }
            if (bitDepth == 1)
            {
                int byteIdx = rowStart + col / 8;
                int shift = 7 - (col % 8);
                return (data[byteIdx] >> shift) & 0x01;
            }
            return 0;
        }

        private static PngResult SeparateAlpha(byte[] unfiltered, int width, int height,
            int bitDepth, int colorType)
        {
            int bytesPerSample = bitDepth / 8;
            int colorComponents = (colorType == 4) ? 1 : 3; // gray+alpha or RGBA
            int srcComponents = colorComponents + 1; // +1 for alpha
            int srcRowBytes = width * srcComponents * bytesPerSample;
            int colorRowBytes = width * colorComponents * bytesPerSample;
            int alphaRowBytes = width * bytesPerSample;

            var colorPixels = new byte[height * colorRowBytes];
            var alphaPixels = new byte[height * alphaRowBytes];

            for (int row = 0; row < height; row++)
            {
                int srcRowStart = row * srcRowBytes;
                int dstColorStart = row * colorRowBytes;
                int dstAlphaStart = row * alphaRowBytes;

                for (int col = 0; col < width; col++)
                {
                    int srcPixelStart = srcRowStart + col * srcComponents * bytesPerSample;
                    int dstColorPixel = dstColorStart + col * colorComponents * bytesPerSample;
                    int dstAlphaPixel = dstAlphaStart + col * bytesPerSample;

                    // Copy color samples
                    Buffer.BlockCopy(unfiltered, srcPixelStart, colorPixels, dstColorPixel,
                        colorComponents * bytesPerSample);
                    // Copy alpha sample
                    Buffer.BlockCopy(unfiltered, srcPixelStart + colorComponents * bytesPerSample,
                        alphaPixels, dstAlphaPixel, bytesPerSample);
                }
            }

            var colorCompressed = CompressWithPngFilter(colorPixels, width, height, colorComponents, bitDepth);
            var alphaCompressed = CompressWithPngFilter(alphaPixels, width, height, 1, bitDepth);

            return new PngResult
            {
                ColorData = colorCompressed,
                AlphaData = alphaCompressed,
                Width = width,
                Height = height,
                BitDepth = bitDepth,
                Components = colorComponents
            };
        }

        /// <summary>
        /// Compresses raw pixel data with PNG "None" filter (filter byte = 0) per row,
        /// then Deflate-compresses the result.
        /// </summary>
        private static byte[] CompressWithPngFilter(byte[] pixels, int width, int height,
            int components, int bitDepth)
        {
            int rowBytes = (width * components * bitDepth + 7) / 8;
            // Add filter byte (0 = None) before each row
            var filtered = new byte[height * (1 + rowBytes)];
            for (int row = 0; row < height; row++)
            {
                int srcStart = row * rowBytes;
                int dstStart = row * (1 + rowBytes);
                filtered[dstStart] = 0; // None filter
                Buffer.BlockCopy(pixels, srcStart, filtered, dstStart + 1, rowBytes);
            }

            using (var ms = new MemoryStream())
            {
                // Write zlib header (CM=8, CINFO=7, FCHECK to make header divisible by 31)
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);

                using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                {
                    ds.Write(filtered, 0, filtered.Length);
                }

                // Write Adler32 checksum
                uint adler = Adler32(filtered);
                ms.WriteByte((byte)(adler >> 24));
                ms.WriteByte((byte)(adler >> 16));
                ms.WriteByte((byte)(adler >> 8));
                ms.WriteByte((byte)adler);

                return ms.ToArray();
            }
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        private static void UndoFilters(byte[] raw, byte[] output, int height, int scanlineBytes, int bytesPerPixel)
        {
            for (int row = 0; row < height; row++)
            {
                int rawRowStart = row * (1 + scanlineBytes);
                int filterByte = raw[rawRowStart];
                int srcStart = rawRowStart + 1;
                int dstStart = row * scanlineBytes;
                int prevRowStart = (row - 1) * scanlineBytes;

                for (int col = 0; col < scanlineBytes; col++)
                {
                    byte rawByte = raw[srcStart + col];
                    byte a = (col >= bytesPerPixel) ? output[dstStart + col - bytesPerPixel] : (byte)0;
                    byte b = (row > 0) ? output[prevRowStart + col] : (byte)0;
                    byte c = (row > 0 && col >= bytesPerPixel) ? output[prevRowStart + col - bytesPerPixel] : (byte)0;

                    switch (filterByte)
                    {
                        case 0: // None
                            output[dstStart + col] = rawByte;
                            break;
                        case 1: // Sub
                            output[dstStart + col] = (byte)(rawByte + a);
                            break;
                        case 2: // Up
                            output[dstStart + col] = (byte)(rawByte + b);
                            break;
                        case 3: // Average
                            output[dstStart + col] = (byte)(rawByte + ((a + b) / 2));
                            break;
                        case 4: // Paeth
                            output[dstStart + col] = (byte)(rawByte + PaethPredictor(a, b, c));
                            break;
                        default:
                            throw new ArgumentException($"Unknown PNG filter type: {filterByte}");
                    }
                }
            }
        }

        private static byte PaethPredictor(byte a, byte b, byte c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            if (pb <= pc) return b;
            return c;
        }

        private static int GetComponentCount(int colorType)
        {
            switch (colorType)
            {
                case 0: return 1;  // Grayscale
                case 2: return 3;  // RGB
                case 3: return 1;  // Indexed (1 byte per pixel = palette index)
                case 4: return 2;  // Grayscale + Alpha
                case 6: return 4;  // RGBA
                default: throw new ArgumentException($"Unsupported PNG color type: {colorType}");
            }
        }

        private static int CalculateScanlineBytes(int width, int components, int bitDepth)
        {
            return (width * components * bitDepth + 7) / 8;
        }

        private static int ReadInt32BE(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) |
                   (data[offset + 2] << 8) | data[offset + 3];
        }
    }
}
