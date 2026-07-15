// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Common.Utilities
{
    internal static class SvgPreviewCacheHelper
    {
        internal const string CacheVersion = "v1";

        private const int MaxCacheFiles = 256;
        private static readonly byte[] SeparatorBytes = Encoding.UTF8.GetBytes(":");
        private static readonly byte[] NewLineBytes = Encoding.UTF8.GetBytes("\n");

        internal static string BuildCacheKey(params string[] cacheInputs)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            foreach (var input in cacheInputs)
            {
                var normalizedInput = input ?? string.Empty;
                hash.AppendData(Encoding.UTF8.GetBytes(normalizedInput.Length.ToString(CultureInfo.InvariantCulture)));
                hash.AppendData(SeparatorBytes);
                hash.AppendData(Encoding.UTF8.GetBytes(normalizedInput));
                hash.AppendData(NewLineBytes);
            }

            return Convert.ToHexString(hash.GetHashAndReset());
        }

        internal static string GetOrCreateCacheFilePath(string cacheRootFolder, string cacheKey, Func<string> contentFactory)
        {
            Directory.CreateDirectory(cacheRootFolder);
            var cacheFilePath = Path.Combine(cacheRootFolder, $"{cacheKey}.html");

            if (!IsUsableCacheFile(cacheFilePath))
            {
                WriteCacheFile(cacheFilePath, contentFactory());
            }

            TouchCacheFile(cacheFilePath);
            CleanupCacheFolder(cacheRootFolder, cacheFilePath);

            return cacheFilePath;
        }

        internal static void CleanupCacheFolder(string cacheRootFolder, string? currentCacheFilePath = null, int maxCacheFiles = MaxCacheFiles)
        {
            if (!Directory.Exists(cacheRootFolder))
            {
                return;
            }

            var maxOtherCacheFiles = Math.Max(maxCacheFiles - (currentCacheFilePath == null ? 0 : 1), 0);
            var filesToDelete = new DirectoryInfo(cacheRootFolder)
                .EnumerateFiles("*.html")
                .Where(file => !string.Equals(file.FullName, currentCacheFilePath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Skip(maxOtherCacheFiles);

            foreach (var file in filesToDelete)
            {
                try
                {
                    file.Delete();
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static bool IsUsableCacheFile(string cacheFilePath)
        {
            try
            {
                return File.Exists(cacheFilePath) && new FileInfo(cacheFilePath).Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void TouchCacheFile(string cacheFilePath)
        {
            try
            {
                File.SetLastWriteTimeUtc(cacheFilePath, DateTime.UtcNow);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void WriteCacheFile(string cacheFilePath, string contents)
        {
            var cacheFolder = Path.GetDirectoryName(cacheFilePath) ?? Directory.GetCurrentDirectory();
            var tempFilePath = Path.Combine(cacheFolder, $"{Path.GetFileNameWithoutExtension(cacheFilePath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                File.WriteAllText(tempFilePath, contents);

                if (File.Exists(cacheFilePath) && new FileInfo(cacheFilePath).Length == 0)
                {
                    File.Delete(cacheFilePath);
                }

                File.Move(tempFilePath, cacheFilePath);
            }
            catch (IOException)
            {
                if (!IsUsableCacheFile(cacheFilePath))
                {
                    throw;
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
