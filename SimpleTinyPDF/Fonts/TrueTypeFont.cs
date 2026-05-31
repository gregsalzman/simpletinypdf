using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Pure C# parser for TrueType (.ttf) and OpenType (.otf) font files.
    /// Extracts the minimum data needed for PDF embedding and text measurement.
    /// </summary>
    internal sealed class TrueTypeFont
    {
        internal byte[] RawData { get; }
        internal string PostScriptName { get; private set; }
        internal string FamilyName { get; private set; }
        internal int UnitsPerEm { get; private set; }
        internal int Ascender { get; private set; }
        internal int Descender { get; private set; }
        internal int CapHeight { get; private set; }
        internal float ItalicAngle { get; private set; }
        internal int StemV { get; private set; }
        internal int[] FontBBox { get; private set; }
        internal int Flags { get; private set; }
        internal bool IsCff { get; private set; }
        internal bool IsFixedPitch { get; private set; }

        internal string SubsetTag { get; set; }
        internal string SubsetPostScriptName =>
            SubsetTag != null ? SubsetTag + "+" + PostScriptName : PostScriptName;

        private ushort[] _advanceWidths;
        private int _numOfLongHorMetrics;
        private Dictionary<int, int> _cmapTable; // Unicode codepoint -> glyph ID
        private readonly HashSet<int> _usedCharacters = new HashSet<int>();
        private Dictionary<string, (uint offset, uint length)> _tables;

        internal TrueTypeFont(byte[] data)
        {
            RawData = data ?? throw new ArgumentNullException(nameof(data));
            Parse();
        }

        /// <summary>
        /// Returns the glyph ID for a Unicode code point, or 0 (the .notdef glyph) if not found.
        /// </summary>
        internal int GetGlyphId(int codePoint)
        {
            return _cmapTable.TryGetValue(codePoint, out int gid) ? gid : 0;
        }

        /// <summary>
        /// Returns the glyph ID for a Unicode character, or 0 (the .notdef glyph) if not found.
        /// </summary>
        internal int GetGlyphId(char c) => GetGlyphId((int)c);

        /// <summary>
        /// Returns the advance width for a glyph ID, in font design units.
        /// </summary>
        internal int GetAdvanceWidth(int glyphId)
        {
            if (glyphId < _numOfLongHorMetrics)
                return _advanceWidths[glyphId];
            // Glyphs beyond numOfLongHorMetrics use the last entry's advance width
            return _numOfLongHorMetrics > 0
                ? _advanceWidths[_numOfLongHorMetrics - 1]
                : 0;
        }

        /// <summary>
        /// Returns the width for a Unicode code point in 1/1000 em units.
        /// </summary>
        internal int GetCharWidth(int codePoint)
        {
            int gid = GetGlyphId(codePoint);
            int rawWidth = GetAdvanceWidth(gid);
            return (int)(rawWidth * 1000L / UnitsPerEm);
        }

        /// <summary>
        /// Returns the character width in 1/1000 em units (matching FontMetrics convention).
        /// </summary>
        internal int GetCharWidth(char c) => GetCharWidth((int)c);

        /// <summary>
        /// Records characters used with this font for ToUnicode CMap and /W array generation.
        /// Handles UTF-16 surrogate pairs for supplementary plane characters.
        /// </summary>
        internal void RecordUsedCharacters(string text)
        {
            if (text == null) return;
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
                _usedCharacters.Add(cp);
            }
        }

        /// <summary>
        /// Returns a mapping of glyph ID → Unicode code point for all used characters.
        /// Skips glyph ID 0 (.notdef). Used to build the ToUnicode CMap.
        /// </summary>
        internal Dictionary<int, int> GetUsedGlyphToUnicodeMap()
        {
            var map = new Dictionary<int, int>();
            foreach (int cp in _usedCharacters)
            {
                int gid = GetGlyphId(cp);
                if (gid != 0 && !map.ContainsKey(gid))
                    map[gid] = cp;
            }
            return map;
        }

        /// <summary>
        /// Returns the set of glyph IDs used across the document. Skips glyph 0.
        /// Used to build the /W width array.
        /// </summary>
        internal HashSet<int> GetUsedGlyphIds()
        {
            var set = new HashSet<int>();
            foreach (int cp in _usedCharacters)
            {
                int gid = GetGlyphId(cp);
                if (gid != 0)
                    set.Add(gid);
            }
            return set;
        }

        /// <summary>
        /// Returns a subset of the font binary containing only the used glyphs.
        /// For CFF fonts or when subsetting is disabled, returns the full RawData.
        /// </summary>
        internal byte[] GetSubsetData(bool subset)
        {
            if (!subset || IsCff)
                return RawData;

            var usedGlyphIds = GetUsedGlyphIds();
            if (usedGlyphIds.Count == 0)
                return RawData;

            if (SubsetTag == null)
                SubsetTag = TrueTypeSubsetter.GenerateSubsetTag();

            return TrueTypeSubsetter.Subset(RawData, usedGlyphIds, _tables);
        }

        private void Parse()
        {
            if (RawData.Length < 12)
                throw new ArgumentException("Font data is too small to be a valid font file.");

            uint sfVersion = ReadUInt32(0);
            // 0x00010000 = TrueType, 0x4F54544F ('OTTO') = OpenType-CFF
            IsCff = sfVersion == 0x4F54544F;

            int numTables = ReadUInt16(4);
            var tables = new Dictionary<string, (uint offset, uint length)>();

            for (int i = 0; i < numTables; i++)
            {
                int pos = 12 + i * 16;
                string tag = ReadTag(pos);
                uint offset = ReadUInt32(pos + 8);
                uint length = ReadUInt32(pos + 12);
                tables[tag] = (offset, length);
            }

            _tables = tables;

            ParseHead(tables);
            ParseHhea(tables);
            ParseMaxp(tables);
            ParseHmtx(tables);
            ParseCmap(tables);
            ParseName(tables);
            ParseOs2(tables);
            ParsePost(tables);
            ComputeFlags();
        }

        private void ParseHead(Dictionary<string, (uint offset, uint length)> tables)
        {
            if (!tables.TryGetValue("head", out var t))
                throw new ArgumentException("Font is missing required 'head' table.");

            int o = (int)t.offset;
            UnitsPerEm = ReadUInt16(o + 18);
            if (UnitsPerEm == 0) UnitsPerEm = 1000;

            int xMin = ReadInt16(o + 36);
            int yMin = ReadInt16(o + 38);
            int xMax = ReadInt16(o + 40);
            int yMax = ReadInt16(o + 42);
            FontBBox = new[]
            {
                xMin * 1000 / UnitsPerEm,
                yMin * 1000 / UnitsPerEm,
                xMax * 1000 / UnitsPerEm,
                yMax * 1000 / UnitsPerEm
            };
        }

        private void ParseHhea(Dictionary<string, (uint offset, uint length)> tables)
        {
            if (!tables.TryGetValue("hhea", out var t))
                throw new ArgumentException("Font is missing required 'hhea' table.");

            int o = (int)t.offset;
            Ascender = ReadInt16(o + 4);
            Descender = ReadInt16(o + 6);
            _numOfLongHorMetrics = ReadUInt16(o + 34);
        }

        private void ParseMaxp(Dictionary<string, (uint offset, uint length)> tables)
        {
            if (!tables.TryGetValue("maxp", out var t))
                throw new ArgumentException("Font is missing required 'maxp' table.");

            int numGlyphs = ReadUInt16((int)t.offset + 4);
            _advanceWidths = new ushort[numGlyphs];
        }

        private void ParseHmtx(Dictionary<string, (uint offset, uint length)> tables)
        {
            if (!tables.TryGetValue("hmtx", out var t))
                throw new ArgumentException("Font is missing required 'hmtx' table.");

            int o = (int)t.offset;
            for (int i = 0; i < _numOfLongHorMetrics && i < _advanceWidths.Length; i++)
            {
                _advanceWidths[i] = ReadUInt16(o);
                o += 4; // advanceWidth (2) + lsb (2)
            }

            // Fill remaining glyphs with the last advance width
            if (_numOfLongHorMetrics > 0)
            {
                ushort lastWidth = _advanceWidths[_numOfLongHorMetrics - 1];
                for (int i = _numOfLongHorMetrics; i < _advanceWidths.Length; i++)
                    _advanceWidths[i] = lastWidth;
            }
        }

        private void ParseCmap(Dictionary<string, (uint offset, uint length)> tables)
        {
            _cmapTable = new Dictionary<int, int>();

            if (!tables.TryGetValue("cmap", out var t))
                throw new ArgumentException("Font is missing required 'cmap' table.");

            int o = (int)t.offset;
            int numSubtables = ReadUInt16(o + 2);

            // Find a suitable subtable: prefer full-Unicode (format 12) over BMP-only (format 4)
            // Priority: (3,10) Windows full > (0,4+) Unicode full > (3,1) Windows BMP > (0,*) Unicode BMP
            int subtableOffset = -1;
            int bestPriority = int.MaxValue;
            for (int i = 0; i < numSubtables; i++)
            {
                int entryPos = o + 4 + i * 8;
                int platformId = ReadUInt16(entryPos);
                int encodingId = ReadUInt16(entryPos + 2);
                int offset = (int)ReadUInt32(entryPos + 4);

                int priority;
                if (platformId == 3 && encodingId == 10)       // Windows, full Unicode
                    priority = 0;
                else if (platformId == 0 && encodingId >= 4)   // Unicode, full repertoire
                    priority = 1;
                else if (platformId == 3 && encodingId == 1)   // Windows, Unicode BMP
                    priority = 2;
                else if (platformId == 0 && encodingId == 3)   // Unicode, Unicode BMP
                    priority = 3;
                else if (platformId == 0)                      // Any Unicode platform
                    priority = 4;
                else
                    continue;

                if (priority < bestPriority)
                {
                    bestPriority = priority;
                    subtableOffset = o + offset;
                    if (priority == 0) break; // Best possible, stop searching
                }
            }

            if (subtableOffset < 0)
                return; // No usable cmap subtable found

            int format = ReadUInt16(subtableOffset);
            if (format == 4)
                ParseCmapFormat4(subtableOffset);
            else if (format == 12)
                ParseCmapFormat12(subtableOffset);
            // Other formats not supported in Phase 1
        }

        private void ParseCmapFormat4(int offset)
        {
            int segCount = ReadUInt16(offset + 6) / 2;
            int endCodeBase = offset + 14;
            int startCodeBase = endCodeBase + segCount * 2 + 2; // +2 for reservedPad
            int idDeltaBase = startCodeBase + segCount * 2;
            int idRangeOffsetBase = idDeltaBase + segCount * 2;

            for (int seg = 0; seg < segCount; seg++)
            {
                int endCode = ReadUInt16(endCodeBase + seg * 2);
                int startCode = ReadUInt16(startCodeBase + seg * 2);
                int idDelta = ReadInt16(idDeltaBase + seg * 2);
                int idRangeOffset = ReadUInt16(idRangeOffsetBase + seg * 2);

                if (startCode == 0xFFFF) break;

                for (int c = startCode; c <= endCode; c++)
                {
                    int glyphId;
                    if (idRangeOffset == 0)
                    {
                        glyphId = (c + idDelta) & 0xFFFF;
                    }
                    else
                    {
                        int rangePos = idRangeOffsetBase + seg * 2 + idRangeOffset + (c - startCode) * 2;
                        glyphId = ReadUInt16(rangePos);
                        if (glyphId != 0)
                            glyphId = (glyphId + idDelta) & 0xFFFF;
                    }

                    if (glyphId != 0 && !_cmapTable.ContainsKey(c))
                        _cmapTable[c] = glyphId;
                }
            }
        }

        private void ParseCmapFormat12(int offset)
        {
            int numGroups = (int)ReadUInt32(offset + 12);
            int groupBase = offset + 16;

            for (int i = 0; i < numGroups; i++)
            {
                int pos = groupBase + i * 12;
                uint startCharCode = ReadUInt32(pos);
                uint endCharCode = ReadUInt32(pos + 4);
                uint startGlyphId = ReadUInt32(pos + 8);

                for (uint c = startCharCode; c <= endCharCode && c <= 0x10FFFF; c++)
                {
                    int glyphId = (int)(startGlyphId + (c - startCharCode));
                    if (glyphId != 0 && !_cmapTable.ContainsKey((int)c))
                        _cmapTable[(int)c] = glyphId;
                }
            }
        }

        private void ParseName(Dictionary<string, (uint offset, uint length)> tables)
        {
            if (!tables.TryGetValue("name", out var t))
            {
                PostScriptName = "UnknownFont";
                FamilyName = "UnknownFont";
                return;
            }

            int o = (int)t.offset;
            int count = ReadUInt16(o + 2);
            int stringOffset = ReadUInt16(o + 4);
            int storageBase = o + stringOffset;

            string psName = null;
            string familyName = null;

            for (int i = 0; i < count; i++)
            {
                int recPos = o + 6 + i * 12;
                int platformId = ReadUInt16(recPos);
                int encodingId = ReadUInt16(recPos + 2);
                // int languageId = ReadUInt16(recPos + 4);
                int nameId = ReadUInt16(recPos + 6);
                int length = ReadUInt16(recPos + 8);
                int strOffset = ReadUInt16(recPos + 10);

                // Prefer Windows/Unicode (3,1) names
                if (platformId != 3 || encodingId != 1) continue;

                int strStart = storageBase + strOffset;
                if (strStart + length > RawData.Length) continue;

                string value = ReadUtf16Be(strStart, length);

                if (nameId == 6 && psName == null) // PostScript name
                    psName = value;
                else if (nameId == 1 && familyName == null) // Family name
                    familyName = value;

                if (psName != null && familyName != null) break;
            }

            // Fallback: try platform 1 (Macintosh) if Windows names not found
            if (psName == null || familyName == null)
            {
                for (int i = 0; i < count; i++)
                {
                    int recPos = o + 6 + i * 12;
                    int platformId = ReadUInt16(recPos);
                    int nameId = ReadUInt16(recPos + 6);
                    int length = ReadUInt16(recPos + 8);
                    int strOffset = ReadUInt16(recPos + 10);

                    if (platformId != 1) continue; // Macintosh

                    int strStart = storageBase + strOffset;
                    if (strStart + length > RawData.Length) continue;

                    string value = Encoding.ASCII.GetString(RawData, strStart, length);

                    if (nameId == 6 && psName == null)
                        psName = value;
                    else if (nameId == 1 && familyName == null)
                        familyName = value;
                }
            }

            PostScriptName = SanitizePsName(psName ?? familyName ?? "UnknownFont");
            FamilyName = familyName ?? psName ?? "UnknownFont";
        }

        private void ParseOs2(Dictionary<string, (uint offset, uint length)> tables)
        {
            if (!tables.TryGetValue("OS/2", out var t))
            {
                // Fallback: use hhea values
                StemV = 80;
                CapHeight = Ascender;
                return;
            }

            int o = (int)t.offset;
            int weightClass = ReadUInt16(o + 4);

            // StemV estimation from weight class
            StemV = 10 + 220 * (weightClass - 50) / 900;
            if (StemV < 10) StemV = 10;

            // Prefer typo metrics if available (version >= 2 has sCapHeight at offset 88)
            int version = ReadUInt16(o);
            int typoAscender = ReadInt16(o + 68);
            int typoDescender = ReadInt16(o + 70);

            if (typoAscender != 0)
                Ascender = typoAscender;
            if (typoDescender != 0)
                Descender = typoDescender;

            if (version >= 2 && t.length >= 90)
                CapHeight = ReadInt16(o + 88);
            else
                CapHeight = Ascender;
        }

        private void ParsePost(Dictionary<string, (uint offset, uint length)> tables)
        {
            if (!tables.TryGetValue("post", out var t))
            {
                ItalicAngle = 0;
                IsFixedPitch = false;
                return;
            }

            int o = (int)t.offset;
            // italicAngle is a 16.16 fixed-point number at offset 4
            int whole = ReadInt16(o + 4);
            int frac = ReadUInt16(o + 6);
            ItalicAngle = whole + frac / 65536f;

            IsFixedPitch = ReadUInt32(o + 12) != 0;
        }

        private void ComputeFlags()
        {
            int flags = 0;
            if (IsFixedPitch) flags |= 1;        // FixedPitch
            flags |= (1 << 5);                    // Nonsymbolic
            if (Math.Abs(ItalicAngle) > 0.01f) flags |= (1 << 6); // Italic
            Flags = flags;
        }

        // ── Binary reading helpers (big-endian) ──

        private ushort ReadUInt16(int offset)
        {
            if (offset + 1 >= RawData.Length) return 0;
            return (ushort)((RawData[offset] << 8) | RawData[offset + 1]);
        }

        private short ReadInt16(int offset)
        {
            return (short)ReadUInt16(offset);
        }

        private uint ReadUInt32(int offset)
        {
            if (offset + 3 >= RawData.Length) return 0;
            return (uint)(
                (RawData[offset] << 24) |
                (RawData[offset + 1] << 16) |
                (RawData[offset + 2] << 8) |
                RawData[offset + 3]);
        }

        private string ReadTag(int offset)
        {
            if (offset + 3 >= RawData.Length) return "";
            return new string(new[]
            {
                (char)RawData[offset],
                (char)RawData[offset + 1],
                (char)RawData[offset + 2],
                (char)RawData[offset + 3]
            });
        }

        private string ReadUtf16Be(int offset, int byteLength)
        {
            var chars = new char[byteLength / 2];
            for (int i = 0; i < chars.Length; i++)
            {
                int pos = offset + i * 2;
                chars[i] = (char)((RawData[pos] << 8) | RawData[pos + 1]);
            }
            return new string(chars);
        }

        private static string SanitizePsName(string name)
        {
            // PostScript names must not contain spaces or special chars
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (c > 32 && c < 127 && c != '(' && c != ')' && c != '<' && c != '>'
                    && c != '[' && c != ']' && c != '{' && c != '}' && c != '/' && c != '%')
                    sb.Append(c);
            }
            return sb.Length > 0 ? sb.ToString() : "UnknownFont";
        }
    }
}
