using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Result of QR code encoding: a 2D boolean grid of modules.
    /// </summary>
    internal sealed class QrResult
    {
        internal bool[,] Modules { get; }
        internal int Size { get; }

        internal QrResult(bool[,] modules, int size)
        {
            Modules = modules;
            Size = size;
        }
    }

    /// <summary>
    /// Encodes data as a QR code using byte-mode encoding.
    /// Supports versions 1-40 and all four error correction levels.
    /// </summary>
    internal static class QrCodeEncoder
    {
        // ── GF(256) Arithmetic ──────────────────────────────────────

        private static readonly byte[] ExpTable = new byte[256];
        private static readonly byte[] LogTable = new byte[256];

        static QrCodeEncoder()
        {
            int val = 1;
            for (int i = 0; i < 256; i++)
            {
                ExpTable[i] = (byte)val;
                if (i < 255)
                    LogTable[val] = (byte)i;
                val <<= 1;
                if (val >= 256)
                    val ^= 0x11D;
            }
        }

        private static byte GfMul(byte a, byte b)
        {
            if (a == 0 || b == 0) return 0;
            return ExpTable[(LogTable[a] + LogTable[b]) % 255];
        }

        // ── EC Block Info (packed flat) ─────────────────────────────
        // Layout: 40 versions × 4 EC levels × 6 fields = 960 ushorts
        // Fields per entry: totalDataCW, ecCWPerBlock, blocks1, data1, blocks2, data2
        // Index: ((version-1)*4 + ecIdx) * 6 + fieldIdx
        private static readonly ushort[] EcData =
        {
            // V1: L, M, Q, H
            19,7,1,19,0,0,  16,10,1,16,0,0,  13,13,1,13,0,0,  9,17,1,9,0,0,
            // V2
            34,10,1,34,0,0,  28,16,1,28,0,0,  22,22,1,22,0,0,  16,28,1,16,0,0,
            // V3
            55,15,1,55,0,0,  44,26,1,44,0,0,  34,18,2,17,0,0,  26,22,2,13,0,0,
            // V4
            80,20,1,80,0,0,  64,18,2,32,0,0,  48,26,2,24,0,0,  36,16,4,9,0,0,
            // V5
            108,26,1,108,0,0,  86,24,2,43,0,0,  62,18,2,15,2,16,  46,22,2,11,2,12,
            // V6
            136,18,2,68,0,0,  108,16,4,27,0,0,  76,24,4,19,0,0,  60,28,4,15,0,0,
            // V7
            156,20,2,78,0,0,  124,18,4,31,0,0,  88,18,2,14,4,15,  66,26,4,13,1,14,
            // V8
            194,24,2,97,0,0,  154,22,2,38,2,39,  110,22,4,18,2,19,  86,26,4,14,2,15,
            // V9
            232,30,2,116,0,0,  182,22,3,36,2,37,  132,20,4,16,4,17,  100,24,4,12,4,13,
            // V10
            274,18,2,68,2,69,  216,26,4,43,1,44,  154,24,6,19,2,20,  122,28,6,15,2,16,
            // V11
            324,20,4,81,0,0,  254,30,1,50,4,51,  180,28,4,22,4,23,  140,24,3,12,8,13,
            // V12
            370,24,2,92,2,93,  290,22,6,36,2,37,  206,26,4,20,6,21,  158,28,7,14,4,15,
            // V13
            428,26,4,107,0,0,  334,22,8,37,1,38,  244,24,8,20,4,21,  180,22,12,11,4,12,
            // V14
            461,30,3,115,1,116,  365,24,4,40,5,41,  261,20,11,16,5,17,  197,24,11,12,5,13,
            // V15
            523,22,5,87,1,88,  415,24,5,41,5,42,  295,30,5,24,7,25,  223,24,11,12,7,13,
            // V16
            589,24,5,98,1,99,  453,28,7,45,3,46,  325,24,15,19,2,20,  253,30,3,15,13,16,
            // V17
            647,28,1,107,5,108,  507,28,10,46,1,47,  367,28,1,22,15,23,  283,28,2,14,17,15,
            // V18
            721,30,5,120,1,121,  563,26,9,43,4,44,  397,28,17,22,1,23,  313,28,2,14,19,15,
            // V19
            795,28,3,113,4,114,  627,26,3,44,11,45,  445,26,17,21,4,22,  341,26,9,13,16,14,
            // V20
            861,28,3,107,5,108,  669,26,3,41,13,42,  485,28,15,24,5,25,  385,28,15,15,10,16,
            // V21
            932,28,4,116,4,117,  714,26,17,42,0,0,  512,30,17,22,6,23,  406,28,19,16,6,17,
            // V22
            1006,28,2,111,7,112,  782,28,17,46,0,0,  568,24,7,24,16,25,  442,30,34,13,0,0,
            // V23
            1094,30,4,121,5,122,  860,28,4,47,14,48,  614,30,11,24,14,25,  464,30,16,15,14,16,
            // V24
            1174,30,6,117,4,118,  914,28,6,45,14,46,  664,30,11,24,16,25,  514,30,30,16,2,17,
            // V25
            1276,26,8,106,4,107,  1000,28,8,47,13,48,  718,30,7,24,22,25,  538,30,22,15,13,16,
            // V26
            1370,28,10,114,2,115,  1062,28,19,46,4,47,  754,28,28,22,6,23,  596,30,33,16,4,17,
            // V27
            1468,30,8,122,4,123,  1128,28,22,45,3,46,  808,30,8,23,26,24,  628,30,12,15,28,16,
            // V28
            1531,30,3,117,10,118,  1193,28,3,45,23,46,  871,30,4,24,31,25,  661,30,11,15,31,16,
            // V29
            1631,30,7,116,7,117,  1267,28,21,45,7,46,  911,30,1,23,37,24,  701,30,19,15,26,16,
            // V30
            1735,30,5,115,10,116,  1373,28,19,47,10,48,  985,30,15,24,25,25,  745,30,23,15,25,16,
            // V31
            1843,30,13,115,3,116,  1455,28,2,46,29,47,  1033,30,42,24,1,25,  793,30,23,15,28,16,
            // V32
            1955,30,17,115,0,0,  1541,28,10,46,23,47,  1115,30,10,24,35,25,  845,30,19,15,35,16,
            // V33
            2071,30,17,115,1,116,  1631,28,14,46,21,47,  1171,30,29,24,19,25,  901,30,11,15,46,16,
            // V34
            2191,30,13,115,6,116,  1725,28,14,46,23,47,  1231,30,44,24,7,25,  961,30,59,16,1,17,
            // V35
            2306,30,12,121,7,122,  1812,28,12,47,26,48,  1286,30,39,24,14,25,  986,30,22,15,41,16,
            // V36
            2434,30,6,121,14,122,  1914,28,6,47,34,48,  1354,30,46,24,10,25,  1054,30,2,15,64,16,
            // V37
            2566,30,17,122,4,123,  1992,28,29,46,14,47,  1426,30,49,24,10,25,  1096,30,24,15,46,16,
            // V38
            2702,30,4,122,18,123,  2102,28,13,46,32,47,  1502,30,48,24,14,25,  1142,30,42,15,32,16,
            // V39
            2812,30,20,117,4,118,  2216,28,40,47,7,48,  1582,30,43,24,22,25,  1222,30,10,15,67,16,
            // V40
            2956,30,19,118,6,119,  2334,28,18,47,31,48,  1666,30,34,24,34,25,  1276,30,20,15,61,16,
        };

        private static int EcLevelIndex(QrErrorCorrection level)
        {
            switch (level)
            {
                case QrErrorCorrection.Low: return 0;
                case QrErrorCorrection.Medium: return 1;
                case QrErrorCorrection.Quartile: return 2;
                case QrErrorCorrection.High: return 3;
                default: return 1;
            }
        }

        /// <summary>Returns the byte-mode data capacity for a given version and EC level.</summary>
        private static int GetByteCapacity(int version, int ecIdx)
        {
            int totalDataCW = EcData[((version - 1) * 4 + ecIdx) * 6];
            int overhead = version <= 9 ? 12 : 20; // mode indicator (4) + char count (8 or 16) bits
            return (totalDataCW * 8 - overhead) / 8;
        }

        /// <summary>Returns the 6 EC-block fields for a version and EC level.</summary>
        private static void GetEcInfo(int version, int ecIdx,
            out int totalData, out int ecPerBlock, out int blocks1, out int data1, out int blocks2, out int data2)
        {
            int i = ((version - 1) * 4 + ecIdx) * 6;
            totalData = EcData[i];
            ecPerBlock = EcData[i + 1];
            blocks1 = EcData[i + 2];
            data1 = EcData[i + 3];
            blocks2 = EcData[i + 4];
            data2 = EcData[i + 5];
        }

        // Alignment pattern center positions per version, packed flat.
        // V1 has no alignment patterns. For V2+, count = (version / 7) + 2.
        // Offsets stored separately for direct indexing.
        private static readonly byte[] AlignPosData =
        {
            6,18,                         // V2
            6,22,                         // V3
            6,26,                         // V4
            6,30,                         // V5
            6,34,                         // V6
            6,22,38,                      // V7
            6,24,42,                      // V8
            6,26,46,                      // V9
            6,28,50,                      // V10
            6,30,54,                      // V11
            6,32,58,                      // V12
            6,34,62,                      // V13
            6,26,46,66,                   // V14
            6,26,48,70,                   // V15
            6,26,50,74,                   // V16
            6,30,54,78,                   // V17
            6,30,56,82,                   // V18
            6,30,58,86,                   // V19
            6,34,62,90,                   // V20
            6,28,50,72,94,               // V21
            6,26,50,74,98,               // V22
            6,30,54,78,102,              // V23
            6,28,54,80,106,              // V24
            6,32,58,84,110,              // V25
            6,30,58,86,114,              // V26
            6,34,62,90,118,              // V27
            6,26,50,74,98,122,           // V28
            6,30,54,78,102,126,          // V29
            6,26,52,78,104,130,          // V30
            6,30,56,82,108,134,          // V31
            6,34,60,86,112,138,          // V32
            6,30,58,86,114,142,          // V33
            6,34,62,90,118,146,          // V34
            6,30,54,78,102,126,150,      // V35
            6,24,50,76,102,128,154,      // V36
            6,28,54,80,106,132,158,      // V37
            6,32,58,84,110,136,162,      // V38
            6,26,54,82,110,138,166,      // V39
            6,30,58,86,114,142,170,      // V40
        };

        // Offset into AlignPosData for each version (V1=unused, V2..V40)
        private static readonly byte[] AlignPosOffset =
        {
            0,   // V1 (unused)
            0,2,4,6,8,                     // V2-V6 (2 each)
            10,13,16,19,22,25,28,          // V7-V13 (3 each)
            31,35,39,43,47,51,55,          // V14-V20 (4 each)
            59,64,69,74,79,84,89,          // V21-V27 (5 each)
            94,100,106,112,118,124,130,    // V28-V34 (6 each)
            136,143,150,157,164,171,       // V35-V40 (7 each)
        };

        private static int GetAlignmentCount(int version)
        {
            return version < 2 ? 0 : (version / 7) + 2;
        }

        // ── Public Entry Point ──────────────────────────────────────

        internal static QrResult Encode(string data, QrErrorCorrection ecLevel)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            int ecIdx = EcLevelIndex(ecLevel);

            // Determine version
            int version = -1;
            for (int v = 1; v <= 40; v++)
            {
                if (dataBytes.Length <= GetByteCapacity(v, ecIdx))
                {
                    version = v;
                    break;
                }
            }
            if (version < 0)
                throw new ArgumentException(
                    $"Data is too large for any QR code version at {ecLevel} error correction " +
                    $"({dataBytes.Length} bytes, max {GetByteCapacity(40, ecIdx)}).", nameof(data));

            int size = 4 * version + 17;

            byte[] codewords = EncodeData(dataBytes, version, ecIdx);
            byte[] finalSequence = AddErrorCorrection(codewords, version, ecIdx);

            var matrix = new bool[size, size];
            var isFunction = new bool[size, size];

            PlaceFunctionPatterns(matrix, isFunction, version, size);
            PlaceDataBits(matrix, isFunction, finalSequence, size);

            int bestMask = 0;
            int bestPenalty = int.MaxValue;
            bool[,] bestMatrix = null;

            for (int mask = 0; mask < 8; mask++)
            {
                var candidate = (bool[,])matrix.Clone();
                ApplyMask(candidate, isFunction, mask, size);
                PlaceFormatInfo(candidate, ecIdx, mask, size);
                if (version >= 7)
                    PlaceVersionInfo(candidate, version, size);

                int penalty = ComputePenalty(candidate, size);
                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    bestMask = mask;
                    bestMatrix = candidate;
                }
            }

            return new QrResult(bestMatrix, size);
        }

        // ── Data Encoding ───────────────────────────────────────────

        private static byte[] EncodeData(byte[] dataBytes, int version, int ecIdx)
        {
            GetEcInfo(version, ecIdx, out int totalDataCodewords, out _, out _, out _, out _, out _);

            var bits = new List<bool>();

            // Mode indicator: byte mode = 0100
            bits.Add(false); bits.Add(true); bits.Add(false); bits.Add(false);

            // Character count indicator
            int countBits = version <= 9 ? 8 : 16;
            for (int i = countBits - 1; i >= 0; i--)
                bits.Add(((dataBytes.Length >> i) & 1) == 1);

            // Data bytes
            foreach (byte b in dataBytes)
                for (int i = 7; i >= 0; i--)
                    bits.Add(((b >> i) & 1) == 1);

            // Terminator (up to 4 zero bits)
            int totalBits = totalDataCodewords * 8;
            int terminatorLen = Math.Min(4, totalBits - bits.Count);
            for (int i = 0; i < terminatorLen; i++)
                bits.Add(false);

            // Pad to byte boundary
            while (bits.Count % 8 != 0)
                bits.Add(false);

            // Pad to capacity with alternating 0xEC, 0x11
            byte[] padBytes = { 0xEC, 0x11 };
            int padIdx = 0;
            while (bits.Count < totalBits)
            {
                byte pb = padBytes[padIdx % 2];
                for (int i = 7; i >= 0; i--)
                    bits.Add(((pb >> i) & 1) == 1);
                padIdx++;
            }

            // Convert to bytes
            var codewords = new byte[totalDataCodewords];
            for (int i = 0; i < totalDataCodewords; i++)
            {
                byte val = 0;
                for (int b = 0; b < 8; b++)
                    if (bits[i * 8 + b])
                        val |= (byte)(1 << (7 - b));
                codewords[i] = val;
            }

            return codewords;
        }

        // ── Error Correction ────────────────────────────────────────

        private static byte[] AddErrorCorrection(byte[] data, int version, int ecIdx)
        {
            GetEcInfo(version, ecIdx, out _, out int ecPerBlock, out int blocks1, out int data1, out int blocks2, out int data2);
            int totalBlocks = blocks1 + blocks2;

            byte[] generator = GenerateGeneratorPolynomial(ecPerBlock);

            var dataBlocks = new byte[totalBlocks][];
            var ecBlocks = new byte[totalBlocks][];
            int dataPos = 0;

            for (int b = 0; b < totalBlocks; b++)
            {
                int blockDataLen = b < blocks1 ? data1 : data2;
                dataBlocks[b] = new byte[blockDataLen];
                Array.Copy(data, dataPos, dataBlocks[b], 0, blockDataLen);
                dataPos += blockDataLen;
                ecBlocks[b] = ComputeReedSolomon(dataBlocks[b], generator, ecPerBlock);
            }

            var result = new List<byte>();
            int maxDataLen = Math.Max(data1, blocks2 > 0 ? data2 : data1);
            for (int i = 0; i < maxDataLen; i++)
                for (int b = 0; b < totalBlocks; b++)
                    if (i < dataBlocks[b].Length)
                        result.Add(dataBlocks[b][i]);

            for (int i = 0; i < ecPerBlock; i++)
                for (int b = 0; b < totalBlocks; b++)
                    result.Add(ecBlocks[b][i]);

            return result.ToArray();
        }

        private static byte[] GenerateGeneratorPolynomial(int degree)
        {
            byte[] poly = { 1 };
            for (int i = 0; i < degree; i++)
            {
                var newPoly = new byte[poly.Length + 1];
                byte factor = ExpTable[i];
                for (int j = 0; j < poly.Length; j++)
                {
                    newPoly[j] ^= poly[j];
                    newPoly[j + 1] ^= GfMul(poly[j], factor);
                }
                poly = newPoly;
            }
            return poly;
        }

        private static byte[] ComputeReedSolomon(byte[] data, byte[] generator, int ecCount)
        {
            var remainder = new byte[generator.Length - 1];
            foreach (byte b in data)
            {
                byte factor = (byte)(b ^ remainder[0]);
                for (int i = 0; i < remainder.Length - 1; i++)
                    remainder[i] = remainder[i + 1];
                remainder[remainder.Length - 1] = 0;
                for (int i = 0; i < remainder.Length; i++)
                    remainder[i] ^= GfMul(generator[i + 1], factor);
            }
            var ec = new byte[ecCount];
            Array.Copy(remainder, ec, ecCount);
            return ec;
        }

        // ── Module Placement ────────────────────────────────────────

        private static void PlaceFunctionPatterns(bool[,] matrix, bool[,] isFunction, int version, int size)
        {
            PlaceFinderPattern(matrix, isFunction, 0, 0, size);
            PlaceFinderPattern(matrix, isFunction, size - 7, 0, size);
            PlaceFinderPattern(matrix, isFunction, 0, size - 7, size);

            for (int i = 0; i < 8; i++)
            {
                MarkFunction(isFunction, i, 7, size);
                MarkFunction(isFunction, 7, i, size);
                MarkFunction(isFunction, size - 8 + i, 7, size);
                MarkFunction(isFunction, size - 8, i, size);
                MarkFunction(isFunction, i, size - 8, size);
                MarkFunction(isFunction, 7, size - 8 + i, size);
            }

            for (int i = 8; i < size - 8; i++)
            {
                matrix[6, i] = (i % 2 == 0);
                isFunction[6, i] = true;
                matrix[i, 6] = (i % 2 == 0);
                isFunction[i, 6] = true;
            }

            if (version >= 2)
            {
                int count = GetAlignmentCount(version);
                int offset = AlignPosOffset[version - 1];
                for (int pi = 0; pi < count; pi++)
                {
                    for (int pj = 0; pj < count; pj++)
                    {
                        int row = AlignPosData[offset + pi];
                        int col = AlignPosData[offset + pj];
                        if (IsFinderRegion(row, col, size))
                            continue;
                        PlaceAlignmentPattern(matrix, isFunction, row, col, size);
                    }
                }
            }

            matrix[version * 4 + 9, 8] = true;
            isFunction[version * 4 + 9, 8] = true;

            for (int i = 0; i < 9; i++)
            {
                MarkFunction(isFunction, 8, i, size);
                MarkFunction(isFunction, i, 8, size);
            }
            for (int i = 0; i < 8; i++)
            {
                MarkFunction(isFunction, 8, size - 1 - i, size);
                MarkFunction(isFunction, size - 1 - i, 8, size);
            }

            if (version >= 7)
            {
                for (int i = 0; i < 6; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        MarkFunction(isFunction, i, size - 11 + j, size);
                        MarkFunction(isFunction, size - 11 + j, i, size);
                    }
            }
        }

        private static void PlaceFinderPattern(bool[,] matrix, bool[,] isFunction, int row, int col, int size)
        {
            for (int r = -1; r <= 7; r++)
            {
                for (int c = -1; c <= 7; c++)
                {
                    int mr = row + r;
                    int mc = col + c;
                    if (mr < 0 || mr >= size || mc < 0 || mc >= size)
                        continue;

                    bool dark;
                    if (r == -1 || r == 7 || c == -1 || c == 7)
                        dark = false;
                    else if (r == 0 || r == 6 || c == 0 || c == 6)
                        dark = true;
                    else if (r >= 2 && r <= 4 && c >= 2 && c <= 4)
                        dark = true;
                    else
                        dark = false;

                    matrix[mr, mc] = dark;
                    isFunction[mr, mc] = true;
                }
            }
        }

        private static void PlaceAlignmentPattern(bool[,] matrix, bool[,] isFunction, int centerRow, int centerCol, int size)
        {
            for (int r = -2; r <= 2; r++)
            {
                for (int c = -2; c <= 2; c++)
                {
                    int mr = centerRow + r;
                    int mc = centerCol + c;
                    if (mr < 0 || mr >= size || mc < 0 || mc >= size)
                        continue;

                    bool dark = (r == -2 || r == 2 || c == -2 || c == 2 || (r == 0 && c == 0));
                    matrix[mr, mc] = dark;
                    isFunction[mr, mc] = true;
                }
            }
        }

        private static bool IsFinderRegion(int row, int col, int size)
        {
            return (row <= 8 && col <= 8) ||
                   (row <= 8 && col >= size - 8) ||
                   (row >= size - 8 && col <= 8);
        }

        private static void MarkFunction(bool[,] isFunction, int row, int col, int size)
        {
            if (row >= 0 && row < size && col >= 0 && col < size)
                isFunction[row, col] = true;
        }

        private static void PlaceDataBits(bool[,] matrix, bool[,] isFunction, byte[] data, int size)
        {
            int bitIdx = 0;
            int totalBits = data.Length * 8;

            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;

                for (int vert = 0; vert < size; vert++)
                {
                    bool upward = ((size - 1 - right) / 2) % 2 == 0;
                    int row = upward ? size - 1 - vert : vert;

                    for (int dx = 0; dx <= 1; dx++)
                    {
                        int col = right - dx;
                        if (col < 0) continue;
                        if (isFunction[row, col]) continue;

                        if (bitIdx < totalBits)
                        {
                            int byteIdx = bitIdx / 8;
                            int bitPos = 7 - (bitIdx % 8);
                            matrix[row, col] = ((data[byteIdx] >> bitPos) & 1) == 1;
                            bitIdx++;
                        }
                    }
                }
            }
        }

        // ── Masking ─────────────────────────────────────────────────

        private static void ApplyMask(bool[,] matrix, bool[,] isFunction, int maskPattern, int size)
        {
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    if (isFunction[row, col]) continue;

                    bool invert;
                    switch (maskPattern)
                    {
                        case 0: invert = (row + col) % 2 == 0; break;
                        case 1: invert = row % 2 == 0; break;
                        case 2: invert = col % 3 == 0; break;
                        case 3: invert = (row + col) % 3 == 0; break;
                        case 4: invert = (row / 2 + col / 3) % 2 == 0; break;
                        case 5: invert = (row * col) % 2 + (row * col) % 3 == 0; break;
                        case 6: invert = ((row * col) % 2 + (row * col) % 3) % 2 == 0; break;
                        case 7: invert = ((row + col) % 2 + (row * col) % 3) % 2 == 0; break;
                        default: invert = false; break;
                    }

                    if (invert)
                        matrix[row, col] = !matrix[row, col];
                }
            }
        }

        // ── Format and Version Info ─────────────────────────────────

        private static void PlaceFormatInfo(bool[,] matrix, int ecIdx, int mask, int size)
        {
            int[] ecBits = { 0b01, 0b00, 0b11, 0b10 };
            int formatData = (ecBits[ecIdx] << 3) | mask;

            int encoded = formatData << 10;
            int generator = 0b10100110111;
            int temp = encoded;
            for (int i = 4; i >= 0; i--)
            {
                if ((temp >> (i + 10)) != 0)
                    temp ^= generator << i;
            }
            encoded = (formatData << 10) | temp;
            encoded ^= 0b101010000010010;

            int[] rowPositions = { 0, 1, 2, 3, 4, 5, 7, 8, size - 7, size - 6, size - 5, size - 4, size - 3, size - 2, size - 1 };
            int[] colPositions = { size - 1, size - 2, size - 3, size - 4, size - 5, size - 6, size - 7, size - 8, 7, 5, 4, 3, 2, 1, 0 };

            for (int i = 0; i < 15; i++)
            {
                bool bit = ((encoded >> i) & 1) == 1;
                matrix[8, colPositions[i]] = bit;
                matrix[rowPositions[i], 8] = bit;
            }
        }

        private static void PlaceVersionInfo(bool[,] matrix, int version, int size)
        {
            int versionData = version;
            int encoded = versionData << 12;
            int generator = 0b1111100100101;
            int temp = encoded;
            for (int i = 5; i >= 0; i--)
            {
                if ((temp >> (i + 12)) != 0)
                    temp ^= generator << i;
            }
            encoded = (versionData << 12) | temp;

            for (int i = 0; i < 18; i++)
            {
                bool bit = ((encoded >> i) & 1) == 1;
                int row = i / 3;
                int col = size - 11 + (i % 3);
                matrix[row, col] = bit;
                matrix[col, row] = bit;
            }
        }

        // ── Penalty Scoring ─────────────────────────────────────────

        private static int ComputePenalty(bool[,] matrix, int size)
        {
            int penalty = 0;

            for (int row = 0; row < size; row++)
            {
                int run = 1;
                for (int col = 1; col < size; col++)
                {
                    if (matrix[row, col] == matrix[row, col - 1]) { run++; }
                    else { if (run >= 5) penalty += run - 2; run = 1; }
                }
                if (run >= 5) penalty += run - 2;
            }

            for (int col = 0; col < size; col++)
            {
                int run = 1;
                for (int row = 1; row < size; row++)
                {
                    if (matrix[row, col] == matrix[row - 1, col]) { run++; }
                    else { if (run >= 5) penalty += run - 2; run = 1; }
                }
                if (run >= 5) penalty += run - 2;
            }

            for (int row = 0; row < size - 1; row++)
                for (int col = 0; col < size - 1; col++)
                {
                    bool val = matrix[row, col];
                    if (val == matrix[row, col + 1] && val == matrix[row + 1, col] && val == matrix[row + 1, col + 1])
                        penalty += 3;
                }

            for (int row = 0; row < size; row++)
                for (int col = 0; col <= size - 11; col++)
                    if (MatchesFinderLikePattern(matrix, row, col, true))
                        penalty += 40;

            for (int col = 0; col < size; col++)
                for (int row = 0; row <= size - 11; row++)
                    if (MatchesFinderLikePattern(matrix, row, col, false))
                        penalty += 40;

            int darkCount = 0;
            for (int row = 0; row < size; row++)
                for (int col = 0; col < size; col++)
                    if (matrix[row, col]) darkCount++;

            int totalModules = size * size;
            int percent = (darkCount * 100) / totalModules;
            int prevFive = (percent / 5) * 5;
            int nextFive = prevFive + 5;
            penalty += Math.Min(Math.Abs(prevFive - 50) / 5, Math.Abs(nextFive - 50) / 5) * 10;

            return penalty;
        }

        // Finder-like patterns as bitmasks: 10111010000 and 00001011101
        private const ushort FinderPattern1 = 0b10111010000;
        private const ushort FinderPattern2 = 0b00001011101;

        private static bool MatchesFinderLikePattern(bool[,] matrix, int row, int col, bool horizontal)
        {
            ushort bits = 0;
            for (int i = 0; i < 11; i++)
            {
                bool val = horizontal ? matrix[row, col + i] : matrix[row + i, col];
                if (val) bits |= (ushort)(1 << (10 - i));
            }
            return bits == FinderPattern1 || bits == FinderPattern2;
        }
    }
}
