// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"
#include "../settings_window.h"
#include <common/interop/pipe_caller_auth.h>
#include <common/logger/logger.h>
#include <imagehlp.h>
#include <spdlog/sinks/null_sink.h>
#include <winioctl.h>

#include <cstddef>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <memory>
#include <stdexcept>
#include <vector>

#pragma comment(lib, "imagehlp.lib")

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using TestLogger = Microsoft::VisualStudio::CppUnitTestFramework::Logger;

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
                    TestLogger::WriteMessage(L"Settings launch fixture cleanup failed.");
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

        std::wstring MicrosoftRuntimeImagePath()
        {
            HMODULE module = nullptr;
            Assert::IsTrue(GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                                              MicrosoftRuntimeFilename,
                                              &module) != FALSE);
            wchar_t modulePath[MAX_PATH * 2]{};
            const DWORD length = GetModuleFileNameW(module, modulePath, ARRAYSIZE(modulePath));
            Assert::IsTrue(length != 0 && length < ARRAYSIZE(modulePath));
            return (std::filesystem::path(modulePath).parent_path() / L"TestFixtures" / MicrosoftRuntimeFilename).wstring();
        }

        HRESULT GetFileSignaturePresence(const std::wstring& path, bool& hasSignature)
        {
            wil::unique_hfile file(CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr));
            if (!file)
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            wil::unique_handle mapping(CreateFileMappingW(file.get(), nullptr, PAGE_READONLY | SEC_IMAGE_NO_EXECUTE, 0, 0, nullptr));
            if (!mapping)
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            wil::unique_mapview_ptr<BYTE> image(static_cast<BYTE*>(MapViewOfFile(mapping.get(), FILE_MAP_READ, 0, 0, 0)));
            if (!image)
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            return interop_auth::GetMappedImageSignaturePresence(image.get(), hasSignature);
        }

        wil::unique_hfile OpenTestFile(const std::wstring& path)
        {
            wil::unique_hfile file(CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr));
            Assert::IsTrue(static_cast<bool>(file));
            return file;
        }
    }

    TEST_CLASS (SettingsLaunchTests)
    {
    public:
        TEST_CLASS_INITIALIZE(InitializeLogging)
            {
                ::Logger::init(std::vector<spdlog::sink_ptr>{ std::make_shared<spdlog::sinks::null_sink_mt>() });
            }

            TEST_METHOD (LoadedHostSignatureMetadataMatchesItsImage)
            {
                wchar_t path[MAX_PATH * 2]{};
                DWORD length = ARRAYSIZE(path);
                Assert::IsTrue(QueryFullProcessImageNameW(GetCurrentProcess(), 0, path, &length) != FALSE);
                bool fileHasSignature = true;
                bool loadedHostHasSignature = true;
                Assert::IsTrue(SUCCEEDED(GetFileSignaturePresence(std::wstring(path, length), fileHasSignature)));
                Assert::IsTrue(SUCCEEDED(interop_auth::GetOwnModuleSignaturePresence(loadedHostHasSignature)));
                Assert::AreEqual(fileHasSignature, loadedHostHasSignature);
                Assert::AreEqual(interop_auth::GetModuleVersion(std::wstring(path, length)), interop_auth::GetOwnModuleVersion());
                const auto message = L"Loaded host metadata: file signature=" + std::to_wstring(fileHasSignature) +
                                     L", loaded signature=" + std::to_wstring(loadedHostHasSignature) +
                                     L", version=" + std::to_wstring(interop_auth::GetOwnModuleVersion());
                TestLogger::WriteMessage(message.c_str());
            }

            TEST_METHOD (ResolvedLaunchPathRemainsStable)
            {
                LaunchFixture fixture;
                const auto source = fixture.first + L"\\sample.dat";
                Assert::IsTrue(CopyFileW(MicrosoftRuntimeImagePath().c_str(), source.c_str(), FALSE) != FALSE);
                const auto version = interop_auth::GetModuleVersion(source);
                CreateJunction(fixture.alias, fixture.first);
                unsigned int launches = 0;
                Assert::IsTrue(VerifyAndLaunch(fixture.alias + L"\\sample.dat", true, version, [&](const std::wstring& launchPath) {
                    ++launches;
                    Assert::IsTrue(launchPath.rfind(L"\\\\?\\", 0) == 0);
                    Assert::IsTrue(RemoveDirectoryW(fixture.alias.c_str()) != FALSE);
                    CreateJunction(fixture.alias, fixture.second);
                    Assert::AreEqual(version, interop_auth::GetModuleVersion(launchPath));
                    Assert::AreEqual(0ULL, interop_auth::GetModuleVersion(fixture.alias + L"\\sample.dat"));
                    return true;
                }));
                Assert::AreEqual(1U, launches);
            }

            TEST_METHOD (LaunchFileSharingFollowsActionLifetime)
            {
                LaunchFixture fixture;
                const std::wstring source = fixture.first + L"\\sample.dat";
                const std::wstring moved = fixture.first + L"\\moved.dat";
                Assert::IsTrue(CopyFileW(MicrosoftRuntimeImagePath().c_str(), source.c_str(), FALSE) != FALSE);
                Assert::IsTrue(VerifyAndLaunch(source, true, interop_auth::GetModuleVersion(source), [&](const std::wstring&) {
                    wil::unique_hfile writer(CreateFileW(source.c_str(), GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr));
                    const DWORD writeError = GetLastError();
                    Assert::IsFalse(static_cast<bool>(writer));
                    Assert::AreEqual(static_cast<DWORD>(ERROR_SHARING_VIOLATION), writeError);
                    Assert::IsFalse(MoveFileExW(source.c_str(), moved.c_str(), 0) != FALSE);
                    Assert::IsFalse(MoveFileExW(fixture.first.c_str(), (fixture.root + L"\\moved").c_str(), 0) != FALSE);
                    return true;
                }));
                Assert::IsTrue(MoveFileExW(source.c_str(), moved.c_str(), 0) != FALSE);
                Assert::IsTrue(MoveFileExW(moved.c_str(), source.c_str(), 0) != FALSE);
            }

            TEST_METHOD (MatchingMicrosoftImageRequiresVersionAndFileHandle)
            {
                const auto path = MicrosoftRuntimeImagePath();
                auto file = OpenTestFile(path);
                const auto version = interop_auth::GetModuleVersion(path);
                Assert::IsTrue(version != 0);
                bool hasSignature = false;
                Assert::IsTrue(SUCCEEDED(GetFileSignaturePresence(path, hasSignature)));
                Assert::IsTrue(hasSignature);
                const auto verified = interop_auth::VerifyMicrosoftSignedLaunchImage(file.get(), path, version);
                Assert::IsTrue(verified.accepted, verified.reasonCode);
                Assert::IsFalse(interop_auth::VerifyMicrosoftSignedLaunchImage(file.get(), path, version + 1).accepted);
                Assert::IsFalse(interop_auth::VerifyMicrosoftSignedLaunchImage(file.get(), path, 0).accepted);
                Assert::IsFalse(interop_auth::VerifyMicrosoftSignedLaunchImage(nullptr, path, version).accepted);
                Assert::IsFalse(interop_auth::VerifyMicrosoftSignedLaunchImage(INVALID_HANDLE_VALUE, path, version).accepted);
                unsigned int launches = 0;
                const auto launch = [&](const std::wstring&) {
                    ++launches;
                    return true;
                };
                Assert::IsFalse(VerifyAndLaunch(path, true, version + 1, launch));
                Assert::IsFalse(VerifyAndLaunch(path, true, 0, launch));
                Assert::IsFalse(VerifyAndLaunch(path + L".missing", true, version, launch));
                Assert::AreEqual(0U, launches);
                Assert::IsTrue(VerifyAndLaunch(path, true, version, launch));
                Assert::AreEqual(1U, launches);
            }

            TEST_METHOD (ChangedImageIsRejectedEvenWithMatchingVersionResources)
            {
                LaunchFixture fixture;
                const std::wstring copy = fixture.first + L"\\sample.dat";
                Assert::IsTrue(CopyFileW(MicrosoftRuntimeImagePath().c_str(), copy.c_str(), FALSE) != FALSE);
                const auto version = interop_auth::GetModuleVersion(copy);
                Assert::IsTrue(VerifyAndLaunch(copy, true, version, [](const std::wstring&) { return true; }));
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
                auto file = OpenTestFile(copy);
                Assert::IsTrue(version != 0);
                Assert::AreEqual(version, interop_auth::GetModuleVersion(copy));
                const auto verified = interop_auth::VerifyMicrosoftSignedLaunchImage(file.get(), copy, version);
                Assert::IsFalse(verified.accepted);
                Assert::AreEqual(L"not-microsoft-signed", verified.reasonCode);
                bool hasSignature = true;
                Assert::IsTrue(SUCCEEDED(GetFileSignaturePresence(copy, hasSignature)));
                Assert::IsTrue(hasSignature);
                unsigned int launches = 0;
                Assert::IsFalse(VerifyAndLaunch(copy, true, version, [&](const std::wstring&) {
                    ++launches;
                    return true;
                }));
                Assert::AreEqual(0U, launches);
            }

            TEST_METHOD (UnsignedImageIsDistinguishedFromSignatureVerificationFailure)
            {
                LaunchFixture fixture;
                const std::wstring copy = fixture.first + L"\\sample.dat";
                Assert::IsTrue(CopyFileW(MicrosoftRuntimeImagePath().c_str(), copy.c_str(), FALSE) != FALSE);
                {
                    wil::unique_hfile file(CreateFileW(copy.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr));
                    Assert::IsTrue(static_cast<bool>(file));
                    DWORD certificates = 0;
                    Assert::IsTrue(ImageEnumerateCertificates(file.get(), CERT_SECTION_TYPE_ANY, &certificates, nullptr, 0) != FALSE);
                    Assert::IsTrue(certificates != 0);
                    for (DWORD remaining = certificates; remaining != 0; --remaining)
                    {
                        DWORD available = 0;
                        DWORD index = 0;
                        Assert::IsTrue(ImageEnumerateCertificates(file.get(), CERT_SECTION_TYPE_ANY, &available, &index, 1) != FALSE);
                        Assert::AreEqual(remaining, available);
                        Assert::IsTrue(ImageRemoveCertificate(file.get(), index) != FALSE);
                    }
                }

                bool hasSignature = true;
                Assert::IsTrue(SUCCEEDED(GetFileSignaturePresence(copy, hasSignature)));
                Assert::IsFalse(hasSignature);

                auto file = OpenTestFile(copy);
                Assert::IsFalse(interop_auth::VerifyMicrosoftSignedLaunchImage(file.get(), copy, interop_auth::GetModuleVersion(copy)).accepted);
                Assert::IsFalse(VerifyAndLaunch(copy, true, interop_auth::GetModuleVersion(copy), [](const std::wstring&) -> bool {
                    Assert::Fail(L"Required verification must finish before the launch action.");
                }));
                Assert::IsTrue(FAILED(GetFileSignaturePresence(fixture.root + L"\\missing.exe", hasSignature)));
            }

            TEST_METHOD (MalformedSignatureMaterialDoesNotCountAsUnsigned)
            {
                LaunchFixture fixture;
                const auto source = MicrosoftRuntimeImagePath();
                bool hasSignature = false;
                Assert::IsTrue(SUCCEEDED(GetFileSignaturePresence(source, hasSignature)));
                Assert::IsTrue(hasSignature);

                std::ifstream original(std::filesystem::path(source), std::ios::binary);
                IMAGE_DOS_HEADER dos{};
                original.read(reinterpret_cast<char*>(&dos), sizeof(dos));
                Assert::IsTrue(original.good());
                Assert::IsTrue(dos.e_magic == IMAGE_DOS_SIGNATURE && dos.e_lfanew > 0);
                original.seekg(dos.e_lfanew);
                IMAGE_NT_HEADERS headers{};
                original.read(reinterpret_cast<char*>(&headers), sizeof(headers));
                Assert::IsTrue(original.good());
                Assert::IsTrue(headers.Signature == IMAGE_NT_SIGNATURE);
                const DWORD certificateOffset = headers.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_SECURITY].VirtualAddress;
                Assert::IsTrue(certificateOffset != 0);
                const auto directoryOffset = static_cast<std::streamoff>(
                    dos.e_lfanew + offsetof(IMAGE_NT_HEADERS, OptionalHeader) +
                    offsetof(IMAGE_OPTIONAL_HEADER, DataDirectory) + IMAGE_DIRECTORY_ENTRY_SECURITY * sizeof(IMAGE_DATA_DIRECTORY));
                struct CertificateMutation
                {
                    std::streamoff offset;
                    DWORD value;
                    std::streamsize length;
                };
                const CertificateMutation mutations[] = {
                    { certificateOffset, 0, sizeof(DWORD) },
                    { static_cast<std::streamoff>(certificateOffset + offsetof(WIN_CERTIFICATE, wCertificateType)), 0xffff, sizeof(WORD) },
                    { directoryOffset, MAXDWORD, sizeof(DWORD) },
                    { directoryOffset + static_cast<std::streamoff>(offsetof(IMAGE_DATA_DIRECTORY, Size)), 1, sizeof(DWORD) },
                };
                for (const auto& mutation : mutations)
                {
                    const auto copy = fixture.first + L"\\sample.dat";
                    Assert::IsTrue(CopyFileW(source.c_str(), copy.c_str(), FALSE) != FALSE);
                    {
                        std::fstream bytes(std::filesystem::path(copy), std::ios::in | std::ios::out | std::ios::binary);
                        bytes.seekp(mutation.offset);
                        bytes.write(reinterpret_cast<const char*>(&mutation.value), mutation.length);
                        Assert::IsTrue(bytes.good());
                    }
                    hasSignature = true;
                    const HRESULT presence = GetFileSignaturePresence(copy, hasSignature);
                    Assert::IsTrue(FAILED(presence) || hasSignature);
                    Assert::IsFalse(VerifyAndLaunch(copy, true, interop_auth::GetModuleVersion(source), [](const std::wstring&) -> bool {
                        Assert::Fail(L"Invalid launch metadata must not invoke the launch action.");
                    }));
                }
            }

            TEST_METHOD (LaunchActionFailureReleasesTheFile)
            {
                LaunchFixture fixture;
                const auto path = fixture.first + L"\\sample.dat";
                const auto moved = fixture.first + L"\\moved.dat";
                Assert::IsTrue(CopyFileW(MicrosoftRuntimeImagePath().c_str(), path.c_str(), FALSE) != FALSE);
                const auto version = interop_auth::GetModuleVersion(path);
                Assert::IsFalse(VerifyAndLaunch(path, true, version, [](const std::wstring&) { return false; }));
                Assert::IsTrue(MoveFileExW(path.c_str(), moved.c_str(), 0) != FALSE);
                Assert::IsTrue(MoveFileExW(moved.c_str(), path.c_str(), 0) != FALSE);
                Assert::ExpectException<std::runtime_error>([&]() {
                    VerifyAndLaunch(path, true, version, [](const std::wstring&) -> bool {
                        throw std::runtime_error("launch action failed");
                    });
                });
                Assert::IsTrue(MoveFileExW(path.c_str(), moved.c_str(), 0) != FALSE);
                Assert::IsTrue(MoveFileExW(moved.c_str(), path.c_str(), 0) != FALSE);
                Assert::IsTrue(VerifyAndLaunch(path, true, version, [](const std::wstring&) { return true; }));
            }

            TEST_METHOD (StartupCancellationStillPreventsCreation)
            {
                SettingsWindowStartup startup;
                startup.CompleteStartup();
                unsigned int creations = 0;
                const auto path = MicrosoftRuntimeImagePath();
                Assert::IsFalse(VerifyAndLaunch(path, true, interop_auth::GetModuleVersion(path), [&](const std::wstring&) {
                    startup.Stop();
                    return startup.TryLaunch([&]() {
                        ++creations;
                        return true;
                    });
                }));
                Assert::AreEqual(0U, creations);
            }

            TEST_METHOD (LaunchActionStartsTheProcessWithoutRequiredVerification)
            {
                const auto path = SystemImagePath(L"cmd.exe");
                STARTUPINFOW startup{ sizeof(startup) };
                wil::unique_process_information process;
                std::wstring arguments = L"cmd.exe /d /c exit 0";
                Assert::IsTrue(VerifyAndLaunch(path, false, 0, [&](const std::wstring& launchPath) {
                    Assert::AreEqual(path, launchPath);
                    return CreateProcessW(launchPath.c_str(), arguments.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &startup, &process) != FALSE;
                }));
                const auto started = GetTickCount64();
                const DWORD waited = WaitForSingleObject(process.hProcess, 10'000);
                if (waited != WAIT_OBJECT_0)
                {
                    const auto message = L"Owned cmd.exe /d /c exit 0 fixture wait failed: PID=" + std::to_wstring(process.dwProcessId) +
                                         L", budget=10000ms, elapsed=" + std::to_wstring(GetTickCount64() - started) +
                                         L"ms, wait=" + std::to_wstring(waited);
                    TestLogger::WriteMessage(message.c_str());
                    PostThreadMessageW(process.dwThreadId, WM_QUIT, 0, 0);
                    if (WaitForSingleObject(process.hProcess, 1'000) != WAIT_OBJECT_0)
                    {
                        const BOOL terminated = TerminateProcess(process.hProcess, 1);
                        const DWORD cleaned = WaitForSingleObject(process.hProcess, 1'000);
                        const auto cleanup = L"Owned fixture cleanup: terminate=" + std::to_wstring(terminated) +
                                             L", wait=" + std::to_wstring(cleaned);
                        TestLogger::WriteMessage(cleanup.c_str());
                    }
                }
                Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), waited);
                DWORD exitCode = 1;
                Assert::IsTrue(GetExitCodeProcess(process.hProcess, &exitCode) != FALSE);
                Assert::AreEqual(0UL, exitCode);
            }
    };
}
