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
    }
}
