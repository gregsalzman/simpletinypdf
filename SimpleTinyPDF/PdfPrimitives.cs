using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SimpleTinyPDF
{
    internal class PdfObj
    {
        internal int ObjectNumber;
        internal string Ref => $"{ObjectNumber} 0 R";
        internal virtual void WriteTo(PdfBinaryWriter w) { }
    }

    internal class PdfDict : PdfObj
    {
        internal readonly List<KeyValuePair<string, string>> Entries = new List<KeyValuePair<string, string>>();

        internal void Set(string key, string value)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Key == key)
                {
                    Entries[i] = new KeyValuePair<string, string>(key, value);
                    return;
                }
            }
            Entries.Add(new KeyValuePair<string, string>(key, value));
        }

        internal override void WriteTo(PdfBinaryWriter w)
        {
            w.WriteAscii("<<\n");
            foreach (var kv in Entries)
                w.WriteAscii($"/{kv.Key} {kv.Value}\n");
            w.WriteAscii(">>\n");
        }
    }

    internal class PdfStream : PdfDict
    {
        internal byte[] Data = Array.Empty<byte>();

        internal override void WriteTo(PdfBinaryWriter w)
        {
            Set("Length", Data.Length.ToString());
            w.WriteAscii("<<\n");
            foreach (var kv in Entries)
                w.WriteAscii($"/{kv.Key} {kv.Value}\n");
            w.WriteAscii(">>\nstream\n");
            w.WriteBytes(Data);
            w.WriteAscii("\nendstream\n");
        }
    }

    internal class PdfArray : PdfObj
    {
        internal string Value;
        internal override void WriteTo(PdfBinaryWriter w)
        {
            w.WriteAscii(Value + "\n");
        }
    }

    internal class PdfBinaryWriter
    {
        private readonly Stream _stream;
        internal long Position => _stream.Position;

        internal PdfBinaryWriter(Stream stream) => _stream = stream;

        internal void WriteAscii(string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            _stream.Write(bytes, 0, bytes.Length);
        }

        internal void WriteBytes(byte[] data) =>
            _stream.Write(data, 0, data.Length);
    }

    internal static class PdfStringHelper
    {
        // Map Unicode characters to WinAnsiEncoding byte values
        internal static readonly Dictionary<char, byte> UnicodeToWinAnsi = new Dictionary<char, byte>
        {
            { '\u20AC', 0x80 }, // Euro sign
            { '\u201A', 0x82 }, // Single low-9 quotation mark
            { '\u0192', 0x83 }, // Latin small letter f with hook
            { '\u201E', 0x84 }, // Double low-9 quotation mark
            { '\u2026', 0x85 }, // Horizontal ellipsis
            { '\u2020', 0x86 }, // Dagger
            { '\u2021', 0x87 }, // Double dagger
            { '\u02C6', 0x88 }, // Modifier letter circumflex accent
            { '\u2030', 0x89 }, // Per mille sign
            { '\u0160', 0x8A }, // Latin capital letter S with caron
            { '\u2039', 0x8B }, // Single left-pointing angle quotation mark
            { '\u0152', 0x8C }, // Latin capital ligature OE
            { '\u017D', 0x8E }, // Latin capital letter Z with caron
            { '\u2018', 0x91 }, // Left single quotation mark
            { '\u2019', 0x92 }, // Right single quotation mark
            { '\u201C', 0x93 }, // Left double quotation mark
            { '\u201D', 0x94 }, // Right double quotation mark
            { '\u2022', 0x95 }, // Bullet
            { '\u2013', 0x96 }, // En dash
            { '\u2014', 0x97 }, // Em dash
            { '\u02DC', 0x98 }, // Small tilde
            { '\u2122', 0x99 }, // Trade mark sign
            { '\u0161', 0x9A }, // Latin small letter s with caron
            { '\u203A', 0x9B }, // Single right-pointing angle quotation mark
            { '\u0153', 0x9C }, // Latin small ligature oe
            { '\u017E', 0x9E }, // Latin small letter z with caron
            { '\u0178', 0x9F }, // Latin capital letter Y with diaeresis
        };

        internal static string Escape(string text) => Escape(text, null);

        internal static string Escape(string text, EncodingExtension ext)
        {
            if (text == null) return "()";
            var sb = new StringBuilder(text.Length + 10);
            sb.Append('(');
            foreach (char c in text)
            {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '(') sb.Append("\\(");
                else if (c == ')') sb.Append("\\)");
                else if (c >= 32 && c <= 126) sb.Append(c);
                else if (c < 256) sb.AppendFormat("\\{0}", Convert.ToString((int)c, 8).PadLeft(3, '0'));
                else if (UnicodeToWinAnsi.TryGetValue(c, out byte winAnsiCode))
                    sb.AppendFormat("\\{0}", Convert.ToString(winAnsiCode, 8).PadLeft(3, '0'));
                else if (ext != null && GlyphMapping.UnicodeToGlyphName.ContainsKey(c))
                {
                    if (!ext.TryEncode(c, out byte extCode))
                        throw new NotSupportedException(
                            $"Character '{c}' (U+{(int)c:X4}) cannot be encoded: the maximum of {ext.Capacity} " +
                            "extended characters per font per page has been reached.");
                    sb.AppendFormat("\\{0}", Convert.ToString(extCode, 8).PadLeft(3, '0'));
                }
                // Characters with no known glyph mapping are silently dropped
            }
            sb.Append(')');
            return sb.ToString();
        }

        internal static string F(float value) =>
            value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
