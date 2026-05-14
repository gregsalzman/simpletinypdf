using System;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Encodes data as a Code 39 barcode.
    /// Each character maps to 9 elements (5 bars + 4 spaces alternating)
    /// where exactly 3 of the 9 are wide. Characters are separated by
    /// a narrow inter-character gap.
    /// </summary>
    internal static class Code39Encoder
    {
        private const int NarrowWidth = 1;
        private const int WideWidth = 3;

        // Each pattern is a 9-bit mask stored in a ushort.
        // Bit 8 = element 0 (first bar), bit 0 = element 8 (last bar).
        // 1 = wide, 0 = narrow. Elements alternate bar/space/bar/space/bar/space/bar/space/bar.
        // Index: 0-9 = digits, 10-35 = A-Z, 36='-', 37='.', 38=' ', 39='$', 40='/', 41='+', 42='%', 43='*'
        private static readonly ushort[] Patterns =
        {
            0x034, // 0: NNNWWNWNN (3,4,6)
            0x121, // 1: WNNWNNNNW (0,3,8)
            0x061, // 2: NNWWNNNNW (2,3,8)
            0x160, // 3: WNWWNNNNN (0,2,3)
            0x031, // 4: NNNWWNNNW (3,4,8)
            0x130, // 5: WNNWWNNNN (0,3,4)
            0x070, // 6: NNWWWNNNN (2,3,4)
            0x025, // 7: NNNWNNWNW (3,6,8)
            0x124, // 8: WNNWNNWNN (0,3,6)
            0x064, // 9: NNWWNNWNN (2,3,6)
            0x109, // A: WNNNNWNNW (0,5,8)
            0x049, // B: NNWNNWNNW (2,5,8)
            0x148, // C: WNWNNWNNN (0,2,5)
            0x019, // D: NNNNWWNNW (4,5,8)
            0x118, // E: WNNNWWNNN (0,4,5)
            0x058, // F: NNWNWWNNN (2,4,5)
            0x00D, // G: NNNNNWWNW (5,6,8)
            0x10C, // H: WNNNNWWNN (0,5,6)
            0x04C, // I: NNWNNWWNN (2,5,6)
            0x01C, // J: NNNNWWWNN (4,5,6)
            0x103, // K: WNNNNNNWW (0,7,8)
            0x043, // L: NNWNNNNWW (2,7,8)
            0x142, // M: WNWNNNNWN (0,2,7)
            0x013, // N: NNNNWNNWW (4,7,8)
            0x112, // O: WNNNWNNWN (0,4,7)
            0x052, // P: NNWNWNNWN (2,4,7)
            0x007, // Q: NNNNNNWWW (6,7,8)
            0x106, // R: WNNNNNWWN (0,6,7)
            0x046, // S: NNWNNNWWN (2,6,7)
            0x016, // T: NNNNWNWWN (4,6,7)
            0x181, // U: WWNNNNNNW (0,1,8)
            0x0C1, // V: NWWNNNNNW (1,2,8)
            0x1C0, // W: WWWNNNNNN (0,1,2)
            0x091, // X: NWNNWNNNW (1,4,8)
            0x190, // Y: WWNNWNNNN (0,1,4)
            0x0D0, // Z: NWWNWNNNN (1,2,4)
            0x085, // -: NWNNNNWNW (1,6,8)
            0x184, // .: WWNNNNWNN (0,1,6)
            0x0C4, // ' ': NWWNNNWNN (1,2,6)
            0x0A8, // $: NWNWNWNNN (1,3,5)
            0x0A2, // /: NWNWNNNWN (1,3,7)
            0x08A, // +: NWNNNWNWN (1,5,7)
            0x02A, // %: NNNWNWNWN (3,5,7)
            0x094, // *: NWNNWNWNN (1,4,6)
        };

        // Map ASCII char to pattern index. Returns -1 for invalid characters.
        private static int CharToIndex(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'A' && c <= 'Z') return c - 'A' + 10;
            switch (c)
            {
                case '-': return 36;
                case '.': return 37;
                case ' ': return 38;
                case '$': return 39;
                case '/': return 40;
                case '+': return 41;
                case '%': return 42;
                case '*': return 43;
                default: return -1;
            }
        }

        internal static bool[] Encode(string data, out string displayText)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) throw new ArgumentException("Data must not be empty.", nameof(data));

            data = data.ToUpperInvariant();
            displayText = data;

            foreach (char c in data)
            {
                int idx = CharToIndex(c);
                if (idx < 0 || c == '*')
                    throw new ArgumentException(
                        $"Character '{c}' is not valid in Code 39. " +
                        "Allowed: 0-9, A-Z, - . $ / + % and SPACE.", nameof(data));
            }

            // Full sequence: *{data}*
            int symbolCount = data.Length + 2;
            int totalModules = symbolCount * (6 * NarrowWidth + 3 * WideWidth)
                             + (symbolCount - 1) * NarrowWidth;
            var modules = new bool[totalModules];
            int pos = 0;

            EncodeSymbol(43, modules, ref pos); // start *
            foreach (char c in data)
            {
                pos += NarrowWidth; // inter-character gap
                EncodeSymbol(CharToIndex(c), modules, ref pos);
            }
            pos += NarrowWidth;
            EncodeSymbol(43, modules, ref pos); // stop *

            return modules;
        }

        private static void EncodeSymbol(int index, bool[] modules, ref int pos)
        {
            ushort pattern = Patterns[index];
            for (int i = 0; i < 9; i++)
            {
                bool wide = ((pattern >> (8 - i)) & 1) == 1;
                int width = wide ? WideWidth : NarrowWidth;
                bool isBar = (i % 2 == 0);
                for (int m = 0; m < width; m++)
                    modules[pos++] = isBar;
            }
        }
    }
}
