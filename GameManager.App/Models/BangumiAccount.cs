namespace GameManager.App.Models;

public sealed record BangumiAccount(
    string Username,
    string Nickname,
    string AvatarUrl,
    string AccessToken,
    DateTime VerifiedAtUtc,
    bool RequiresReconnect = false);
