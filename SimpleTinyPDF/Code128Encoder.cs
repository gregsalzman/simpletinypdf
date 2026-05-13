using System;
using System.Collections.Generic;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Encodes data as a Code 128 barcode with automatic subset selection (A, B, C).
    /// </summary>
    internal static class Code128Encoder
    {
        private const int StartA = 103;
        private const int StartB = 104;
        private const int StartC = 105;
        private const int CodeA = 101;
        private const int CodeB = 100;
        private const int CodeC = 99;

        // Each codeword's 6 bar/space widths (values 1-4) packed into a ushort.
        // Each width stored as (width-1) in 2 bits: bits 11-10=w0, 9-8=w1, ..., 1-0=w5.
        private static readonly ushort[] PackedWidths =
        {
            0x455,0x545,0x554,0x116,0x125,0x215,0x152,0x161,
            0x251,0x512,0x521,0x611,0x059,0x149,0x158,0x095,
            0x185,0x194,0x590,0x509,0x518,0x491,0x581,0x848,
            0x815,0x905,0x914,0x851,0x941,0x950,0x446,0x464,
            0x644,0x026,0x206,0x224,0x062,0x242,0x260,0x422,
            0x602,0x620,0x04A,0x068,0x248,0x086,0x0A4,0x284,
            0x884,0x428,0x608,0x482,0x4A0,0x488,0x806,0x824,
            0xA04,0x842,0x860,0xA40,0x8C0,0x530,0xE00,0x017,
            0x035,0x107,0x134,0x305,0x314,0x053,0x071,0x143,
            0x170,0x341,0x350,0x710,0x503,0xC80,0x701,0x2C0,
            0x01D,0x10D,0x11C,0x0D1,0x1C1,0x1D0,0xC11,0xD01,
            0xD10,0x44C,0x4C4,0xC44,0x00E,0x02C,0x20C,0x0C2,
            0x0E0,0xC02,0xC20,0x08C,0x0C8,0x80C,0xC08,0x431,
            0x413,0x419,
        };

        // Stop pattern: 7 elements = 13 modules
        private static readonly byte[] StopWidths = { 2, 3, 3, 1, 1, 1, 2 };

        internal static bool[] Encode(string data, out string displayText)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) throw new ArgumentException("Data must not be empty.", nameof(data));

            displayText = data;

            var codewords = new List<int>();
            int i = 0;

            int currentSubset;
            if (CanStartWithC(data, 0))
            {
                codewords.Add(StartC);
                currentSubset = 'C';
            }
            else if (NeedsSubsetA(data, 0))
            {
                codewords.Add(StartA);
                currentSubset = 'A';
            }
            else
            {
                codewords.Add(StartB);
                currentSubset = 'B';
            }

            while (i < data.Length)
            {
                if (currentSubset == 'C')
                {
                    if (i + 1 < data.Length && IsDigit(data[i]) && IsDigit(data[i + 1]))
                    {
                        codewords.Add((data[i] - '0') * 10 + (data[i + 1] - '0'));
                        i += 2;
                    }
                    else
                    {
                        if (NeedsSubsetA(data, i))
                        {
                            codewords.Add(CodeA);
                            currentSubset = 'A';
                        }
                        else
                        {
                            codewords.Add(CodeB);
                            currentSubset = 'B';
                        }
                    }
                }
                else if (currentSubset == 'B')
                {
                    if (CanSwitchToC(data, i))
                    {
                        codewords.Add(CodeC);
                        currentSubset = 'C';
                    }
                    else
                    {
                        char c = data[i];
                        if (c < 32 || c > 127)
                            throw new ArgumentException(
                                $"Character '{c}' (0x{(int)c:X2}) is not encodable in Code 128.", nameof(data));
                        codewords.Add(c - 32);
                        i++;
                    }
                }
                else // Subset A
                {
                    if (CanSwitchToC(data, i))
                    {
                        codewords.Add(CodeC);
                        currentSubset = 'C';
                    }
                    else
                    {
                        char c = data[i];
                        if (c < 0 || c > 95)
                        {
                            codewords.Add(CodeB);
                            currentSubset = 'B';
                            continue;
                        }
                        codewords.Add(c < 32 ? c + 64 : c - 32);
                        i++;
                    }
                }
            }

            // Check digit
            int checkSum = codewords[0];
            for (int j = 1; j < codewords.Count; j++)
                checkSum += codewords[j] * j;
            codewords.Add(checkSum % 103);

            // Convert to modules: each codeword = 11 modules, stop = 13 modules
            int totalModules = codewords.Count * 11 + 13;
            var modules = new bool[totalModules];
            int pos = 0;

            foreach (int cw in codewords)
                ExpandCodeword(PackedWidths[cw], modules, ref pos);

            ExpandWidths(StopWidths, modules, ref pos);

            return modules;
        }

        private static void ExpandCodeword(ushort packed, bool[] modules, ref int pos)
        {
            for (int i = 0; i < 6; i++)
            {
                int width = ((packed >> (10 - i * 2)) & 3) + 1;
                bool isBar = (i % 2 == 0);
                for (int m = 0; m < width; m++)
                    modules[pos++] = isBar;
            }
        }

        private static void ExpandWidths(byte[] widths, bool[] modules, ref int pos)
        {
            for (int i = 0; i < widths.Length; i++)
            {
                bool isBar = (i % 2 == 0);
                for (int m = 0; m < widths[i]; m++)
                    modules[pos++] = isBar;
            }
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static bool CanStartWithC(string data, int pos)
        {
            int consecutive = CountConsecutiveDigits(data, pos);
            return consecutive >= 4 || (consecutive >= 2 && consecutive == data.Length - pos);
        }

        private static bool CanSwitchToC(string data, int pos)
        {
            int consecutive = CountConsecutiveDigits(data, pos);
            return consecutive >= 4 || (consecutive >= 2 && pos + consecutive == data.Length);
        }

        private static bool NeedsSubsetA(string data, int pos)
        {
            return pos < data.Length && data[pos] < 32;
        }

        private static int CountConsecutiveDigits(string data, int pos)
        {
            int count = 0;
            while (pos + count < data.Length && IsDigit(data[pos + count]))
                count++;
            return count;
        }
    }
}
