// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using NLog;
using Wox.Infrastructure.Exception;
using Wox.Plugin;

namespace PowerLauncher.Helper
{
    public static class ErrorReporting
    {
        private static void Report(Exception e, bool waitForClose)
        {
            if (e != null)
            {
                var logger = LogManager.GetLogger("UnHandledException");
                logger.Fatal(ExceptionFormatter.FormatException(e));

                var reportWindow = new ReportWindow(e);

                if (waitForClose)
                {
                    reportWindow.ShowDialog();
                }
                else
                {
                    reportWindow.Show();
                }
            }
        }

        public static void ShowMessageBox(string title, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title);
            });
        }

        public static void UnhandledExceptionHandle(object sender, UnhandledExceptionEventArgs e)
        {
            // Handle specific DWM composition COM exception gracefully
            if (e?.ExceptionObject is System.Runtime.InteropServices.COMException comEx && 
                (comEx.HResult == unchecked((int)0xD0000701) || comEx.HResult == -805306367))
            {
                var logger = LogManager.GetLogger("DWMCompositionException");
                logger.Info("DWM composition not available on background thread - continuing without advanced window styling");
                return;
            }

            // handle other non-ui thread exceptions
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Report((Exception)e?.ExceptionObject, true);
            });
        }

        public static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Handle specific DWM composition COM exception gracefully
            if (e?.Exception is System.Runtime.InteropServices.COMException comEx && 
                (comEx.HResult == unchecked((int)0xD0000701) || comEx.HResult == -805306367))
            {
                var logger = LogManager.GetLogger("DWMCompositionException");
                logger.Info("DWM composition not available on UI thread - continuing without advanced window styling");
                e.Handled = true;
                return;
            }

            // handle other ui thread exceptions
            Report(e?.Exception, false);

            // prevent application exist, so the user can copy prompted error info
            e.Handled = true;
        }

        public static void TaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            // Handle specific DWM composition COM exception gracefully
            if (e?.Exception?.InnerException is System.Runtime.InteropServices.COMException comEx && 
                (comEx.HResult == unchecked((int)0xD0000701) || comEx.HResult == -805306367))
            {
                var logger = LogManager.GetLogger("DWMCompositionException");
                logger.Info("DWM composition not available in unobserved task - continuing without advanced window styling");
                e.SetObserved();
                return;
            }

            // Check if any inner exception in the aggregate is the DWM COM exception
            if (e?.Exception != null)
            {
                foreach (var ex in e.Exception.InnerExceptions)
                {
                    if (ex is System.Runtime.InteropServices.COMException innerComEx &&
                        (innerComEx.HResult == unchecked((int)0xD0000701) || innerComEx.HResult == -805306367))
                    {
                        var logger = LogManager.GetLogger("DWMCompositionException");
                        logger.Info("DWM composition not available in unobserved task (inner exception) - continuing without advanced window styling");
                        e.SetObserved();
                        return;
                    }
                }
            }

            // handle other unobserved task exceptions
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Report(e?.Exception, false);
            });
            e.SetObserved();
        }

        public static string RuntimeInfo()
        {
            var info = $"\nVersion: {Constant.Version}" +
                       $"\nOS Version: {Environment.OSVersion.VersionString}" +
                       $"\nIntPtr Length: {IntPtr.Size}" +
                       $"\nx64: {Environment.Is64BitOperatingSystem}";
            return info;
        }
    }
}
