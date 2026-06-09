using System;
using System.Collections.Generic;
using System.Globalization;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Minimal ASN.1 DER (Distinguished Encoding Rules) encoder for building PKCS#7 structures.
    /// </summary>
    internal static class DerEncoder
    {
        // ── Tag constants ──

        internal const byte TagInteger = 0x02;
        internal const byte TagBitString = 0x03;
        internal const byte TagOctetString = 0x04;
        internal const byte TagNull = 0x05;
        internal const byte TagOid = 0x06;
        internal const byte TagUtf8String = 0x0C;
        internal const byte TagPrintableString = 0x13;
        internal const byte TagIa5String = 0x16;
        internal const byte TagUtcTime = 0x17;
        internal const byte TagGeneralizedTime = 0x18;
        internal const byte TagSequence = 0x30;
        internal const byte TagSet = 0x31;

        // ── Length encoding ──

        internal static byte[] EncodeLength(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (length < 128)
                return new byte[] { (byte)length };
            if (length < 256)
                return new byte[] { 0x81, (byte)length };
            if (length < 65536)
                return new byte[] { 0x82, (byte)(length >> 8), (byte)(length & 0xFF) };
            return new byte[]
            {
                0x83,
                (byte)(length >> 16),
                (byte)((length >> 8) & 0xFF),
                (byte)(length & 0xFF)
            };
        }

        // ── Primitive wrappers ──

        internal static byte[] Wrap(byte tag, byte[] data)
        {
            var len = EncodeLength(data.Length);
            var result = new byte[1 + len.Length + data.Length];
            result[0] = tag;
            Buffer.BlockCopy(len, 0, result, 1, len.Length);
            Buffer.BlockCopy(data, 0, result, 1 + len.Length, data.Length);
            return result;
        }

        internal static byte[] Sequence(params byte[][] children)
            => Wrap(TagSequence, Concat(children));

        internal static byte[] Set(params byte[][] children)
            => Wrap(TagSet, Concat(children));

        internal static byte[] ContextExplicit(int tagNumber, byte[] data)
            => Wrap((byte)(0xA0 | tagNumber), data);

        internal static byte[] ContextImplicit(int tagNumber, byte[] data)
            => Wrap((byte)(0xA0 | tagNumber), data);

        internal static byte[] Integer(byte[] value)
        {
            // Strip leading zeros but keep at least one byte
            int start = 0;
            while (start < value.Length - 1 && value[start] == 0 && (value[start + 1] & 0x80) == 0)
                start++;

            // If high bit is set, prepend 0x00 for positive sign
            if ((value[start] & 0x80) != 0)
            {
                var padded = new byte[value.Length - start + 1];
                padded[0] = 0;
                Buffer.BlockCopy(value, start, padded, 1, value.Length - start);
                return Wrap(TagInteger, padded);
            }

            if (start == 0)
                return Wrap(TagInteger, value);

            var trimmed = new byte[value.Length - start];
            Buffer.BlockCopy(value, start, trimmed, 0, trimmed.Length);
            return Wrap(TagInteger, trimmed);
        }

        internal static byte[] IntegerFromInt(int value)
        {
            // Encode a small non-negative integer
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value < 128)
                return Wrap(TagInteger, new byte[] { (byte)value });
            if (value < 32768)
                return Wrap(TagInteger, new byte[] { (byte)(value >> 8), (byte)(value & 0xFF) });
            return Integer(new byte[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            });
        }

        internal static byte[] OctetString(byte[] data)
            => Wrap(TagOctetString, data);

        internal static byte[] BitString(byte[] data)
        {
            // Prepend unused-bits byte (always 0 for whole-byte data)
            var padded = new byte[data.Length + 1];
            padded[0] = 0; // 0 unused bits
            Buffer.BlockCopy(data, 0, padded, 1, data.Length);
            return Wrap(TagBitString, padded);
        }

        internal static byte[] Null() => new byte[] { TagNull, 0x00 };

        internal static byte[] UtcTime(DateTime dt)
        {
            // Format: YYMMDDHHMMSSZ
            var s = dt.ToUniversalTime().ToString("yyMMddHHmmss", CultureInfo.InvariantCulture) + "Z";
            var data = System.Text.Encoding.ASCII.GetBytes(s);
            return Wrap(TagUtcTime, data);
        }

        internal static byte[] GeneralizedTime(DateTime dt)
        {
            // Format: YYYYMMDDHHMMSSZ
            var s = dt.ToUniversalTime().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "Z";
            var data = System.Text.Encoding.ASCII.GetBytes(s);
            return Wrap(TagGeneralizedTime, data);
        }

        internal static byte[] PrintableString(string text)
        {
            var data = System.Text.Encoding.ASCII.GetBytes(text);
            return Wrap(TagPrintableString, data);
        }

        // ── OID encoding ──

        internal static byte[] ObjectIdentifier(string oid)
        {
            var parts = oid.Split('.');
            if (parts.Length < 2) throw new ArgumentException("OID must have at least two components.", nameof(oid));

            var components = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                components[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);

            var bytes = new List<byte>();

            // First two components packed: 40 * c0 + c1
            bytes.Add((byte)(40 * components[0] + components[1]));

            // Remaining components in base-128
            for (int i = 2; i < components.Length; i++)
                EncodeBase128(bytes, components[i]);

            return Wrap(TagOid, bytes.ToArray());
        }

        private static void EncodeBase128(List<byte> output, int value)
        {
            if (value < 128)
            {
                output.Add((byte)value);
                return;
            }

            // Collect base-128 digits
            var digits = new List<byte>();
            int v = value;
            while (v > 0)
            {
                digits.Add((byte)(v & 0x7F));
                v >>= 7;
            }

            // Output in big-endian order with continuation bits
            for (int i = digits.Count - 1; i >= 0; i--)
            {
                byte b = digits[i];
                if (i > 0) b |= 0x80; // continuation bit
                output.Add(b);
            }
        }

        // ── Utilities ──

        internal static byte[] Concat(params byte[][] arrays)
        {
            int total = 0;
            for (int i = 0; i < arrays.Length; i++)
                total += arrays[i].Length;

            var result = new byte[total];
            int offset = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                Buffer.BlockCopy(arrays[i], 0, result, offset, arrays[i].Length);
                offset += arrays[i].Length;
            }
            return result;
        }

        internal static byte[] Boolean(bool value)
            => Wrap(0x01, new byte[] { value ? (byte)0xFF : (byte)0x00 });

        // ── DER parsing helpers (for reading TSA responses) ──

        internal static byte ReadTag(byte[] data, ref int pos)
        {
            if (pos >= data.Length) throw new InvalidOperationException("Unexpected end of DER data.");
            return data[pos++];
        }

        internal static int ReadLength(byte[] data, ref int pos)
        {
            if (pos >= data.Length) throw new InvalidOperationException("Unexpected end of DER data.");
            byte b = data[pos++];
            if (b < 128)
                return b;
            int numBytes = b & 0x7F;
            if (numBytes == 0 || numBytes > 4)
                throw new InvalidOperationException($"Unsupported DER length encoding: {numBytes} bytes.");
            int length = 0;
            for (int i = 0; i < numBytes; i++)
            {
                if (pos >= data.Length) throw new InvalidOperationException("Unexpected end of DER data.");
                length = (length << 8) | data[pos++];
            }
            return length;
        }

        internal static byte[] ReadContents(byte[] data, ref int pos, byte expectedTag)
        {
            byte tag = ReadTag(data, ref pos);
            if (tag != expectedTag)
                throw new InvalidOperationException(
                    $"Expected DER tag 0x{expectedTag:X2} but got 0x{tag:X2}.");
            int length = ReadLength(data, ref pos);
            var result = new byte[length];
            Buffer.BlockCopy(data, pos, result, 0, length);
            pos += length;
            return result;
        }

        internal static int ReadIntegerValue(byte[] data, ref int pos)
        {
            var bytes = ReadContents(data, ref pos, TagInteger);
            int value = 0;
            for (int i = 0; i < bytes.Length; i++)
                value = (value << 8) | bytes[i];
            return value;
        }

        internal static void SkipTlv(byte[] data, ref int pos)
        {
            if (pos >= data.Length) return;
            pos++; // skip tag
            int length = ReadLength(data, ref pos);
            pos += length;
        }
    }
}
