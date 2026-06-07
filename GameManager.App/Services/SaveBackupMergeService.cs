using System.IO;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class SaveBackupMergeService
{
    public SaveBackupMergeResult MergeRemoteIntoLocal(string localSaveBackupsDirectory, string remoteSaveBackupsDirectory)
    {
        if (!Directory.Exists(remoteSaveBackupsDirectory))
        {
            return new SaveBackupMergeResult(0, 0);
        }

        Directory.CreateDirectory(localSaveBackupsDirectory);

        var addedCount = 0;
        var updatedCount = 0;
        foreach (var remoteFile in Directory.EnumerateFiles(remoteSaveBackupsDirectory, "*.zip", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(remoteSaveBackupsDirectory, remoteFile);
            var localFile = BuildLocalPath(localSaveBackupsDirectory, relativePath);
            if (localFile is null)
            {
                continue;
            }

            var localDirectory = Path.GetDirectoryName(localFile);
            if (!string.IsNullOrWhiteSpace(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            if (!File.Exists(localFile))
            {
                CopyBackup(remoteFile, localFile);
                addedCount++;
                continue;
            }

            if (File.GetLastWriteTimeUtc(remoteFile) > File.GetLastWriteTimeUtc(localFile))
            {
                CopyBackup(remoteFile, localFile);
                updatedCount++;
            }
        }

        return new SaveBackupMergeResult(addedCount, updatedCount);
    }

    private static void CopyBackup(string sourcePath, string destinationPath)
    {
        var temporaryPath = destinationPath + $".tmp-{Guid.NewGuid():N}";
        try
        {
            File.Copy(sourcePath, temporaryPath, true);
            File.Move(temporaryPath, destinationPath, true);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }

        File.SetLastWriteTimeUtc(destinationPath, File.GetLastWriteTimeUtc(sourcePath));
    }

    private static string? BuildLocalPath(string localRoot, string relativePath)
    {
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Length == 0 || segments.Any(IsUnsafePathSegment))
        {
            return null;
        }

        var root = Path.GetFullPath(localRoot);
        var localPath = Path.GetFullPath(Path.Combine([root, .. segments]));
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return localPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            ? localPath
            : null;
    }

    private static bool IsUnsafePathSegment(string segment)
    {
        return string.IsNullOrWhiteSpace(segment) ||
            segment == "." ||
            segment == ".." ||
            segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(SafePathSegment.Create(segment), segment, StringComparison.Ordinal);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary cleanup must not hide the merge result.
        }
    }
}
