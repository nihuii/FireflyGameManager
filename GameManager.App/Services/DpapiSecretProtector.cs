using System.Security.Cryptography;
using System.Text;

namespace GameManager.App.Services;

public sealed class DpapiSecretProtector : ISecretProtector
{
    private readonly byte[] entropy;

    public DpapiSecretProtector(string purpose = "FireflyGameManager.WebDav.v2")
    {
        entropy = Encoding.UTF8.GetBytes(purpose);
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        return Convert.ToBase64String(ProtectedData.Protect(bytes, entropy, DataProtectionScope.CurrentUser));
    }

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
        {
            return string.Empty;
        }

        var bytes = Convert.FromBase64String(protectedValue);
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, entropy, DataProtectionScope.CurrentUser));
    }
}
