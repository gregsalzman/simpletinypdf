using System;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Encodes data as an EAN-13 barcode. Also handles UPC-A (which is EAN-13 with a leading zero).
    /// </summary>
    internal static class Ean13Encoder
    {
        // L-code patterns (odd parity) for left-side digits
        private static readonly byte[][] LPatterns =
        {
            new byte[] { 0,0,0,1,1,0,1 }, // 0
            new byte[] { 0,0,1,1,0,0,1 }, // 1
            new byte[] { 0,0,1,0,0,1,1 }, // 2
            new byte[] { 0,1,1,1,1,0,1 }, // 3
            new byte[] { 0,1,0,0,0,1,1 }, // 4
            new byte[] { 0,1,1,0,0,0,1 }, // 5
            new byte[] { 0,1,0,1,1,1,1 }, // 6
            new byte[] { 0,1,1,1,0,1,1 }, // 7
            new byte[] { 0,1,1,0,1,1,1 }, // 8
            new byte[] { 0,0,0,1,0,1,1 }, // 9
        };

        // G-code patterns (even parity) for left-side digits
        private static readonly byte[][] GPatterns =
        {
            new byte[] { 0,1,0,0,1,1,1 }, // 0
            new byte[] { 0,1,1,0,0,1,1 }, // 1
            new byte[] { 0,0,1,1,0,1,1 }, // 2
            new byte[] { 0,1,0,0,0,0,1 }, // 3
            new byte[] { 0,0,1,1,1,0,1 }, // 4
            new byte[] { 0,1,1,1,0,0,1 }, // 5
            new byte[] { 0,0,0,0,1,0,1 }, // 6
            new byte[] { 0,0,1,0,0,0,1 }, // 7
            new byte[] { 0,0,0,1,0,0,1 }, // 8
            new byte[] { 0,0,1,0,1,1,1 }, // 9
        };

        // R-code patterns for right-side digits
        private static readonly byte[][] RPatterns =
        {
            new byte[] { 1,1,1,0,0,1,0 }, // 0
            new byte[] { 1,1,0,0,1,1,0 }, // 1
            new byte[] { 1,1,0,1,1,0,0 }, // 2
            new byte[] { 1,0,0,0,0,1,0 }, // 3
            new byte[] { 1,0,1,1,1,0,0 }, // 4
            new byte[] { 1,0,0,1,1,1,0 }, // 5
            new byte[] { 1,0,1,0,0,0,0 }, // 6
            new byte[] { 1,0,0,0,1,0,0 }, // 7
            new byte[] { 1,0,0,1,0,0,0 }, // 8
            new byte[] { 1,1,1,0,1,0,0 }, // 9
        };

        // Parity encoding table: for each first digit, defines whether left-side
        // digits 2-7 use L or G encoding. 0 = L, 1 = G.
        private static readonly byte[][] ParityTable =
        {
            new byte[] { 0,0,0,0,0,0 }, // 0: LLLLLL
            new byte[] { 0,0,1,0,1,1 }, // 1: LLGLGG
            new byte[] { 0,0,1,1,0,1 }, // 2: LLGGLG
            new byte[] { 0,0,1,1,1,0 }, // 3: LLGGGL
            new byte[] { 0,1,0,0,1,1 }, // 4: LGLLGG
            new byte[] { 0,1,1,0,0,1 }, // 5: LGGLG -- wait, LGGLLG
            new byte[] { 0,1,1,1,0,0 }, // 6: LGGGLL
            new byte[] { 0,1,0,1,0,1 }, // 7: LGLGLG
            new byte[] { 0,1,0,1,1,0 }, // 8: LGLGGL
            new byte[] { 0,1,1,0,1,0 }, // 9: LGGLGL
        };

        internal static bool[] Encode(string data, out string displayText)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            // Accept 12 digits (compute check) or 13 digits (verify check)
            if (data.Length == 12)
            {
                data = data + ComputeCheckDigit(data);
            }
            else if (data.Length == 13)
            {
                char expected = ComputeCheckDigit(data.Substring(0, 12));
                if (data[12] != expected)
                    throw new ArgumentException(
                        $"Invalid check digit: expected '{expected}', got '{data[12]}'.", nameof(data));
            }
            else
            {
                throw new ArgumentException("EAN-13 data must be 12 or 13 digits.", nameof(data));
            }

            foreach (char c in data)
            {
                if (c < '0' || c > '9')
                    throw new ArgumentException("EAN-13 data must contain only digits.", nameof(data));
            }

            displayText = data;
            int[] digits = new int[13];
            for (int i = 0; i < 13; i++)
                digits[i] = data[i] - '0';

            // EAN-13 structure: 95 modules total
            // Start guard (3) + left digits (42) + center guard (5) + right digits (42) + end guard (3)
            var modules = new bool[95];
            int pos = 0;

            // Start guard: 101
            modules[pos++] = true;
            modules[pos++] = false;
            modules[pos++] = true;

            // Left side: 6 digits (digits[1]-digits[6]) using L/G encoding per parity table
            byte[] parity = ParityTable[digits[0]];
            for (int i = 0; i < 6; i++)
            {
                byte[] pattern = parity[i] == 0 ? LPatterns[digits[i + 1]] : GPatterns[digits[i + 1]];
                for (int j = 0; j < 7; j++)
                    modules[pos++] = pattern[j] == 1;
            }

            // Center guard: 01010
            modules[pos++] = false;
            modules[pos++] = true;
            modules[pos++] = false;
            modules[pos++] = true;
            modules[pos++] = false;

            // Right side: 6 digits (digits[7]-digits[12]) using R encoding
            for (int i = 0; i < 6; i++)
            {
                byte[] pattern = RPatterns[digits[i + 7]];
                for (int j = 0; j < 7; j++)
                    modules[pos++] = pattern[j] == 1;
            }

            // End guard: 101
            modules[pos++] = true;
            modules[pos++] = false;
            modules[pos++] = true;

            return modules;
        }

        internal static bool[] EncodeUpcA(string data, out string displayText)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            // UPC-A is 11 or 12 digits; convert to EAN-13 by prepending "0"
            if (data.Length == 11 || data.Length == 12)
            {
                var result = Encode("0" + data, out displayText);
                // Display text for UPC-A omits the leading zero
                displayText = displayText.Substring(1);
                return result;
            }

            throw new ArgumentException("UPC-A data must be 11 or 12 digits.", nameof(data));
        }

        private static char ComputeCheckDigit(string first12)
        {
            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int d = first12[i] - '0';
                sum += (i % 2 == 0) ? d : d * 3;
            }
            int check = (10 - (sum % 10)) % 10;
            return (char)('0' + check);
        }
    }
}
