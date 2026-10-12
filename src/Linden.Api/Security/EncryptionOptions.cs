namespace Linden.Api.Security;

public class EncryptionOptions
{
    // Base64 of 32 random bytes. Comes from user-secrets in development
    // and from the Encryption__Key environment variable in production.
    public string Key { get; set; } = "";
}
