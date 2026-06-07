using System.IO;
using GameManager.App.Models;

namespace GameManager.App.Services;

public static class CloudCoverMergePolicy
{
    public static bool ShouldApply(GameCloudMetadata remote, Game? localBeforeMerge)
    {
        if (localBeforeMerge is null)
        {
            return true;
        }

        var remoteUpdated = remote.UpdatedAtUtc.ToUniversalTime();
        var localUpdated = localBeforeMerge.UpdatedAtUtc.ToUniversalTime();
        if (remoteUpdated > localUpdated)
        {
            return true;
        }

        return remoteUpdated == localUpdated &&
            (string.IsNullOrWhiteSpace(localBeforeMerge.CoverImagePath) || !File.Exists(localBeforeMerge.CoverImagePath));
    }
}
