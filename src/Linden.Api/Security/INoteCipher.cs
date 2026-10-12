namespace Linden.Api.Security;

public interface INoteCipher
{
    // "context" is authenticated but not encrypted. We pass the client id,
    // so a note copied onto another client's row fails to decrypt.
    string Encrypt(string plainText, string context);
    string Decrypt(string payload, string context);
}
