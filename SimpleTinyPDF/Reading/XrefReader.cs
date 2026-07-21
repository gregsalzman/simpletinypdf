using System.Collections.Generic;
using System.Globalization;

namespace SimpleTinyPDF
{
    /// <summary>One cross-reference entry.</summary>
    internal struct XrefEntry
    {
        /// <summary>0 = free, 1 = regular (byte offset), 2 = stored in an object stream.</summary>
        internal byte Type;
        /// <summary>Type 1: byte offset of the object. Type 2: object number of the containing object stream.</summary>
        internal long Value;
        /// <summary>Type 1: generation number. Type 2: index within the object stream.</summary>
        internal int Extra;
    }

    /// <summary>
    /// The merged cross-reference information of a PDF file: all update sections
    /// combined with newest-wins semantics, plus the merged trailer dictionary.
    /// </summary>
    internal sealed class XrefTable
    {
        internal readonly Dictionary<int, XrefEntry> Entries = new Dictionary<int, XrefEntry>();
        internal readonly CosDict Trailer = new CosDict();
        internal bool WasRepaired;

        /// <summary>Adds an entry unless the object already has one (sections are read newest-first).</summary>
        internal void AddEntry(int objectNumber, XrefEntry entry)
        {
            if (!Entries.ContainsKey(objectNumber))
                Entries[objectNumber] = entry;
        }

        /// <summary>Merges trailer keys, keeping the first-seen (newest) value per key.</summary>
        internal void MergeTrailer(CosDict dict)
        {
            foreach (var kv in dict.Entries)
            {
                if (!Trailer.ContainsKey(kv.Key))
                    Trailer.Set(kv.Key, kv.Value);
            }
        }
    }

    /// <summary>
    /// Reads the cross-reference information of a PDF file: classic tables, cross-reference
    /// streams, incremental-update /Prev chains, and hybrid-reference files. Falls back to a
    /// full-file scan when the cross-reference data is missing or broken.
    /// </summary>
    internal static class XrefReader
    {
        /// <summary>Reads the cross-reference table. Throws <see cref="PdfParseException"/> when it cannot be parsed.</summary>
        internal static XrefTable Read(PdfParser parser)
        {
            long startXref = FindStartXref(parser.Data);
            if (startXref < 0 || startXref >= parser.Data.Length)
                throw new PdfParseException("No usable startxref offset found.");

            var table = new XrefTable();
            var visited = new HashSet<long>();
            long offset = startXref;
            while (offset >= 0 && offset < parser.Data.Length && visited.Add(offset))
            {
                var sectionTrailer = ReadSection(parser, (int)offset, table);
                offset = sectionTrailer.GetInteger("Prev") ?? -1;
            }
            if (table.Trailer.Get("Root") == null)
                throw new PdfParseException("Cross-reference trailer has no /Root entry.");
            return table;
        }

        /// <summary>
        /// Reads one cross-reference section (classic table or xref stream) and merges it
        /// into <paramref name="table"/>. Returns the section's own trailer dictionary.
        /// </summary>
        private static CosDict ReadSection(PdfParser parser, int offset, XrefTable table)
        {
            var lexer = new PdfLexer(parser.Data, offset);
            lexer.SkipWhitespace();
            if (lexer.TryReadKeyword("xref"))
            {
                var trailer = ReadClassicSection(parser, lexer, table);
                // Hybrid-reference file: the classic section points at an extra xref stream
                // holding entries for objects hidden in object streams. Classic entries win.
                long? xrefStm = trailer.GetInteger("XRefStm");
                if (xrefStm.HasValue && xrefStm.Value >= 0 && xrefStm.Value < parser.Data.Length)
                    ReadStreamSection(parser, (int)xrefStm.Value, table);
                return trailer;
            }
            return ReadStreamSection(parser, offset, table);
        }

        private static CosDict ReadClassicSection(PdfParser parser, PdfLexer lexer, XrefTable table)
        {
            while (true)
            {
                lexer.SkipWhitespace();
                if (lexer.TryReadKeyword("trailer"))
                {
                    parser.Position = lexer.Pos;
                    var trailer = parser.ParseValue() as CosDict ?? new CosDict();
                    table.MergeTrailer(trailer);
                    return trailer;
                }

                // Subsection header: "start count"
                string startToken = lexer.ReadRegularToken();
                lexer.SkipWhitespace();
                string countToken = lexer.ReadRegularToken();
                if (!int.TryParse(startToken, NumberStyles.None, CultureInfo.InvariantCulture, out int start) ||
                    !int.TryParse(countToken, NumberStyles.None, CultureInfo.InvariantCulture, out int count))
                    throw new PdfParseException("Malformed cross-reference subsection header.");

                for (int i = 0; i < count; i++)
                {
                    lexer.SkipWhitespace();
                    string offsetToken = lexer.ReadRegularToken();
                    lexer.SkipWhitespace();
                    string genToken = lexer.ReadRegularToken();
                    lexer.SkipWhitespace();
                    string flagToken = lexer.ReadRegularToken();
                    if (!long.TryParse(offsetToken, NumberStyles.None, CultureInfo.InvariantCulture, out long entryOffset) ||
                        !int.TryParse(genToken, NumberStyles.None, CultureInfo.InvariantCulture, out int generation))
                        throw new PdfParseException("Malformed cross-reference entry.");

                    if (flagToken == "n")
                        table.AddEntry(start + i, new XrefEntry { Type = 1, Value = entryOffset, Extra = generation });
                    else
                        table.AddEntry(start + i, new XrefEntry { Type = 0 });
                }
            }
        }

        private static CosDict ReadStreamSection(PdfParser parser, int offset, XrefTable table)
        {
            var body = parser.ParseIndirectObject(offset, out _);
            var stream = body as CosStream;
            if (stream == null || stream.GetName("Type") != "XRef")
                throw new PdfParseException($"Expected a cross-reference stream at byte offset {offset}.");

            // All entries in an xref stream dictionary are direct values per spec
            byte[] data = FlateFilter.DecodeReadableStream(stream, v => v);

            var w = stream.Get("W") as CosArray;
            if (w == null || w.Items.Count < 2)
                throw new PdfParseException("Cross-reference stream has a missing or malformed /W array.");
            int w0 = FieldWidth(w, 0), w1 = FieldWidth(w, 1), w2 = FieldWidth(w, 2);
            int entryWidth = w0 + w1 + w2;
            if (entryWidth <= 0)
                throw new PdfParseException("Cross-reference stream /W widths are all zero.");

            var subsections = new List<(int start, int count)>();
            if (stream.Get("Index") is CosArray index)
            {
                for (int i = 0; i + 1 < index.Items.Count; i += 2)
                {
                    if (index.Items[i] is CosInteger s && index.Items[i + 1] is CosInteger c)
                        subsections.Add(((int)s.Value, (int)c.Value));
                }
            }
            else
            {
                subsections.Add((0, (int)(stream.GetInteger("Size") ?? 0)));
            }

            int pos = 0;
            foreach (var (start, count) in subsections)
            {
                for (int i = 0; i < count && pos + entryWidth <= data.Length; i++, pos += entryWidth)
                {
                    long type = w0 == 0 ? 1 : ReadBigEndian(data, pos, w0);
                    long value = ReadBigEndian(data, pos + w0, w1);
                    long extra = ReadBigEndian(data, pos + w0 + w1, w2);
                    table.AddEntry(start + i, new XrefEntry { Type = (byte)type, Value = value, Extra = (int)extra });
                }
            }

            table.MergeTrailer(stream);
            return stream;
        }

        private static int FieldWidth(CosArray w, int index) =>
            index < w.Items.Count && w.Items[index] is CosInteger i ? (int)i.Value : 0;

        private static long ReadBigEndian(byte[] data, int offset, int width)
        {
            long value = 0;
            for (int i = 0; i < width; i++)
                value = (value << 8) | data[offset + i];
            return value;
        }

        /// <summary>Finds the offset given by the last startxref keyword, scanning the file tail.</summary>
        private static long FindStartXref(byte[] data)
        {
            int windowStart = data.Length > 2048 ? data.Length - 2048 : 0;
            int found = -1;
            int pos = windowStart;
            while (true)
            {
                int next = PdfParser.IndexOf(data, "startxref", pos);
                if (next < 0)
                    break;
                found = next;
                pos = next + 1;
            }
            if (found < 0)
            {
                // Not in the tail window; scan the whole file as a last resort
                found = PdfParser.IndexOf(data, "startxref", 0);
                if (found < 0)
                    return -1;
            }
            var lexer = new PdfLexer(data, found + "startxref".Length);
            lexer.SkipWhitespace();
            string token = lexer.ReadRegularToken();
            return long.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out long offset) ? offset : -1;
        }

        /// <summary>
        /// Rebuilds the cross-reference table by scanning the whole file for "N G obj"
        /// headers (last definition wins), locating the trailer or, failing that, the
        /// document catalog. Object streams found during the scan are expanded so their
        /// contained objects are reachable too.
        /// </summary>
        internal static XrefTable Repair(PdfParser parser)
        {
            byte[] data = parser.Data;
            var table = new XrefTable { WasRepaired = true };
            var found = new Dictionary<int, (long offset, int generation)>();

            int pos = 0;
            while (true)
            {
                int objPos = PdfParser.IndexOf(data, "obj", pos);
                if (objPos < 0)
                    break;
                pos = objPos + 3;
                if (objPos + 3 < data.Length && PdfLexer.IsRegular(data[objPos + 3]))
                    continue; // part of a longer token like "endobj"

                // Backtrack: whitespace, generation digits, whitespace, object-number digits
                int p = objPos - 1;
                while (p >= 0 && PdfLexer.IsWhitespace(data[p])) p--;
                int genEnd = p;
                while (p >= 0 && data[p] >= (byte)'0' && data[p] <= (byte)'9') p--;
                int genStart = p + 1;
                if (genEnd < genStart) continue;
                while (p >= 0 && PdfLexer.IsWhitespace(data[p])) p--;
                int numEnd = p;
                while (p >= 0 && data[p] >= (byte)'0' && data[p] <= (byte)'9') p--;
                int numStart = p + 1;
                if (numEnd < numStart) continue;
                if (p >= 0 && PdfLexer.IsRegular(data[p])) continue; // e.g. "x12 0 obj"

                string numToken = System.Text.Encoding.ASCII.GetString(data, numStart, numEnd - numStart + 1);
                string genToken = System.Text.Encoding.ASCII.GetString(data, genStart, genEnd - genStart + 1);
                if (int.TryParse(numToken, NumberStyles.None, CultureInfo.InvariantCulture, out int number) &&
                    int.TryParse(genToken, NumberStyles.None, CultureInfo.InvariantCulture, out int generation))
                {
                    found[number] = (numStart, generation); // later definitions overwrite earlier ones
                }
            }

            if (found.Count == 0)
                throw new PdfParseException("The file contains no recognizable PDF objects.");

            foreach (var kv in found)
                table.Entries[kv.Key] = new XrefEntry { Type = 1, Value = kv.Value.offset, Extra = kv.Value.generation };

            // Expand object streams so their contained objects get entries too
            int catalogNumber = -1;
            foreach (var kv in found)
            {
                CosValue body;
                try
                {
                    body = parser.ParseIndirectObject((int)kv.Value.offset, out _);
                }
                catch (PdfParseException)
                {
                    continue;
                }
                if (body is CosStream stream && stream.GetName("Type") == "ObjStm")
                {
                    Dictionary<int, CosValue> contained;
                    try
                    {
                        contained = ObjectStreamReader.Expand(stream, v => v);
                    }
                    catch (PdfParseException)
                    {
                        continue;
                    }
                    int index = 0;
                    foreach (var inner in contained)
                    {
                        table.AddEntry(inner.Key, new XrefEntry { Type = 2, Value = kv.Key, Extra = index++ });
                    }
                }
                else if (body is CosDict dict && !(body is CosStream) && dict.GetName("Type") == "Catalog")
                {
                    catalogNumber = kv.Key;
                }
            }

            // Trailer: prefer the last trailer keyword in the file, fall back to the catalog scan
            int trailerPos = -1, searchFrom = 0;
            while (true)
            {
                int next = PdfParser.IndexOf(data, "trailer", searchFrom);
                if (next < 0)
                    break;
                trailerPos = next;
                searchFrom = next + 1;
            }
            if (trailerPos >= 0)
            {
                parser.Position = trailerPos + "trailer".Length;
                if (parser.ParseValue() is CosDict trailerDict)
                    table.MergeTrailer(trailerDict);
            }
            if (table.Trailer.Get("Root") == null && catalogNumber >= 0)
                table.Trailer.Set("Root", new CosReference(catalogNumber, 0));
            if (table.Trailer.Get("Root") == null)
                throw new PdfParseException("Could not locate the document catalog while repairing the file.");
            return table;
        }
    }
}
