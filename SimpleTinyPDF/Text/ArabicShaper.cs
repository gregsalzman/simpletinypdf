// SimpleTinyPDF — Arabic contextual shaping via Unicode presentation forms.
//
// The approach (substituting Arabic Presentation Forms-A/B code points for
// base letters according to their joining context) was popularized for pure
// managed code by the MIT-licensed "Arabic Support for Unity" project by
// Abdullah Konash (https://github.com/Konash/arabic-support-unity), which
// served as a reference for this implementation.
//
// The contextual form and lam-alef ligature tables below were generated from
// the Unicode Character Database (UnicodeData.txt) decomposition mappings,
// © Unicode, Inc., used under the Unicode License (https://www.unicode.org/license.txt).
//
// See the Third-Party Code section of the SimpleTinyPDF README for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SimpleTinyPDF.Text
{
    /// <summary>
    /// Replaces Arabic base letters (U+0600 block and Arabic Extended letters
    /// with presentation forms) with their contextual presentation form
    /// (isolated / initial / medial / final) so that Arabic renders joined
    /// without requiring an OpenType GSUB shaping engine. Also substitutes
    /// the four mandatory lam-alef ligatures.
    ///
    /// Input and output are both in logical order; bidi reordering is a
    /// separate step (see <see cref="TextShaper"/>).
    /// </summary>
    internal static class ArabicShaper
    {
        /// <summary>
        /// The presentation-form code points for one Arabic base letter.
        /// A value of 0 means the letter has no such form (e.g. right-joining
        /// letters like alef have no initial/medial forms).
        /// </summary>
        private readonly struct ContextualForms
        {
            public readonly char Isolated;
            public readonly char Final;
            public readonly char Initial;
            public readonly char Medial;

            public ContextualForms(int isolated, int final, int initial, int medial)
            {
                Isolated = (char)isolated;
                Final = (char)final;
                Initial = (char)initial;
                Medial = (char)medial;
            }
        }

        /// <summary>Isolated and final forms of a lam-alef ligature.</summary>
        private readonly struct LamAlefForms
        {
            public readonly char Isolated;
            public readonly char Final;

            public LamAlefForms(int isolated, int final)
            {
                Isolated = (char)isolated;
                Final = (char)final;
            }
        }

        private const char Lam = (char)0x0644;
        private const char Tatweel = (char)0x0640;
        private const char ZeroWidthJoiner = (char)0x200D;
        private const char ZeroWidthNonJoiner = (char)0x200C;
        private const char Shadda = (char)0x0651;

        // Precomposed shadda+vowel ligature forms (Arabic Presentation Forms-A).
        // Substituted only when the target font actually contains the glyph;
        // fonts implement these as zero-advance mark glyphs with the vowel
        // correctly stacked above the shadda.
        private const char ShaddaFatha = (char)0xFC60;
        private const char ShaddaDamma = (char)0xFC61;
        private const char ShaddaKasra = (char)0xFC62;

        // Generated from UnicodeData.txt: base letter -> contextual presentation forms.
        private static readonly Dictionary<char, ContextualForms> Forms = new Dictionary<char, ContextualForms>
        {
            { (char)0x0621, new ContextualForms(0xFE80, 0x0000, 0x0000, 0x0000) },
            { (char)0x0622, new ContextualForms(0xFE81, 0xFE82, 0x0000, 0x0000) },
            { (char)0x0623, new ContextualForms(0xFE83, 0xFE84, 0x0000, 0x0000) },
            { (char)0x0624, new ContextualForms(0xFE85, 0xFE86, 0x0000, 0x0000) },
            { (char)0x0625, new ContextualForms(0xFE87, 0xFE88, 0x0000, 0x0000) },
            { (char)0x0626, new ContextualForms(0xFE89, 0xFE8A, 0xFE8B, 0xFE8C) },
            { (char)0x0627, new ContextualForms(0xFE8D, 0xFE8E, 0x0000, 0x0000) },
            { (char)0x0628, new ContextualForms(0xFE8F, 0xFE90, 0xFE91, 0xFE92) },
            { (char)0x0629, new ContextualForms(0xFE93, 0xFE94, 0x0000, 0x0000) },
            { (char)0x062A, new ContextualForms(0xFE95, 0xFE96, 0xFE97, 0xFE98) },
            { (char)0x062B, new ContextualForms(0xFE99, 0xFE9A, 0xFE9B, 0xFE9C) },
            { (char)0x062C, new ContextualForms(0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0) },
            { (char)0x062D, new ContextualForms(0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4) },
            { (char)0x062E, new ContextualForms(0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8) },
            { (char)0x062F, new ContextualForms(0xFEA9, 0xFEAA, 0x0000, 0x0000) },
            { (char)0x0630, new ContextualForms(0xFEAB, 0xFEAC, 0x0000, 0x0000) },
            { (char)0x0631, new ContextualForms(0xFEAD, 0xFEAE, 0x0000, 0x0000) },
            { (char)0x0632, new ContextualForms(0xFEAF, 0xFEB0, 0x0000, 0x0000) },
            { (char)0x0633, new ContextualForms(0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4) },
            { (char)0x0634, new ContextualForms(0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8) },
            { (char)0x0635, new ContextualForms(0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC) },
            { (char)0x0636, new ContextualForms(0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0) },
            { (char)0x0637, new ContextualForms(0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4) },
            { (char)0x0638, new ContextualForms(0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8) },
            { (char)0x0639, new ContextualForms(0xFEC9, 0xFECA, 0xFECB, 0xFECC) },
            { (char)0x063A, new ContextualForms(0xFECD, 0xFECE, 0xFECF, 0xFED0) },
            { (char)0x0641, new ContextualForms(0xFED1, 0xFED2, 0xFED3, 0xFED4) },
            { (char)0x0642, new ContextualForms(0xFED5, 0xFED6, 0xFED7, 0xFED8) },
            { (char)0x0643, new ContextualForms(0xFED9, 0xFEDA, 0xFEDB, 0xFEDC) },
            { (char)0x0644, new ContextualForms(0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0) },
            { (char)0x0645, new ContextualForms(0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4) },
            { (char)0x0646, new ContextualForms(0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8) },
            { (char)0x0647, new ContextualForms(0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC) },
            { (char)0x0648, new ContextualForms(0xFEED, 0xFEEE, 0x0000, 0x0000) },
            { (char)0x0649, new ContextualForms(0xFEEF, 0xFEF0, 0xFBE8, 0xFBE9) },
            { (char)0x064A, new ContextualForms(0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4) },
            { (char)0x0671, new ContextualForms(0xFB50, 0xFB51, 0x0000, 0x0000) },
            { (char)0x0677, new ContextualForms(0xFBDD, 0x0000, 0x0000, 0x0000) },
            { (char)0x0679, new ContextualForms(0xFB66, 0xFB67, 0xFB68, 0xFB69) },
            { (char)0x067A, new ContextualForms(0xFB5E, 0xFB5F, 0xFB60, 0xFB61) },
            { (char)0x067B, new ContextualForms(0xFB52, 0xFB53, 0xFB54, 0xFB55) },
            { (char)0x067E, new ContextualForms(0xFB56, 0xFB57, 0xFB58, 0xFB59) },
            { (char)0x067F, new ContextualForms(0xFB62, 0xFB63, 0xFB64, 0xFB65) },
            { (char)0x0680, new ContextualForms(0xFB5A, 0xFB5B, 0xFB5C, 0xFB5D) },
            { (char)0x0683, new ContextualForms(0xFB76, 0xFB77, 0xFB78, 0xFB79) },
            { (char)0x0684, new ContextualForms(0xFB72, 0xFB73, 0xFB74, 0xFB75) },
            { (char)0x0686, new ContextualForms(0xFB7A, 0xFB7B, 0xFB7C, 0xFB7D) },
            { (char)0x0687, new ContextualForms(0xFB7E, 0xFB7F, 0xFB80, 0xFB81) },
            { (char)0x0688, new ContextualForms(0xFB88, 0xFB89, 0x0000, 0x0000) },
            { (char)0x068C, new ContextualForms(0xFB84, 0xFB85, 0x0000, 0x0000) },
            { (char)0x068D, new ContextualForms(0xFB82, 0xFB83, 0x0000, 0x0000) },
            { (char)0x068E, new ContextualForms(0xFB86, 0xFB87, 0x0000, 0x0000) },
            { (char)0x0691, new ContextualForms(0xFB8C, 0xFB8D, 0x0000, 0x0000) },
            { (char)0x0698, new ContextualForms(0xFB8A, 0xFB8B, 0x0000, 0x0000) },
            { (char)0x06A4, new ContextualForms(0xFB6A, 0xFB6B, 0xFB6C, 0xFB6D) },
            { (char)0x06A6, new ContextualForms(0xFB6E, 0xFB6F, 0xFB70, 0xFB71) },
            { (char)0x06A9, new ContextualForms(0xFB8E, 0xFB8F, 0xFB90, 0xFB91) },
            { (char)0x06AD, new ContextualForms(0xFBD3, 0xFBD4, 0xFBD5, 0xFBD6) },
            { (char)0x06AF, new ContextualForms(0xFB92, 0xFB93, 0xFB94, 0xFB95) },
            { (char)0x06B1, new ContextualForms(0xFB9A, 0xFB9B, 0xFB9C, 0xFB9D) },
            { (char)0x06B3, new ContextualForms(0xFB96, 0xFB97, 0xFB98, 0xFB99) },
            { (char)0x06BA, new ContextualForms(0xFB9E, 0xFB9F, 0x0000, 0x0000) },
            { (char)0x06BB, new ContextualForms(0xFBA0, 0xFBA1, 0xFBA2, 0xFBA3) },
            { (char)0x06BE, new ContextualForms(0xFBAA, 0xFBAB, 0xFBAC, 0xFBAD) },
            { (char)0x06C0, new ContextualForms(0xFBA4, 0xFBA5, 0x0000, 0x0000) },
            { (char)0x06C1, new ContextualForms(0xFBA6, 0xFBA7, 0xFBA8, 0xFBA9) },
            { (char)0x06C5, new ContextualForms(0xFBE0, 0xFBE1, 0x0000, 0x0000) },
            { (char)0x06C6, new ContextualForms(0xFBD9, 0xFBDA, 0x0000, 0x0000) },
            { (char)0x06C7, new ContextualForms(0xFBD7, 0xFBD8, 0x0000, 0x0000) },
            { (char)0x06C8, new ContextualForms(0xFBDB, 0xFBDC, 0x0000, 0x0000) },
            { (char)0x06C9, new ContextualForms(0xFBE2, 0xFBE3, 0x0000, 0x0000) },
            { (char)0x06CB, new ContextualForms(0xFBDE, 0xFBDF, 0x0000, 0x0000) },
            { (char)0x06CC, new ContextualForms(0xFBFC, 0xFBFD, 0xFBFE, 0xFBFF) },
            { (char)0x06D0, new ContextualForms(0xFBE4, 0xFBE5, 0xFBE6, 0xFBE7) },
            { (char)0x06D2, new ContextualForms(0xFBAE, 0xFBAF, 0x0000, 0x0000) },
            { (char)0x06D3, new ContextualForms(0xFBB0, 0xFBB1, 0x0000, 0x0000) },
        };

        // Generated from UnicodeData.txt: alef variant -> lam-alef ligature forms.
        private static readonly Dictionary<char, LamAlefForms> LamAlefLigatures = new Dictionary<char, LamAlefForms>
        {
            { (char)0x0622, new LamAlefForms(0xFEF5, 0xFEF6) },
            { (char)0x0623, new LamAlefForms(0xFEF7, 0xFEF8) },
            { (char)0x0625, new LamAlefForms(0xFEF9, 0xFEFA) },
            { (char)0x0627, new LamAlefForms(0xFEFB, 0xFEFC) },
        };

        private enum JoiningType
        {
            NonJoining,     // U: does not join at all
            RightJoining,   // R: joins only with the preceding letter (has final form only)
            DualJoining,    // D: joins on both sides (has initial and medial forms)
            JoinCausing,    // C: tatweel and ZWJ; causes neighbours to join
            Transparent,    // T: combining marks; invisible to joining
        }

        private static JoiningType GetJoiningType(char c)
        {
            if (c == Tatweel || c == ZeroWidthJoiner)
                return JoiningType.JoinCausing;
            if (Forms.TryGetValue(c, out var forms))
            {
                if (forms.Initial != 0)
                    return JoiningType.DualJoining;
                if (forms.Final != 0)
                    return JoiningType.RightJoining;
                return JoiningType.NonJoining;
            }
            if (c >= ShaddaFatha && c <= ShaddaKasra)   // precomposed marks are Lo, not Mn
                return JoiningType.Transparent;
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                return JoiningType.Transparent;
            return JoiningType.NonJoining;
        }

        /// <summary>
        /// True if the string contains at least one character the shaper acts on.
        /// </summary>
        internal static bool ContainsArabic(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 0x0600 && c <= 0x08FF)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Shape a logical-order string: substitute contextual presentation forms
        /// and mandatory lam-alef ligatures. Non-Arabic characters pass through
        /// unchanged. The result remains in logical order.
        /// </summary>
        /// <param name="text">Logical-order text.</param>
        /// <param name="hasGlyph">Optional test for whether the target font can
        /// display a code point; enables optional substitutions like the
        /// precomposed shadda+vowel forms.</param>
        internal static string Shape(string text, Func<int, bool> hasGlyph = null)
        {
            if (hasGlyph != null)
                text = CombineShaddaVowels(text, hasGlyph);

            var sb = new StringBuilder(text.Length);
            int n = text.Length;
            for (int i = 0; i < n; i++)
            {
                char c = text[i];
                if (!Forms.TryGetValue(c, out var forms))
                {
                    sb.Append(c);
                    continue;
                }

                bool joinsPrev = ScanJoinsForward(text, i);

                // Mandatory lam-alef ligature: lam followed by an alef variant
                // (combining marks in between are transparent to joining).
                if (c == Lam)
                {
                    int next = NextNonTransparent(text, i);
                    if (next >= 0 && LamAlefLigatures.TryGetValue(text[next], out var lig))
                    {
                        sb.Append(joinsPrev ? lig.Final : lig.Isolated);
                        // Keep any combining marks that sat between lam and alef.
                        for (int j = i + 1; j < next; j++)
                            sb.Append(text[j]);
                        i = next;
                        continue;
                    }
                }

                bool joinsNext = forms.Initial != 0 && ScanJoinsBackward(text, i);
                joinsPrev = joinsPrev && forms.Final != 0;

                char shaped;
                if (joinsPrev && joinsNext)
                    shaped = forms.Medial;
                else if (joinsPrev)
                    shaped = forms.Final;
                else if (joinsNext)
                    shaped = forms.Initial;
                else
                    shaped = forms.Isolated;

                sb.Append(shaped != 0 ? shaped : c);
            }

            // ZWJ/ZWNJ have served their joining purpose; remove them so no
            // glyph lookup is attempted for them.
            sb.Replace(((char)0x200C).ToString(), "").Replace(((char)0x200D).ToString(), "");
            return sb.ToString();
        }

        /// <summary>
        /// Replace adjacent shadda+vowel mark pairs (in either logical order)
        /// with the precomposed presentation-form ligature, when the target
        /// font contains it. Without this, both marks render at their default
        /// height and the vowel disappears inside the shadda; the precomposed
        /// glyph has the vowel correctly stacked above the shadda.
        /// </summary>
        private static string CombineShaddaVowels(string text, Func<int, bool> hasGlyph)
        {
            if (text.IndexOf(Shadda) < 0)
                return text;

            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char combined = (char)0;
                if (i + 1 < text.Length)
                {
                    char a = text[i];
                    char b = text[i + 1];
                    if (b == Shadda)
                    {
                        // canonical order: vowel first, shadda second
                        combined = VowelWithShadda(a);
                    }
                    else if (a == Shadda)
                    {
                        // keyboard order: shadda first, vowel second
                        combined = VowelWithShadda(b);
                    }
                }

                if (combined != 0 && hasGlyph(combined))
                {
                    sb.Append(combined);
                    i++;
                }
                else
                {
                    sb.Append(text[i]);
                }
            }
            return sb.ToString();
        }

        private static char VowelWithShadda(char vowel)
        {
            switch (vowel)
            {
                case (char)0x064E: return ShaddaFatha;
                case (char)0x064F: return ShaddaDamma;
                case (char)0x0650: return ShaddaKasra;
                default: return (char)0;
            }
        }

        /// <summary>
        /// True if the nearest non-transparent character before index
        /// joins toward the character at index (i.e. is dual-joining or join-causing).
        /// </summary>
        private static bool ScanJoinsForward(string text, int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                var t = GetJoiningType(text[i]);
                if (t == JoiningType.Transparent)
                    continue;
                return t == JoiningType.DualJoining || t == JoiningType.JoinCausing;
            }
            return false;
        }

        /// <summary>
        /// True if the nearest non-transparent character after index
        /// joins toward the character at index (i.e. can connect on its right side).
        /// </summary>
        private static bool ScanJoinsBackward(string text, int index)
        {
            for (int i = index + 1; i < text.Length; i++)
            {
                var t = GetJoiningType(text[i]);
                if (t == JoiningType.Transparent)
                    continue;
                return t == JoiningType.DualJoining || t == JoiningType.RightJoining || t == JoiningType.JoinCausing;
            }
            return false;
        }

        private static int NextNonTransparent(string text, int index)
        {
            for (int i = index + 1; i < text.Length; i++)
            {
                if (GetJoiningType(text[i]) != JoiningType.Transparent)
                    return i;
            }
            return -1;
        }
    }
}
