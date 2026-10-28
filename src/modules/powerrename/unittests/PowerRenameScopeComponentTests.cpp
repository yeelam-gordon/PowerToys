#include "..\PowerRenameUILib\pch.h"

#ifndef POWERRENAME_SCOPE_COMPONENT_TESTS
#error This fixture belongs only to the explicitly selected component-test application.
#endif

#include "PowerRenameXAML\MainWindow.xaml.h"
#include <microsoft.ui.xaml.window.h>
#include <Aclapi.h>
#include <PowerRenameRegEx.h>
#include <Renaming.h>
#include <Settings.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <spdlog/sinks/daily_file_sink.h>

#include <array>
#include <deque>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <stdexcept>

extern HWND CurrentWindow;
extern CComPtr<IPowerRenameManager> g_prManager;
extern std::function<void(void)> g_itemToggledCallback;
extern std::vector<std::wstring> g_files;

namespace
{
    namespace fs = std::filesystem;
    constexpr unsigned commandCaseCount = 34;

    void Require(bool condition, const char* message)
    {
        if (!condition)
            throw std::runtime_error(message);
    }

    struct Call
    {
        std::string phase;
        std::string method;
        DWORD value;
        HRESULT result;
        bool written;
        DWORD state;
    };

    struct Evidence
    {
        std::string phase = "setup";
        std::vector<Call> calls;
        std::vector<std::string> failures;
        unsigned cases = 0;
        unsigned passed = 0;
        bool windowConstructionAttempted = false;
        bool windowConstructionCompleted = false;
        bool constructorAbandoned = false;
        DWORD sta = GetCurrentThreadId();

        void Record(const char* method, DWORD value = 0, HRESULT result = S_OK, bool written = false, DWORD state = 0)
        {
            if (GetCurrentThreadId() != sta)
                failures.push_back("call outside owning STA");
            calls.push_back({ phase, method, value, result, written, state });
        }
    };

    Evidence& Log()
    {
        static Evidence evidence;
        return evidence;
    }

    struct OwnedPaths
    {
        fs::path root;
        std::wstring scenario;
        std::vector<wil::unique_handle> pins;
        wil::unique_handle parent;

        void Pin(const fs::path& path, bool directory, bool immutable = false)
        {
            wil::unique_handle handle{ CreateFileW(path.c_str(), FILE_READ_ATTRIBUTES | GENERIC_READ,
                FILE_SHARE_READ | (immutable ? 0 : FILE_SHARE_WRITE), nullptr, OPEN_EXISTING,
                FILE_FLAG_OPEN_REPARSE_POINT | (directory ? FILE_FLAG_BACKUP_SEMANTICS : 0), nullptr) };
            Require(handle.is_valid(), "owned path open");
            BY_HANDLE_FILE_INFORMATION info{};
            Require(GetFileInformationByHandle(handle.get(), &info) != FALSE, "owned path identity");
            Require(!(info.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT), "reparse point rejected");
            Require(!!(info.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == directory, "owned path kind");
            Require(directory || info.nNumberOfLinks == 1, "hard link rejected");
            wchar_t finalPath[32768]{};
            const auto length = GetFinalPathNameByHandleW(handle.get(), finalPath, ARRAYSIZE(finalPath), FILE_NAME_NORMALIZED);
            Require(length && length < ARRAYSIZE(finalPath) &&
                CompareStringOrdinal(finalPath, -1, (L"\\\\?\\" + path.wstring()).c_str(), -1, TRUE) == CSTR_EQUAL,
                "owned path resolved elsewhere");
            pins.push_back(std::move(handle));
        }

        OwnedPaths()
        {
            int argc = 0;
            auto argv = CommandLineToArgvW(GetCommandLineW(), &argc);
            Require(argv != nullptr, "controller arguments");
            wil::unique_hlocal argvOwner{ argv };
            Require(argc == 7 && std::wstring_view{ argv[1] } == L"--scope-fixture", "explicit controller fixture required");
            root = argv[2];
            Require(root.is_absolute() && root == root.lexically_normal() &&
                root.wstring().find(L"..") == std::wstring::npos &&
                root.wstring().find(L':', 2) == std::wstring::npos &&
                root.wstring().substr(0, 2) != L"\\\\", "canonical local fixture required");
            const auto pid = std::stoul(argv[4]);
            Require(pid != 0 && pid != GetCurrentProcessId(), "controller PID");
            parent.reset(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, pid));
            Require(parent.is_valid() && WaitForSingleObject(parent.get(), 0) == WAIT_TIMEOUT, "live controller required");
            FILETIME created{}, exited{}, kernel{}, user{};
            Require(GetProcessTimes(parent.get(), &created, &exited, &kernel, &user) != FALSE, "controller start time");
            ULARGE_INTEGER start{};
            start.LowPart = created.dwLowDateTime;
            start.HighPart = created.dwHighDateTime;
            Require(start.QuadPart == std::stoull(argv[5]), "controller PID reused");
            wchar_t image[32768]{};
            DWORD size = ARRAYSIZE(image);
            Require(QueryFullProcessImageNameW(parent.get(), 0, image, &size) != FALSE &&
                CompareStringOrdinal(image, -1, argv[6], -1, TRUE) == CSTR_EQUAL, "controller full image");

            auto part = root.root_path();
            Pin(part, true);
            for (const auto& segment : root.relative_path())
            {
                part /= segment;
                Pin(part, true);
            }
            PSID ownerSid = nullptr;
            PACL acl = nullptr;
            PSECURITY_DESCRIPTOR descriptor = nullptr;
            Require(GetSecurityInfo(pins.back().get(), SE_FILE_OBJECT,
                OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION, &ownerSid, nullptr, &acl, nullptr,
                &descriptor) == ERROR_SUCCESS, "fixture security descriptor");
            wil::unique_hlocal descriptorOwner{ descriptor };
            wil::unique_handle token;
            Require(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, token.put()) != FALSE, "fixture process token");
            DWORD tokenBytes = 0;
            GetTokenInformation(token.get(), TokenUser, nullptr, 0, &tokenBytes);
            std::vector<unsigned char> tokenData(tokenBytes);
            Require(GetTokenInformation(token.get(), TokenUser, tokenData.data(), tokenBytes, &tokenBytes) != FALSE,
                "fixture token identity");
            const auto tokenUser = reinterpret_cast<const TOKEN_USER*>(tokenData.data());
            Require(EqualSid(ownerSid, tokenUser->User.Sid) && acl && acl->AceCount == 1, "exclusive fixture owner/ACL");
            SECURITY_DESCRIPTOR_CONTROL descriptorControl{};
            DWORD revision = 0;
            Require(GetSecurityDescriptorControl(descriptor, &descriptorControl, &revision) &&
                (descriptorControl & SE_DACL_PROTECTED), "fixture inherited ACL rejected");
            void* ace = nullptr;
            Require(GetAce(acl, 0, &ace) != FALSE, "fixture ACL entry");
            const auto allow = static_cast<ACCESS_ALLOWED_ACE*>(ace);
            Require(allow->Header.AceType == ACCESS_ALLOWED_ACE_TYPE &&
                EqualSid(&allow->SidStart, tokenUser->User.Sid), "exclusive fixture access");
            TOKEN_ELEVATION elevation{};
            Require(GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &tokenBytes) &&
                !elevation.TokenIsElevated, "elevated component execution prohibited");
            Pin(root / L"owner.txt", false, true);
            Pin(root / L"test-case.json", false, true);
            std::ifstream owner{ root / L"owner.txt" };
            std::string nonce;
            std::getline(owner, nonce);
            Require(nonce == winrt::to_string(argv[3]) && nonce.size() == 36, "controller ownership nonce");
            Pin(root / L"PowerRename", true);
            Pin(root / L"PowerRename" / L"Logs", true);
            Pin(root / L"PowerRename" / L"Logs" / get_product_version(), true);
            for (const auto* name : { L"language.json", L"settings.json", L"log_settings.json" })
                Pin(root / name, false);
            for (const auto* name : { L"power-rename-settings.json", L"power-rename-ui-flags",
                     L"power-rename-last-run-data.json", L"search-mru.json", L"replace-mru.json" })
                Pin(root / L"PowerRename" / name, false);

            const auto language = json::from_file((root / L"language.json").wstring());
            const auto module = json::from_file((root / L"PowerRename" / L"power-rename-settings.json").wstring());
            const auto testCase = json::from_file((root / L"test-case.json").wstring());
            Require(testCase && testCase->Size() == 1, "explicit owned test scenario required");
            scenario = testCase->GetNamedString(L"scenario").c_str();
            Require(scenario == L"commands" || scenario == L"persist-read" ||
                        scenario == L"persist-write" || scenario == L"persist-off",
                    "unknown test scenario");
            const bool enabled = scenario == L"persist-read" || scenario == L"persist-write";
            Require(language && language->GetNamedString(L"language") == L"", "language override prohibited");
            Require(module && module->GetNamedBoolean(L"PersistState") == enabled &&
                        module->GetNamedBoolean(L"MRUEnabled") == enabled &&
                        !module->GetNamedBoolean(L"UseBoostLib"),
                    "preseeded module settings differ from checked scenario");
            for (const auto& entry : fs::recursive_directory_iterator(root))
            {
                Require(!entry.is_symlink() && !(GetFileAttributesW(entry.path().c_str()) & FILE_ATTRIBUTE_REPARSE_POINT),
                    "owned tree contains reparse point");
                if (entry.is_regular_file())
                    Require(fs::hard_link_count(entry.path()) == 1, "owned tree contains hard link");
            }
        }
    };

    OwnedPaths& Paths()
    {
        // App's language lookup precedes OnLaunched; no cross-TU initialization order or ambient fallback.
        try
        {
            static OwnedPaths paths;
            return paths;
        }
        catch (...)
        {
            ExitProcess(86);
        }
    }

    unsigned ExpectedCaseCount()
    {
        return Paths().scenario == L"commands"      ? commandCaseCount :
               Paths().scenario == L"persist-write" ? 1u :
                                                      2u;
    }

    struct FlagRead
    {
        HRESULT result;
        bool write;
        DWORD value;
    };

    class MainWindowRecordingManager final : public IPowerRenameManager
    {
        ULONG references = 1;
        CComPtr<IPowerRenameItemFactory> itemFactory;
        CComPtr<IPowerRenameRegEx> regex;
        IPowerRenameManagerEvents* sink = nullptr;

        HRESULT Unexpected(const char* method) const
        {
            Log().Record(method, 0, E_UNEXPECTED, false, flags);
            Log().failures.push_back(std::string{ "unexpected manager call: " } + method);
            return E_UNEXPECTED;
        }

        HRESULT Count(const char* method, UINT* count, HRESULT result = S_OK, UINT value = 0)
        {
            *count = value;
            Log().Record(method, value, result, true, flags);
            return result;
        }

    public:
        DWORD flags = 0;
        DWORD filter = PowerRenameFilters::None;
        DWORD cookie = 0;
        std::deque<FlagRead> reads;
        std::deque<HRESULT> writes;
        HRESULT countResult = S_OK;
        UINT selectedCount = 0;
        UINT renamingCount = 0;
        std::vector<CComPtr<IPowerRenameItem>> items;

        HRESULT QueryInterface(REFIID iid, void** result) override
        {
            *result = nullptr;
            const bool supported = iid == __uuidof(IUnknown) || iid == __uuidof(IPowerRenameManager);
            Log().Record(iid == __uuidof(IUnknown) ? "QI.IUnknown" :
                iid == __uuidof(IPowerRenameManager) ? "QI.IPowerRenameManager" : "QI.unsupported",
                0, supported ? S_OK : E_NOINTERFACE);
            if (!supported)
            {
                Log().failures.push_back("unexpected QI");
                return E_NOINTERFACE;
            }
            *result = static_cast<IPowerRenameManager*>(this);
            AddRef();
            return S_OK;
        }

        ULONG AddRef() override
        {
            Log().Record("AddRef", ++references);
            return references;
        }

        ULONG Release() override
        {
            const auto remaining = --references;
            Log().Record("Release", remaining);
            if (!remaining)
                delete this;
            return remaining;
        }

        HRESULT Advise(IPowerRenameManagerEvents* events, DWORD* output) override
        {
            Require(!sink && events && output, "one owned Advise");
            sink = events;
            const auto ref = sink->AddRef();
            cookie = 0x502;
            *output = cookie;
            Log().Record("sink.AddRef", ref);
            Log().Record("Advise", cookie, S_OK, true);
            return S_OK;
        }

        HRESULT UnAdvise(DWORD value) override
        {
            Log().Record("UnAdvise", value);
            if (!sink || value != cookie)
                return Unexpected("UnAdvise.invalid-cookie");
            Log().Record("sink.Release", sink->Release());
            sink = nullptr;
            cookie = 0;
            return S_OK;
        }

        void AbandonUnwoundWindowEvents()
        {
            Log().Record("Advise.abandoned-after-constructor-unwind", cookie, E_UNEXPECTED, false, flags);
            if (sink)
                Log().failures.push_back("Embedded events lifetime ended; retained Advise reference cannot be released");
            sink = nullptr;
            cookie = 0;
        }

        HRESULT GetFlags(DWORD* output) override
        {
            const auto input = *output;
            FlagRead script{ S_OK, true, flags };
            if (!reads.empty())
            {
                script = reads.front();
                reads.pop_front();
            }
            if (script.write)
                *output = script.value;
            Log().Record("GetFlags.input", input);
            Log().Record("GetFlags", *output, script.result, script.write, flags);
            return script.result;
        }

        HRESULT PutFlags(DWORD value) override
        {
            HRESULT result = S_OK;
            if (!writes.empty())
            {
                result = writes.front();
                writes.pop_front();
            }
            if (SUCCEEDED(result))
                flags = value;
            Log().Record("PutFlags", value, result, false, flags);
            return result;
        }

        HRESULT GetItemCount(UINT* count) override { return Count("GetItemCount", count, countResult, static_cast<UINT>(items.size())); }
        HRESULT GetVisibleItemCount(UINT* count) override
        {
            return Count("GetVisibleItemCount", count, S_OK, items.empty() ? 0u : filter == PowerRenameFilters::ShouldRename ? 2u :
                                                                                                                               static_cast<UINT>(items.size()));
        }
        HRESULT GetSelectedItemCount(UINT* count) override
        {
            UINT value = selectedCount;
            if (!items.empty())
            {
                value = 0;
                for (const auto& item : items)
                {
                    bool selected = false;
                    const auto hr = item->GetSelected(&selected);
                    if (FAILED(hr))
                        return hr;
                    if (selected)
                        ++value;
                }
            }
            return Count("GetSelectedItemCount", count, S_OK, value);
        }
        HRESULT GetRenameItemCount(UINT* count) override
        {
            UINT value = renamingCount;
            if (!items.empty())
            {
                value = 0;
                for (const auto& item : items)
                {
                    bool eligible = false;
                    const auto hr = item->ShouldRenameItem(flags, &eligible);
                    if (FAILED(hr))
                        return hr;
                    if (eligible)
                        ++value;
                }
            }
            return Count("GetRenameItemCount", count, S_OK, value);
        }

        HRESULT PutRenameItemFactory(IPowerRenameItemFactory* factory) override
        {
            Log().Record("PutRenameItemFactory");
            itemFactory = factory;
            return factory ? S_OK : E_POINTER;
        }

        HRESULT GetRenameRegEx(IPowerRenameRegEx** output) override
        {
            *output = nullptr;
            HRESULT result = S_OK;
            if (!regex)
                result = CPowerRenameRegEx::s_CreateInstance(false, &regex);
            if (SUCCEEDED(result))
                result = regex.CopyTo(output);
            Log().Record("GetRenameRegEx", 0, result, *output != nullptr);
            return result;
        }

        HRESULT Start() override { return Unexpected("Start"); }
        HRESULT Stop() override { return Unexpected("Stop"); }
        HRESULT Reset() override { return Unexpected("Reset"); }
        HRESULT Shutdown() override { return Unexpected("Shutdown"); }
        HRESULT Rename(HWND window, bool close) override
        {
            if (Paths().scenario == L"commands")
                return Unexpected("Rename");
            Require(window == CurrentWindow && !close, "owned nonclosing lifecycle Rename request");
            Log().Record("Rename.no-file-operation", close, S_OK, false, flags);
            return S_OK;
        }
        HRESULT UpdateChildrenPath(int, size_t) override { return Unexpected("UpdateChildrenPath"); }
        HRESULT GetCloseUIWindowAfterRenaming(bool* value) override { *value = false; return Unexpected("GetCloseUIWindowAfterRenaming"); }
        HRESULT AddItem(IPowerRenameItem*) override { return Unexpected("AddItem"); }
        HRESULT GetItemByIndex(UINT index, IPowerRenameItem** output) override
        {
            *output = nullptr;
            const auto hr = index < items.size() ? items[index].CopyTo(output) : E_FAIL;
            Log().Record("GetItemByIndex", index, hr, *output != nullptr, flags);
            return hr;
        }
        HRESULT GetVisibleItemByIndex(UINT index, IPowerRenameItem** output) override
        {
            return GetItemByIndex(GetVisibleItemRealIndex(index), output);
        }
        HRESULT SetVisible() override { return Unexpected("SetVisible"); }
        HRESULT GetItemById(int, IPowerRenameItem** output) override { *output = nullptr; return Unexpected("GetItemById"); }
        HRESULT GetFilter(DWORD* output) override
        {
            *output = filter;
            Log().Record("GetFilter", filter, S_OK, true, flags);
            return S_OK;
        }
        HRESULT SwitchFilter(int index) override
        {
            if (index != 0)
                return Unexpected("SwitchFilter.nonzero");
            filter = filter == PowerRenameFilters::None ? PowerRenameFilters::ShouldRename : PowerRenameFilters::None;
            Log().Record("SwitchFilter", 0, S_OK, false, filter);
            return S_OK;
        }
        HRESULT PutRenameRegEx(IPowerRenameRegEx*) override { return Unexpected("PutRenameRegEx"); }
        HRESULT GetRenameItemFactory(IPowerRenameItemFactory** output) override { *output = nullptr; return Unexpected("GetRenameItemFactory"); }
        uint32_t GetVisibleItemRealIndex(uint32_t index) const override
        {
            const auto size = items.empty() ? 0u : filter == PowerRenameFilters::ShouldRename ? 2u :
                                                                                                static_cast<UINT>(items.size());
            if (index >= size)
                Unexpected("GetVisibleItemRealIndex.out-of-range");
            Log().Record("GetVisibleItemRealIndex", index, S_OK, true, filter);
            return index;
        }

        ~MainWindowRecordingManager()
        {
            if (sink)
                Log().failures.push_back("destroyed with live Advise");
            Log().Record("manager.destroy");
        }
    };

    CComPtr<IPowerRenameManager> fixtureManager;
    MainWindowRecordingManager* recorder = nullptr;

    void Hidden(HWND hwnd)
    {
        Require(GetCurrentThreadId() == Log().sta, "window lifecycle STA");
        Require(IsWindow(hwnd) && !IsWindowVisible(hwnd) && GetForegroundWindow() != hwnd,
            "component window became visible or foreground");
        Require(WaitForSingleObject(Paths().parent.get(), 0) == WAIT_TIMEOUT, "controller exited");
    }

    void VerifyFlagTrace(size_t start, const std::vector<DWORD>& expectedPuts, DWORD finalFlags)
    {
        std::vector<DWORD> puts;
        std::vector<DWORD> resets;
        std::vector<std::string> methods;
        for (auto index = start; index < Log().calls.size(); ++index)
        {
            const auto& call = Log().calls[index];
            methods.push_back(call.method);
            if (call.method == "GetFlags.input")
                Require(call.value == 0, "flag command input must start zeroed");
            if (call.method == "PutFlags")
                puts.push_back(call.value);
            if (call.method == "Reset")
                resets.push_back(call.state);
        }
        std::vector<std::string> expected;
        for (size_t index = 0; index < expectedPuts.size(); ++index)
            for (const auto* method : { "GetFlags.input", "GetFlags", "PutFlags", "GetItemCount", "Reset" })
                expected.emplace_back(method);
        Require(puts == expectedPuts && resets == expectedPuts && methods == expected &&
                    recorder->flags == finalFlags,
                "actual option command flags, preservation and Reset trace");
    }

    void RunAdditionalUiTests(winrt::PowerRenameUI::implementation::MainWindow* implementation, HWND hwnd)
    {
        using Toggle = winrt::Microsoft::UI::Xaml::Controls::Primitives::ToggleButton;
        const auto regex = implementation->checkBox_regex().as<Toggle>();
        const auto sensitive = implementation->checkBox_case().as<Toggle>();
        const auto matchAll = implementation->checkBox_matchAll().as<Toggle>();
        const auto enumerate = implementation->toggleButton_enumItems().as<Toggle>();
        const auto files = implementation->toggleButton_includeFiles().as<Toggle>();
        const auto folders = implementation->toggleButton_includeFolders().as<Toggle>();
        const auto subfolders = implementation->toggleButton_includeSubfolders().as<Toggle>();
        const auto upper = implementation->toggleButton_upperCase();
        const auto lower = implementation->toggleButton_lowerCase();
        const auto title = implementation->toggleButton_titleCase();
        const auto capitalized = implementation->toggleButton_capitalize();
        const auto prepare = [](const char* name) {
            Log().phase = std::string{ "setup:" } + name;
            recorder->countResult = S_OK;
            recorder->reads.clear();
            recorder->writes.clear();
        };
        struct FlagCase
        {
            const char* name;
            Toggle control;
            bool checked;
            DWORD initial;
            DWORD expected;
        };
        const std::array<FlagCase, 8> direct{ {
            { "regex-on", regex, true, 0x11, 0x15 },
            { "regex-off", regex, false, 0x15, 0x11 },
            { "sensitive-on", sensitive, true, 0x10, 0x11 },
            { "sensitive-off", sensitive, false, 0x11, 0x10 },
            { "match-all-on", matchAll, true, 0x11, 0x13 },
            { "match-all-off", matchAll, false, 0x13, 0x11 },
            { "enumerate-on", enumerate, true, 0x11, 0x19 },
            { "enumerate-off", enumerate, false, 0x19, 0x11 },
        } };
        for (const auto& test : direct)
        {
            prepare(test.name);
            test.control.IsChecked(!test.checked);
            recorder->flags = test.initial;
            Hidden(hwnd);
            const auto start = Log().calls.size();
            Log().phase = std::string{ "action:" } + test.name;
            ++Log().cases;
            test.control.IsChecked(test.checked);
            Require(test.control.IsChecked().Value() == test.checked, "actual direct control state");
            VerifyFlagTrace(start, { test.expected }, test.expected);
            Hidden(hwnd);
            ++Log().passed;
        }

        const auto runSelectionFilterTests = [&] {
            Log().phase = "setup:selection-filter";
            recorder->flags = 0;
            const std::array<PCWSTR, 3> names{ L"match1.txt", L"match2.txt", L"keepme.txt" };
            for (size_t index = 0; index < names.size(); ++index)
            {
                CComPtr<IPowerRenameItem> item;
                winrt::check_hresult(CPowerRenameItem::s_CreateInstance(nullptr, IID_PPV_ARGS(&item)));
                winrt::check_hresult(item->PutOriginalName(names[index]));
                winrt::check_hresult(item->PutPath((std::wstring{ L"C:\\fixtures\\" } + names[index]).c_str()));
                winrt::check_hresult(item->PutSelected(true));
                winrt::check_hresult(item->PutStatus(PowerRenameItemRenameStatus::ShouldRename));
                if (index < 2)
                    winrt::check_hresult(item->PutNewName(index == 0 ? L"X1.txt" : L"X2.txt"));
                recorder->items.push_back(std::move(item));
            }
            const auto header = implementation->checkBox_selectAll();
            Require(header.IsChecked().Value(), "header starts checked");
            ++Log().cases;
            for (const bool selected : { false, true })
            {
                Log().phase = selected ? "action:header-check" : "action:header-uncheck";
                Hidden(hwnd);
                header.IsChecked(selected);
                for (const auto& item : recorder->items)
                {
                    bool actual = !selected;
                    winrt::check_hresult(item->GetSelected(&actual));
                    Require(actual == selected, "actual header changes every real item");
                }
                const auto unchanged = Log().calls.size();
                implementation->SelectAll(nullptr, nullptr);
                Require(Log().calls.size() == unchanged, "repeated header state is a no-op");
                Log().Record("HeaderSelected", selected);
                Hidden(hwnd);
            }
            ++Log().passed;

            const auto source = implementation->ExplorerItems();
            const auto view = source.as<winrt::Windows::Foundation::Collections::IVectorView<winrt::Windows::Foundation::IInspectable>>();
            const auto verify = [&](uint32_t count) {
                Require(view.Size() == count, "actual source projected count");
                for (uint32_t index = 0; index < count; ++index)
                {
                    const auto row = view.GetAt(index).as<winrt::PowerRenameUI::ExplorerItemViewModel>();
                    Require(row.IdVM() == static_cast<int32_t>(index) && row.OriginalVM() == names[index],
                            "actual source preserves filtered and restored identities");
                }
            };
            ++Log().cases;
            Log().phase = "action:show-renamed";
            Hidden(hwnd);
            implementation->ShowRenamed(nullptr, nullptr);
            Require(implementation->button_showRenamed().IsChecked() &&
                        !implementation->button_showAll().IsChecked(),
                    "actual renamed filter control state");
            verify(2);
            const auto switches = [&] {
                size_t count = 0;
                for (const auto& call : Log().calls)
                    if (call.method == "SwitchFilter")
                        ++count;
                return count;
            };
            Require(switches() == 1, "desired renamed command switches once");
            Log().phase = "action:show-renamed-again";
            implementation->ShowRenamed(nullptr, nullptr);
            verify(2);
            Require(switches() == 1, "desired renamed command is idempotent");
            Log().phase = "action:show-all";
            implementation->ShowAll(nullptr, nullptr);
            Require(implementation->button_showAll().IsChecked() &&
                        !implementation->button_showRenamed().IsChecked(),
                    "actual all-files filter control state");
            verify(3);
            Require(switches() == 2, "desired all-files command switches once");
            Log().phase = "action:show-all-again";
            implementation->ShowAll(nullptr, nullptr);
            verify(3);
            Require(switches() == 2, "desired all-files command is idempotent");
            Hidden(hwnd);
            ++Log().passed;
        };

        struct IncludeCase
        {
            const char* name;
            unsigned included;
            std::vector<DWORD> puts;
            DWORD expected;
        };
        const std::array<IncludeCase, 8> includes{ {
            { "include-000", 0, {}, 0x71 },
            { "include-001", 1, { 0x61 }, 0x61 },
            { "include-010", 2, { 0x51 }, 0x51 },
            { "include-011", 3, { 0x61, 0x41 }, 0x41 },
            { "include-100", 4, { 0x31 }, 0x31 },
            { "include-101", 5, { 0x61, 0x21 }, 0x21 },
            { "include-110", 6, { 0x51, 0x11 }, 0x11 },
            { "include-111", 7, { 0x61, 0x41, 0x01 }, 0x01 },
        } };
        for (const auto& test : includes)
        {
            prepare(test.name);
            files.IsChecked(false);
            folders.IsChecked(false);
            subfolders.IsChecked(false);
            recorder->flags = 0x71;
            Hidden(hwnd);
            const auto start = Log().calls.size();
            Log().phase = std::string{ "action:" } + test.name;
            ++Log().cases;
            files.IsChecked((test.included & 1) != 0);
            folders.IsChecked((test.included & 2) != 0);
            subfolders.IsChecked((test.included & 4) != 0);
            Require(files.IsChecked().Value() == ((test.included & 1) != 0) &&
                        folders.IsChecked().Value() == ((test.included & 2) != 0) &&
                        subfolders.IsChecked().Value() == ((test.included & 4) != 0),
                    "three include controls remain independent");
            VerifyFlagTrace(start, test.puts, test.expected);
            Hidden(hwnd);
            ++Log().passed;
        }

        struct CaseTransition
        {
            const char* name;
            Toggle from;
            Toggle to;
            DWORD initial;
            DWORD expected;
        };
        const std::array<CaseTransition, 6> transitions{ {
            { "upper-to-lower", upper, lower, 0x211, 0x411 },
            { "lower-to-upper", lower, upper, 0x411, 0x211 },
            { "upper-to-title", upper, title, 0x211, 0x811 },
            { "title-to-upper", title, upper, 0x811, 0x211 },
            { "lower-to-title", lower, title, 0x411, 0x811 },
            { "title-to-lower", title, lower, 0x811, 0x411 },
        } };
        for (const auto& test : transitions)
        {
            prepare(test.name);
            upper.IsChecked(false);
            lower.IsChecked(false);
            title.IsChecked(false);
            capitalized.IsChecked(false);
            test.from.IsChecked(true);
            recorder->flags = test.initial;
            Hidden(hwnd);
            const auto start = Log().calls.size();
            Log().phase = std::string{ "action:" } + test.name;
            ++Log().cases;
            test.to.IsChecked(true);
            const unsigned selected = static_cast<unsigned>(upper.IsChecked().Value()) +
                                      static_cast<unsigned>(lower.IsChecked().Value()) + static_cast<unsigned>(title.IsChecked().Value()) +
                                      static_cast<unsigned>(capitalized.IsChecked().Value());
            Require(selected == 1 && test.to.IsChecked().Value() && !test.from.IsChecked().Value(),
                    "actual case controls are mutually exclusive");
            VerifyFlagTrace(start, { 0x11, test.expected }, test.expected);
            Hidden(hwnd);
            ++Log().passed;
        }

        prepare("apply-count-sequence");
        Require(static_cast<bool>(g_itemToggledCallback), "actual item-toggle callback required");
        recorder->selectedCount = 3;
        ++Log().cases;
        for (const auto count : { 0u, 2u, 0u })
        {
            recorder->renamingCount = count;
            Log().phase = "action:apply-count-" + std::to_string(count);
            Hidden(hwnd);
            const auto start = Log().calls.size();
            g_itemToggledCallback();
            Require(Log().calls.size() == start + 2 &&
                        Log().calls[start].method == "GetSelectedItemCount" &&
                        Log().calls[start].value == 3 &&
                        Log().calls[start + 1].method == "GetRenameItemCount" &&
                        Log().calls[start + 1].value == count,
                    "actual count command consumes selected and rename counts");
            const bool enabled = implementation->button_rename().IsEnabled();
            Require(enabled == (count != 0), "actual Apply enablement follows rename count");
            Log().Record("ApplyEnabled", enabled);
            Hidden(hwnd);
        }
        recorder->selectedCount = 0;
        recorder->renamingCount = 0;
        ++Log().passed;
        runSelectionFilterTests();
    }

    void RunLifecycleTests(winrt::PowerRenameUI::implementation::MainWindow* implementation, HWND hwnd)
    {
        const bool off = Paths().scenario == L"persist-off";
        const bool writing = Paths().scenario == L"persist-write";
        Log().phase = "action:" + winrt::to_string(Paths().scenario);
        Hidden(hwnd);
        ++Log().cases;
        const auto search = implementation->textBox_search();
        const auto replacement = implementation->textBox_replace();
        const PCWSTR expectedSearch = off ? L"" : writing ? L"before" :
                                                            L"persist";
        const PCWSTR expectedReplace = off ? L"" : writing ? L"OLD" :
                                                             L"KEPT";
        Require(search.Text() == expectedSearch && replacement.Text() == expectedReplace,
                "actual constructor persistence gate and restored text");
        CComPtr<IPowerRenameRegEx> engine;
        winrt::check_hresult(recorder->GetRenameRegEx(&engine));
        CComHeapPtr<wchar_t> engineSearch;
        CComHeapPtr<wchar_t> engineReplace;
        winrt::check_hresult(engine->GetSearchTerm(&engineSearch));
        winrt::check_hresult(engine->GetReplaceTerm(&engineReplace));
        Require(engineSearch && engineReplace &&
                    std::wstring_view(engineSearch) == expectedSearch &&
                    std::wstring_view(engineReplace) == expectedReplace,
                "constructor automatically delivers actual engine terms");
        Log().Record(("SearchText=" + winrt::to_string(search.Text())).c_str());
        Log().Record(("ReplaceText=" + winrt::to_string(replacement.Text())).c_str());
        if (!off && !writing)
        {
            CComPtr<IPowerRenameItem> item;
            winrt::check_hresult(CPowerRenameItem::s_CreateInstance(nullptr, IID_PPV_ARGS(&item)));
            winrt::check_hresult(item->PutOriginalName(L"persist.txt"));
            winrt::check_hresult(item->PutPath(L"C:\\fixtures\\persist.txt"));
            unsigned long index = 0;
            Require(DoRename(engine, index, item), "restored engine terms produce actual preview");
            CComHeapPtr<wchar_t> candidate;
            winrt::check_hresult(item->GetNewName(&candidate));
            Require(candidate && std::wstring_view(candidate) == L"KEPT.txt" && index == 1,
                    "literal preview uses constructor-restored inputs");
            Log().Record("RestoredPreview=KEPT.txt", index);
        }
        if (writing || off)
        {
            search.Text(writing ? L"persist" : L"do-not-save");
            replacement.Text(writing ? L"KEPT" : L"NO-SAVE");
            recorder->selectedCount = 1;
            recorder->renamingCount = 1;
            g_itemToggledCallback();
            Require(implementation->button_rename().IsEnabled(), "recording Apply unit precondition");
            implementation->button_rename_Click(implementation->button_rename(), nullptr);
            LastRunSettings fresh;
            Require(fresh.GetSearchText() == L"persist" && fresh.GetReplaceText() == L"KEPT",
                    "actual Apply writes enabled input, disabled input leaves seed untouched");
            const auto disk = json::from_file((Paths().root / L"PowerRename" / L"power-rename-last-run-data.json").wstring());
            Require(disk && disk->GetNamedString(L"SearchText") == L"persist" &&
                        disk->GetNamedString(L"ReplaceText") == L"KEPT",
                    "independent actual LastRun disk readback");
            CSettings settings;
            Require(settings.GetFlags() == (writing ? recorder->flags : 0),
                    "actual flag persistence follows enabled gate");
            Log().Record(writing ? "ActualApplySaved=persist,KEPT" : "ActualApplyDidNotSave");
            recorder->selectedCount = 0;
            recorder->renamingCount = 0;
        }
        Hidden(hwnd);
        ++Log().passed;
        if (!writing)
        {
            ++Log().cases;
            Log().phase = off ? "action:mru-off-prefix" : "action:mru-on-prefix";
            search.Text(L"p");
            const auto values = implementation->SearchMRU();
            bool matching = false;
            for (const auto& value : values)
                if (value == L"persist")
                    matching = true;
            Require(off ? values.Size() == 0 : matching && values.Size() == 2,
                    "actual MRU initialization gate with known stored prefix entries");
            Log().Record(off ? "SearchSuggestions=empty" : "SearchSuggestions=persist,previous", values.Size());
            Hidden(hwnd);
            ++Log().passed;
        }
    }

    void SaveResult()
    {
        json::JsonObject result;
        result.SetNamedValue(L"status", json::value(Log().failures.empty() && Log().cases == ExpectedCaseCount() && Log().passed == ExpectedCaseCount() && Log().windowConstructionCompleted && !Log().constructorAbandoned ? L"PASS" : L"FAIL"));
        result.SetNamedValue(L"windowConstructionAttempted", json::value(Log().windowConstructionAttempted));
        result.SetNamedValue(L"windowConstructionCompleted", json::value(Log().windowConstructionCompleted));
        result.SetNamedValue(L"constructorAbandoned", json::value(Log().constructorAbandoned));
        result.SetNamedValue(L"cases", json::value(Log().cases));
        result.SetNamedValue(L"passed", json::value(Log().passed));
        result.SetNamedValue(L"thread", json::value(Log().sta));
        json::JsonArray calls;
        for (const auto& call : Log().calls)
        {
            json::JsonObject row;
            row.SetNamedValue(L"phase", json::value(winrt::to_hstring(call.phase)));
            row.SetNamedValue(L"method", json::value(winrt::to_hstring(call.method)));
            row.SetNamedValue(L"value", json::value(call.value));
            row.SetNamedValue(L"hresult", json::value(static_cast<int>(call.result)));
            row.SetNamedValue(L"written", json::value(call.written));
            row.SetNamedValue(L"state", json::value(call.state));
            calls.Append(row);
        }
        result.SetNamedValue(L"ledger", calls);
        json::JsonArray failures;
        for (const auto& failure : Log().failures)
            failures.Append(json::value(winrt::to_hstring(failure)));
        result.SetNamedValue(L"failures", failures);
        const auto text = winrt::to_string(result.Stringify());
        wil::unique_handle file{ CreateFileW((Paths().root / L"result.json").c_str(), GENERIC_WRITE,
            FILE_SHARE_READ, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OPEN_REPARSE_POINT, nullptr) };
        Require(file.is_valid(), "exclusive result");
        DWORD written = 0;
        Require(WriteFile(file.get(), text.data(), static_cast<DWORD>(text.size()), &written, nullptr) &&
            written == text.size() && FlushFileBuffers(file.get()), "complete result flush");
    }

    void Run()
    {
        auto savedManager = std::move(g_prManager);
        auto savedCallback = std::move(g_itemToggledCallback);
        auto savedFiles = std::move(g_files);
        const auto savedWindow = CurrentWindow;
        g_files.clear();
        winrt::PowerRenameUI::MainWindow window{ nullptr };
        winrt::Microsoft::UI::Xaml::Controls::ComboBox control{ nullptr };
        winrt::Windows::Foundation::Collections::IObservableVector<winrt::Windows::Foundation::IInspectable> items{ nullptr };
        winrt::event_token resetToken{};
        bool subscribed = false;
        bool closed = false;
        HWND hwnd = nullptr;
        try
        {
            auto logger = spdlog::get(LogSettings::powerRenameLoggerName);
            Require(logger && logger->sinks().size() == 1, "actual registered file logger required");
            const auto sink = std::dynamic_pointer_cast<spdlog::sinks::daily_file_sink_mt>(logger->sinks().front());
            Require(sink != nullptr, "actual daily file sink required; null fallback prohibited");
            Require(fs::path{ sink->filename() }.parent_path() ==
                Paths().root / L"PowerRename" / L"Logs" / get_product_version(), "logger outside owned version directory");

            Log().windowConstructionAttempted = true;
            window = winrt::make<winrt::PowerRenameUI::implementation::MainWindow>();
            Log().windowConstructionCompleted = true;
            auto implementation = winrt::get_self<winrt::PowerRenameUI::implementation::MainWindow>(window);
            winrt::check_hresult(window.as<IWindowNative>()->get_WindowHandle(&hwnd));
            Hidden(hwnd);
            Require(recorder && fixtureManager && g_prManager == fixtureManager && recorder->cookie == 0x502,
                "factory must own actual advised manager");
            window.Closed([&](auto const&, auto const&) {
                closed = true;
                Log().Record("Closed");
            });
            control = implementation->comboBox_renameParts();
            items = window.ExplorerItems().as<winrt::Windows::Foundation::Collections::IObservableVector<winrt::Windows::Foundation::IInspectable>>();
            resetToken = items.VectorChanged([](auto const&, auto const& args) {
                Require(args.CollectionChange() == winrt::Windows::Foundation::Collections::CollectionChange::Reset,
                    "unexpected vector change");
                Log().Record("Reset", 0, S_OK, false, recorder->flags);
            });
            subscribed = true;

            if (Paths().scenario == L"commands")
            {
                struct Case
                {
                    const char* name;
                    int from;
                    int to;
                    DWORD initial;
                    std::vector<FlagRead> reads;
                    std::vector<HRESULT> writes;
                    HRESULT count;
                    std::vector<DWORD> puts;
                    std::vector<DWORD> resets;
                    HRESULT thrown;
                };
                // Independent literal UI-02 oracle: unrelated U=0x11, N=0x80, E=0x100.
                const std::array<Case, 9> cases{ {
                    { "N-to-E", 1, 2, 0x91, {}, {}, S_OK, { 0x11, 0x111 }, { 0x11, 0x111 }, S_OK },
                    { "E-to-N", 2, 1, 0x111, {}, {}, S_OK, { 0x11, 0x91 }, { 0x11, 0x91 }, S_OK },
                    { "N-to-zero-second-no-op", 1, 0, 0x91, {}, {}, S_OK, { 0x11, 0x11 }, { 0x11, 0x11 }, S_OK },
                    { "E-to-zero-first-no-op", 2, 0, 0x111, {}, {}, S_OK, { 0x111, 0x11 }, { 0x111, 0x11 }, S_OK },
                    { "N-to-unselected", 1, -1, 0x91, {}, {}, S_OK, {}, {}, S_OK },
                    { "failed-unwritten-reads", 1, 2, 0x91, { { E_FAIL, false, 0 }, { E_FAIL, false, 0 } }, {}, S_OK, { 0, 0x100 }, { 0, 0x100 }, S_OK },
                    { "failed-written-read", 1, 2, 0x91, { { E_FAIL, true, 0x91 }, { S_OK, true, 0x11 } }, {}, S_OK, { 0x11, 0x111 }, { 0x11, 0x111 }, S_OK },
                    { "failed-writes-still-reset", 1, 2, 0x91, {}, { E_FAIL, E_FAIL }, S_OK, { 0x11, 0x191 }, { 0x91, 0x91 }, S_OK },
                    { "count-failure-before-reset", 1, 2, 0x91, {}, {}, E_ACCESSDENIED, { 0x11 }, {}, E_ACCESSDENIED },
                } };
                for (const auto& test : cases)
                {
                    Log().phase = std::string{ "setup:" } + test.name;
                    recorder->countResult = S_OK;
                    recorder->reads.clear();
                    recorder->writes.clear();
                    control.SelectedIndex(test.from);
                    recorder->flags = test.initial;
                    recorder->reads.assign(test.reads.begin(), test.reads.end());
                    recorder->writes.assign(test.writes.begin(), test.writes.end());
                    recorder->countResult = test.count;
                    Hidden(hwnd);
                    const auto start = Log().calls.size();
                    Log().phase = std::string{ "action:" } + test.name;
                    ++Log().cases;
                    HRESULT thrown = S_OK;
                    try
                    {
                        control.SelectedIndex(test.to);
                    }
                    catch (const winrt::hresult_error& error)
                    {
                        thrown = error.code();
                    }
                    Require(thrown == test.thrown, "count failure differs from ignored flag failure");
                    std::vector<DWORD> puts;
                    std::vector<DWORD> resets;
                    std::vector<std::string> methods;
                    for (auto index = start; index < Log().calls.size(); ++index)
                    {
                        const auto& call = Log().calls[index];
                        methods.push_back(call.method);
                        if (call.method == "GetFlags.input")
                            Require(call.value == 0, "GetFlags output was not zero-initialized");
                        if (call.method == "PutFlags")
                            puts.push_back(call.value);
                        if (call.method == "Reset")
                            resets.push_back(call.state);
                    }
                    std::vector<std::string> expected;
                    for (size_t index = 0; index < test.puts.size(); ++index)
                    {
                        for (const auto* method : { "GetFlags.input", "GetFlags", "PutFlags", "GetItemCount" })
                            expected.emplace_back(method);
                        if (index < test.resets.size())
                            expected.emplace_back("Reset");
                    }
                    Require(puts == test.puts && resets == test.resets && methods == expected,
                            "UI-02 ordered call/intermediate/no-op/Reset oracle");
                    Require(recorder->reads.empty() && recorder->writes.empty(), "unconsumed script");
                    Hidden(hwnd);
                    ++Log().passed;
                }
                RunAdditionalUiTests(implementation, hwnd);
            }
            else
            {
                RunLifecycleTests(implementation, hwnd);
            }
        }
        catch (const winrt::hresult_error& error)
        {
            Log().failures.push_back("HRESULT " + std::to_string(error.code().value) + ": " + winrt::to_string(error.message()));
        }
        catch (const std::exception& error)
        {
            Log().failures.push_back(error.what());
        }
        catch (...)
        {
            Log().failures.push_back("unknown component failure");
        }

        if (Log().windowConstructionAttempted && !Log().windowConstructionCompleted)
        {
            Log().phase = "failed-construction";
            Log().constructorAbandoned = true;
            Log().failures.push_back("MainWindow constructor unwound; normal window/COM/global cleanup is prohibited");
            if (recorder)
                recorder->AbandonUnwoundWindowEvents();
            g_itemToggledCallback = {};
            Log().Record("normal-cleanup.skipped-process-exit-required", 88, E_UNEXPECTED);
            try
            {
                SaveResult();
            }
            catch (...)
            {
                ExitProcess(87);
            }
            ExitProcess(88);
        }

        Log().phase = "teardown";
        auto cleanup = [](const char* name, auto&& action) {
            try
            {
                action();
                Log().Record(name);
            }
            catch (const winrt::hresult_error& error)
            {
                Log().failures.push_back(std::string{ name } + ": HRESULT " +
                    std::to_string(error.code().value) + ": " + winrt::to_string(error.message()));
            }
            catch (const std::exception& error)
            {
                Log().failures.push_back(std::string{ name } + ": " + error.what());
            }
            catch (...)
            {
                Log().failures.push_back(std::string{ name } + ": unknown failure");
            }
        };
        cleanup("revoke.Reset", [&] { if (subscribed) items.VectorChanged(resetToken); });
        control = nullptr;
        items = nullptr;
        cleanup("UnAdvise.alive", [&] {
            if (recorder && recorder->cookie)
            {
                Require(Log().windowConstructionCompleted, "UnAdvise requires completed construction");
                Require(IsWindow(CurrentWindow), "UnAdvise requires live window");
                winrt::check_hresult(recorder->UnAdvise(recorder->cookie));
            }
        });
        cleanup("Close.actual", [&] {
            if (window)
            {
                Hidden(hwnd);
                window.Close();
                Require(closed && !IsWindow(hwnd), "actual Closed/owned HWND destruction");
            }
            else
                Require(CurrentWindow == savedWindow, "partial construction left HWND");
        });
        window = nullptr;
        g_itemToggledCallback = {};
        g_prManager.Release();
        fixtureManager.Release();
        recorder = nullptr;
        g_itemToggledCallback = std::move(savedCallback);
        g_prManager = std::move(savedManager);
        g_files = std::move(savedFiles);
        CurrentWindow = savedWindow;
        Log().Record("globals.restored");
        cleanup("logger.flush", [] {
            auto logger = spdlog::get(LogSettings::powerRenameLoggerName);
            Require(logger != nullptr, "registered logger lost");
            logger->flush();
        });
        try
        {
            SaveResult();
        }
        catch (...)
        {
            ExitProcess(87);
        }
        winrt::Microsoft::UI::Xaml::Application::Current().Exit();
    }
}

namespace PTSettingsHelper
{
    std::wstring get_root_save_folder_location()
    {
        return Paths().root.wstring();
    }

    std::wstring get_powertoys_general_save_file_location()
    {
        return (Paths().root / L"settings.json").wstring();
    }

    std::wstring get_module_save_folder_location(std::wstring_view module)
    {
        if (module != L"PowerRename")
            ExitProcess(86);
        return (Paths().root / L"PowerRename").wstring();
    }

    std::wstring get_log_settings_file_location()
    {
        return (Paths().root / L"log_settings.json").wstring();
    }
}

HRESULT CPowerRenameManager::s_CreateInstance(IPowerRenameManager** output)
{
    Require(output && !fixtureManager, "one fixture manager factory");
    Log().Record("factory", 1);
    recorder = new MainWindowRecordingManager;
    fixtureManager = recorder;
    *output = recorder;
    return S_OK;
}

void StartScopeSelectionComponentTests()
{
    Paths();
    Require(winrt::Microsoft::UI::Dispatching::DispatcherQueue::GetForCurrentThread().TryEnqueue([] { Run(); }),
        "component dispatcher scheduling");
}
