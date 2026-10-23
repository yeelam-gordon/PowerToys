// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"
#include "../settings_launch_image.h"
#include <common/interop/pipe_caller_auth.h>
#include <winioctl.h>

#include <cstring>
#include <filesystem>
#include <fstream>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RunnerUnitTests
{
    namespace
    {
        constexpr wchar_t MicrosoftRuntimeFilename[] = L"msvcp140.dll";

        class LaunchFixture
        {
        public:
            LaunchFixture()
            {
                wchar_t temporary[MAX_PATH]{};
                wchar_t unique[MAX_PATH]{};
                Assert::IsTrue(GetTempPathW(ARRAYSIZE(temporary), temporary) != 0);
                Assert::IsTrue(GetTempFileNameW(temporary, L"ptl", 0, unique) != 0);
                root = unique;
                Assert::IsTrue(DeleteFileW(root.c_str()) != FALSE);
                Assert::IsTrue(CreateDirectoryW(root.c_str(), nullptr) != FALSE);
                first = root + L"\\first";
                second = root + L"\\second";
                alias = root + L"\\alias";
                Assert::IsTrue(CreateDirectoryW(first.c_str(), nullptr) != FALSE);
                Assert::IsTrue(CreateDirectoryW(second.c_str(), nullptr) != FALSE);
                std::ofstream(std::filesystem::path(first + L"\\sample.dat")) << "first";
                std::ofstream(std::filesystem::path(second + L"\\sample.dat")) << "second";
            }

            ~LaunchFixture()
            {
                RemoveDirectoryW(alias.c_str());
                DeleteFileW((first + L"\\sample.dat").c_str());
                DeleteFileW((second + L"\\sample.dat").c_str());
                RemoveDirectoryW(first.c_str());
                RemoveDirectoryW(second.c_str());
                if (!RemoveDirectoryW(root.c_str()))
                {
                    Logger::WriteMessage(L"Settings launch fixture cleanup failed.");
                }
            }

            std::wstring root;
            std::wstring first;
            std::wstring second;
            std::wstring alias;
        };

        void CreateJunction(const std::wstring& link, const std::wstring& target)
        {
            Assert::IsTrue(CreateDirectoryW(link.c_str(), nullptr) != FALSE);
            wil::unique_hfile directory(CreateFileW(link.c_str(),
                                                   GENERIC_WRITE,
                                                   FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                                                   nullptr,
                                                   OPEN_EXISTING,
                                                   FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
                                                   nullptr));
            Assert::IsTrue(static_cast<bool>(directory));
            struct MountPointData
            {
                DWORD tag;
                WORD size;
                WORD reserved;
                WORD substituteOffset;
                WORD substituteLength;
                WORD printOffset;
                WORD printLength;
            };
            const std::wstring substitute = L"\\??\\" + target;
            const auto substituteBytes = static_cast<WORD>(substitute.size() * sizeof(wchar_t));
            const auto printBytes = static_cast<WORD>(target.size() * sizeof(wchar_t));
            std::vector<BYTE> buffer(sizeof(MountPointData) + substituteBytes + printBytes + 2 * sizeof(wchar_t));
            auto* data = reinterpret_cast<MountPointData*>(buffer.data());
            data->tag = IO_REPARSE_TAG_MOUNT_POINT;
            data->size = static_cast<WORD>(buffer.size() - 8);
            data->substituteLength = substituteBytes;
            data->printOffset = static_cast<WORD>(substituteBytes + sizeof(wchar_t));
            data->printLength = printBytes;
            memcpy(buffer.data() + sizeof(MountPointData), substitute.c_str(), substituteBytes);
            memcpy(buffer.data() + sizeof(MountPointData) + data->printOffset, target.c_str(), printBytes);
            DWORD returned = 0;
            Assert::IsTrue(DeviceIoControl(directory.get(), FSCTL_SET_REPARSE_POINT, buffer.data(), static_cast<DWORD>(buffer.size()), nullptr, 0, &returned, nullptr) != FALSE);
        }

        std::wstring SystemImagePath(const wchar_t* filename)
        {
            wchar_t system[MAX_PATH]{};
            Assert::IsTrue(GetSystemDirectoryW(system, ARRAYSIZE(system)) != 0);
            return std::wstring(system) + L"\\" + filename;
        }
    }

    TEST_CLASS (SettingsLaunchImageTests)
    {
    public:
        TEST_METHOD (ResolvedImageRemainsStableWhenInputJunctionChanges)
        {
            LaunchFixture fixture;
            CreateJunction(fixture.alias, fixture.first);
            SettingsLaunchImage image;
            Assert::IsTrue(SUCCEEDED(image.Open(fixture.alias + L"\\sample.dat")));
            Assert::IsTrue(RemoveDirectoryW(fixture.alias.c_str()) != FALSE);
            CreateJunction(fixture.alias, fixture.second);

            std::string resolved;
            std::string aliased;
            std::ifstream(std::filesystem::path(image.Path())) >> resolved;
            std::ifstream(std::filesystem::path(fixture.alias + L"\\sample.dat")) >> aliased;
            Assert::AreEqual(std::string("first"), resolved);
            Assert::AreEqual(std::string("second"), aliased);
        }

        TEST_METHOD (ReadGuardPreventsReplacementAndAncestorRenameUntilReleased)
        {
            LaunchFixture fixture;
            const std::wstring source = fixture.first + L"\\sample.dat";
            const std::wstring moved = fixture.first + L"\\moved.dat";
            {
                SettingsLaunchImage image;
                Assert::IsTrue(SUCCEEDED(image.Open(source)));
                wil::unique_hfile writer(CreateFileW(source.c_str(), GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr));
                const DWORD writeError = GetLastError();
                Assert::IsFalse(static_cast<bool>(writer));
                Assert::AreEqual(static_cast<DWORD>(ERROR_SHARING_VIOLATION), writeError);
                Assert::IsFalse(MoveFileExW(source.c_str(), moved.c_str(), 0) != FALSE);
                Assert::IsFalse(MoveFileExW(fixture.first.c_str(), (fixture.root + L"\\moved").c_str(), 0) != FALSE);
            }
            Assert::IsTrue(MoveFileExW(source.c_str(), moved.c_str(), 0) != FALSE);
            Assert::IsTrue(MoveFileExW(moved.c_str(), source.c_str(), 0) != FALSE);
        }

        TEST_METHOD (MatchingMicrosoftImageRequiresVersionAndFileHandle)
        {
            SettingsLaunchImage image;
            Assert::IsTrue(SUCCEEDED(image.Open(SystemImagePath(MicrosoftRuntimeFilename))));
            const auto version = interop_auth::GetModuleVersion(image.Path());
            Assert::IsTrue(version != 0);
            const auto verified = interop_auth::VerifyMicrosoftSignedLaunchImage(image.File(), image.Path(), version);
            Assert::IsTrue(verified.accepted, verified.reasonCode);
            Assert::IsFalse(interop_auth::VerifyMicrosoftSignedLaunchImage(image.File(), image.Path(), version + 1).accepted);
            Assert::IsFalse(interop_auth::VerifyMicrosoftSignedLaunchImage(image.File(), image.Path(), 0).accepted);
            Assert::IsFalse(interop_auth::VerifyMicrosoftSignedLaunchImage(nullptr, image.Path(), version).accepted);
            Assert::IsFalse(interop_auth::VerifyMicrosoftSignedLaunchImage(INVALID_HANDLE_VALUE, image.Path(), version).accepted);
        }

        TEST_METHOD (ChangedImageIsRejectedEvenWithMatchingVersionResources)
        {
            LaunchFixture fixture;
            const std::wstring copy = fixture.first + L"\\sample.dat";
            Assert::IsTrue(CopyFileW(SystemImagePath(MicrosoftRuntimeFilename).c_str(), copy.c_str(), FALSE) != FALSE);
            const auto version = interop_auth::GetModuleVersion(copy);
            {
                SettingsLaunchImage pristine;
                Assert::IsTrue(SUCCEEDED(pristine.Open(copy)));
                const auto verified = interop_auth::VerifyMicrosoftSignedLaunchImage(pristine.File(), pristine.Path(), version);
                Assert::IsTrue(verified.accepted, verified.reasonCode);
            }
            {
                std::fstream bytes(std::filesystem::path(copy), std::ios::in | std::ios::out | std::ios::binary);
                Assert::IsTrue(bytes.good());
                bytes.seekg(0x400);
                char value = 0;
                bytes.read(&value, 1);
                Assert::IsTrue(bytes.good());
                value ^= 1;
                bytes.seekp(0x400);
                bytes.write(&value, 1);
                Assert::IsTrue(bytes.good());
            }
            SettingsLaunchImage image;
            Assert::IsTrue(SUCCEEDED(image.Open(copy)));
            Assert::IsTrue(version != 0);
            Assert::AreEqual(version, interop_auth::GetModuleVersion(image.Path()));
            const auto verified = interop_auth::VerifyMicrosoftSignedLaunchImage(image.File(), image.Path(), version);
            Assert::IsFalse(verified.accepted);
            Assert::AreEqual(L"not-microsoft-signed", verified.reasonCode);
        }

        TEST_METHOD (ResolvedImageCanBeCreatedWhileGuardIsHeld)
        {
            SettingsLaunchImage image;
            Assert::IsTrue(SUCCEEDED(image.Open(SystemImagePath(L"cmd.exe"))));
            STARTUPINFOW startup{ sizeof(startup) };
            wil::unique_process_information process;
            std::wstring arguments = L"cmd.exe /d /c exit 0";
            Assert::IsTrue(CreateProcessW(image.Path().c_str(), arguments.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &startup, &process) != FALSE);
            image = {};
            const auto started = GetTickCount64();
            const DWORD waited = WaitForSingleObject(process.hProcess, 10'000);
            if (waited != WAIT_OBJECT_0)
            {
                const auto message = L"Owned cmd.exe /d /c exit 0 fixture wait failed: PID=" + std::to_wstring(process.dwProcessId) +
                                     L", budget=10000ms, elapsed=" + std::to_wstring(GetTickCount64() - started) +
                                     L"ms, wait=" + std::to_wstring(waited);
                Logger::WriteMessage(message.c_str());
                PostThreadMessageW(process.dwThreadId, WM_QUIT, 0, 0);
                if (WaitForSingleObject(process.hProcess, 1'000) != WAIT_OBJECT_0)
                {
                    const BOOL terminated = TerminateProcess(process.hProcess, 1);
                    const DWORD cleaned = WaitForSingleObject(process.hProcess, 1'000);
                    const auto cleanup = L"Owned fixture cleanup: terminate=" + std::to_wstring(terminated) +
                                         L", wait=" + std::to_wstring(cleaned);
                    Logger::WriteMessage(cleanup.c_str());
                }
            }
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), waited);
            DWORD exitCode = 1;
            Assert::IsTrue(GetExitCodeProcess(process.hProcess, &exitCode) != FALSE);
            Assert::AreEqual(0UL, exitCode);
        }
    };
}
