// SimpleTinyPDF — right-to-left text processing entry point.
//
// Combines Arabic contextual shaping (ArabicShaper) with the Unicode
// Bidirectional Algorithm (Bidi, adapted from RichTextKit) to convert
// logical-order text into the visual-order, presentation-form text that is
// measured and drawn. The level-run reordering below follows the reference
// implementation of UAX #9 rule L2 (https://unicode.org/reports/tr9/).

using System;
using System.Threading;

namespace SimpleTinyPDF.Text
{
    /// <summary>
    /// Converts logical-order text containing right-to-left script
    /// (Arabic, Hebrew, ...) into visual-order text ready for left-to-right
    /// glyph emission:
    ///   1. Arabic letters are replaced with contextual presentation forms
    ///      and mandatory lam-alef ligatures (logical order preserved),
    ///   2. the Unicode Bidirectional Algorithm (UAX #9) resolves embedding
    ///      levels and the line is reordered to visual order (rules L1/L2),
    ///   3. mirrored characters (brackets etc.) in RTL runs are swapped (L4),
    ///   4. zero-width bidi control characters are removed.
    /// Text without RTL content is returned unchanged.
    /// </summary>
    internal static class TextShaper
    {
        [ThreadStatic]
        private static BidiData _bidiData;

        /// <summary>
        /// Fast scan: true if the string contains any character that requires
        /// bidi processing or Arabic shaping. False for pure LTR text, which
        /// must pass through the drawing pipeline completely unchanged.
        /// </summary>
        internal static bool NeedsProcessing(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c < 0x0590)
                    continue;
                if (c <= 0x08FF)                    // Hebrew, Arabic, Syriac, Thaana, Arabic Extended
                    return true;
                if (c >= 0x200C && c <= 0x200F)     // ZWNJ, ZWJ, LRM, RLM
                    return true;
                if (c >= 0x202A && c <= 0x202E)     // LRE, RLE, PDF, LRO, RLO
                    return true;
                if (c >= 0x2066 && c <= 0x2069)     // LRI, RLI, FSI, PDI
                    return true;
                if (c >= 0xFB1D && c <= 0xFDFF)     // Hebrew/Arabic presentation forms A
                    return true;
                if (c >= 0xFE70 && c <= 0xFEFF)     // Arabic presentation forms B
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Process one line of logical-order text into visual-order,
        /// presentation-form text. The input must be a single line (the
        /// wrapping code splits lines before drawing them).
        /// </summary>
        /// <param name="text">Logical-order text.</param>
        /// <param name="font">Optional target font; enables glyph-availability
        /// dependent substitutions (precomposed shadda+vowel forms).</param>
        internal static string Process(string text, PdfFontSource font = null)
        {
            if (!NeedsProcessing(text))
                return text;

            // 1. Arabic contextual shaping (logical order in, logical order out)
            if (ArabicShaper.ContainsArabic(text))
            {
                Func<int, bool> hasGlyph = null;
                if (font != null && !font.IsBuiltIn)
                {
                    var customFont = font.CustomFont;
                    hasGlyph = cp => customFont.GetGlyphId(cp) != 0;
                }
                text = ArabicShaper.Shape(text, hasGlyph);
            }

            // Decode to code points (surrogate-aware)
            int n = 0;
            var codePoints = new int[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    codePoints[n++] = char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                else
                {
                    codePoints[n++] = text[i];
                }
            }

            // 2. Resolve embedding levels with the Unicode Bidirectional Algorithm.
            // Paragraph embedding level 2 = auto-detect from the first strong character.
            var data = _bidiData ?? (_bidiData = new BidiData());
            data.Init(new Slice<int>(codePoints, 0, n), 2);
            var bidi = Bidi.Instance.Value;
            bidi.Process(data);
            var levels = bidi.ResolvedLevels;

            // 3. Rule L4: mirror brackets and other mirrored characters in RTL runs
            for (int i = 0; i < n; i++)
            {
                if ((levels[i] & 1) != 0)
                    codePoints[i] = BidiMirroring.Mirror(codePoints[i]);
            }

            // 4. Rule L2: compute visual order by reversing level runs from the
            // highest level down to the lowest odd level. Reordering operates on
            // display clusters (a base character plus any following combining
            // marks) rather than single code points, so that marks stay attached
            // to their base letter when a run is reversed (rule L3).
            int clusterCount = 0;
            var clusterStart = new int[n];
            var clusterLevels = new sbyte[n];
            for (int i = 0; i < n; i++)
            {
                if (clusterCount > 0 && IsCombiningMark(codePoints[i]))
                    continue;
                clusterStart[clusterCount] = i;
                clusterLevels[clusterCount] = levels[i];
                clusterCount++;
            }

            sbyte highest = 0;
            sbyte lowestOdd = sbyte.MaxValue;
            for (int i = 0; i < clusterCount; i++)
            {
                sbyte level = clusterLevels[i];
                if (level > highest)
                    highest = level;
                if ((level & 1) != 0 && level < lowestOdd)
                    lowestOdd = level;
            }

            var order = new int[clusterCount];
            for (int i = 0; i < clusterCount; i++)
                order[i] = i;

            for (sbyte level = highest; level >= lowestOdd; level--)
            {
                for (int i = 0; i < clusterCount; i++)
                {
                    if (clusterLevels[i] < level)
                        continue;
                    int start = i;
                    while (i < clusterCount && clusterLevels[i] >= level)
                        i++;
                    for (int j = start, k = i - 1; j < k; j++, k--)
                    {
                        int tmp = order[j];
                        order[j] = order[k];
                        order[k] = tmp;
                    }
                }
            }

            // 5. Emit in visual order, dropping zero-width bidi controls
            var sb = new System.Text.StringBuilder(n);
            for (int v = 0; v < clusterCount; v++)
            {
                int cluster = order[v];
                int from = clusterStart[cluster];
                int to = cluster + 1 < clusterCount ? clusterStart[cluster + 1] : n;
                for (int i = from; i < to; i++)
                {
                    int cp = codePoints[i];
                    if (IsBidiControl(cp))
                        continue;
                    if (cp > 0xFFFF)
                        sb.Append(char.ConvertFromUtf32(cp));
                    else
                        sb.Append((char)cp);
                }
            }
            return sb.ToString();
        }

        private static bool IsCombiningMark(int cp)
        {
            if (cp > 0xFFFF)
                return false;
            if (cp >= 0xFC5E && cp <= 0xFC63)   // precomposed shadda+vowel forms are Lo, not Mn
                return true;
            return System.Globalization.CharUnicodeInfo.GetUnicodeCategory((char)cp)
                == System.Globalization.UnicodeCategory.NonSpacingMark;
        }

        private static bool IsBidiControl(int cp)
        {
            return cp == 0x061C                     // Arabic letter mark
                || cp == 0x200C || cp == 0x200D     // ZWNJ, ZWJ
                || cp == 0x200E || cp == 0x200F     // LRM, RLM
                || (cp >= 0x202A && cp <= 0x202E)   // LRE, RLE, PDF, LRO, RLO
                || (cp >= 0x2066 && cp <= 0x2069);  // LRI, RLI, FSI, PDI
        }
    }
}
