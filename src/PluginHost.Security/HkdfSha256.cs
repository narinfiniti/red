using System.Security.Cryptography;

namespace PluginHost.Security;

public static class HkdfSha256
{
    public static byte[] DeriveKey(byte[] ikm, byte[] salt, byte[] info, int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        using var hmac = new HMACSHA256(salt);
        var prk = hmac.ComputeHash(ikm);

        try
        {
            var okm = new byte[length];
            var previous = Array.Empty<byte>();
            var offset = 0;
            byte counter = 1;

            while (offset < length)
            {
                using var stepHmac = new HMACSHA256(prk);
                var buffer = new byte[previous.Length + info.Length + 1];
                Buffer.BlockCopy(previous, 0, buffer, 0, previous.Length);
                Buffer.BlockCopy(info, 0, buffer, previous.Length, info.Length);
                buffer[^1] = counter;

                var t = stepHmac.ComputeHash(buffer);
                var toCopy = Math.Min(t.Length, length - offset);
                Buffer.BlockCopy(t, 0, okm, offset, toCopy);
                offset += toCopy;

                previous = t;
                counter++;
            }

            return okm;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(prk);
        }
    }
}
