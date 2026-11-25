#include "pch.h"
#include "TwoWayPipeMessageIPCManaged.h"
#include "TwoWayPipeMessageIPCManaged.g.cpp"
#include "two_way_pipe_message_ipc_impl.h"
#include <functional>
#include <wil/resource.h>

namespace winrt::PowerToys::Interop::implementation
{
    TwoWayPipeMessageIPCManaged::TwoWayPipeMessageIPCManaged(hstring const& inputPipeName, hstring const& outputPipeName, winrt::PowerToys::Interop::TwoWayPipeIPCReadCallback const& _callback)
    {
        this->_callback = _callback;
        _outputPipeName = outputPipeName;
        if (_callback != nullptr)
        {
            _internalReadCallback = [this](const std::wstring& msg) {
                this->_callback(msg);
            };
        }
        _pipe = new TwoWayPipeMessageIPC(std::wstring{ inputPipeName }, std::wstring{ outputPipeName }, _internalReadCallback);
    }

    void TwoWayPipeMessageIPCManaged::Send(hstring const& msg)
    {
        _pipe->send(std::wstring{ msg });
    }
    void TwoWayPipeMessageIPCManaged::Start()
    {
        _pipe->start(nullptr);
    }
    void TwoWayPipeMessageIPCManaged::StartWithPeer(uint32_t processId, hstring const& directory, array_view<hstring const> executableNames)
    {
        if (!processId || directory.empty() || executableNames.empty())
        {
            throw hresult_invalid_argument(L"A pipe peer process and image identity are required");
        }
        winrt::handle peer(OpenProcess(SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId));
        check_bool(static_cast<bool>(peer));
        if (WaitForSingleObject(peer.get(), 0) != WAIT_TIMEOUT)
        {
            throw hresult_error(HRESULT_FROM_WIN32(ERROR_PROCESS_ABORTED));
        }
        interop_auth::CallerPolicy policy;
        policy.enabled = true;
        policy.expectedClientPid = processId;
        policy.expectedDirectory = directory;
        for (const auto& name : executableNames)
        {
            if (name.empty())
            {
                throw hresult_invalid_argument(L"A pipe peer executable name is required");
            }
            policy.allowedBasenames.emplace_back(name);
        }
        policy.expectedVersion = interop_auth::GetOwnModuleVersion();
        if (!policy.expectedVersion)
        {
            throw hresult_error(HRESULT_FROM_WIN32(ERROR_RESOURCE_DATA_NOT_FOUND));
        }
        policy.authenticateServer = true;
        policy.logReject = [](const interop_auth::AuthResult& result) {
            OutputDebugStringW(L"PowerToys IPC peer rejected: ");
            OutputDebugStringW(result.reasonCode);
            OutputDebugStringW(L"\n");
        };
        interop_auth::VerificationCache cache;
        const auto deadline = GetTickCount64() + 10000;
        while (true)
        {
            if (WaitForSingleObject(peer.get(), 0) != WAIT_TIMEOUT)
            {
                throw hresult_error(HRESULT_FROM_WIN32(ERROR_PROCESS_ABORTED));
            }
            wil::unique_hfile connection(CreateFileW(
                _outputPipeName.c_str(), FILE_READ_DATA | FILE_WRITE_DATA | SYNCHRONIZE, 0, nullptr, OPEN_EXISTING, SECURITY_SQOS_PRESENT | SECURITY_IDENTIFICATION, nullptr));
            if (connection)
            {
                const auto result = interop_auth::AuthenticateServer(connection.get(), policy, cache);
                if (!result.accepted)
                {
                    throw hresult_access_denied(std::wstring(L"The pipe server does not match the expected peer: ") + result.reasonCode);
                }
                break;
            }
            const auto error = GetLastError();
            if (error != ERROR_FILE_NOT_FOUND && error != ERROR_PIPE_BUSY)
            {
                throw hresult_error(HRESULT_FROM_WIN32(error));
            }
            if (GetTickCount64() >= deadline)
            {
                throw hresult_error(HRESULT_FROM_WIN32(ERROR_TIMEOUT));
            }
            WaitNamedPipeW(_outputPipeName.c_str(), 20);
            Sleep(20);
        }
        _peerProcess = std::move(peer);
        _pipe->start(nullptr, policy);
    }
    void TwoWayPipeMessageIPCManaged::End()
    {
        _pipe->end();
    }
    void TwoWayPipeMessageIPCManaged::Close()
    {
        delete _pipe;
    }
}
