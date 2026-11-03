// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include <PowerRenameItem.h>
#include <PowerRenameRegEx.h>
#include <Renaming.h>
#include <ShObjIdl.h>
#include <array>
#include <sstream>
#include <stdexcept>
#include <string>
#include <thread>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace PowerRenameDependencyFunctionalTests
{
    namespace
    {
        using Status = PowerRenameItemRenameStatus;

        struct Result
        {
            std::wstring errors;
            std::wostringstream evidence;
        };

        void Require(bool condition, const char* operation)
        {
            if (!condition)
            {
                throw std::runtime_error(std::string(operation) + "; Win32=" + std::to_string(GetLastError()));
            }
        }

        void Ok(HRESULT hr, const char* operation)
        {
            if (hr != S_OK)
            {
                throw std::runtime_error(std::string(operation) + "; HRESULT=" + std::to_string(hr));
            }
        }

        void Failure(Result& result, const std::exception& error)
        {
            const std::string message = error.what();
            result.errors += std::wstring(message.begin(), message.end()) + L"\n";
        }

        struct Handle
        {
            HANDLE value;
            Result& result;
            ~Handle()
            {
                if (value != INVALID_HANDLE_VALUE && !CloseHandle(value))
                {
                    result.errors += L"CloseHandle failed; Win32=" + std::to_wstring(GetLastError()) + L"\n";
                }
            }
        };

        bool SameIdentity(const BY_HANDLE_FILE_INFORMATION& left, const BY_HANDLE_FILE_INFORMATION& right)
        {
            return left.dwVolumeSerialNumber == right.dwVolumeSerialNumber &&
                   left.nFileIndexHigh == right.nFileIndexHigh && left.nFileIndexLow == right.nFileIndexLow;
        }

        BY_HANDLE_FILE_INFORMATION Snapshot(const std::wstring& path, bool folder, Result& result)
        {
            Handle handle{ CreateFileW(path.c_str(), FILE_READ_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nullptr),
                           result };
            Require(handle.value != INVALID_HANDLE_VALUE, "open identity");
            BY_HANDLE_FILE_INFORMATION info{};
            Require(GetFileInformationByHandle(handle.value, &info), "read identity");
            Require(!(info.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT), "reject reparse point");
            Require(!!(info.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == folder, "filesystem type");
            Require(folder || info.nNumberOfLinks == 1, "file link count");
            return info;
        }

        void Absent(const std::wstring& path)
        {
            const DWORD attributes = GetFileAttributesW(path.c_str());
            const DWORD error = GetLastError();
            Require(attributes == INVALID_FILE_ATTRIBUTES &&
                        (error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND),
                    "expected absent path");
        }

        void Children(const std::wstring& directory, PCWSTR expected)
        {
            WIN32_FIND_DATAW data{};
            HANDLE find = FindFirstFileW((directory + L"\\*").c_str(), &data);
            Require(find != INVALID_HANDLE_VALUE, "enumerate directory");
            unsigned count = 0;
            bool matching = true;
            do
            {
                if (wcscmp(data.cFileName, L".") && wcscmp(data.cFileName, L".."))
                {
                    ++count;
                    matching = matching && expected && wcscmp(data.cFileName, expected) == 0;
                }
            } while (FindNextFileW(find, &data));
            const DWORD error = GetLastError();
            const BOOL closed = FindClose(find);
            Require(closed, "FindClose");
            Require(error == ERROR_NO_MORE_FILES, "complete enumeration");
            Require(matching && count == (expected ? 1u : 0u), "exact directory contents");
        }

        struct Row
        {
            PCWSTR id;
            PCWSTR original;
            bool folder;
            DWORD flags;
            PCWSTR candidate;
            Status status;
            bool eligible;
            unsigned long index;
        };

        struct Fixture
        {
            Result& result;
            std::wstring directory;
            std::wstring path;
            bool folder = false;
            bool directoryOwned = false;
            bool childOwned = false;
            bool directoryRecorded = false;
            bool childRecorded = false;
            BY_HANDLE_FILE_INFORMATION directoryIdentity{};
            BY_HANDLE_FILE_INFORMATION childIdentity{};

            void Create(const Row& row)
            {
                std::array<wchar_t, MAX_PATH> current{};
                const DWORD length = GetCurrentDirectoryW(static_cast<DWORD>(current.size()), current.data());
                Require(length && length < current.size(), "short project fixture parent");
                std::wstring parent(current.data());
                Require(parent.size() > 3 && parent[1] == L':' && parent[2] == L'\\', "absolute local parent");
                const std::wstring drive = parent.substr(0, 3);
                Require(GetDriveTypeW(drive.c_str()) == DRIVE_FIXED, "fixed local parent");
                for (std::wstring ancestor = parent;;)
                {
                    Snapshot(ancestor, true, result);
                    if (ancestor.size() == 3)
                    {
                        break;
                    }
                    const auto slash = ancestor.find_last_of(L'\\');
                    ancestor.resize(slash <= 2 ? 3 : slash);
                }
                GUID guid{};
                Ok(CoCreateGuid(&guid), "CoCreateGuid");
                std::array<wchar_t, 40> text{};
                Require(StringFromGUID2(guid, text.data(), static_cast<int>(text.size())) != 0, "StringFromGUID2");
                directory = parent + L"\\RFEX-" + text.data();
                path = directory + L"\\" + row.original;
                folder = row.folder;
                Require(path.size() < 240 && (directory + L"\\zzz_file.txt").size() < 240 &&
                            (directory + L"\\zzz_folder").size() < 240,
                        "short original and candidate paths");
                Require(CreateDirectoryW(directory.c_str(), nullptr), "create owned GUID directory");
                directoryOwned = true;
                directoryIdentity = Snapshot(directory, true, result);
                directoryRecorded = true;
                if (folder)
                {
                    Require(CreateDirectoryW(path.c_str(), nullptr), "create empty folder");
                    childOwned = true;
                    childIdentity = Snapshot(path, true, result);
                    childRecorded = true;
                }
                else
                {
                    Handle file{ CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr), result };
                    Require(file.value != INVALID_HANDLE_VALUE, "CREATE_NEW file");
                    childOwned = true;
                    Require(GetFileInformationByHandle(file.value, &childIdentity), "record new file identity");
                    childRecorded = true;
                    constexpr char bytes[] = "RFEX-r1\n";
                    DWORD written = 0;
                    Require(WriteFile(file.value, bytes, 8, &written, nullptr) && written == 8, "write sentinel");
                }
                result.evidence << L"fixture=" << path << L"; volume=" << childIdentity.dwVolumeSerialNumber
                                << L"; fileIndex=" << childIdentity.nFileIndexHigh << L":" << childIdentity.nFileIndexLow << L"\n";
            }

            void Verify(const Row& row)
            {
                Require(SameIdentity(directoryIdentity, Snapshot(directory, true, result)), "unchanged GUID directory identity");
                Require(SameIdentity(childIdentity, Snapshot(path, folder, result)), "unchanged original identity");
                Children(directory, row.original);
                Absent(directory + L"\\zzz_file.txt");
                Absent(directory + L"\\zzz_folder");
                if (folder)
                {
                    Children(path, nullptr);
                    result.evidence << L"empty folder identity verified\n";
                }
                else
                {
                    Handle file{ CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr),
                                 result };
                    Require(file.value != INVALID_HANDLE_VALUE, "open sentinel");
                    std::array<char, 9> bytes{};
                    DWORD read = 0;
                    Require(ReadFile(file.value, bytes.data(), static_cast<DWORD>(bytes.size()), &read, nullptr), "read sentinel");
                    Require(read == 8 && std::string(bytes.data(), read) == "RFEX-r1\n", "exact original bytes and EOF");
                    result.evidence << L"bytes=524645582D72310A; identity/type/links unchanged\n";
                }
            }

            void Cleanup()
            {
                if (childOwned)
                {
                    try
                    {
                        Require(childRecorded, "cleanup requires recorded child identity");
                        Require(SameIdentity(childIdentity, Snapshot(path, folder, result)), "cleanup child identity");
                        if (folder)
                        {
                            Children(path, nullptr);
                        }
                        Require(folder ? RemoveDirectoryW(path.c_str()) : DeleteFileW(path.c_str()), "delete exact owned child");
                        Absent(path);
                    }
                    catch (const std::exception& error)
                    {
                        Failure(result, error);
                        result.errors += L"leftover child=" + path + L"\n";
                    }
                }
                if (directoryOwned)
                {
                    try
                    {
                        Require(directoryRecorded, "cleanup requires recorded directory identity");
                        Require(SameIdentity(directoryIdentity, Snapshot(directory, true, result)), "cleanup directory identity");
                        Children(directory, nullptr);
                        Require(RemoveDirectoryW(directory.c_str()), "delete exact owned GUID directory");
                        Absent(directory);
                        result.evidence << L"cleanup=owned child and GUID directory absent; COM/handles released\n";
                    }
                    catch (const std::exception& error)
                    {
                        Failure(result, error);
                        result.errors += L"leftover directory=" + directory + L"\n";
                    }
                }
            }
        };

        void Observe(IPowerRenameItem* item, const Fixture& fixture, const Row& row, PCWSTR candidate, Status expectedStatus, Result& result)
        {
            CComHeapPtr<wchar_t> path;
            CComHeapPtr<wchar_t> original;
            CComHeapPtr<wchar_t> newName;
            Ok(item->GetPath(&path), "GetPath");
            Ok(item->GetOriginalName(&original), "GetOriginalName");
            Ok(item->GetNewName(&newName), "GetNewName");
            Require(path && fixture.path == static_cast<PCWSTR>(path), "exact public path");
            Require(original && wcscmp(original, row.original) == 0, "exact original name");
            Require(candidate ? newName && wcscmp(newName, candidate) == 0 : !newName, "exact candidate including null");
            bool folder = false;
            bool selected = false;
            bool subfolder = true;
            UINT depth = 99;
            Status status = Status::ItemNameInvalidChar;
            Ok(item->GetIsFolder(&folder), "GetIsFolder");
            Ok(item->GetSelected(&selected), "GetSelected");
            Ok(item->GetDepth(&depth), "GetDepth");
            Ok(item->GetIsSubFolderContent(&subfolder), "GetIsSubFolderContent");
            Ok(item->GetStatus(&status), "GetStatus");
            Require(folder == row.folder && selected && depth == 0 && !subfolder && status == expectedStatus, "public classification/state");
            result.evidence << L"path=" << static_cast<PCWSTR>(path) << L"; original=" << static_cast<PCWSTR>(original)
                            << L"; folder=" << folder << L"; selected=" << selected << L"; depth=" << depth
                            << L"; subfolder=" << subfolder << L"; status=" << static_cast<int>(status)
                            << L"; candidate=" << (candidate ? candidate : L"<null>") << L"\n";
        }

        void Run(const Row& row)
        {
            Result result;
            std::thread worker([&] {
                const HRESULT initialized = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
                result.evidence << row.id << L"; CoInitializeEx=" << initialized << L"; flags=" << row.flags << L"\n";
                if (FAILED(initialized))
                {
                    result.errors = L"COM initialization failed: " + std::to_wstring(initialized);
                    return;
                }
                Fixture fixture{ result };
                try
                {
                    fixture.Create(row);
                    CComPtr<IShellItem> shell;
                    Ok(SHCreateItemFromParsingName(fixture.path.c_str(), nullptr, IID_PPV_ARGS(&shell)), "real Shell acquisition");
                    Require(shell != nullptr, "nonnull real Shell");
                    CComPtr<IPowerRenameItem> item;
                    Ok(CPowerRenameItem::s_CreateInstance(shell, IID_PPV_ARGS(&item)), "actual item factory");
                    Require(item != nullptr, "nonnull actual item");
                    Observe(item, fixture, row, nullptr, Status::Init, result);
                    fixture.Verify(row);
                    Ok(item->PutSelected(true), "PutSelected");
                    Ok(item->PutDepth(0), "PutDepth");
                    Ok(item->PutStatus(Status::Init), "PutStatus");
                    Ok(item->PutNewName(L"stale-candidate"), "PutNewName");
                    CComPtr<IPowerRenameRegEx> engine;
                    Ok(CPowerRenameRegEx::s_CreateInstance(false, &engine), "explicit-false engine");
                    Require(engine != nullptr, "nonnull engine");
                    Ok(engine->PutFlags(row.flags), "PutFlags");
                    Ok(engine->PutSearchTerm(L"aaa", false), "PutSearchTerm");
                    Ok(engine->PutReplaceTerm(L"zzz", false), "PutReplaceTerm");
                    DWORD flags = MAXDWORD;
                    Ok(engine->GetFlags(&flags), "GetFlags");
                    Require(flags == row.flags, "exact engine flags");
                    Observe(item, fixture, row, L"stale-candidate", Status::Init, result);
                    unsigned long index = 7;
                    const bool renamed = DoRename(engine, index, item);
                    bool eligible = !row.eligible;
                    Ok(item->ShouldRenameItem(row.flags, &eligible), "ShouldRenameItem");
                    result.evidence << L"DoRename=" << renamed << L"; ShouldRenameItem=" << eligible << L"; index=7->" << index << L"\n";
                    Require(renamed == row.eligible && eligible == row.eligible && index == row.index, "eligibility and exact index");
                    Observe(item, fixture, row, row.candidate, row.status, result);
                    fixture.Verify(row);
                }
                catch (const std::exception& error)
                {
                    Failure(result, error);
                }
                catch (...)
                {
                    result.errors += L"unexpected non-standard exception\n";
                }
                fixture.Cleanup();
                CoUninitialize();
                result.evidence << L"CoUninitialize=once after COM release and fixture cleanup\n";
            });
            worker.join();
            Logger::WriteMessage(result.evidence.str().c_str());
            Assert::IsTrue(result.errors.empty(), result.errors.c_str());
        }
    }

    TEST_CLASS (RealShellExclusions)
    {
        TEST_METHOD (FileIncludedWithoutExclusions)
        {
            Run({ L"RFEX-01", L"aaa_file.txt", false, 0, L"zzz_file.txt", Status::ShouldRename, true, 8 });
        }
        TEST_METHOD (FolderIncludedWithoutExclusions)
        {
            Run({ L"RFEX-02", L"aaa_folder", true, 0, L"zzz_folder", Status::ShouldRename, true, 8 });
        }
        TEST_METHOD (ExcludeFilesClearsRealFileCandidate)
        {
            Run({ L"RFEX-03", L"aaa_file.txt", false, ExcludeFiles, nullptr, Status::Init, false, 7 });
        }
        TEST_METHOD (ExcludeFilesRetainsRealFolderCandidate)
        {
            Run({ L"RFEX-04", L"aaa_folder", true, ExcludeFiles, L"zzz_folder", Status::ShouldRename, true, 8 });
        }
        TEST_METHOD (ExcludeFoldersRetainsRealFileCandidate)
        {
            Run({ L"RFEX-05", L"aaa_file.txt", false, ExcludeFolders, L"zzz_file.txt", Status::ShouldRename, true, 8 });
        }
        TEST_METHOD (ExcludeFoldersClearsRealFolderCandidate)
        {
            Run({ L"RFEX-06", L"aaa_folder", true, ExcludeFolders, nullptr, Status::Init, false, 7 });
        }
    };
}
