using System.Collections.Generic;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Low-level tokenizer over the raw bytes of a PDF file. Follows the byte-array +
    /// integer-offset style used by <see cref="TrueTypeFont"/>: all reads are bounds-checked
    /// and never throw on truncated input.
    /// </summary>
    internal sealed class PdfLexer
    {
        internal readonly byte[] Data;
        internal int Pos;

        internal PdfLexer(byte[] data, int pos = 0)
        {
            Data = data;
            Pos = pos;
        }

        internal static bool IsWhitespace(byte b) =>
            b == 0x00 || b == 0x09 || b == 0x0A || b == 0x0C || b == 0x0D || b == 0x20;

        internal static bool IsDelimiter(byte b) =>
            b == (byte)'(' || b == (byte)')' || b == (byte)'<' || b == (byte)'>' ||
            b == (byte)'[' || b == (byte)']' || b == (byte)'{' || b == (byte)'}' ||
            b == (byte)'/' || b == (byte)'%';

        internal static bool IsRegular(byte b) => !IsWhitespace(b) && !IsDelimiter(b);

        /// <summary>Returns the byte at the current position, or -1 at end of data.</summary>
        internal int Peek() => Pos >= 0 && Pos < Data.Length ? Data[Pos] : -1;

        /// <summary>Returns the byte at the current position + <paramref name="offset"/>, or -1 past end.</summary>
        internal int PeekAt(int offset)
        {
            int p = Pos + offset;
            return p >= 0 && p < Data.Length ? Data[p] : -1;
        }

        /// <summary>Skips whitespace and %-comments (comments run to end of line).</summary>
        internal void SkipWhitespace()
        {
            while (Pos < Data.Length)
            {
                byte b = Data[Pos];
                if (IsWhitespace(b))
                {
                    Pos++;
                }
                else if (b == (byte)'%')
                {
                    while (Pos < Data.Length && Data[Pos] != 0x0A && Data[Pos] != 0x0D)
                        Pos++;
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>Reads a run of regular characters (used for numbers and keywords). Empty string if none.</summary>
        internal string ReadRegularToken()
        {
            int start = Pos;
            while (Pos < Data.Length && IsRegular(Data[Pos]))
                Pos++;
            return Encoding.ASCII.GetString(Data, start, Pos - start);
        }

        /// <summary>
        /// If the bytes at the current position spell <paramref name="keyword"/> followed by a
        /// non-regular character (or end of data), consumes it and returns true.
        /// </summary>
        internal bool TryReadKeyword(string keyword)
        {
            if (Pos + keyword.Length > Data.Length)
                return false;
            for (int i = 0; i < keyword.Length; i++)
            {
                if (Data[Pos + i] != (byte)keyword[i])
                    return false;
            }
            int after = Pos + keyword.Length;
            if (after < Data.Length && IsRegular(Data[after]))
                return false;
            Pos = after;
            return true;
        }

        /// <summary>
        /// Reads a name. The current position must be at the '/'. Decodes #xx escapes;
        /// a malformed # sequence is kept literally.
        /// </summary>
        internal string ReadName()
        {
            Pos++; // skip '/'
            var sb = new StringBuilder();
            while (Pos < Data.Length && IsRegular(Data[Pos]))
            {
                byte b = Data[Pos];
                if (b == (byte)'#' && Pos + 2 < Data.Length &&
                    TryHexDigit(Data[Pos + 1], out int hi) && TryHexDigit(Data[Pos + 2], out int lo))
                {
                    sb.Append((char)((hi << 4) | lo));
                    Pos += 3;
                }
                else
                {
                    sb.Append((char)b);
                    Pos++;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Reads a literal string. The current position must be at the '('. Handles nested
        /// balanced parentheses, standard escapes, 1-3 digit octal escapes, backslash-newline
        /// continuation, and normalizes raw CR/CRLF line endings to LF per the PDF spec.
        /// </summary>
        internal byte[] ReadLiteralString()
        {
            Pos++; // skip '('
            var bytes = new List<byte>();
            int depth = 1;
            while (Pos < Data.Length)
            {
                byte b = Data[Pos++];
                if (b == (byte)'\\')
                {
                    if (Pos >= Data.Length)
                        break;
                    byte e = Data[Pos++];
                    switch (e)
                    {
                        case (byte)'n': bytes.Add(0x0A); break;
                        case (byte)'r': bytes.Add(0x0D); break;
                        case (byte)'t': bytes.Add(0x09); break;
                        case (byte)'b': bytes.Add(0x08); break;
                        case (byte)'f': bytes.Add(0x0C); break;
                        case (byte)'(': bytes.Add((byte)'('); break;
                        case (byte)')': bytes.Add((byte)')'); break;
                        case (byte)'\\': bytes.Add((byte)'\\'); break;
                        case 0x0D: // line continuation: backslash + CR (+ optional LF)
                            if (Pos < Data.Length && Data[Pos] == 0x0A)
                                Pos++;
                            break;
                        case 0x0A: // line continuation: backslash + LF
                            break;
                        default:
                            if (e >= (byte)'0' && e <= (byte)'7')
                            {
                                int value = e - (byte)'0';
                                for (int i = 0; i < 2 && Pos < Data.Length; i++)
                                {
                                    byte d = Data[Pos];
                                    if (d < (byte)'0' || d > (byte)'7')
                                        break;
                                    value = (value << 3) | (d - (byte)'0');
                                    Pos++;
                                }
                                bytes.Add((byte)value);
                            }
                            else
                            {
                                // Unknown escape: the backslash is ignored per spec
                                bytes.Add(e);
                            }
                            break;
                    }
                }
                else if (b == (byte)'(')
                {
                    depth++;
                    bytes.Add(b);
                }
                else if (b == (byte)')')
                {
                    depth--;
                    if (depth == 0)
                        break;
                    bytes.Add(b);
                }
                else if (b == 0x0D)
                {
                    // Raw CR or CRLF inside a string is normalized to LF
                    if (Pos < Data.Length && Data[Pos] == 0x0A)
                        Pos++;
                    bytes.Add(0x0A);
                }
                else
                {
                    bytes.Add(b);
                }
            }
            return bytes.ToArray();
        }

        /// <summary>
        /// Reads a hex string. The current position must be at the '&lt;'. Whitespace inside is
        /// ignored; an odd number of digits is padded with a trailing zero.
        /// </summary>
        internal byte[] ReadHexString()
        {
            Pos++; // skip '<'
            var bytes = new List<byte>();
            int pending = -1;
            while (Pos < Data.Length)
            {
                byte b = Data[Pos++];
                if (b == (byte)'>')
                    break;
                if (TryHexDigit(b, out int digit))
                {
                    if (pending < 0)
                    {
                        pending = digit;
                    }
                    else
                    {
                        bytes.Add((byte)((pending << 4) | digit));
                        pending = -1;
                    }
                }
                // Whitespace and any other bytes are skipped
            }
            if (pending >= 0)
                bytes.Add((byte)(pending << 4));
            return bytes.ToArray();
        }

        private static bool TryHexDigit(byte b, out int value)
        {
            if (b >= (byte)'0' && b <= (byte)'9') { value = b - (byte)'0'; return true; }
            if (b >= (byte)'A' && b <= (byte)'F') { value = b - (byte)'A' + 10; return true; }
            if (b >= (byte)'a' && b <= (byte)'f') { value = b - (byte)'a' + 10; return true; }
            value = 0;
            return false;
        }
    }
}
