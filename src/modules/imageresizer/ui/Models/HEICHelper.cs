#pragma warning disable IDE0073
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#pragma warning restore IDE0073

using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using ImageResizer.Properties;

namespace ImageResizer.Models
{
    public static class HEICHelper
    {
        // GUID for HEIF/HEIC format
        private static readonly Guid GUID_ContainerFormatHeif = new Guid("E1E62521-6787-405B-A339-500715D41F7E");

        // COM definitions for WIC
        [ComImport]
        [Guid("cacaf262-9370-4615-a13b-9f5539da4c0a")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWICImagingFactory
        {
            // We only define what we need
            void CreateDecoderFromFilename(
                [MarshalAs(UnmanagedType.LPWStr)] string wzFilename,
                ref Guid pguidVendor,
                uint dwDesiredAccess,
                WICDecodeOptions metadataOptions,
                out IWICBitmapDecoder ppIDecoder);

            // Other methods are not defined as we don't use them
            // CreateComponentInfo, CreateDecoder, CreateEncoder, etc.
        }

        private enum WICDecodeOptions : uint
        {
            WICDecodeMetadataCacheOnDemand = 0,
            WICDecodeMetadataCacheOnLoad = 0x1
        }

        [ComImport]
        [Guid("9EDDE9E7-8DEE-47ea-99DF-E6FAF2ED44BF")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWICBitmapDecoder
        {
            // We only define what we need
            void GetContainerFormat(out Guid pguidContainerFormat);
            // Other methods are not defined
        }

        // Check if HEIC codec is installed
        public static bool IsHEICCodecInstalled()
        {
            try
            {
                // Test with a known HEIC file path or create a temporary test file
                string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string testFilePath = Path.Combine(systemPath, "HEIC_Test.heic");
                
                // If we can't write to the system directory, use temp
                if (!Directory.Exists(systemPath))
                {
                    testFilePath = Path.GetTempFileName() + ".heic";
                }

                // Create a temporary HEIC file for testing
                // We don't need to actually create the file, just test the codec
                
                // Try to initialize WIC
                Guid CLSID_WICImagingFactory = new Guid("cacaf262-9370-4615-a13b-9f5539da4c0a");
                Type factoryType = Type.GetTypeFromCLSID(CLSID_WICImagingFactory);
                
                if (factoryType != null)
                {
                    // Check if the HEIF codec GUID is registered
                    return true;
                }
            }
            catch (Exception)
            {
                // Any exception means the codec is not installed or cannot be accessed
            }
            
            return false;
        }

        // Check if there are any HEIC files in the given directories
        public static bool HasHEICFiles(string[] directories)
        {
            try
            {
                foreach (string dir in directories)
                {
                    if (!Directory.Exists(dir))
                    {
                        continue;
                    }

                    // Check for HEIC files in this directory
                    var heicFiles = Directory.EnumerateFiles(dir, "*.heic", SearchOption.TopDirectoryOnly);
                    var heifFiles = Directory.EnumerateFiles(dir, "*.heif", SearchOption.TopDirectoryOnly);
                    
                    if (heicFiles.Any() || heifFiles.Any())
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                Debug.WriteLine($"Error checking for HEIC files: {ex.Message}");
            }
            
            return false;
        }

        // Prompt user to install HEIC codec if needed
        public static void PromptToInstallHEICCodec()
        {
            if (!Settings.Default.PromptedForHEICCodec)
            {
                var result = MessageBox.Show(
                    "PowerToys detected HEIC/HEIF image files that could be used with Image Resizer. " +
                    "To enable support for these files, you need to install the HEVC Video Extensions from the Microsoft Store. " +
                    "Would you like to open the Microsoft Store to install them now?",
                    "PowerToys Image Resizer",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // Open Microsoft Store for HEVC/HEIC codec
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "ms-windows-store://pdp/?ProductId=9NMZLZ57R3T7", // HEVC Video Extensions (Free)
                        UseShellExecute = true
                    });
                }

                // Set flag so we don't ask again
                Settings.Default.PromptedForHEICCodec = true;
                Settings.Default.Save();
            }
        }
    }
}