// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using Peek.Common.Models;
using Peek.UI.Helpers;
using Peek.UI.Models;

namespace Peek.UI
{
    public partial class NeighboringItemsQuery : ObservableObject
    {
        [ObservableProperty]
        private bool isMultipleFilesActivation;

        public NeighboringItems? GetNeighboringItems(Windows.Win32.Foundation.HWND foregroundWindowHandle)
        {
            var selectedItemsShellArray = FileExplorerHelper.GetSelectedItems(foregroundWindowHandle);
            var selectedItemsCount = selectedItemsShellArray?.GetCount() ?? 0;

            if (selectedItemsShellArray == null || selectedItemsCount < 1)
            {
                return null;
            }

            bool hasMoreThanOneItem = selectedItemsCount > 1;
            IsMultipleFilesActivation = hasMoreThanOneItem;

            var neighboringItemsShellArray = hasMoreThanOneItem ? selectedItemsShellArray : FileExplorerHelper.GetItems(foregroundWindowHandle);
            return neighboringItemsShellArray == null ? null : new NeighboringItems(neighboringItemsShellArray);
        }

        public NeighboringItems? GetNeighboringItemsFromFilePath(string filePath)
        {
            try
            {
                // Resolve relative path to absolute path
                string absolutePath = Path.GetFullPath(filePath);

                if (!File.Exists(absolutePath) && !Directory.Exists(absolutePath))
                {
                    return null;
                }

                // Get the directory containing the file
                string? directory = File.Exists(absolutePath) ? Path.GetDirectoryName(absolutePath) : absolutePath;
                
                if (string.IsNullOrEmpty(directory))
                {
                    return null;
                }

                // Get all files in the directory, sorted alphabetically
                var allFiles = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
                    .Where(f => File.Exists(f))
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (allFiles.Count == 0)
                {
                    return null;
                }

                // Create FileItem objects for all files
                var fileItems = allFiles.Select(f => new FileItem(f, Path.GetFileName(f)) as IFileSystemItem).ToList();

                // If a specific file was requested, find its index and reorder to start from that file
                if (File.Exists(absolutePath))
                {
                    int targetIndex = allFiles.FindIndex(f => string.Equals(f, absolutePath, StringComparison.OrdinalIgnoreCase));
                    if (targetIndex >= 0)
                    {
                        // Reorder the list to start with the target file
                        var reorderedItems = new List<IFileSystemItem>();
                        for (int i = 0; i < fileItems.Count; i++)
                        {
                            reorderedItems.Add(fileItems[(targetIndex + i) % fileItems.Count]);
                        }
                        fileItems = reorderedItems;
                    }
                }

                IsMultipleFilesActivation = false;
                return new NeighboringItems(fileItems);
            }
            catch
            {
                return null;
            }
        }
    }
}
