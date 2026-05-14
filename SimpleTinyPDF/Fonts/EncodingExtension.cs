using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Tracks dynamic byte-slot assignments for characters outside WinAnsiEncoding.
    /// One instance per font per page. Uses the PDF Differences array to remap
    /// unused byte positions to named glyphs in the standard Type 1 fonts.
    /// </summary>
    internal sealed class EncodingExtension
    {
        // Byte positions not used by WinAnsiEncoding: 1-31, 127, 129, 141, 143, 144, 157
        private static readonly byte[] AvailableSlots =
        {
            1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,
            127,129,141,143,144,157
        };

        private readonly Dictionary<char, byte> _assignments = new Dictionary<char, byte>();
        private int _nextIndex;

        internal bool HasExtensions => _assignments.Count > 0;

        internal int Capacity => AvailableSlots.Length;

        internal int UsedSlots => _assignments.Count;

        /// <summary>
        /// Gets or assigns a byte code for an extended character.
        /// Returns true if the character was encoded (either already assigned or newly assigned).
        /// Returns false if capacity is exhausted.
        /// </summary>
        internal bool TryEncode(char c, out byte code)
        {
            if (_assignments.TryGetValue(c, out code))
                return true;

            if (_nextIndex >= AvailableSlots.Length)
            {
                code = 0;
                return false;
            }

            code = AvailableSlots[_nextIndex++];
            _assignments[c] = code;
            return true;
        }

        /// <summary>
        /// Builds the PDF encoding dictionary string with a Differences array.
        /// Example output: &lt;&lt; /Type /Encoding /BaseEncoding /WinAnsiEncoding /Differences [1 /aogonek 3 /cacute /ccaron] &gt;&gt;
        /// </summary>
        internal string GetEncodingDict()
        {
            if (!HasExtensions)
                return "/WinAnsiEncoding";

            // Build sorted list of (byte code, glyph name)
            var entries = new List<(byte code, string glyphName)>();
            foreach (var kv in _assignments)
            {
                if (GlyphMapping.UnicodeToGlyphName.TryGetValue(kv.Key, out string name))
                    entries.Add((kv.Value, name));
            }
            entries.Sort((a, b) => a.code.CompareTo(b.code));

            // Build Differences array, grouping consecutive codes
            var sb = new StringBuilder();
            sb.Append("<< /Type /Encoding /BaseEncoding /WinAnsiEncoding /Differences [");

            int? lastCode = null;
            foreach (var (code, glyphName) in entries)
            {
                if (lastCode == null || code != lastCode.Value + 1)
                    sb.AppendFormat(" {0}", code);
                sb.AppendFormat(" /{0}", glyphName);
                lastCode = code;
            }

            sb.Append("] >>");
            return sb.ToString();
        }

    }
}
