using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Linden.Api.Security;

// AES-256-GCM. Stored layout, then base64:
//   [1 byte version][12 bytes nonce][16 bytes tag][ciphertext]
// A new random nonce is used for every encryption. The version byte
// leaves room for key rotation later.
public sealed class AesGcmNoteCipher : INoteCipher
{
    private const byte Version = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmNoteCipher(IOptions<EncryptionOptions> options)
    {
        var raw = options.Value.Key;
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException(
                "Encryption:Key is missing. Generate one and store it with user-secrets (see README).");

        try { _key = Convert.FromBase64String(raw); }
        catch (FormatException) { throw new InvalidOperationException("Encryption:Key must be base64."); }

        if (_key.Length != 32)
            throw new InvalidOperationException("Encryption:Key must decode to exactly 32 bytes.");
    }

    public string Encrypt(string plainText, string context)
    {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(context));

        var result = new byte[1 + NonceSize + TagSize + cipher.Length];
        result[0] = Version;
        Buffer.BlockCopy(nonce, 0, result, 1, NonceSize);
        Buffer.BlockCopy(tag, 0, result, 1 + NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, result, 1 + NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string payload, string context)
    {
        var data = Convert.FromBase64String(payload);
        if (data.Length < 1 + NonceSize + TagSize || data[0] != Version)
            throw new CryptographicException("Unknown note format.");

        var nonce = data.AsSpan(1, NonceSize);
        var tag = data.AsSpan(1 + NonceSize, TagSize);
        var cipher = data.AsSpan(1 + NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        // throws CryptographicException if the data or the context was changed
        aes.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(context));
        return Encoding.UTF8.GetString(plain);
    }
}
