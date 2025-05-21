#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.IO;

using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Utilities;
using ImageResizer.ViewModels;
using ImageResizer.Views;
using ManagedCommon;

namespace ImageResizer
{
    public partial class App : Application, IDisposable
    {
        static App()
        {
            try
            {
                string appLanguage = LanguageHelper.LoadLanguage();
                if (!string.IsNullOrEmpty(appLanguage))
                {
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
                }
            }
            catch (CultureNotFoundException)
            {
                // error
            }

            Console.InputEncoding = Encoding.Unicode;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Fix for .net 3.1.19 making Image Resizer not adapt to DPI changes.
            NativeMethods.SetProcessDPIAware();

            if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredImageResizerEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
            {
                /* TODO: Add logs to ImageResizer.
                 * Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                 */
                Environment.Exit(0); // Current.Exit won't work until there's a window opened.
                return;
            }

            var batch = ResizeBatch.FromCommandLine(Console.In, e?.Args);
            
            // Check settings and if we haven't prompted for HEIC codec yet
            if (!Settings.Default.PromptedForHEICCodec)
            {
                CheckForHeicFiles();
            }

            // TODO: Add command-line parameters that can be used in lieu of the input page (issue #14)
            var mainWindow = new MainWindow(new MainViewModel(batch, Settings.Default));
            mainWindow.Show();

            // Temporary workaround for issue #1273
            WindowHelpers.BringToForeground(new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle);
        }

        // Check for HEIC files in common locations
        private void CheckForHeicFiles()
        {
            try
            {
                // Check if HEIC codec is already installed
                if (HEICHelper.IsHEICCodecInstalled())
                {
                    return;
                }
                
                // Get common paths for pictures
                string[] commonPictureDirs = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonPictures),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                };
                
                if (HEICHelper.HasHEICFiles(commonPictureDirs))
                {
                    HEICHelper.PromptToInstallHEICCodec();
                }
            }
            catch (Exception)
            {
                // Silently ignore any errors in this optional feature
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            GC.SuppressFinalize(this);
        }
    }
}
