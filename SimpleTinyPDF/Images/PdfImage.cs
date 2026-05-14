using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SimpleTinyPDF
{
    internal enum ImageFormat
    {
        Jpeg,
        Png
    }

    /// <summary>
    /// Represents an image (JPEG or PNG) that can be placed on PDF pages.
    /// </summary>
    public sealed class PdfImage
    {
        private readonly byte[] _data;
        private readonly byte[] _contentHash;
        private readonly int _hashCode;

        /// <summary>Image width in pixels (display-oriented, accounts for EXIF rotation).</summary>
        public int PixelWidth { get; }

        /// <summary>Image height in pixels (display-oriented, accounts for EXIF rotation).</summary>
        public int PixelHeight { get; }

        /// <summary>Number of color components (1=gray, 3=RGB, 4=CMYK).</summary>
        internal int ComponentCount { get; }

        /// <summary>Bits per component (typically 8, sometimes 16 for PNG).</summary>
        internal int BitsPerComponent { get; }

        /// <summary>EXIF orientation tag value (1-8, default 1=normal).</summary>
        internal int ExifOrientation { get; }

        /// <summary>Image format (JPEG or PNG).</summary>
        internal ImageFormat Format { get; }

        /// <summary>Raw pixel width from the file header (before EXIF rotation).</summary>
        internal int RawPixelWidth { get; }

        /// <summary>Raw pixel height from the file header (before EXIF rotation).</summary>
        internal int RawPixelHeight { get; }

        /// <summary>Separated alpha channel data for PNG images with transparency.</summary>
        internal byte[] AlphaMask { get; }

        internal byte[] GetData() => _data;

        private PdfImage(byte[] data, int displayWidth, int displayHeight, int rawWidth, int rawHeight,
            int components, int bitsPerComponent, int exifOrientation, ImageFormat format, byte[] alphaMask,
            byte[] contentHash)
        {
            _data = data;
            PixelWidth = displayWidth;
            PixelHeight = displayHeight;
            RawPixelWidth = rawWidth;
            RawPixelHeight = rawHeight;
            ComponentCount = components;
            BitsPerComponent = bitsPerComponent;
            ExifOrientation = exifOrientation;
            Format = format;
            AlphaMask = alphaMask;
            _contentHash = contentHash;
            _hashCode = BitConverter.ToInt32(_contentHash, 0);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (!(obj is PdfImage other)) return false;
            if (_contentHash.Length != other._contentHash.Length) return false;
            for (int i = 0; i < _contentHash.Length; i++)
                if (_contentHash[i] != other._contentHash[i]) return false;
            return true;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => _hashCode;

        /// <summary>Loads an image from a file path.</summary>
        public static PdfImage FromFile(string filePath) =>
            FromBytes(File.ReadAllBytes(filePath));

        /// <summary>Loads an image (JPEG or PNG) from a byte array.</summary>
        public static PdfImage FromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length < 4)
                throw new ArgumentException("Invalid image data.");

            byte[] contentHash;
            using (var sha256 = SHA256.Create())
                contentHash = sha256.ComputeHash(imageData);

            // Auto-detect format by magic bytes
            if (imageData[0] == 0xFF && imageData[1] == 0xD8)
                return ParseJpeg(imageData, contentHash);
            if (imageData[0] == 0x89 && imageData[1] == 0x50 &&
                imageData[2] == 0x4E && imageData[3] == 0x47)
                return ParsePng(imageData, contentHash);

            throw new ArgumentException("Unsupported image format. Only JPEG and PNG are supported.");
        }

        /// <summary>Loads an image from a stream.</summary>
        public static PdfImage FromStream(Stream stream)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return FromBytes(ms.ToArray());
            }
        }

        private static PdfImage ParseJpeg(byte[] data, byte[] contentHash)
        {
            ParseJpegHeader(data, out int width, out int height, out int components, out int bitsPerComponent);
            if (width <= 0 || height <= 0)
                throw new ArgumentException("JPEG has invalid dimensions.");

            int orientation = ParseExifOrientation(data);

            int displayWidth = width;
            int displayHeight = height;
            // Orientations 5-8 swap width and height
            if (orientation >= 5 && orientation <= 8)
            {
                displayWidth = height;
                displayHeight = width;
            }

            return new PdfImage(data, displayWidth, displayHeight, width, height,
                components, bitsPerComponent, orientation, ImageFormat.Jpeg, null, contentHash);
        }

        private static PdfImage ParsePng(byte[] data, byte[] contentHash)
        {
            var result = PngDecoder.Decode(data);
            return new PdfImage(result.ColorData, result.Width, result.Height,
                result.Width, result.Height, result.Components, result.BitDepth,
                1, ImageFormat.Png, result.AlphaData, contentHash);
        }

        private static void ParseJpegHeader(byte[] data, out int width, out int height,
            out int components, out int bitsPerComponent)
        {
            int i = 2; // skip SOI
            while (i < data.Length - 1)
            {
                if (data[i] != 0xFF)
                    throw new ArgumentException("Invalid JPEG structure.");

                byte marker = data[i + 1];
                i += 2;

                // SOF markers (SOF0, SOF1, SOF2)
                if (marker == 0xC0 || marker == 0xC1 || marker == 0xC2)
                {
                    if (i + 8 > data.Length)
                        throw new ArgumentException("Truncated JPEG SOF segment.");
                    // length (2 bytes), precision (1 byte), height (2), width (2), components (1)
                    bitsPerComponent = data[i + 2];
                    height = (data[i + 3] << 8) | data[i + 4];
                    width = (data[i + 5] << 8) | data[i + 6];
                    components = data[i + 7];
                    return;
                }

                // Skip non-SOF markers
                if (marker == 0xD9) // EOI
                    break;
                if (marker == 0xD8 || (marker >= 0xD0 && marker <= 0xD7))
                    continue; // standalone markers, no length

                if (i + 1 >= data.Length)
                    break;
                int segmentLength = (data[i] << 8) | data[i + 1];
                if (segmentLength < 2)
                    throw new ArgumentException("Invalid JPEG segment length.");
                if (i + segmentLength > data.Length)
                    throw new ArgumentException("Truncated JPEG segment.");
                i += segmentLength;
            }

            throw new ArgumentException("Could not find SOF marker in JPEG data.");
        }

        /// <summary>
        /// Parses EXIF orientation tag from JPEG APP1 segment.
        /// Returns orientation 1-8 (1 = normal if not found).
        /// </summary>
        private static int ParseExifOrientation(byte[] data)
        {
            int i = 2; // skip SOI
            while (i < data.Length - 1)
            {
                if (data[i] != 0xFF) return 1;
                byte marker = data[i + 1];
                i += 2;

                if (marker == 0xD9) break; // EOI
                if (marker == 0xD8 || (marker >= 0xD0 && marker <= 0xD7))
                    continue;

                if (i + 1 >= data.Length) break;
                int segmentLength = (data[i] << 8) | data[i + 1];
                if (segmentLength < 2 || i + segmentLength > data.Length) break;

                // APP1 marker = 0xE1
                if (marker == 0xE1 && segmentLength >= 8)
                {
                    int segStart = i + 2; // skip length bytes
                    // Check "Exif\0\0" header
                    if (segStart + 6 <= i + segmentLength &&
                        data[segStart] == 0x45 && data[segStart + 1] == 0x78 &&
                        data[segStart + 2] == 0x69 && data[segStart + 3] == 0x66 &&
                        data[segStart + 4] == 0x00 && data[segStart + 5] == 0x00)
                    {
                        int tiffStart = segStart + 6;
                        int segEnd = i + segmentLength;
                        return ReadExifOrientation(data, tiffStart, segEnd);
                    }
                }

                i += segmentLength;
            }
            return 1; // default: normal orientation
        }

        private static int ReadExifOrientation(byte[] data, int tiffStart, int segEnd)
        {
            if (tiffStart + 8 > segEnd) return 1;

            // Byte order
            bool littleEndian;
            if (data[tiffStart] == 0x49 && data[tiffStart + 1] == 0x49)
                littleEndian = true; // "II"
            else if (data[tiffStart] == 0x4D && data[tiffStart + 1] == 0x4D)
                littleEndian = false; // "MM"
            else
                return 1;

            // Verify magic 42
            int magic = ReadUInt16(data, tiffStart + 2, littleEndian);
            if (magic != 42) return 1;

            // Offset to IFD0
            int ifdOffset = (int)ReadUInt32(data, tiffStart + 4, littleEndian);
            int ifdPos = tiffStart + ifdOffset;
            if (ifdPos + 2 > segEnd) return 1;

            int entryCount = ReadUInt16(data, ifdPos, littleEndian);
            ifdPos += 2;

            for (int e = 0; e < entryCount; e++)
            {
                int entryStart = ifdPos + e * 12;
                if (entryStart + 12 > segEnd) break;

                int tag = ReadUInt16(data, entryStart, littleEndian);
                if (tag == 0x0112) // Orientation tag
                {
                    int value = ReadUInt16(data, entryStart + 8, littleEndian);
                    if (value >= 1 && value <= 8) return value;
                    return 1;
                }
            }
            return 1;
        }

        private static ushort ReadUInt16(byte[] data, int offset, bool littleEndian)
        {
            if (littleEndian)
                return (ushort)(data[offset] | (data[offset + 1] << 8));
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static uint ReadUInt32(byte[] data, int offset, bool littleEndian)
        {
            if (littleEndian)
                return (uint)(data[offset] | (data[offset + 1] << 8) |
                              (data[offset + 2] << 16) | (data[offset + 3] << 24));
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) |
                          (data[offset + 2] << 8) | data[offset + 3]);
        }
    }
}
