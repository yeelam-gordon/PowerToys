// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

using Microsoft.Win32.SafeHandles;

namespace Microsoft.PowerToys.Settings.UI.Library.Utilities
{
    public static class NamedPipePeerVerification
    {
#if DEBUG
        private const bool RequireTrustedMicrosoftSignature = false;
#else
        private const bool RequireTrustedMicrosoftSignature = true;
#endif

        public static bool TryVerifyClient(
            NamedPipeServerStream stream,
            string expectedExePath,
            string expectedFileVersion,
            string intendedUserSid,
            int intendedSessionId,
            out string rejectionReason)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ValidateExpectedPeer(expectedExePath, intendedUserSid);

            if (!stream.IsConnected)
            {
                rejectionReason = "pipe-not-connected";
                return false;
            }

            if (!NativeMethods.GetNamedPipeClientProcessId(stream.SafePipeHandle, out var processId))
            {
                rejectionReason = "client-pid-unavailable";
                return false;
            }

            return TryVerifyPeerProcess(
                processId,
                expectedExePath,
                expectedFileVersion,
                intendedUserSid,
                intendedSessionId,
                allowLocalSystem: false,
                out rejectionReason);
        }

        public static bool TryVerifyServer(
            NamedPipeClientStream stream,
            string expectedExePath,
            string expectedFileVersion,
            string intendedUserSid,
            int intendedSessionId,
            bool allowLocalSystem,
            out string rejectionReason)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ValidateExpectedPeer(expectedExePath, intendedUserSid);

            if (!stream.IsConnected)
            {
                rejectionReason = "pipe-not-connected";
                return false;
            }

            if (!NativeMethods.GetNamedPipeServerProcessId(stream.SafePipeHandle, out var processId))
            {
                rejectionReason = "server-pid-unavailable";
                return false;
            }

            return TryVerifyPeerProcess(
                processId,
                expectedExePath,
                expectedFileVersion,
                intendedUserSid,
                intendedSessionId,
                allowLocalSystem,
                out rejectionReason);
        }

        private static void ValidateExpectedPeer(string expectedExePath, string intendedUserSid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedExePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(intendedUserSid);
        }

        private static bool TryVerifyPeerProcess(
            uint processId,
            string expectedExePath,
            string expectedFileVersion,
            string intendedUserSid,
            int intendedSessionId,
            bool allowLocalSystem,
            out string rejectionReason)
        {
            try
            {
                using var processHandle = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, processId);
                if (processHandle.IsInvalid)
                {
                    rejectionReason = "identity-unavailable";
                    return false;
                }

                // Hold the process handle through the full check so the PID cannot be reused mid-verification.
                if (!NativeMethods.GetProcessTimes(processHandle, out var creationTime, out _, out _, out _))
                {
                    rejectionReason = "identity-unavailable";
                    return false;
                }

                if (creationTime.ToLong() == 0)
                {
                    rejectionReason = "invalid-process-instance";
                    return false;
                }

                if (!NativeMethods.ProcessIdToSessionId(processId, out var actualSessionId))
                {
                    rejectionReason = "identity-unavailable";
                    return false;
                }

                var actualImagePath = NativeMethods.GetProcessImagePath(processHandle);
                var actualFullPath = Path.GetFullPath(actualImagePath);
                var expectedFullPath = Path.GetFullPath(expectedExePath);
                var actualFileVersion = FileVersionInfo.GetVersionInfo(actualFullPath).FileVersion ?? string.Empty;

                if (!NativeMethods.OpenProcessToken(processHandle, NativeMethods.TokenQuery, out var tokenHandle))
                {
                    rejectionReason = "identity-unavailable";
                    return false;
                }

                string actualUserSid;
                using (tokenHandle)
                using (var identity = new WindowsIdentity(tokenHandle.DangerousGetHandle()))
                {
                    actualUserSid = identity.User?.Value ?? throw new InvalidOperationException("The process token has no user SID.");
                }

                if (unchecked((int)actualSessionId) != intendedSessionId)
                {
                    rejectionReason = "wrong-session";
                    return false;
                }

                var isLocalSystem = string.Equals(
                    actualUserSid,
                    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value,
                    StringComparison.OrdinalIgnoreCase);
                if (!string.Equals(actualUserSid, intendedUserSid, StringComparison.OrdinalIgnoreCase) &&
                    !(allowLocalSystem && isLocalSystem))
                {
                    rejectionReason = "wrong-user";
                    return false;
                }

                if (!string.Equals(actualFullPath, expectedFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    rejectionReason = "wrong-image";
                    return false;
                }

                if (!string.IsNullOrEmpty(expectedFileVersion) &&
                    !string.Equals(actualFileVersion, expectedFileVersion, StringComparison.Ordinal))
                {
                    rejectionReason = "wrong-version";
                    return false;
                }

                if (RequireTrustedMicrosoftSignature && !HasTrustedMicrosoftSignature(actualFullPath))
                {
                    rejectionReason = "untrusted-signature";
                    return false;
                }

                rejectionReason = string.Empty;
                return true;
            }
            catch (Win32Exception)
            {
                rejectionReason = "identity-unavailable";
                return false;
            }
            catch (InvalidOperationException)
            {
                rejectionReason = "identity-unavailable";
                return false;
            }
        }

        private static bool HasTrustedMicrosoftSignature(string imagePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

            try
            {
                if (!HasIntactAuthenticodeSignature(imagePath))
                {
                    return false;
                }

#pragma warning disable SYSLIB0057 // Embedded Authenticode signer extraction has no X509CertificateLoader equivalent.
                using var signer = new X509Certificate2(X509Certificate.CreateFromSignedFile(imagePath));
#pragma warning restore SYSLIB0057
                if (!signer.Subject.Contains("Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                using var roots = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                roots.Open(OpenFlags.ReadOnly);

                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.3"));

                var rootCertificates = roots.Certificates;
                try
                {
                    chain.ChainPolicy.CustomTrustStore.AddRange(rootCertificates);

                    if (TryGetEmbeddedPkcs7Store(imagePath, out var store, out var message))
                    {
                        using (store)
                        using (message)
                        using (var embeddedStore = new X509Store(store.DangerousGetHandle()))
                        {
                            var extraCertificates = embeddedStore.Certificates;
                            try
                            {
                                chain.ChainPolicy.ExtraStore.AddRange(extraCertificates);
                                return chain.Build(signer);
                            }
                            finally
                            {
                                DisposeCertificates(extraCertificates);
                            }
                        }
                    }

                    return chain.Build(signer);
                }
                finally
                {
                    DisposeCertificates(rootCertificates);
                }
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        private static void DisposeCertificates(X509Certificate2Collection certificates)
        {
            foreach (var certificate in certificates)
            {
                certificate.Dispose();
            }
        }

        private static bool HasIntactAuthenticodeSignature(string imagePath)
        {
            var fileInfo = new WinTrustFileInfo
            {
                StructSize = unchecked((uint)Marshal.SizeOf<WinTrustFileInfo>()),
                FilePath = imagePath,
            };
            var fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
            try
            {
                Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
                var trustData = new WinTrustData
                {
                    StructSize = unchecked((uint)Marshal.SizeOf<WinTrustData>()),
                    UiChoice = NativeMethods.WinTrustUiNone,
                    RevocationChecks = NativeMethods.WinTrustRevokeNone,
                    UnionChoice = NativeMethods.WinTrustChoiceFile,
                    FileInfo = fileInfoPointer,
                    StateAction = NativeMethods.WinTrustStateActionVerify,
                    ProviderFlags = NativeMethods.WinTrustSaferFlag | NativeMethods.WinTrustCacheOnlyUrlRetrieval,
                };

                var action = NativeMethods.WinTrustActionGenericVerifyV2;
                var status = NativeMethods.WinVerifyTrust(new IntPtr(-1), ref action, ref trustData);
                trustData.StateAction = NativeMethods.WinTrustStateActionClose;
                _ = NativeMethods.WinVerifyTrust(new IntPtr(-1), ref action, ref trustData);
                return status == 0;
            }
            finally
            {
                Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPointer);
                Marshal.FreeHGlobal(fileInfoPointer);
            }
        }

        private static bool TryGetEmbeddedPkcs7Store(
            string imagePath,
            out SafeCertStoreHandle store,
            out SafeCryptMsgHandle message)
        {
            if (NativeMethods.CryptQueryObject(
                    NativeMethods.CertQueryObjectFile,
                    imagePath,
                    NativeMethods.CertQueryContentFlagPkcs7SignedEmbed,
                    NativeMethods.CertQueryFormatFlagBinary,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    out store,
                    out message,
                    IntPtr.Zero))
            {
                return true;
            }

            store = new SafeCertStoreHandle();
            message = new SafeCryptMsgHandle();
            return false;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            internal uint LowDateTime;
            internal uint HighDateTime;

            internal long ToLong()
            {
                return unchecked((long)(((ulong)HighDateTime << 32) | LowDateTime));
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo
        {
            internal uint StructSize;
            internal string FilePath;
            internal IntPtr FileHandle;
            internal IntPtr KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustData
        {
            internal uint StructSize;
            internal IntPtr PolicyCallbackData;
            internal IntPtr SipClientData;
            internal uint UiChoice;
            internal uint RevocationChecks;
            internal uint UnionChoice;
            internal IntPtr FileInfo;
            internal uint StateAction;
            internal IntPtr StateData;
            internal string UrlReference;
            internal uint ProviderFlags;
            internal uint UiContext;
        }

        private sealed class SafeCertStoreHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeCertStoreHandle()
                : base(true)
            {
            }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.CertCloseStore(handle, 0);
            }
        }

        private sealed class SafeCryptMsgHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeCryptMsgHandle()
                : base(true)
            {
            }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.CryptMsgClose(handle);
            }
        }

        private static class NativeMethods
        {
            internal const uint ProcessQueryLimitedInformation = 0x1000;
            internal const uint TokenQuery = 0x0008;
            internal const uint WinTrustUiNone = 2;
            internal const uint WinTrustRevokeNone = 0;
            internal const uint WinTrustChoiceFile = 1;
            internal const uint WinTrustStateActionVerify = 1;
            internal const uint WinTrustStateActionClose = 2;
            internal const uint WinTrustSaferFlag = 0x100;
            internal const uint WinTrustCacheOnlyUrlRetrieval = 0x1000;
            internal const uint CertQueryObjectFile = 0x00000001;
            internal const uint CertQueryContentFlagPkcs7SignedEmbed = 0x00000400;
            internal const uint CertQueryFormatFlagBinary = 0x00000002;
            internal static readonly Guid WinTrustActionGenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern SafeProcessHandle OpenProcess(
                uint processAccess,
                [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
                uint processId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetProcessTimes(
                SafeProcessHandle process,
                out FileTime creationTime,
                out FileTime exitTime,
                out FileTime kernelTime,
                out FileTime userTime);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool OpenProcessToken(
                SafeProcessHandle processHandle,
                uint desiredAccess,
                out SafeAccessTokenHandle tokenHandle);

            [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool QueryFullProcessImageName(
                SafeProcessHandle process,
                uint flags,
                char[] exeName,
                ref uint size);

            [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
            internal static extern int WinVerifyTrust(
                IntPtr windowHandle,
                [In] ref Guid actionId,
                ref WinTrustData trustData);

            [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptQueryObject(
                uint objectType,
                string @object,
                uint expectedContentTypeFlags,
                uint expectedFormatTypeFlags,
                uint flags,
                IntPtr messageAndCertEncodingType,
                IntPtr contentType,
                IntPtr formatType,
                out SafeCertStoreHandle certStore,
                out SafeCryptMsgHandle message,
                IntPtr context);

            [DllImport("crypt32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CertCloseStore(IntPtr certStore, uint flags);

            [DllImport("crypt32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptMsgClose(IntPtr cryptMsg);

            internal static string GetProcessImagePath(SafeProcessHandle process)
            {
                var buffer = new char[32768];
                var length = unchecked((uint)buffer.Length);
                if (!QueryFullProcessImageName(process, 0, buffer, ref length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return new string(buffer, 0, unchecked((int)length));
            }
        }
    }
}
