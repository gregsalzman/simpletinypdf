using System.Collections.Generic;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Maps European Unicode characters (outside WinAnsiEncoding) to Adobe glyph names
    /// and base characters for width estimation. These glyphs exist in the standard 14
    /// Type 1 PDF fonts but are not reachable via WinAnsiEncoding.
    /// </summary>
    internal static class GlyphMapping
    {
        /// <summary>
        /// Maps Unicode code points to Adobe glyph names for characters that exist
        /// in the standard Type 1 fonts but are not covered by WinAnsiEncoding.
        /// </summary>
        internal static readonly Dictionary<char, string> UnicodeToGlyphName = new Dictionary<char, string>
        {
            // Polish
            { '\u0104', "Aogonek" },    // Ą
            { '\u0105', "aogonek" },    // ą
            { '\u0106', "Cacute" },     // Ć
            { '\u0107', "cacute" },     // ć
            { '\u0118', "Eogonek" },    // Ę
            { '\u0119', "eogonek" },    // ę
            { '\u0141', "Lslash" },     // Ł
            { '\u0142', "lslash" },     // ł
            { '\u0143', "Nacute" },     // Ń
            { '\u0144', "nacute" },     // ń
            { '\u015A', "Sacute" },     // Ś
            { '\u015B', "sacute" },     // ś
            { '\u0179', "Zacute" },     // Ź
            { '\u017A', "zacute" },     // ź
            { '\u017B', "Zdotaccent" }, // Ż
            { '\u017C', "zdotaccent" }, // ż

            // Czech / Slovak
            { '\u010C', "Ccaron" },     // Č
            { '\u010D', "ccaron" },     // č
            { '\u010E', "Dcaron" },     // Ď
            { '\u010F', "dcaron" },     // ď
            { '\u011A', "Ecaron" },     // Ě
            { '\u011B', "ecaron" },     // ě
            { '\u0139', "Lacute" },     // Ĺ
            { '\u013A', "lacute" },     // ĺ
            { '\u013D', "Lcaron" },     // Ľ
            { '\u013E', "lcaron" },     // ľ
            { '\u0147', "Ncaron" },     // Ň
            { '\u0148', "ncaron" },     // ň
            { '\u0158', "Rcaron" },     // Ř
            { '\u0159', "rcaron" },     // ř
            { '\u0154', "Racute" },     // Ŕ
            { '\u0155', "racute" },     // ŕ
            { '\u0164', "Tcaron" },     // Ť
            { '\u0165', "tcaron" },     // ť
            { '\u016E', "Uring" },      // Ů
            { '\u016F', "uring" },      // ů

            // Hungarian
            { '\u0150', "Ohungarumlaut" },  // Ő
            { '\u0151', "ohungarumlaut" },  // ő
            { '\u0170', "Uhungarumlaut" },  // Ű
            { '\u0171', "uhungarumlaut" },  // ű

            // Romanian
            { '\u0102', "Abreve" },     // Ă
            { '\u0103', "abreve" },     // ă
            { '\u0218', "Scommaaccent" }, // Ș (comma below)
            { '\u0219', "scommaaccent" }, // ș
            { '\u021A', "Tcommaaccent" }, // Ț (comma below)
            { '\u021B', "tcommaaccent" }, // ț
            { '\u015E', "Scedilla" },   // Ş (cedilla, legacy form)
            { '\u015F', "scedilla" },   // ş
            { '\u0162', "Tcedilla" },   // Ţ (cedilla, legacy form)
            { '\u0163', "tcedilla" },   // ţ

            // Croatian / Slovenian (Š š Ž ž already in WinAnsi)
            { '\u0110', "Dcroat" },     // Đ
            { '\u0111', "dcroat" },     // đ

            // Turkish
            { '\u011E', "Gbreve" },     // Ğ
            { '\u011F', "gbreve" },     // ğ
            { '\u0130', "Idotaccent" }, // İ
            { '\u0131', "dotlessi" },   // ı

            // Baltic (Lithuanian, Latvian, Estonian)
            { '\u0100', "Amacron" },    // Ā
            { '\u0101', "amacron" },    // ā
            { '\u0112', "Emacron" },    // Ē
            { '\u0113', "emacron" },    // ē
            { '\u0116', "Edotaccent" }, // Ė
            { '\u0117', "edotaccent" }, // ė
            { '\u0122', "Gcommaaccent" }, // Ģ
            { '\u0123', "gcommaaccent" }, // ģ
            { '\u012A', "Imacron" },    // Ī
            { '\u012B', "imacron" },    // ī
            { '\u012E', "Iogonek" },    // Į
            { '\u012F', "iogonek" },    // į
            { '\u0136', "Kcommaaccent" }, // Ķ
            { '\u0137', "kcommaaccent" }, // ķ
            { '\u013B', "Lcommaaccent" }, // Ļ
            { '\u013C', "lcommaaccent" }, // ļ
            { '\u0145', "Ncommaaccent" }, // Ņ
            { '\u0146', "ncommaaccent" }, // ņ
            { '\u016A', "Umacron" },    // Ū
            { '\u016B', "umacron" },    // ū
            { '\u0172', "Uogonek" },    // Ų
            { '\u0173', "uogonek" },    // ų
        };

        /// <summary>
        /// Maps each extended character to its base Latin character for width lookup.
        /// In standard Type 1 fonts, accented variants have the same width as the base.
        /// </summary>
        internal static readonly Dictionary<char, char> BaseCharForWidth = new Dictionary<char, char>
        {
            // A-based
            { '\u0104', 'A' }, { '\u0105', 'a' }, // Ą ą
            { '\u0102', 'A' }, { '\u0103', 'a' }, // Ă ă
            { '\u0100', 'A' }, { '\u0101', 'a' }, // Ā ā

            // C-based
            { '\u0106', 'C' }, { '\u0107', 'c' }, // Ć ć
            { '\u010C', 'C' }, { '\u010D', 'c' }, // Č č

            // D-based
            { '\u010E', 'D' }, { '\u010F', 'd' }, // Ď ď
            { '\u0110', 'D' }, { '\u0111', 'd' }, // Đ đ

            // E-based
            { '\u0118', 'E' }, { '\u0119', 'e' }, // Ę ę
            { '\u011A', 'E' }, { '\u011B', 'e' }, // Ě ě
            { '\u0112', 'E' }, { '\u0113', 'e' }, // Ē ē
            { '\u0116', 'E' }, { '\u0117', 'e' }, // Ė ė

            // G-based
            { '\u011E', 'G' }, { '\u011F', 'g' }, // Ğ ğ
            { '\u0122', 'G' }, { '\u0123', 'g' }, // Ģ ģ

            // I-based
            { '\u0130', 'I' }, { '\u0131', 'i' }, // İ ı
            { '\u012A', 'I' }, { '\u012B', 'i' }, // Ī ī
            { '\u012E', 'I' }, { '\u012F', 'i' }, // Į į

            // K-based
            { '\u0136', 'K' }, { '\u0137', 'k' }, // Ķ ķ

            // L-based
            { '\u0139', 'L' }, { '\u013A', 'l' }, // Ĺ ĺ
            { '\u013B', 'L' }, { '\u013C', 'l' }, // Ļ ļ
            { '\u013D', 'L' }, { '\u013E', 'l' }, // Ľ ľ
            { '\u0141', 'L' }, { '\u0142', 'l' }, // Ł ł

            // N-based
            { '\u0143', 'N' }, { '\u0144', 'n' }, // Ń ń
            { '\u0145', 'N' }, { '\u0146', 'n' }, // Ņ ņ
            { '\u0147', 'N' }, { '\u0148', 'n' }, // Ň ň

            // O-based
            { '\u0150', 'O' }, { '\u0151', 'o' }, // Ő ő

            // R-based
            { '\u0154', 'R' }, { '\u0155', 'r' }, // Ŕ ŕ
            { '\u0158', 'R' }, { '\u0159', 'r' }, // Ř ř

            // S-based
            { '\u015A', 'S' }, { '\u015B', 's' }, // Ś ś
            { '\u015E', 'S' }, { '\u015F', 's' }, // Ş ş
            { '\u0218', 'S' }, { '\u0219', 's' }, // Ș ș

            // T-based
            { '\u0162', 'T' }, { '\u0163', 't' }, // Ţ ţ
            { '\u0164', 'T' }, { '\u0165', 't' }, // Ť ť
            { '\u021A', 'T' }, { '\u021B', 't' }, // Ț ț

            // U-based
            { '\u016A', 'U' }, { '\u016B', 'u' }, // Ū ū
            { '\u016E', 'U' }, { '\u016F', 'u' }, // Ů ů
            { '\u0170', 'U' }, { '\u0171', 'u' }, // Ű ű
            { '\u0172', 'U' }, { '\u0173', 'u' }, // Ų ų

            // Z-based
            { '\u0179', 'Z' }, { '\u017A', 'z' }, // Ź ź
            { '\u017B', 'Z' }, { '\u017C', 'z' }, // Ż ż
        };
    }
}
