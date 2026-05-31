using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Subsets a TrueType font binary to include only the used glyphs.
    /// Unused glyph slots are zero-filled (glyph IDs are not remapped).
    /// CFF/OpenType fonts are not supported — pass them through unmodified.
    /// </summary>
    internal static class TrueTypeSubsetter
    {
        private static readonly Random Rng = new Random();

        /// <summary>
        /// Generates a random 6-letter uppercase tag for the PDF subset naming convention.
        /// </summary>
        internal static string GenerateSubsetTag()
        {
            var chars = new char[6];
            lock (Rng)
            {
                for (int i = 0; i < 6; i++)
                    chars[i] = (char)('A' + Rng.Next(26));
            }
            return new string(chars);
        }

        /// <summary>
        /// Creates a subset TrueType binary containing only the specified glyphs
        /// (plus .notdef at glyph 0 and any composite glyph components).
        /// </summary>
        internal static byte[] Subset(byte[] fontData, HashSet<int> usedGlyphIds,
            IReadOnlyDictionary<string, (uint offset, uint length)> tables)
        {
            if (!tables.ContainsKey("glyf") || !tables.ContainsKey("loca"))
                return fontData; // Not a TrueType-outline font

            int numGlyphs = ReadUInt16(fontData, (int)tables["maxp"].offset + 4);
            int locaFormat = ReadInt16(fontData, (int)tables["head"].offset + 50);

            // Parse loca table → glyph offsets
            var glyfOffset = (int)tables["glyf"].offset;
            var locaOffset = (int)tables["loca"].offset;
            var glyphOffsets = ParseLoca(fontData, locaOffset, locaFormat, numGlyphs);

            // Compute transitive closure (include .notdef + composite components)
            var retained = ComputeGlyphClosure(fontData, glyfOffset, glyphOffsets,
                usedGlyphIds, numGlyphs);

            // Build subset glyf table
            var newGlyf = BuildSubsetGlyf(fontData, glyfOffset, glyphOffsets, retained, numGlyphs);

            // Build new loca table from the new glyf offsets
            var newLoca = BuildSubsetLoca(fontData, glyfOffset, glyphOffsets, retained,
                numGlyphs, locaFormat);

            // Build minimal post table (format 3.0)
            var newPost = BuildMinimalPost(fontData, tables);

            // Determine which tables to include in the subset font
            var tablesToInclude = new List<(string tag, byte[] data)>();

            // Tables copied verbatim
            string[] copyTags = { "head", "hhea", "maxp", "hmtx", "cmap", "name", "OS/2",
                                  "cvt ", "fpgm", "prep" };
            foreach (var tag in copyTags)
            {
                if (tables.TryGetValue(tag, out var t))
                {
                    var data = new byte[t.length];
                    Array.Copy(fontData, t.offset, data, 0, t.length);
                    tablesToInclude.Add((tag, data));
                }
            }

            // Rebuilt tables
            tablesToInclude.Add(("glyf", newGlyf));
            tablesToInclude.Add(("loca", newLoca));
            tablesToInclude.Add(("post", newPost));

            // Remove duplicates (post was copied above, replace it)
            tablesToInclude = tablesToInclude
                .GroupBy(t => t.tag)
                .Select(g => g.Last()) // rebuilt tables override copied ones
                .OrderBy(t => t.tag)
                .ToList();

            return AssembleFont(tablesToInclude);
        }

        private static uint[] ParseLoca(byte[] data, int locaOffset, int locaFormat, int numGlyphs)
        {
            var offsets = new uint[numGlyphs + 1];
            for (int i = 0; i <= numGlyphs; i++)
            {
                if (locaFormat == 0)
                    offsets[i] = (uint)(ReadUInt16(data, locaOffset + i * 2) * 2);
                else
                    offsets[i] = ReadUInt32(data, locaOffset + i * 4);
            }
            return offsets;
        }

        private static HashSet<int> ComputeGlyphClosure(byte[] data, int glyfOffset,
            uint[] glyphOffsets, HashSet<int> usedGlyphIds, int numGlyphs)
        {
            var retained = new HashSet<int>(usedGlyphIds);
            retained.Add(0); // Always include .notdef

            var queue = new Queue<int>(retained);
            while (queue.Count > 0)
            {
                int gid = queue.Dequeue();
                if (gid < 0 || gid >= numGlyphs) continue;

                uint start = glyphOffsets[gid];
                uint end = glyphOffsets[gid + 1];
                if (end <= start) continue; // Empty glyph

                int offset = glyfOffset + (int)start;
                if (offset + 10 > data.Length) continue;

                short numberOfContours = ReadInt16(data, offset);
                if (numberOfContours >= 0) continue; // Simple glyph, no components

                // Composite glyph — walk component entries
                int pos = offset + 10; // Skip header (numberOfContours + bbox)
                while (pos + 4 <= data.Length)
                {
                    ushort flags = ReadUInt16(data, pos);
                    ushort componentGid = ReadUInt16(data, pos + 2);
                    pos += 4;

                    if (componentGid < numGlyphs && retained.Add(componentGid))
                        queue.Enqueue(componentGid);

                    // Skip arguments based on flags
                    if ((flags & 0x0001) != 0) // ARG_1_AND_2_ARE_WORDS
                        pos += 4;
                    else
                        pos += 2;

                    // Skip transform data
                    if ((flags & 0x0008) != 0)       // WE_HAVE_A_SCALE
                        pos += 2;
                    else if ((flags & 0x0040) != 0)  // WE_HAVE_AN_X_AND_Y_SCALE
                        pos += 4;
                    else if ((flags & 0x0080) != 0)  // WE_HAVE_A_TWO_BY_TWO
                        pos += 8;

                    if ((flags & 0x0020) == 0) // MORE_COMPONENTS
                        break;
                }
            }

            return retained;
        }

        private static byte[] BuildSubsetGlyf(byte[] data, int glyfOffset,
            uint[] glyphOffsets, HashSet<int> retained, int numGlyphs)
        {
            using (var ms = new MemoryStream())
            {
                for (int gid = 0; gid < numGlyphs; gid++)
                {
                    if (retained.Contains(gid))
                    {
                        uint start = glyphOffsets[gid];
                        uint end = glyphOffsets[gid + 1];
                        int len = (int)(end - start);
                        if (len > 0)
                        {
                            ms.Write(data, glyfOffset + (int)start, len);
                            // Pad to 2-byte boundary (loca short format requires even offsets)
                            if (len % 2 != 0)
                                ms.WriteByte(0);
                        }
                    }
                    // Unused glyphs: zero-length entry (no bytes written, loca entries will be equal)
                }

                return ms.ToArray();
            }
        }

        private static byte[] BuildSubsetLoca(byte[] data, int glyfOffset,
            uint[] glyphOffsets, HashSet<int> retained, int numGlyphs, int locaFormat)
        {
            // Compute new offsets by walking through the same order as BuildSubsetGlyf
            var newOffsets = new uint[numGlyphs + 1];
            uint currentOffset = 0;

            for (int gid = 0; gid < numGlyphs; gid++)
            {
                newOffsets[gid] = currentOffset;
                if (retained.Contains(gid))
                {
                    uint start = glyphOffsets[gid];
                    uint end = glyphOffsets[gid + 1];
                    int len = (int)(end - start);
                    if (len > 0)
                    {
                        currentOffset += (uint)len;
                        // Account for padding to 2-byte boundary
                        if (len % 2 != 0)
                            currentOffset++;
                    }
                }
            }
            newOffsets[numGlyphs] = currentOffset;

            // Write loca table
            int entrySize = locaFormat == 0 ? 2 : 4;
            var loca = new byte[(numGlyphs + 1) * entrySize];
            for (int i = 0; i <= numGlyphs; i++)
            {
                if (locaFormat == 0)
                    WriteUInt16(loca, i * 2, (ushort)(newOffsets[i] / 2));
                else
                    WriteUInt32(loca, i * 4, newOffsets[i]);
            }

            return loca;
        }

        private static byte[] BuildMinimalPost(byte[] data,
            IReadOnlyDictionary<string, (uint offset, uint length)> tables)
        {
            // Format 3.0: no glyph names, 32 bytes
            var post = new byte[32];
            if (tables.TryGetValue("post", out var t) && t.length >= 32)
            {
                // Copy the first 32 bytes (preserves italicAngle, isFixedPitch, etc.)
                Array.Copy(data, t.offset, post, 0, 32);
            }
            // Set format to 3.0 (0x00030000)
            WriteUInt32(post, 0, 0x00030000);
            return post;
        }

        private static byte[] AssembleFont(List<(string tag, byte[] data)> tables)
        {
            int numTables = tables.Count;

            // Calculate searchRange, entrySelector, rangeShift
            int searchRange = 1;
            int entrySelector = 0;
            while (searchRange * 2 <= numTables)
            {
                searchRange *= 2;
                entrySelector++;
            }
            searchRange *= 16;
            int rangeShift = numTables * 16 - searchRange;

            // Calculate total size
            int headerSize = 12 + numTables * 16;
            int dataOffset = headerSize;

            // Pad each table to 4-byte boundary
            var paddedSizes = new int[numTables];
            for (int i = 0; i < numTables; i++)
                paddedSizes[i] = (tables[i].data.Length + 3) & ~3;

            int totalSize = headerSize;
            for (int i = 0; i < numTables; i++)
                totalSize += paddedSizes[i];

            var result = new byte[totalSize];

            // Write offset table header
            WriteUInt32(result, 0, 0x00010000); // sfVersion (TrueType)
            WriteUInt16(result, 4, (ushort)numTables);
            WriteUInt16(result, 6, (ushort)searchRange);
            WriteUInt16(result, 8, (ushort)entrySelector);
            WriteUInt16(result, 10, (ushort)rangeShift);

            // Write table directory and data
            int currentDataOffset = headerSize;
            for (int i = 0; i < numTables; i++)
            {
                var tag = tables[i].tag;
                var tableData = tables[i].data;
                int dirPos = 12 + i * 16;

                // Tag (4 bytes)
                for (int j = 0; j < 4; j++)
                    result[dirPos + j] = j < tag.Length ? (byte)tag[j] : (byte)' ';

                // Checksum
                uint checksum = CalculateChecksum(tableData);
                WriteUInt32(result, dirPos + 4, checksum);

                // Offset
                WriteUInt32(result, dirPos + 8, (uint)currentDataOffset);

                // Length (actual, not padded)
                WriteUInt32(result, dirPos + 12, (uint)tableData.Length);

                // Copy table data
                Array.Copy(tableData, 0, result, currentDataOffset, tableData.Length);

                currentDataOffset += paddedSizes[i];
            }

            // Patch checkSumAdjustment in head table
            PatchHeadChecksum(result, tables);

            return result;
        }

        private static void PatchHeadChecksum(byte[] fontData, List<(string tag, byte[] data)> tables)
        {
            // Find head table offset in the assembled binary
            for (int i = 0; i < tables.Count; i++)
            {
                if (tables[i].tag == "head")
                {
                    int dirPos = 12 + i * 16;
                    int headOffset = (int)ReadUInt32(fontData, dirPos + 8);

                    // Zero out checkSumAdjustment before computing whole-file checksum
                    WriteUInt32(fontData, headOffset + 8, 0);

                    uint wholeChecksum = CalculateChecksum(fontData);
                    WriteUInt32(fontData, headOffset + 8, 0xB1B0AFBA - wholeChecksum);
                    break;
                }
            }
        }

        private static uint CalculateChecksum(byte[] data)
        {
            uint sum = 0;
            int length = (data.Length + 3) & ~3; // Round up to 4-byte boundary
            for (int i = 0; i < length; i += 4)
            {
                uint val = 0;
                for (int j = 0; j < 4; j++)
                {
                    val <<= 8;
                    if (i + j < data.Length)
                        val |= data[i + j];
                }
                sum += val;
            }
            return sum;
        }

        // ── Binary helpers (big-endian) ──

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            if (offset + 1 >= data.Length) return 0;
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static short ReadInt16(byte[] data, int offset)
        {
            return (short)ReadUInt16(data, offset);
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            if (offset + 3 >= data.Length) return 0;
            return (uint)(
                (data[offset] << 24) |
                (data[offset + 1] << 16) |
                (data[offset + 2] << 8) |
                data[offset + 3]);
        }

        private static void WriteUInt16(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)value;
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }
    }
}
