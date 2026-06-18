using System.IO;
using System.Text.Json;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class JsonBangumiAccountStore : IBangumiAccountStore
{
    private readonly string path;
    private readonly ISecretProtector protector;

    public JsonBangumiAccountStore(string path, ISecretProtector? protector = null)
    {
        this.path = path;
        this.protector = protector ?? new DpapiSecretProtector("FireflyGameManager.Bangumi.v1");
    }

    public BangumiAccount? Load()
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<AccountDto>(File.ReadAllText(path));
            if (dto is null || string.IsNullOrWhiteSpace(dto.Username))
            {
                return null;
            }

            return new BangumiAccount(
                dto.Username,
                dto.Nickname ?? string.Empty,
                dto.AvatarUrl ?? string.Empty,
                protector.Unprotect(dto.EncryptedAccessToken ?? string.Empty),
                dto.VerifiedAtUtc,
                dto.RequiresReconnect);
        }
        catch
        {
            return null;
        }
    }

    public void Save(BangumiAccount account)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(new AccountDto
        {
            Username = account.Username,
            Nickname = account.Nickname,
            AvatarUrl = account.AvatarUrl,
            EncryptedAccessToken = protector.Protect(account.AccessToken),
            VerifiedAtUtc = account.VerifiedAtUtc,
            RequiresReconnect = account.RequiresReconnect
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public void Clear()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class AccountDto
    {
        public string? Username { get; set; }

        public string? Nickname { get; set; }

        public string? AvatarUrl { get; set; }

        public string? EncryptedAccessToken { get; set; }

        public DateTime VerifiedAtUtc { get; set; }

        public bool RequiresReconnect { get; set; }
    }
}
