using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace GameManager.App.Services;

public sealed class RemoteImageCacheService : IRemoteImageCacheService
{
    private const int MaxImageBytes = 8 * 1024 * 1024;
    private readonly string cacheDirectory;
    private readonly Func<HttpClient> createClient;

    public RemoteImageCacheService(string cacheDirectory, Func<HttpClient>? createClient = null)
    {
        this.cacheDirectory = cacheDirectory;
        this.createClient = createClient ?? (() => new HttpClient());
    }

    public async Task<string?> DownloadAsync(
        string provider,
        string subjectId,
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        Directory.CreateDirectory(cacheDirectory);
        var tempPath = Path.Combine(cacheDirectory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            using var client = createClient();
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentLength is > MaxImageBytes)
            {
                return null;
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var memory = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                if (memory.Length + read > MaxImageBytes)
                {
                    return null;
                }

                await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            var bytes = memory.ToArray();
            var extension = DetectExtension(bytes);
            if (extension is null || !CanDecodeImage(bytes))
            {
                return null;
            }

            var fileName = $"{SafePathSegment.Create(provider, "provider")}-{SafePathSegment.Create(subjectId, "subject")}{extension}";
            var targetPath = Path.Combine(cacheDirectory, fileName);
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);
            File.Move(tempPath, targetPath, true);
            return targetPath;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Cache cleanup must not turn a successful metadata import into a failure.
            }
        }
    }

    private static string? DetectExtension(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return ".png";
        }

        return null;
    }

    private static bool CanDecodeImage(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            return decoder.Frames.Count > 0 && decoder.Frames[0].PixelWidth > 0 && decoder.Frames[0].PixelHeight > 0;
        }
        catch
        {
            return false;
        }
    }
}
