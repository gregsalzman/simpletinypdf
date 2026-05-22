using System;
using System.IO;
using System.Security.Cryptography;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Handles PDF encryption key derivation and data encryption for AES-128 (V4/R4) and AES-256 (V5/R6).
    /// </summary>
    internal sealed class PdfEncryptor
    {
        // Standard 32-byte password padding from PDF specification (Table 3.19 / ISO 32000-1 7.6.3.3)
        private static readonly byte[] PasswordPadding =
        {
            0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
            0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
            0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
            0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
        };

        private readonly PdfEncryptionLevel _level;
        private readonly byte[] _fileEncryptionKey;

        internal byte[] OValue { get; }
        internal byte[] UValue { get; }
        internal byte[] OEValue { get; }   // V5/R6 only
        internal byte[] UEValue { get; }   // V5/R6 only
        internal byte[] PermsValue { get; } // V5/R6 only
        internal int PValue { get; }

        internal PdfEncryptor(PdfEncryptionOptions options, byte[] fileId)
        {
            _level = options.Level;
            PValue = ComputePValue(options.Permissions);

            if (_level == PdfEncryptionLevel.Aes128)
            {
                var ownerPwd = PadPassword(options.OwnerPassword);
                var userPwd = PadPassword(options.UserPassword);
                OValue = ComputeOValueV4(ownerPwd, userPwd);
                _fileEncryptionKey = ComputeFileKeyV4(userPwd, OValue, PValue, fileId);
                UValue = ComputeUValueV4(_fileEncryptionKey, fileId);
            }
            else
            {
                // AES-256 (V5/R6): generate random 32-byte file encryption key
                _fileEncryptionKey = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                    rng.GetBytes(_fileEncryptionKey);

                var userPwdBytes = TruncatePassword256(options.UserPassword);
                var ownerPwdBytes = TruncatePassword256(options.OwnerPassword);

                // Compute U and UE first (O depends on U)
                UValue = ComputeUValueR6(userPwdBytes);
                UEValue = ComputeUEValueR6(userPwdBytes);
                OValue = ComputeOValueR6(ownerPwdBytes);
                OEValue = ComputeOEValueR6(ownerPwdBytes);
                PermsValue = ComputePermsValueR6();
            }
        }

        internal byte[] EncryptStream(byte[] data, int objectNumber, int generationNumber)
        {
            if (_level == PdfEncryptionLevel.Aes128)
            {
                var objKey = DeriveObjectKeyV4(objectNumber, generationNumber);
                return AesCbcEncrypt(objKey, data);
            }
            else
            {
                return AesCbcEncrypt(_fileEncryptionKey, data);
            }
        }

        internal byte[] EncryptString(byte[] data, int objectNumber, int generationNumber)
        {
            // String encryption uses the same algorithm as stream encryption
            return EncryptStream(data, objectNumber, generationNumber);
        }

        internal PdfDict BuildEncryptionDict()
        {
            var dict = new PdfDict();

            if (_level == PdfEncryptionLevel.Aes128)
            {
                dict.Set("Type", "/Encrypt");
                dict.Set("Filter", "/Standard");
                dict.Set("V", "4");
                dict.Set("R", "4");
                dict.Set("Length", "128");
                dict.Set("CF", "<< /StdCF << /Type /CryptFilter /CFM /AESV2 /Length 16 >> >>");
                dict.Set("StmF", "/StdCF");
                dict.Set("StrF", "/StdCF");
                dict.Set("O", "<" + BytesToHex(OValue) + ">");
                dict.Set("U", "<" + BytesToHex(UValue) + ">");
                dict.Set("P", PValue.ToString());
            }
            else
            {
                dict.Set("Type", "/Encrypt");
                dict.Set("Filter", "/Standard");
                dict.Set("V", "5");
                dict.Set("R", "6");
                dict.Set("Length", "256");
                dict.Set("CF", "<< /StdCF << /Type /CryptFilter /CFM /AESV3 /Length 32 >> >>");
                dict.Set("StmF", "/StdCF");
                dict.Set("StrF", "/StdCF");
                dict.Set("O", "<" + BytesToHex(OValue) + ">");
                dict.Set("U", "<" + BytesToHex(UValue) + ">");
                dict.Set("OE", "<" + BytesToHex(OEValue) + ">");
                dict.Set("UE", "<" + BytesToHex(UEValue) + ">");
                dict.Set("Perms", "<" + BytesToHex(PermsValue) + ">");
                dict.Set("P", PValue.ToString());
            }

            return dict;
        }

        // ── AES-128 (V4/R4) ─────────────────────────────────────────────

        /// <summary>Algorithm 2: compute file encryption key for V4/R4.</summary>
        private static byte[] ComputeFileKeyV4(byte[] paddedUserPwd, byte[] oValue, int pValue, byte[] fileId)
        {
            using (var md5 = MD5.Create())
            {
                // Step a-d: MD5(password + O + P + fileID)
                var input = new byte[paddedUserPwd.Length + oValue.Length + 4 + fileId.Length];
                int offset = 0;
                Buffer.BlockCopy(paddedUserPwd, 0, input, offset, paddedUserPwd.Length);
                offset += paddedUserPwd.Length;
                Buffer.BlockCopy(oValue, 0, input, offset, oValue.Length);
                offset += oValue.Length;
                input[offset++] = (byte)(pValue & 0xFF);
                input[offset++] = (byte)((pValue >> 8) & 0xFF);
                input[offset++] = (byte)((pValue >> 16) & 0xFF);
                input[offset++] = (byte)((pValue >> 24) & 0xFF);
                Buffer.BlockCopy(fileId, 0, input, offset, fileId.Length);

                var hash = md5.ComputeHash(input);

                // Step e: for R >= 3, iterate MD5 50 times
                for (int i = 0; i < 50; i++)
                    hash = md5.ComputeHash(hash, 0, 16);

                // 16-byte key for AES-128
                var key = new byte[16];
                Buffer.BlockCopy(hash, 0, key, 0, 16);
                return key;
            }
        }

        /// <summary>Algorithm 3: compute owner password value (/O) for V4/R4.</summary>
        private static byte[] ComputeOValueV4(byte[] paddedOwnerPwd, byte[] paddedUserPwd)
        {
            using (var md5 = MD5.Create())
            {
                // Step a-b: MD5(owner password)
                var hash = md5.ComputeHash(paddedOwnerPwd);

                // Step c: for R >= 3, iterate MD5 50 times
                for (int i = 0; i < 50; i++)
                    hash = md5.ComputeHash(hash);

                // Step d: use first 16 bytes as RC4 key
                var key = new byte[16];
                Buffer.BlockCopy(hash, 0, key, 0, 16);

                // Step e: RC4-encrypt the padded user password
                var encrypted = Rc4.Transform(key, paddedUserPwd);

                // Step f: for R >= 3, do 19 more RC4 rounds with modified keys
                for (int i = 1; i <= 19; i++)
                {
                    var modKey = new byte[16];
                    for (int j = 0; j < 16; j++)
                        modKey[j] = (byte)(key[j] ^ i);
                    encrypted = Rc4.Transform(modKey, encrypted);
                }

                return encrypted; // 32 bytes
            }
        }

        /// <summary>Algorithm 5: compute user password value (/U) for R=4.</summary>
        private static byte[] ComputeUValueV4(byte[] fileKey, byte[] fileId)
        {
            using (var md5 = MD5.Create())
            {
                // Step a: MD5(padding + fileID)
                var input = new byte[PasswordPadding.Length + fileId.Length];
                Buffer.BlockCopy(PasswordPadding, 0, input, 0, PasswordPadding.Length);
                Buffer.BlockCopy(fileId, 0, input, PasswordPadding.Length, fileId.Length);
                var hash = md5.ComputeHash(input);

                // Step b: RC4-encrypt with file key
                var encrypted = Rc4.Transform(fileKey, hash);

                // Step c: 19 more RC4 rounds with modified keys
                for (int i = 1; i <= 19; i++)
                {
                    var modKey = new byte[fileKey.Length];
                    for (int j = 0; j < fileKey.Length; j++)
                        modKey[j] = (byte)(fileKey[j] ^ i);
                    encrypted = Rc4.Transform(modKey, encrypted);
                }

                // Step d: pad to 32 bytes with arbitrary data
                var result = new byte[32];
                Buffer.BlockCopy(encrypted, 0, result, 0, 16);
                // Remaining 16 bytes are zero (acceptable arbitrary padding)
                return result;
            }
        }

        /// <summary>Derive per-object encryption key for AES-128.</summary>
        private byte[] DeriveObjectKeyV4(int objectNumber, int generationNumber)
        {
            using (var md5 = MD5.Create())
            {
                // fileKey + objNum (3 bytes LE) + genNum (2 bytes LE) + "sAlT"
                var input = new byte[_fileEncryptionKey.Length + 5 + 4];
                Buffer.BlockCopy(_fileEncryptionKey, 0, input, 0, _fileEncryptionKey.Length);
                int offset = _fileEncryptionKey.Length;
                input[offset++] = (byte)(objectNumber & 0xFF);
                input[offset++] = (byte)((objectNumber >> 8) & 0xFF);
                input[offset++] = (byte)((objectNumber >> 16) & 0xFF);
                input[offset++] = (byte)(generationNumber & 0xFF);
                input[offset++] = (byte)((generationNumber >> 8) & 0xFF);
                // "sAlT" for AES
                input[offset++] = 0x73; // 's'
                input[offset++] = 0x41; // 'A'
                input[offset++] = 0x6C; // 'l'
                input[offset++] = 0x54; // 'T'

                var hash = md5.ComputeHash(input);

                // Truncate to min(keyLength/8 + 5, 16) = min(16+5, 16) = 16
                var key = new byte[16];
                Buffer.BlockCopy(hash, 0, key, 0, 16);
                return key;
            }
        }

        // ── AES-256 (V5/R6) ─────────────────────────────────────────────

        /// <summary>Compute /U value for R6: hash(32) + validationSalt(8) + keySalt(8) = 48 bytes.</summary>
        private byte[] ComputeUValueR6(byte[] userPwd)
        {
            var validationSalt = RandomBytes(8);
            var keySalt = RandomBytes(8);

            // hash = Algorithm 2.B(password + validationSalt, password, "")
            var input = Concat(userPwd, validationSalt);
            var hash = ComputeHashR6(input, userPwd, Array.Empty<byte>());

            var result = new byte[48];
            Buffer.BlockCopy(hash, 0, result, 0, 32);
            Buffer.BlockCopy(validationSalt, 0, result, 32, 8);
            Buffer.BlockCopy(keySalt, 0, result, 40, 8);
            return result;
        }

        /// <summary>Compute /UE value for R6: AES-CBC-256 encrypted file key (32 bytes).</summary>
        private byte[] ComputeUEValueR6(byte[] userPwd)
        {
            var keySalt = new byte[8];
            Buffer.BlockCopy(UValue, 40, keySalt, 0, 8);

            var input = Concat(userPwd, keySalt);
            var key = ComputeHashR6(input, userPwd, Array.Empty<byte>());

            return AesCbcEncryptZeroIv(key, _fileEncryptionKey);
        }

        /// <summary>Compute /O value for R6: hash(32) + validationSalt(8) + keySalt(8) = 48 bytes.</summary>
        private byte[] ComputeOValueR6(byte[] ownerPwd)
        {
            var validationSalt = RandomBytes(8);
            var keySalt = RandomBytes(8);

            var input = Concat(ownerPwd, validationSalt, UValue);
            var hash = ComputeHashR6(input, ownerPwd, UValue);

            var result = new byte[48];
            Buffer.BlockCopy(hash, 0, result, 0, 32);
            Buffer.BlockCopy(validationSalt, 0, result, 32, 8);
            Buffer.BlockCopy(keySalt, 0, result, 40, 8);
            return result;
        }

        /// <summary>Compute /OE value for R6: AES-CBC-256 encrypted file key (32 bytes).</summary>
        private byte[] ComputeOEValueR6(byte[] ownerPwd)
        {
            var keySalt = new byte[8];
            Buffer.BlockCopy(OValue, 40, keySalt, 0, 8);

            var input = Concat(ownerPwd, keySalt, UValue);
            var key = ComputeHashR6(input, ownerPwd, UValue);

            return AesCbcEncryptZeroIv(key, _fileEncryptionKey);
        }

        /// <summary>Compute /Perms value for R6: AES-ECB-256 encrypted permissions (16 bytes).</summary>
        private byte[] ComputePermsValueR6()
        {
            var data = new byte[16];
            // Bytes 0-3: P value as little-endian unsigned 32-bit
            uint pUnsigned = unchecked((uint)PValue);
            data[0] = (byte)(pUnsigned & 0xFF);
            data[1] = (byte)((pUnsigned >> 8) & 0xFF);
            data[2] = (byte)((pUnsigned >> 16) & 0xFF);
            data[3] = (byte)((pUnsigned >> 24) & 0xFF);
            // Bytes 4-7: 0xFFFFFFFF
            data[4] = 0xFF;
            data[5] = 0xFF;
            data[6] = 0xFF;
            data[7] = 0xFF;
            // Byte 8: 'T' (encrypt metadata = true)
            data[8] = (byte)'T';
            // Bytes 9-11: "adb"
            data[9] = (byte)'a';
            data[10] = (byte)'d';
            data[11] = (byte)'b';
            // Bytes 12-15: random
            var rand = RandomBytes(4);
            Buffer.BlockCopy(rand, 0, data, 12, 4);

            return AesEcbEncrypt(_fileEncryptionKey, data);
        }

        /// <summary>
        /// Algorithm 2.B (ISO 32000-2): iterative hash using SHA-256/384/512.
        /// Returns 32 bytes.
        /// </summary>
        private static byte[] ComputeHashR6(byte[] input, byte[] password, byte[] userKey)
        {
            byte[] k;
            using (var sha256 = SHA256.Create())
                k = sha256.ComputeHash(input);

            int round = 0;
            while (true)
            {
                // Step a: build K1 = password + K + userKey, repeated 64 times
                int seqLen = password.Length + k.Length + userKey.Length;
                var k1 = new byte[seqLen * 64];
                for (int i = 0; i < 64; i++)
                {
                    int off = i * seqLen;
                    Buffer.BlockCopy(password, 0, k1, off, password.Length);
                    off += password.Length;
                    Buffer.BlockCopy(k, 0, k1, off, k.Length);
                    off += k.Length;
                    if (userKey.Length > 0)
                        Buffer.BlockCopy(userKey, 0, k1, off, userKey.Length);
                }

                // Step b: AES-CBC-128 encrypt K1 with key=K[0..15], IV=K[16..31]
                var aesKey = new byte[16];
                var aesIv = new byte[16];
                Buffer.BlockCopy(k, 0, aesKey, 0, 16);
                Buffer.BlockCopy(k, 16, aesIv, 0, 16);
                var e = AesCbcEncryptNoPadding(aesKey, aesIv, k1);

                // Step c: determine hash function based on first 16 bytes of E mod 3
                // Sum the first 16 bytes of E, mod 3 selects SHA-256/384/512
                long sum = 0;
                for (int i = 0; i < 16; i++)
                    sum += e[i];
                int hashId = (int)(((sum % 3) + 3) % 3);

                // Step d: compute new K
                if (hashId == 0)
                {
                    using (var sha = SHA256.Create())
                        k = sha.ComputeHash(e);
                }
                else if (hashId == 1)
                {
                    using (var sha = SHA384.Create())
                        k = sha.ComputeHash(e);
                }
                else
                {
                    using (var sha = SHA512.Create())
                        k = sha.ComputeHash(e);
                }

                // Step e: check termination (minimum 64 rounds, then check last byte of E)
                round++;
                if (round >= 64 && e[e.Length - 1] <= (round - 32))
                    break;
            }

            // Return first 32 bytes
            var result = new byte[32];
            Buffer.BlockCopy(k, 0, result, 0, 32);
            return result;
        }

        // ── Shared cryptographic helpers ─────────────────────────────────

        /// <summary>AES-CBC encryption with random IV prepended to output.</summary>
        private static byte[] AesCbcEncrypt(byte[] key, byte[] plaintext)
        {
            // PKCS#7 pad
            int padLen = 16 - (plaintext.Length % 16);
            if (padLen == 0) padLen = 16;
            var padded = new byte[plaintext.Length + padLen];
            Buffer.BlockCopy(plaintext, 0, padded, 0, plaintext.Length);
            for (int i = plaintext.Length; i < padded.Length; i++)
                padded[i] = (byte)padLen;

            var iv = RandomBytes(16);

            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None; // already padded
                aes.Key = key;
                aes.IV = iv;
                using (var encryptor = aes.CreateEncryptor())
                {
                    var encrypted = encryptor.TransformFinalBlock(padded, 0, padded.Length);
                    var result = new byte[16 + encrypted.Length];
                    Buffer.BlockCopy(iv, 0, result, 0, 16);
                    Buffer.BlockCopy(encrypted, 0, result, 16, encrypted.Length);
                    return result;
                }
            }
        }

        /// <summary>AES-CBC encryption with zero IV, no padding (for /UE, /OE computation).</summary>
        private static byte[] AesCbcEncryptZeroIv(byte[] key, byte[] data)
        {
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None;
                aes.Key = key;
                aes.IV = new byte[16];
                using (var encryptor = aes.CreateEncryptor())
                    return encryptor.TransformFinalBlock(data, 0, data.Length);
            }
        }

        /// <summary>AES-CBC-128 encryption with specified key and IV, no padding (for Algorithm 2.B).</summary>
        private static byte[] AesCbcEncryptNoPadding(byte[] key, byte[] iv, byte[] data)
        {
            // Ensure data length is multiple of 16
            int len = data.Length;
            if (len % 16 != 0)
            {
                int paddedLen = ((len / 16) + 1) * 16;
                var tmp = new byte[paddedLen];
                Buffer.BlockCopy(data, 0, tmp, 0, len);
                data = tmp;
                len = paddedLen;
            }

            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None;
                aes.Key = key;
                aes.IV = iv;
                using (var encryptor = aes.CreateEncryptor())
                    return encryptor.TransformFinalBlock(data, 0, len);
            }
        }

        /// <summary>AES-ECB-256 encryption (for /Perms computation, single block).</summary>
        private static byte[] AesEcbEncrypt(byte[] key, byte[] data)
        {
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.Key = key;
                using (var encryptor = aes.CreateEncryptor())
                    return encryptor.TransformFinalBlock(data, 0, data.Length);
            }
        }

        // ── Permission and password helpers ──────────────────────────────

        /// <summary>Compute the 32-bit /P value from permission flags.</summary>
        internal static int ComputePValue(PdfPermissions perms)
        {
            int p = (int)perms;
            // Bits 13-32 must be set to 1, bits 7-8 must be set to 1
            p |= unchecked((int)0xFFFFF000);
            p |= 0x000000C0;
            return p;
        }

        /// <summary>Pad or truncate password to 32 bytes using standard padding (V4/R4).</summary>
        private static byte[] PadPassword(string password)
        {
            var pwdBytes = System.Text.Encoding.UTF8.GetBytes(password ?? "");
            var padded = new byte[32];
            int len = Math.Min(pwdBytes.Length, 32);
            Buffer.BlockCopy(pwdBytes, 0, padded, 0, len);
            if (len < 32)
                Buffer.BlockCopy(PasswordPadding, 0, padded, len, 32 - len);
            return padded;
        }

        /// <summary>Truncate password to 127 bytes (V5/R6).</summary>
        private static byte[] TruncatePassword256(string password)
        {
            var pwdBytes = System.Text.Encoding.UTF8.GetBytes(password ?? "");
            if (pwdBytes.Length <= 127) return pwdBytes;
            var result = new byte[127];
            Buffer.BlockCopy(pwdBytes, 0, result, 0, 127);
            return result;
        }

        // ── Utility methods ──────────────────────────────────────────────

        private static byte[] RandomBytes(int count)
        {
            var bytes = new byte[count];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return bytes;
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            var result = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }

        private static byte[] Concat(byte[] a, byte[] b, byte[] c)
        {
            var result = new byte[a.Length + b.Length + c.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            Buffer.BlockCopy(c, 0, result, a.Length + b.Length, c.Length);
            return result;
        }

        internal static string BytesToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }
    }
}
