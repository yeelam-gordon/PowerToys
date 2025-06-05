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
            // Handle DWM composition COM exceptions gracefully by checking stack trace pattern
            if (e?.ExceptionObject is System.Runtime.InteropServices.COMException comEx && 
                IsDwmCompositionException(comEx))
            {
                var logger = LogManager.GetLogger("DWMCompositionException");
                logger.Info($"DWM composition exception on background thread (HRESULT: 0x{comEx.HResult:X8}) - continuing without advanced window styling");
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
            // Handle DWM composition COM exceptions gracefully by checking stack trace pattern
            if (e?.Exception is System.Runtime.InteropServices.COMException comEx && 
                IsDwmCompositionException(comEx))
            {
                var logger = LogManager.GetLogger("DWMCompositionException");
                logger.Info($"DWM composition exception on UI thread (HRESULT: 0x{comEx.HResult:X8}) - continuing without advanced window styling");
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
            // Handle DWM composition COM exceptions gracefully by checking stack trace pattern
            if (e?.Exception?.InnerException is System.Runtime.InteropServices.COMException comEx && 
                IsDwmCompositionException(comEx))
            {
                var logger = LogManager.GetLogger("DWMCompositionException");
                logger.Info($"DWM composition exception in unobserved task (HRESULT: 0x{comEx.HResult:X8}) - continuing without advanced window styling");
                e.SetObserved();
                return;
            }

            // Check if any inner exception in the aggregate is a DWM COM exception
            if (e?.Exception != null)
            {
                foreach (var ex in e.Exception.InnerExceptions)
                {
                    if (ex is System.Runtime.InteropServices.COMException innerComEx &&
                        IsDwmCompositionException(innerComEx))
                    {
                        var logger = LogManager.GetLogger("DWMCompositionException");
                        logger.Info($"DWM composition exception in unobserved task (inner exception, HRESULT: 0x{innerComEx.HResult:X8}) - continuing without advanced window styling");
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

        /// <summary>
        /// Determines if a COM exception is related to DWM composition being disabled
        /// by examining the stack trace for DWM-related call patterns.
        /// </summary>
        /// <param name="comException">The COM exception to analyze</param>
        /// <returns>True if the exception is related to DWM composition issues</returns>
        private static bool IsDwmCompositionException(System.Runtime.InteropServices.COMException comException)
        {
            if (comException == null)
                return false;

            var stackTrace = comException.StackTrace;
            if (string.IsNullOrEmpty(stackTrace))
                return false;

            // Check for common DWM composition-related patterns in the stack trace
            return stackTrace.Contains("DwmCompositionChanged") ||
                   stackTrace.Contains("WindowChromeWorker._ExtendGlassFrame") ||
                   stackTrace.Contains("DwmExtendFrameIntoClientArea") ||
                   stackTrace.Contains("DwmSetWindowAttribute");
        }
    }
}
