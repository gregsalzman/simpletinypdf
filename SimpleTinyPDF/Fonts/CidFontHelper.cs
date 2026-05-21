using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Builds CID font data structures for Type0/Identity-H PDF font embedding.
    /// </summary>
    internal static class CidFontHelper
    {
        /// <summary>
        /// Encodes a text string as a hex glyph ID string for CID font content streams.
        /// Each character is mapped to its 2-byte glyph ID (big-endian) in hex.
        /// Example: "Hi" → "&lt;00480069&gt;"
        /// </summary>
        internal static string EncodeTextAsHexGlyphIds(string text, TrueTypeFont font)
        {
            if (string.IsNullOrEmpty(text)) return "<>";
            var sb = new StringBuilder(text.Length * 4 + 2);
            sb.Append('<');
            for (int i = 0; i < text.Length; i++)
            {
                int cp;
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    cp = char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                else
                {
                    cp = text[i];
                }
                int gid = font.GetGlyphId(cp);
                sb.Append(gid.ToString("X4"));
            }
            sb.Append('>');
            return sb.ToString();
        }

        /// <summary>
        /// Builds the /W (width) array for a CIDFont dictionary.
        /// Uses compact grouped format: [gid [w1 w2 w3] gid2 [w4 w5] ...]
        /// Widths are in 1/1000 em units.
        /// </summary>
        internal static string BuildWidthArray(TrueTypeFont font, ICollection<int> usedGlyphIds)
        {
            if (usedGlyphIds == null || usedGlyphIds.Count == 0) return "[]";

            var sorted = usedGlyphIds.OrderBy(g => g).ToList();
            var sb = new StringBuilder("[");

            int i = 0;
            while (i < sorted.Count)
            {
                // Find a run of consecutive glyph IDs
                int start = sorted[i];
                int end = start;
                while (i + 1 < sorted.Count && sorted[i + 1] == end + 1)
                {
                    end = sorted[++i];
                }

                // Emit: startGid [w1 w2 w3 ...]
                sb.Append(start);
                sb.Append('[');
                for (int g = start; g <= end; g++)
                {
                    if (g > start) sb.Append(' ');
                    int rawWidth = font.GetAdvanceWidth(g);
                    int scaledWidth = (int)(rawWidth * 1000L / font.UnitsPerEm);
                    sb.Append(scaledWidth);
                }
                sb.Append(']');
                i++;
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Builds a ToUnicode CMap stream that maps glyph IDs to Unicode codepoints.
        /// This enables text selection and copy/paste in PDF viewers.
        /// </summary>
        internal static byte[] BuildToUnicodeCMap(Dictionary<int, int> glyphIdToUnicode)
        {
            var sb = new StringBuilder();
            sb.Append("/CIDInit /ProcSet findresource begin\n");
            sb.Append("12 dict begin\n");
            sb.Append("begincmap\n");
            sb.Append("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n");
            sb.Append("/CMapName /Adobe-Identity-UCS def\n");
            sb.Append("/CMapType 2 def\n");
            sb.Append("1 begincodespacerange\n");
            sb.Append("<0000> <FFFF>\n");
            sb.Append("endcodespacerange\n");

            // Sort entries for deterministic output
            var entries = glyphIdToUnicode.OrderBy(kv => kv.Key).ToList();

            // PDF spec limits beginbfchar blocks to 100 entries each
            int idx = 0;
            while (idx < entries.Count)
            {
                int blockSize = System.Math.Min(100, entries.Count - idx);
                sb.AppendFormat("{0} beginbfchar\n", blockSize);
                for (int j = 0; j < blockSize; j++)
                {
                    var kv = entries[idx + j];
                    if (kv.Value <= 0xFFFF)
                    {
                        sb.AppendFormat("<{0:X4}> <{1:X4}>\n", kv.Key, kv.Value);
                    }
                    else
                    {
                        // Supplementary plane: encode as UTF-16 surrogate pair
                        int adjusted = kv.Value - 0x10000;
                        int high = 0xD800 + (adjusted >> 10);
                        int low = 0xDC00 + (adjusted & 0x3FF);
                        sb.AppendFormat("<{0:X4}> <{1:X4}{2:X4}>\n", kv.Key, high, low);
                    }
                }
                sb.Append("endbfchar\n");
                idx += blockSize;
            }

            sb.Append("endcmap\n");
            sb.Append("CMapName currentdict /CMap defineresource pop\n");
            sb.Append("end end\n");

            return Encoding.ASCII.GetBytes(sb.ToString());
        }
    }
}
