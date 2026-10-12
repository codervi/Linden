using System.Security.Cryptography;
using Linden.Api.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace Linden.Tests;

public class NoteCipherTests
{
    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static AesGcmNoteCipher CreateCipher(string? key = null)
        => new(Options.Create(new EncryptionOptions { Key = key ?? NewKey() }));

    [Fact]
    public void Decrypt_returns_the_original_text()
    {
        var cipher = CreateCipher();
        var text = "Client reports better sleep. Türkçe: ğüşiöç";

        var encrypted = cipher.Encrypt(text, "client:7");

        Assert.Equal(text, cipher.Decrypt(encrypted, "client:7"));
    }

    [Fact]
    public void Encrypted_text_does_not_contain_the_plain_text()
    {
        var encrypted = CreateCipher().Encrypt("a very recognisable sentence", "client:1");

        var bytes = Convert.FromBase64String(encrypted);
        var plain = System.Text.Encoding.UTF8.GetBytes("recognisable");

        Assert.True(bytes.AsSpan().IndexOf(plain) < 0);
    }

    [Fact]
    public void Same_text_is_encrypted_differently_each_time()
    {
        var cipher = CreateCipher();

        Assert.NotEqual(cipher.Encrypt("same", "client:1"), cipher.Encrypt("same", "client:1"));
    }

    [Fact]
    public void A_note_cannot_be_read_under_another_clients_context()
    {
        var cipher = CreateCipher();
        var encrypted = cipher.Encrypt("private", "client:7");

        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(encrypted, "client:8"));
    }

    [Fact]
    public void Changed_data_is_detected()
    {
        var cipher = CreateCipher();
        var data = Convert.FromBase64String(cipher.Encrypt("private", "client:1"));
        data[^1] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(Convert.ToBase64String(data), "client:1"));
    }

    [Fact]
    public void A_different_key_cannot_decrypt()
    {
        var encrypted = CreateCipher().Encrypt("private", "client:1");

        Assert.ThrowsAny<CryptographicException>(() => CreateCipher().Decrypt(encrypted, "client:1"));
    }

    [Fact]
    public void Empty_key_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => CreateCipher(""));
    }

    [Fact]
    public void Key_that_is_not_32_bytes_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => CreateCipher(Convert.ToBase64String(new byte[16])));
    }

    [Fact]
    public void Key_that_is_not_base64_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => CreateCipher("not base64!!"));
    }
}
