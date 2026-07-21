using System;
using System.Collections.Generic;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Expands an object stream (/Type /ObjStm): a compressed container holding
    /// multiple non-stream objects, introduced in PDF 1.5.
    /// </summary>
    internal static class ObjectStreamReader
    {
        /// <summary>
        /// Decodes the object stream and parses every contained object.
        /// Returns a map from object number to parsed value.
        /// </summary>
        internal static Dictionary<int, CosValue> Expand(CosStream objStm, Func<CosValue, CosValue> resolve)
        {
            byte[] data = FlateFilter.DecodeReadableStream(objStm, resolve);

            int count = (int)((resolve(objStm.Get("N")) as CosInteger)?.Value ?? 0);
            int first = (int)((resolve(objStm.Get("First")) as CosInteger)?.Value ?? 0);
            if (count <= 0 || first <= 0 || first > data.Length)
                throw new PdfParseException("Object stream has missing or invalid /N or /First entries.");

            // Header: N pairs of "objectNumber offsetInStream"
            var headerLexer = new PdfLexer(data);
            var pairs = new List<(int number, int offset)>(count);
            for (int i = 0; i < count; i++)
            {
                headerLexer.SkipWhitespace();
                string numToken = headerLexer.ReadRegularToken();
                headerLexer.SkipWhitespace();
                string offsetToken = headerLexer.ReadRegularToken();
                if (!int.TryParse(numToken, out int number) || !int.TryParse(offsetToken, out int offset))
                    throw new PdfParseException("Object stream has a malformed header.");
                pairs.Add((number, offset));
            }

            var parser = new PdfParser(data);
            var result = new Dictionary<int, CosValue>(count);
            foreach (var (number, offset) in pairs)
            {
                int position = first + offset;
                if (position < 0 || position >= data.Length)
                    continue;
                result[number] = parser.ParseValueAt(position);
            }
            return result;
        }
    }
}
