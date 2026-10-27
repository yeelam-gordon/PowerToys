#include "..\PowerRenameUILib\pch.h"

#ifndef POWERRENAME_SCOPE_COMPONENT_TESTS
#error This fixture belongs only to the explicitly selected component-test application.
#endif

#include "PowerRenameXAML\MainWindow.xaml.h"
#include <microsoft.ui.xaml.window.h>
#include <Aclapi.h>
#include <PowerRenameRegEx.h>
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
            Require(language && language->GetNamedString(L"language") == L"", "language override prohibited");
            Require(module && !module->GetNamedBoolean(L"PersistState") && !module->GetNamedBoolean(L"MRUEnabled") &&
                !module->GetNamedBoolean(L"UseBoostLib"), "preseeded nonpersistent module settings required");
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

    struct FlagRead
    {
        HRESULT result;
        bool write;
        DWORD value;
    };

    class ScopeSelectionRecordingManager final : public IPowerRenameManager
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

        HRESULT Count(const char* method, UINT* count, HRESULT result = S_OK)
        {
            *count = 0;
            Log().Record(method, 0, result, true, flags);
            return result;
        }

    public:
        DWORD flags = 0;
        DWORD cookie = 0;
        std::deque<FlagRead> reads;
        std::deque<HRESULT> writes;
        HRESULT countResult = S_OK;

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

        HRESULT GetItemCount(UINT* count) override { return Count("GetItemCount", count, countResult); }
        HRESULT GetVisibleItemCount(UINT* count) override { return Count("GetVisibleItemCount", count); }
        HRESULT GetSelectedItemCount(UINT* count) override { return Count("GetSelectedItemCount", count); }
        HRESULT GetRenameItemCount(UINT* count) override { return Count("GetRenameItemCount", count); }

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
        HRESULT Rename(HWND, bool) override { return Unexpected("Rename"); }
        HRESULT UpdateChildrenPath(int, size_t) override { return Unexpected("UpdateChildrenPath"); }
        HRESULT GetCloseUIWindowAfterRenaming(bool* value) override { *value = false; return Unexpected("GetCloseUIWindowAfterRenaming"); }
        HRESULT AddItem(IPowerRenameItem*) override { return Unexpected("AddItem"); }
        HRESULT GetItemByIndex(UINT, IPowerRenameItem** output) override { *output = nullptr; return Unexpected("GetItemByIndex"); }
        HRESULT GetVisibleItemByIndex(UINT, IPowerRenameItem** output) override { *output = nullptr; return Unexpected("GetVisibleItemByIndex"); }
        HRESULT SetVisible() override { return Unexpected("SetVisible"); }
        HRESULT GetItemById(int, IPowerRenameItem** output) override { *output = nullptr; return Unexpected("GetItemById"); }
        HRESULT GetFilter(DWORD* output) override { *output = 0; return Unexpected("GetFilter"); }
        HRESULT SwitchFilter(int) override { return Unexpected("SwitchFilter"); }
        HRESULT PutRenameRegEx(IPowerRenameRegEx*) override { return Unexpected("PutRenameRegEx"); }
        HRESULT GetRenameItemFactory(IPowerRenameItemFactory** output) override { *output = nullptr; return Unexpected("GetRenameItemFactory"); }
        uint32_t GetVisibleItemRealIndex(uint32_t index) const override
        {
            Unexpected("GetVisibleItemRealIndex");
            return index;
        }

        ~ScopeSelectionRecordingManager()
        {
            if (sink)
                Log().failures.push_back("destroyed with live Advise");
            Log().Record("manager.destroy");
        }
    };

    CComPtr<IPowerRenameManager> fixtureManager;
    ScopeSelectionRecordingManager* recorder = nullptr;

    void Hidden(HWND hwnd)
    {
        Require(GetCurrentThreadId() == Log().sta, "window lifecycle STA");
        Require(IsWindow(hwnd) && !IsWindowVisible(hwnd) && GetForegroundWindow() != hwnd,
            "component window became visible or foreground");
        Require(WaitForSingleObject(Paths().parent.get(), 0) == WAIT_TIMEOUT, "controller exited");
    }

    void SaveResult()
    {
        json::JsonObject result;
        result.SetNamedValue(L"status", json::value(Log().failures.empty() && Log().cases == 9 && Log().passed == 9 &&
            Log().windowConstructionCompleted && !Log().constructorAbandoned ? L"PASS" : L"FAIL"));
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
                { "failed-unwritten-reads", 1, 2, 0x91, { { E_FAIL, false, 0 }, { E_FAIL, false, 0 } },
                    {}, S_OK, { 0, 0x100 }, { 0, 0x100 }, S_OK },
                { "failed-written-read", 1, 2, 0x91, { { E_FAIL, true, 0x91 }, { S_OK, true, 0x11 } },
                    {}, S_OK, { 0x11, 0x111 }, { 0x11, 0x111 }, S_OK },
                { "failed-writes-still-reset", 1, 2, 0x91, {}, { E_FAIL, E_FAIL },
                    S_OK, { 0x11, 0x191 }, { 0x91, 0x91 }, S_OK },
                { "count-failure-before-reset", 1, 2, 0x91, {}, {},
                    E_ACCESSDENIED, { 0x11 }, {}, E_ACCESSDENIED },
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
    recorder = new ScopeSelectionRecordingManager;
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
