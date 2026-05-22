namespace SimpleTinyPDF
{
    /// <summary>
    /// Minimal RC4 stream cipher used only for V4/R4 password hash computation.
    /// </summary>
    internal static class Rc4
    {
        internal static byte[] Transform(byte[] key, byte[] data)
        {
            // Key-Scheduling Algorithm (KSA)
            var s = new byte[256];
            for (int i = 0; i < 256; i++) s[i] = (byte)i;
            int j = 0;
            for (int i = 0; i < 256; i++)
            {
                j = (j + s[i] + key[i % key.Length]) & 255;
                byte tmp = s[i]; s[i] = s[j]; s[j] = tmp;
            }

            // Pseudo-Random Generation Algorithm (PRGA)
            var result = new byte[data.Length];
            int a = 0, b = 0;
            for (int k = 0; k < data.Length; k++)
            {
                a = (a + 1) & 255;
                b = (b + s[a]) & 255;
                byte tmp = s[a]; s[a] = s[b]; s[b] = tmp;
                result[k] = (byte)(data[k] ^ s[(s[a] + s[b]) & 255]);
            }
            return result;
        }
    }
}
