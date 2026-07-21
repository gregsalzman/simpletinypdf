using System;
using System.IO;
using System.IO.Compression;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Decodes /FlateDecode stream data, including the /DecodeParms predictors used by
    /// cross-reference streams. Only the streams the library itself must read (xref
    /// streams and object streams) are ever decoded; imported page content passes
    /// through untouched.
    /// </summary>
    internal static class FlateFilter
    {
        /// <summary>Inflates zlib-wrapped (or raw) deflate data.</summary>
        internal static byte[] Inflate(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            // PDF Flate data is zlib-wrapped: 2-byte header, deflate body, Adler-32 trailer.
            // Some producers emit raw deflate, so fall back when the header is absent.
            int skip = (data.Length > 2 && (data[0] & 0x0F) == 8 && ((data[0] << 8) | data[1]) % 31 == 0) ? 2 : 0;
            using (var input = new MemoryStream(data, skip, data.Length - skip))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                deflate.CopyTo(output);
                return output.ToArray();
            }
        }

        /// <summary>
        /// Decodes a stream that must be readable (xref stream or object stream): applies
        /// /FlateDecode and any /DecodeParms predictor. Throws <see cref="PdfParseException"/>
        /// for filters the library cannot decode.
        /// </summary>
        internal static byte[] DecodeReadableStream(CosStream stream, Func<CosValue, CosValue> resolve)
        {
            byte[] data = stream.RawData ?? Array.Empty<byte>();
            var filter = resolve(stream.Get("Filter"));
            if (filter == null || filter is CosNull)
                return data;

            string filterName = (filter as CosName)?.Value;
            if (filter is CosArray filterArray)
            {
                if (filterArray.Items.Count == 0)
                    return data;
                if (filterArray.Items.Count == 1)
                    filterName = (resolve(filterArray.Items[0]) as CosName)?.Value;
            }
            if (filterName != "FlateDecode" && filterName != "Fl")
                throw new PdfParseException($"Unsupported filter '{filterName ?? "?"}' on a cross-reference or object stream.");

            byte[] inflated;
            try
            {
                inflated = Inflate(data);
            }
            catch (InvalidDataException ex)
            {
                throw new PdfParseException("Corrupt FlateDecode data in a cross-reference or object stream.", ex);
            }

            var parms = resolve(stream.Get("DecodeParms")) as CosDict;
            if (parms == null && resolve(stream.Get("DecodeParms")) is CosArray parmsArray && parmsArray.Items.Count > 0)
                parms = resolve(parmsArray.Items[0]) as CosDict;
            return ApplyPredictor(inflated, parms);
        }

        /// <summary>Reverses the /Predictor pre-processing described by a /DecodeParms dictionary.</summary>
        internal static byte[] ApplyPredictor(byte[] data, CosDict parms)
        {
            long predictor = parms?.GetInteger("Predictor") ?? 1;
            if (predictor <= 1)
                return data;

            int colors = (int)(parms.GetInteger("Colors") ?? 1);
            int bitsPerComponent = (int)(parms.GetInteger("BitsPerComponent") ?? 8);
            int columns = (int)(parms.GetInteger("Columns") ?? 1);
            int bytesPerPixel = Math.Max(1, colors * bitsPerComponent / 8);
            int rowLength = (columns * colors * bitsPerComponent + 7) / 8;

            if (predictor == 2)
                return ApplyTiffPredictor(data, rowLength, bytesPerPixel);
            return ApplyPngPredictor(data, rowLength, bytesPerPixel);
        }

        private static byte[] ApplyTiffPredictor(byte[] data, int rowLength, int bytesPerPixel)
        {
            // TIFF predictor 2: each sample is stored as a delta from the previous one.
            // Only the 8-bit component case is handled (the only one seen in practice).
            var result = (byte[])data.Clone();
            for (int row = 0; row * rowLength < result.Length; row++)
            {
                int start = row * rowLength;
                int end = Math.Min(start + rowLength, result.Length);
                for (int i = start + bytesPerPixel; i < end; i++)
                    result[i] = (byte)(result[i] + result[i - bytesPerPixel]);
            }
            return result;
        }

        private static byte[] ApplyPngPredictor(byte[] data, int rowLength, int bytesPerPixel)
        {
            // PNG predictors (10-15): every row is prefixed with a filter-type byte,
            // decoded exactly like PNG scanlines (see PngDecoder.UndoFilters).
            int stride = rowLength + 1;
            int rows = data.Length / stride;
            var result = new byte[rows * rowLength];
            var previous = new byte[rowLength];

            for (int row = 0; row < rows; row++)
            {
                int srcStart = row * stride;
                int dstStart = row * rowLength;
                byte filterType = data[srcStart];

                for (int col = 0; col < rowLength; col++)
                {
                    byte raw = data[srcStart + 1 + col];
                    byte a = col >= bytesPerPixel ? result[dstStart + col - bytesPerPixel] : (byte)0;
                    byte b = previous[col];
                    byte c = col >= bytesPerPixel ? previous[col - bytesPerPixel] : (byte)0;

                    byte value;
                    switch (filterType)
                    {
                        case 0: value = raw; break;
                        case 1: value = (byte)(raw + a); break;
                        case 2: value = (byte)(raw + b); break;
                        case 3: value = (byte)(raw + (a + b) / 2); break;
                        case 4: value = (byte)(raw + PaethPredictor(a, b, c)); break;
                        default: value = raw; break;
                    }
                    result[dstStart + col] = value;
                }
                Array.Copy(result, dstStart, previous, 0, rowLength);
            }
            return result;
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
    }
}
