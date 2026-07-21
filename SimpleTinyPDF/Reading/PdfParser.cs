using System;
using System.Globalization;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Parses PDF (COS) values and indirect objects from raw file bytes.
    /// Tolerant of common real-world deviations: wrong /Length values, missing
    /// endobj keywords, comments in unusual places.
    /// </summary>
    internal sealed class PdfParser
    {
        private readonly PdfLexer _lexer;

        /// <summary>
        /// Resolves an indirect /Length reference to its integer value. Wired up by
        /// <see cref="PdfReadDocument"/> once the cross-reference table is available.
        /// </summary>
        internal Func<PdfObjectId, long?> LengthResolver;

        internal PdfParser(byte[] data)
        {
            _lexer = new PdfLexer(data);
        }

        internal byte[] Data => _lexer.Data;

        internal int Position
        {
            get => _lexer.Pos;
            set => _lexer.Pos = value;
        }

        /// <summary>Parses a single value at the given byte offset.</summary>
        internal CosValue ParseValueAt(int offset)
        {
            _lexer.Pos = offset;
            return ParseValue();
        }

        /// <summary>Parses a single value at the current position.</summary>
        internal CosValue ParseValue()
        {
            _lexer.SkipWhitespace();
            int b = _lexer.Peek();
            switch (b)
            {
                case -1:
                    return CosNull.Instance;
                case '/':
                    return new CosName(_lexer.ReadName());
                case '(':
                    return new CosString(_lexer.ReadLiteralString());
                case '<':
                    if (_lexer.PeekAt(1) == '<')
                        return ParseDict();
                    return new CosString(_lexer.ReadHexString());
                case '[':
                    return ParseArray();
                default:
                    if (b == '+' || b == '-' || b == '.' || (b >= '0' && b <= '9'))
                        return ParseNumberOrReference();
                    return ParseKeywordValue();
            }
        }

        private CosValue ParseKeywordValue()
        {
            string token = _lexer.ReadRegularToken();
            switch (token)
            {
                case "true":
                    return CosBool.True;
                case "false":
                    return CosBool.False;
                case "null":
                    return CosNull.Instance;
                case "":
                    // Unexpected delimiter; consume one byte so callers cannot loop forever
                    _lexer.Pos++;
                    return CosNull.Instance;
                default:
                    return CosNull.Instance;
            }
        }

        private CosValue ParseNumberOrReference()
        {
            string token = _lexer.ReadRegularToken();
            if (long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long intValue))
            {
                // Could be "N G R" — look ahead for a second integer followed by the R keyword
                int snapshot = _lexer.Pos;
                _lexer.SkipWhitespace();
                int c = _lexer.Peek();
                if (c >= '0' && c <= '9')
                {
                    string token2 = _lexer.ReadRegularToken();
                    if (int.TryParse(token2, NumberStyles.None, CultureInfo.InvariantCulture, out int generation))
                    {
                        _lexer.SkipWhitespace();
                        if (_lexer.TryReadKeyword("R") && intValue >= 0 && intValue <= int.MaxValue)
                            return new CosReference((int)intValue, generation);
                    }
                }
                _lexer.Pos = snapshot;
                return new CosInteger(intValue);
            }
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double realValue))
                return new CosReal(realValue);
            // Forms like "+.5" or "6." that TryParse may reject on some runtimes
            if (double.TryParse(token.TrimEnd('.'), NumberStyles.Float, CultureInfo.InvariantCulture, out realValue))
                return new CosReal(realValue);
            return new CosInteger(0);
        }

        private CosArray ParseArray()
        {
            _lexer.Pos++; // skip '['
            var array = new CosArray();
            while (true)
            {
                _lexer.SkipWhitespace();
                int b = _lexer.Peek();
                if (b == ']')
                {
                    _lexer.Pos++;
                    break;
                }
                if (b == -1)
                    break;
                array.Items.Add(ParseValue());
            }
            return array;
        }

        private CosDict ParseDict()
        {
            _lexer.Pos += 2; // skip '<<'
            var dict = new CosDict();
            while (true)
            {
                _lexer.SkipWhitespace();
                int b = _lexer.Peek();
                if (b == '>' && _lexer.PeekAt(1) == '>')
                {
                    _lexer.Pos += 2;
                    break;
                }
                if (b == -1)
                    break;
                if (b == '/')
                {
                    string key = _lexer.ReadName();
                    dict.Set(key, ParseValue());
                }
                else
                {
                    // Malformed entry; skip a byte so we cannot loop forever
                    _lexer.Pos++;
                }
            }
            return dict;
        }

        /// <summary>
        /// Parses an indirect object ("N G obj ... endobj") at the given byte offset.
        /// Returns the object body; stream objects come back as <see cref="CosStream"/>.
        /// Throws <see cref="PdfParseException"/> when the offset does not hold an object header.
        /// </summary>
        internal CosValue ParseIndirectObject(int offset, out PdfObjectId id)
        {
            _lexer.Pos = offset;
            _lexer.SkipWhitespace();
            string numToken = _lexer.ReadRegularToken();
            _lexer.SkipWhitespace();
            string genToken = _lexer.ReadRegularToken();
            _lexer.SkipWhitespace();
            if (!int.TryParse(numToken, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ||
                !int.TryParse(genToken, NumberStyles.None, CultureInfo.InvariantCulture, out int generation) ||
                !_lexer.TryReadKeyword("obj"))
            {
                id = default;
                throw new PdfParseException($"Expected an indirect object header at byte offset {offset}.");
            }
            id = new PdfObjectId(number, generation);

            var body = ParseValue();

            _lexer.SkipWhitespace();
            if (body is CosDict dict && _lexer.TryReadKeyword("stream"))
                return ParseStreamBody(dict);

            _lexer.SkipWhitespace();
            _lexer.TryReadKeyword("endobj"); // tolerant if missing
            return body;
        }

        private CosStream ParseStreamBody(CosDict dict)
        {
            // The stream keyword must be followed by CRLF or LF (lone CR tolerated)
            if (_lexer.Peek() == 0x0D)
                _lexer.Pos++;
            if (_lexer.Peek() == 0x0A)
                _lexer.Pos++;
            int dataStart = _lexer.Pos;

            long? length = null;
            var lengthValue = dict.Get("Length");
            if (lengthValue is CosInteger direct)
                length = direct.Value;
            else if (lengthValue is CosReference lengthRef && LengthResolver != null)
                length = LengthResolver(lengthRef.Id);

            int dataEnd = -1;
            if (length.HasValue && length.Value >= 0 && dataStart + length.Value <= Data.Length)
            {
                // Verify that endstream actually follows the declared length
                int probe = dataStart + (int)length.Value;
                var check = new PdfLexer(Data, probe);
                check.SkipWhitespace();
                if (check.TryReadKeyword("endstream"))
                {
                    dataEnd = probe;
                    _lexer.Pos = check.Pos;
                }
            }

            if (dataEnd < 0)
            {
                // /Length missing, indirect and unresolvable, or wrong: scan for endstream
                int keywordPos = IndexOf(Data, "endstream", dataStart);
                if (keywordPos < 0)
                    throw new PdfParseException("Stream object has no endstream keyword.");
                dataEnd = keywordPos;
                // Trim the end-of-line marker that precedes endstream
                if (dataEnd > dataStart && Data[dataEnd - 1] == 0x0A)
                    dataEnd--;
                if (dataEnd > dataStart && Data[dataEnd - 1] == 0x0D)
                    dataEnd--;
                _lexer.Pos = keywordPos + "endstream".Length;
            }

            var stream = new CosStream();
            foreach (var kv in dict.Entries)
                stream.Entries.Add(kv);
            int dataLength = dataEnd - dataStart;
            stream.RawData = new byte[dataLength];
            Array.Copy(Data, dataStart, stream.RawData, 0, dataLength);
            stream.Set("Length", new CosInteger(dataLength));

            _lexer.SkipWhitespace();
            _lexer.TryReadKeyword("endobj"); // tolerant if missing
            return stream;
        }

        /// <summary>Finds the first occurrence of an ASCII keyword in <paramref name="data"/> at or after <paramref name="start"/>.</summary>
        internal static int IndexOf(byte[] data, string keyword, int start)
        {
            int limit = data.Length - keyword.Length;
            for (int i = start < 0 ? 0 : start; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < keyword.Length; j++)
                {
                    if (data[i + j] != (byte)keyword[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return i;
            }
            return -1;
        }
    }
}
