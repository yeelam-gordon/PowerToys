// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

public sealed partial class FaviconLoader : IFaviconLoader, IDisposable
{
    private const int MaxCachedIcons = 256;
    private const int MaxFaviconBytes = 1024 * 1024;
    private static readonly TimeSpan CacheEntryLifetime = TimeSpan.FromDays(7);

    private readonly HttpClient _http = CreateClient();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _authorityLocks = new();
    private bool _disposed;

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        };

        var client = new HttpClient(handler, disposeHandler: true);
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) WindowsCommandPalette/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("image/*");

        return client;
    }

    public async Task<IRandomAccessStream?> TryGetFaviconAsync(Uri siteUri, CancellationToken ct = default)
    {
        if (siteUri.Scheme != Uri.UriSchemeHttp && siteUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var first = BuildFaviconUri(siteUri);
        var cacheKey = BuildCacheKey(first);
        var authorityLock = _authorityLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1));
        await authorityLock.WaitAsync(ct);
        try
        {
            var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
            directory = Path.Combine(directory, "icons");
            Directory.CreateDirectory(directory);

            // The cache filename is a SHA-256 hash of the scheme, host, and port.
            var cacheKeyHash = SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey));
            var iconFileName = $"{Convert.ToHexString(cacheKeyHash)}.icon";
            var iconPath = Path.Combine(directory, iconFileName);

            var cachedIcon = await TryReadCachedIconAsync(iconPath, ct).ConfigureAwait(false);
            if (cachedIcon is not null)
            {
                return cachedIcon;
            }

            // 1) First attempt: favicon on the original authority (preserves port).
            var inputStream = await TryDownloadImageAsync(first, ct).ConfigureAwait(false);
            if (inputStream is null)
            {
                // 2) If the server redirected and "lost" the path, try /favicon.ico on the *final* host.
                // We discover the final host by doing a HEAD/GET to the original URL and inspecting the final RequestUri.
                var finalAuthority = await ResolveFinalAuthorityAsync(first, ct).ConfigureAwait(false);
                if (finalAuthority is null || UriEqualsAuthority(first, finalAuthority))
                {
                    return null;
                }

                var second = BuildFaviconUri(finalAuthority);
                if (second == first)
                {
                    return null; // nothing new to try
                }

                inputStream = await TryDownloadImageAsync(second, ct).ConfigureAwait(false);
            }

            if (inputStream is null)
            {
                return null;
            }

            await TryWriteCacheFileAsync(iconPath, inputStream, ct).ConfigureAwait(false);
            PruneIconCache(directory);
            inputStream.Seek(0);
            return inputStream;
        }
        finally
        {
            authorityLock.Release();
        }
    }

    private static Uri BuildFaviconUri(Uri anyUriOnSite)
    {
        var b = new UriBuilder(anyUriOnSite.Scheme, anyUriOnSite.Host)
        {
            Port = anyUriOnSite.IsDefaultPort ? -1 : anyUriOnSite.Port,
            Path = "/favicon.ico",
        };
        return b.Uri;
    }

    private static string BuildCacheKey(Uri faviconUri)
        => faviconUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped).ToLowerInvariant();

    private static async Task<IRandomAccessStream?> TryReadCachedIconAsync(string iconPath, CancellationToken ct)
    {
        var iconFile = new FileInfo(iconPath);
        if (!iconFile.Exists)
        {
            return null;
        }

        if (DateTime.UtcNow - iconFile.LastWriteTimeUtc > CacheEntryLifetime)
        {
            TryDeleteFile(iconPath);
            return null;
        }

        try
        {
            var iconBytes = await File.ReadAllBytesAsync(iconPath, ct).ConfigureAwait(false);
            var iconStream = new InMemoryRandomAccessStream();
            await iconStream.WriteAsync(iconBytes.AsBuffer());
            iconStream.Seek(0);
            return iconStream;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task TryWriteCacheFileAsync(string iconPath, IRandomAccessStream inputStream, CancellationToken ct)
    {
        var tempPath = $"{iconPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            inputStream.Seek(0);
            using (var writeStream = inputStream.AsStreamForRead())
            using (var outputStream = File.Create(tempPath))
            {
                await writeStream.CopyToAsync(outputStream, ct).ConfigureAwait(false);
            }

            File.Move(tempPath, iconPath, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            TryDeleteFile(tempPath);
            throw;
        }
        catch (IOException)
        {
            TryDeleteFile(tempPath);
        }
        catch (UnauthorizedAccessException)
        {
            TryDeleteFile(tempPath);
        }
    }

    private static void PruneIconCache(string directory)
    {
        try
        {
            var staleFiles = new DirectoryInfo(directory)
                .EnumerateFiles("*.icon")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Skip(MaxCachedIcons);

            foreach (var file in staleFiles)
            {
                TryDeleteFile(file.FullName);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private async Task<Uri?> ResolveFinalAuthorityAsync(Uri url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        // We only need headers to learn the final RequestUri after redirects
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                                    .ConfigureAwait(false);

        var final = resp.RequestMessage?.RequestUri;
        return final is null ? null : new UriBuilder(final.Scheme, final.Host)
        {
            Port = final.IsDefaultPort ? -1 : final.Port,
            Path = "/",
        }.Uri;
    }

    private async Task<IRandomAccessStream?> TryDownloadImageAsync(Uri url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            // If the redirect chain dumped us on an HTML page (common for root), bail.
            var mediaType = resp.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null &&
                !mediaType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (resp.Content.Headers.ContentLength > MaxFaviconBytes)
            {
                return null;
            }

            using var responseStream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var memoryStream = new MemoryStream();
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                if (memoryStream.Length + bytesRead > MaxFaviconBytes)
                {
                    return null;
                }

                memoryStream.Write(buffer, 0, bytesRead);
            }

            if (memoryStream.Length == 0)
            {
                return null;
            }

            var bytes = memoryStream.ToArray();
            var stream = new InMemoryRandomAccessStream();

            using (var output = stream.GetOutputStreamAt(0))
            using (var writer = new DataWriter(output))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync().AsTask(ct);
                await writer.FlushAsync().AsTask(ct);
            }

            stream.Seek(0);
            return stream;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static bool UriEqualsAuthority(Uri a, Uri b)
        => a.Scheme.Equals(b.Scheme, StringComparison.OrdinalIgnoreCase)
        && a.Host.Equals(b.Host, StringComparison.OrdinalIgnoreCase)
        && (a.IsDefaultPort ? -1 : a.Port) == (b.IsDefaultPort ? -1 : b.Port);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _http.Dispose();

        foreach (var semaphore in _authorityLocks.Values)
        {
            semaphore.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
