using System.Security.Cryptography;

namespace PluginHost.Security;

public sealed class AesGcmEnvelopeCrypto
{
    public EncryptedPayload Encrypt(string sessionId, string clientFingerprint, byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(SecurityConstants.NonceSizeBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[SecurityConstants.TagSizeBytes];

        using var aes = new AesGcm(key, SecurityConstants.TagSizeBytes);
        var aad = BuildAad(sessionId, clientFingerprint);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);

        return new EncryptedPayload(
            sessionId,
            clientFingerprint,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(tag),
            SecurityConstants.PayloadVersion);
    }

    public byte[] Decrypt(EncryptedPayload payload, byte[] key)
    {
        var nonce = Convert.FromBase64String(payload.NonceBase64);
        var ciphertext = Convert.FromBase64String(payload.CiphertextBase64);
        var tag = Convert.FromBase64String(payload.TagBase64);

        if (nonce.Length != SecurityConstants.NonceSizeBytes)
        {
            throw new CryptographicException("Invalid nonce size.");
        }

        if (tag.Length != SecurityConstants.TagSizeBytes)
        {
            throw new CryptographicException("Invalid tag size.");
        }

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, SecurityConstants.TagSizeBytes);
        var aad = BuildAad(payload.SessionId, payload.ClientFingerprint);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
        return plaintext;
    }

    private static byte[] BuildAad(string sessionId, string clientFingerprint)
    {
        return System.Text.Encoding.UTF8.GetBytes($"{sessionId}|{clientFingerprint}");
    }
}
