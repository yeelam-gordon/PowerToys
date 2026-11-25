#pragma once
#include "TwoWayPipeMessageIPCManaged.g.h"
#include "two_way_pipe_message_ipc.h"

namespace winrt::PowerToys::Interop::implementation
{
    struct TwoWayPipeMessageIPCManaged : TwoWayPipeMessageIPCManagedT<TwoWayPipeMessageIPCManaged>
    {
        TwoWayPipeMessageIPCManaged() = default;

        TwoWayPipeMessageIPCManaged(hstring const& inputPipeName, hstring const& outputPipeName, winrt::PowerToys::Interop::TwoWayPipeIPCReadCallback const& _callback);
        void Send(hstring const& msg);
        void Start();
        void StartWithPeer(uint32_t processId, hstring const& directory, array_view<hstring const> executableNames);
        void End();
        void Close();

    private:
        TwoWayPipeMessageIPC* _pipe;
        TwoWayPipeIPCReadCallback _callback;
        std::function<void(const std::wstring& msg)> _internalReadCallback;
        std::wstring _outputPipeName;
        winrt::handle _peerProcess;
    };
}
namespace winrt::PowerToys::Interop::factory_implementation
{
    struct TwoWayPipeMessageIPCManaged : TwoWayPipeMessageIPCManagedT<TwoWayPipeMessageIPCManaged, implementation::TwoWayPipeMessageIPCManaged>
    {
    };
}
