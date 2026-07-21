using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Identifies an indirect object in a PDF file (object number + generation).
    /// </summary>
    internal struct PdfObjectId : IEquatable<PdfObjectId>
    {
        internal readonly int Number;
        internal readonly int Generation;

        internal PdfObjectId(int number, int generation)
        {
            Number = number;
            Generation = generation;
        }

        public bool Equals(PdfObjectId other) => Number == other.Number && Generation == other.Generation;
        public override bool Equals(object obj) => obj is PdfObjectId other && Equals(other);
        public override int GetHashCode() => (Number * 397) ^ Generation;
        public override string ToString() => $"{Number} {Generation}";
    }

    /// <summary>
    /// Base class for parsed PDF (COS) values. Instances form a typed tree that
    /// preserves everything needed to re-serialize an imported object.
    /// </summary>
    internal abstract class CosValue
    {
    }

    internal sealed class CosNull : CosValue
    {
        internal static readonly CosNull Instance = new CosNull();
        private CosNull() { }
    }

    internal sealed class CosBool : CosValue
    {
        internal static readonly CosBool True = new CosBool(true);
        internal static readonly CosBool False = new CosBool(false);
        internal readonly bool Value;
        private CosBool(bool value) => Value = value;
    }

    internal sealed class CosInteger : CosValue
    {
        internal readonly long Value;
        internal CosInteger(long value) => Value = value;
    }

    internal sealed class CosReal : CosValue
    {
        internal readonly double Value;
        internal CosReal(double value) => Value = value;
    }

    /// <summary>A PDF name. <see cref="Value"/> is fully decoded (no leading '/' and no #xx escapes).</summary>
    internal sealed class CosName : CosValue
    {
        internal readonly string Value;
        internal CosName(string value) => Value = value;
    }

    /// <summary>A PDF string. <see cref="Raw"/> holds the decoded bytes; the original literal/hex form is not preserved.</summary>
    internal sealed class CosString : CosValue
    {
        internal byte[] Raw;
        internal CosString(byte[] raw) => Raw = raw;

        /// <summary>Decodes the string as text: UTF-16BE when it starts with a BOM, otherwise PDFDocEncoding (approximated as Latin-1).</summary>
        internal string AsText()
        {
            if (Raw == null || Raw.Length == 0) return string.Empty;
            if (Raw.Length >= 2 && Raw[0] == 0xFE && Raw[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(Raw, 2, Raw.Length - 2);
            var sb = new StringBuilder(Raw.Length);
            foreach (byte b in Raw)
                sb.Append((char)b);
            return sb.ToString();
        }
    }

    internal sealed class CosArray : CosValue
    {
        internal readonly List<CosValue> Items = new List<CosValue>();
    }

    /// <summary>An ordered PDF dictionary. Duplicate keys keep the last value.</summary>
    internal class CosDict : CosValue
    {
        internal readonly List<KeyValuePair<string, CosValue>> Entries = new List<KeyValuePair<string, CosValue>>();

        internal void Set(string key, CosValue value)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Key == key)
                {
                    Entries[i] = new KeyValuePair<string, CosValue>(key, value);
                    return;
                }
            }
            Entries.Add(new KeyValuePair<string, CosValue>(key, value));
        }

        internal CosValue Get(string key)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Key == key)
                    return Entries[i].Value;
            }
            return null;
        }

        internal bool ContainsKey(string key) => Get(key) != null;

        internal void Remove(string key)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i].Key == key)
                    Entries.RemoveAt(i);
            }
        }

        /// <summary>Returns the value of <paramref name="key"/> as a long, or null if absent or not an integer.</summary>
        internal long? GetInteger(string key) => Get(key) is CosInteger i ? i.Value : (long?)null;

        /// <summary>Returns the value of <paramref name="key"/> as a name string, or null.</summary>
        internal string GetName(string key) => (Get(key) as CosName)?.Value;
    }

    /// <summary>
    /// A PDF stream object. <see cref="RawData"/> holds the stored (still encoded/filtered)
    /// bytes exactly as they appear in the file.
    /// </summary>
    internal sealed class CosStream : CosDict
    {
        internal byte[] RawData;
    }

    /// <summary>A reference to an indirect object ("N G R").</summary>
    internal sealed class CosReference : CosValue
    {
        internal readonly PdfObjectId Id;
        internal CosReference(int number, int generation) => Id = new PdfObjectId(number, generation);
    }

    internal static class CosNumber
    {
        /// <summary>Formats a parsed numeric value using invariant culture, as PDF syntax requires.</summary>
        internal static string Format(double value) =>
            value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
