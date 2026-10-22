// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security.UnitTests;

internal sealed class TestDirectory : IDisposable
{
    private readonly List<string> links = [];

    internal TestDirectory()
    {
        string ownerRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PowerToys-SettingsBackupRestore-tests");
        Path = System.IO.Path.Combine(ownerRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    internal string CreateDirectory(string relativePath)
    {
        string path = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    internal string CreateFile(string relativePath, string contents)
    {
        string path = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    internal string CreateDirectoryJunction(string relativePath, string targetPath)
    {
        string linkPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(linkPath)!);
        ProcessStartInfo startInfo = new()
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            Arguments = $"/d /c mklink /J \"{linkPath}\" \"{targetPath}\"",
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(startInfo)!;
        if (!process.WaitForExit(10_000))
        {
            process.Kill();
            Assert.Fail($"mklink timed out after 10 seconds (PID {process.Id}).");
        }

        Assert.AreEqual(0, process.ExitCode, $"mklink failed: {process.StandardError.ReadToEnd()}");
        links.Add(linkPath);
        return linkPath;
    }

    internal string CreateStoredArchiveWithDeclaredLength(string relativePath, string entryName, string contents, int declaredLength)
    {
        string archivePath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(archivePath)!);
        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            using StreamWriter writer = new(archive.CreateEntry(entryName, CompressionLevel.NoCompression).Open());
            writer.Write(contents);
        }

        byte[] bytes = File.ReadAllBytes(archivePath);
        int centralDirectoryOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(bytes.Length - 6, 4));
        int localHeaderOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(centralDirectoryOffset + 42, 4));

        // Change both ZIP size fields while retaining the stored payload and its CRC.
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(centralDirectoryOffset + 24, 4), declaredLength);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(localHeaderOffset + 22, 4), declaredLength);
        File.WriteAllBytes(archivePath, bytes);
        return archivePath;
    }

    internal string CreateDirectorySymbolicLink(string relativePath, string targetPath)
    {
        string linkPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(linkPath)!);
        Directory.CreateSymbolicLink(linkPath, targetPath);
        links.Add(linkPath);
        return linkPath;
    }

    internal string CreateHardLink(string relativePath, string targetPath)
    {
        string linkPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(linkPath)!);
        if (!CreateHardLinkW(linkPath, targetPath, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create test-owned hard link.");
        }

        return linkPath;
    }

    public void Dispose()
    {
        foreach (string link in links)
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }
        }

        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }

        string? ownerRoot = System.IO.Path.GetDirectoryName(Path);
        if (ownerRoot != null && Directory.Exists(ownerRoot) && Directory.GetFileSystemEntries(ownerRoot).Length == 0)
        {
            Directory.Delete(ownerRoot);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string fileName, string existingFileName, IntPtr securityAttributes);
}
