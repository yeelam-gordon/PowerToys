#include "pch.h"
#include <PowerRenameInterfaces.h>
#include "..\PowerRenameUILib\PowerRenameSelectionCommands.h"

#include <array>
#include <initializer_list>
#include <string>
#include <utility>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    using Trace = std::vector<std::wstring>;

    [[noreturn]] HRESULT UnexpectedCall(const wchar_t* method)
    {
        Assert::Fail(method);
    }

    template<typename Interface>
    class RecordingComObject : public Interface
    {
    public:
        RecordingComObject(Trace& trace, std::wstring label) :
            m_trace(trace), m_label(std::move(label))
        {
        }

        IFACEMETHODIMP QueryInterface(REFIID iid, void** result) override
        {
            if (!result)
            {
                return E_POINTER;
            }
            *result = nullptr;
            if (iid != __uuidof(IUnknown) && iid != __uuidof(Interface))
            {
                return E_NOINTERFACE;
            }
            *result = static_cast<Interface*>(this);
            AddRef();
            return S_OK;
        }

        IFACEMETHODIMP_(ULONG)
        AddRef() override
        {
            const auto count = InterlockedIncrement(&m_references);
            m_trace.push_back(m_label + L".AddRef:" + std::to_wstring(count));
            return static_cast<ULONG>(count);
        }

        IFACEMETHODIMP_(ULONG)
        Release() override
        {
            const auto count = InterlockedDecrement(&m_references);
            m_trace.push_back(m_label + L".Release:" + std::to_wstring(count));
            if (count == 0)
            {
                delete this;
            }
            return static_cast<ULONG>(count);
        }

    protected:
        virtual ~RecordingComObject() = default;
        Trace& m_trace;
        const std::wstring m_label;

    private:
        long m_references = 1;
    };

    class RecordingItem : public RecordingComObject<IPowerRenameItem>
    {
    public:
        RecordingItem(Trace& trace, UINT index, HRESULT setterResult) :
            RecordingComObject(trace, L"item" + std::to_wstring(index)),
            m_setterResult(setterResult)
        {
        }

        IFACEMETHODIMP PutSelected(bool selected) override
        {
            m_trace.push_back(m_label + L".PutSelected:" + (selected ? L"true" : L"false"));
            if (SUCCEEDED(m_setterResult))
            {
                m_selected = selected;
            }
            return m_setterResult;
        }

        bool Selected() const
        {
            return m_selected;
        }

        IFACEMETHODIMP PutPath(PCWSTR) override { return UnexpectedCall(L"PutPath"); }
        IFACEMETHODIMP GetPath(PWSTR*) override { return UnexpectedCall(L"GetPath"); }
        IFACEMETHODIMP GetTime(DWORD, SYSTEMTIME*) override { return UnexpectedCall(L"GetTime"); }
        IFACEMETHODIMP GetShellItem(IShellItem**) override { return UnexpectedCall(L"GetShellItem"); }
        IFACEMETHODIMP GetOriginalName(PWSTR*) override { return UnexpectedCall(L"GetOriginalName"); }
        IFACEMETHODIMP PutOriginalName(PCWSTR) override { return UnexpectedCall(L"PutOriginalName"); }
        IFACEMETHODIMP GetNewName(PWSTR*) override { return UnexpectedCall(L"GetNewName"); }
        IFACEMETHODIMP PutNewName(PCWSTR) override { return UnexpectedCall(L"PutNewName"); }
        IFACEMETHODIMP GetIsFolder(bool*) override { return UnexpectedCall(L"GetIsFolder"); }
        IFACEMETHODIMP GetIsSubFolderContent(bool*) override { return UnexpectedCall(L"GetIsSubFolderContent"); }
        IFACEMETHODIMP GetSelected(bool*) override { return UnexpectedCall(L"GetSelected"); }
        IFACEMETHODIMP GetId(int*) override { return UnexpectedCall(L"GetId"); }
        IFACEMETHODIMP GetDepth(UINT*) override { return UnexpectedCall(L"GetDepth"); }
        IFACEMETHODIMP PutDepth(int) override { return UnexpectedCall(L"PutDepth"); }
        IFACEMETHODIMP GetStatus(PowerRenameItemRenameStatus*) override { return UnexpectedCall(L"GetStatus"); }
        IFACEMETHODIMP PutStatus(PowerRenameItemRenameStatus) override { return UnexpectedCall(L"PutStatus"); }
        IFACEMETHODIMP ShouldRenameItem(DWORD, bool*) override { return UnexpectedCall(L"ShouldRenameItem"); }
        IFACEMETHODIMP IsItemVisible(DWORD, DWORD, bool*) override { return UnexpectedCall(L"IsItemVisible"); }
        IFACEMETHODIMP Reset() override { return UnexpectedCall(L"Item.Reset"); }

    private:
        const HRESULT m_setterResult;
        bool m_selected = true;
    };

    using Results = std::array<HRESULT, 3>;
    constexpr Results Success = { S_OK, S_OK, S_OK };

    class RecordingManager : public RecordingComObject<IPowerRenameManager>
    {
    public:
        RecordingManager(Trace& trace, const Results& lookupResults, const Results& setterResults) :
            RecordingComObject(trace, L"manager"), m_lookupResults(lookupResults)
        {
            for (UINT index = 0; index < m_items.size(); ++index)
            {
                m_items[index].Attach(new RecordingItem(trace, index, setterResults[index]));
            }
        }

        IFACEMETHODIMP GetItemByIndex(UINT index, IPowerRenameItem** result) override
        {
            m_trace.push_back(L"GetItemByIndex:" + std::to_wstring(index));
            Assert::IsTrue(index < m_items.size(), L"Unexpected total-list index");
            if (!result)
            {
                return E_POINTER;
            }
            *result = nullptr;
            if (SUCCEEDED(m_lookupResults[index]))
            {
                *result = m_items[index];
                (*result)->AddRef();
            }
            return m_lookupResults[index];
        }

        bool AllInitiallySelected() const
        {
            return m_items[0]->Selected() && m_items[1]->Selected() && m_items[2]->Selected();
        }

        IFACEMETHODIMP Advise(IPowerRenameManagerEvents*, DWORD*) override { return UnexpectedCall(L"Advise"); }
        IFACEMETHODIMP UnAdvise(DWORD) override { return UnexpectedCall(L"UnAdvise"); }
        IFACEMETHODIMP Start() override { return UnexpectedCall(L"Start"); }
        IFACEMETHODIMP Stop() override { return UnexpectedCall(L"Stop"); }
        IFACEMETHODIMP Reset() override { return UnexpectedCall(L"Manager.Reset"); }
        IFACEMETHODIMP Shutdown() override { return UnexpectedCall(L"Shutdown"); }
        IFACEMETHODIMP Rename(HWND, bool) override { return UnexpectedCall(L"Rename"); }
        IFACEMETHODIMP UpdateChildrenPath(int, size_t) override { return UnexpectedCall(L"UpdateChildrenPath"); }
        IFACEMETHODIMP GetCloseUIWindowAfterRenaming(bool*) override { return UnexpectedCall(L"GetCloseUIWindowAfterRenaming"); }
        IFACEMETHODIMP AddItem(IPowerRenameItem*) override { return UnexpectedCall(L"AddItem"); }
        IFACEMETHODIMP GetVisibleItemByIndex(UINT, IPowerRenameItem**) override { return UnexpectedCall(L"GetVisibleItemByIndex"); }
        IFACEMETHODIMP SetVisible() override { return UnexpectedCall(L"SetVisible"); }
        IFACEMETHODIMP GetItemById(int, IPowerRenameItem**) override { return UnexpectedCall(L"GetItemById"); }
        IFACEMETHODIMP GetItemCount(UINT*) override { return UnexpectedCall(L"GetItemCount"); }
        IFACEMETHODIMP GetVisibleItemCount(UINT*) override { return UnexpectedCall(L"GetVisibleItemCount"); }
        IFACEMETHODIMP GetSelectedItemCount(UINT*) override { return UnexpectedCall(L"GetSelectedItemCount"); }
        IFACEMETHODIMP GetRenameItemCount(UINT*) override { return UnexpectedCall(L"GetRenameItemCount"); }
        IFACEMETHODIMP GetFlags(DWORD*) override { return UnexpectedCall(L"GetFlags"); }
        IFACEMETHODIMP PutFlags(DWORD) override { return UnexpectedCall(L"PutFlags"); }
        IFACEMETHODIMP GetFilter(DWORD*) override { return UnexpectedCall(L"GetFilter"); }
        IFACEMETHODIMP SwitchFilter(int) override { return UnexpectedCall(L"SwitchFilter"); }
        IFACEMETHODIMP GetRenameRegEx(IPowerRenameRegEx**) override { return UnexpectedCall(L"GetRenameRegEx"); }
        IFACEMETHODIMP PutRenameRegEx(IPowerRenameRegEx*) override { return UnexpectedCall(L"PutRenameRegEx"); }
        IFACEMETHODIMP GetRenameItemFactory(IPowerRenameItemFactory**) override { return UnexpectedCall(L"GetRenameItemFactory"); }
        IFACEMETHODIMP PutRenameItemFactory(IPowerRenameItemFactory*) override { return UnexpectedCall(L"PutRenameItemFactory"); }
        uint32_t GetVisibleItemRealIndex(uint32_t) const override
        {
            Assert::Fail(L"GetVisibleItemRealIndex");
        }

    private:
        const Results m_lookupResults;
        std::array<CComPtr<RecordingItem>, 3> m_items;
    };

    Trace ExpectedItems(std::initializer_list<UINT> indices, bool selected)
    {
        Trace expected;
        for (const auto index : indices)
        {
            const auto label = L"item" + std::to_wstring(index);
            expected.push_back(L"GetItemByIndex:" + std::to_wstring(index));
            expected.push_back(label + L".AddRef:2");
            expected.push_back(label + L".PutSelected:" + (selected ? L"true" : L"false"));
            expected.push_back(label + L".Release:1");
        }
        return expected;
    }

    struct SelectionCase
    {
        const wchar_t* name;
        UINT count;
        bool selected;
        Results lookups;
        Results setters;
        Trace expected;
    };

    void AssertTrace(const Trace& expected, const Trace& actual, const wchar_t* name)
    {
        Assert::IsTrue(expected.size() == actual.size(), name);
        for (size_t index = 0; index < expected.size(); ++index)
        {
            const auto message = std::wstring(name) + L", event " + std::to_wstring(index);
            Assert::AreEqual(expected[index].c_str(), actual[index].c_str(), message.c_str());
        }
    }
}

namespace PowerRenameSelectionCommandsTests
{
    TEST_CLASS (SelectionCommandsTests)
    {
    public:
        TEST_METHOD (RecordsCapturedTotalSelectionAndFailureContinuation)
        {
            auto failedLookup = ExpectedItems({ 0 }, false);
            failedLookup.push_back(L"GetItemByIndex:1");
            const auto lastItem = ExpectedItems({ 2 }, false);
            failedLookup.insert(failedLookup.end(), lastItem.begin(), lastItem.end());

            const std::array<SelectionCase, 7> cases = {
                SelectionCase{ L"Zero captured count", 0, false, Success, Success, {} },
                SelectionCase{ L"Deselect captured total indices without visibility queries", 3, false, Success, Success, ExpectedItems({ 0, 1, 2 }, false) },
                SelectionCase{ L"Select already selected items still writes", 3, true, Success, Success, ExpectedItems({ 0, 1, 2 }, true) },
                SelectionCase{ L"Failed middle lookup continues", 3, false, { S_OK, E_FAIL, S_OK }, Success, failedLookup },
                SelectionCase{ L"S_FALSE lookup with valid item still writes", 3, false, { S_OK, S_FALSE, S_OK }, Success, ExpectedItems({ 0, 1, 2 }, false) },
                SelectionCase{ L"Failed middle setter is ignored", 3, false, Success, { S_OK, E_FAIL, S_OK }, ExpectedItems({ 0, 1, 2 }, false) },
                SelectionCase{ L"S_FALSE setter continues", 3, false, Success, { S_OK, S_FALSE, S_OK }, ExpectedItems({ 0, 1, 2 }, false) }
            };

            for (const auto& testCase : cases)
            {
                Trace actual;
                CComPtr<RecordingManager> manager;
                manager.Attach(new RecordingManager(actual, testCase.lookups, testCase.setters));
                Assert::IsTrue(manager->AllInitiallySelected(), testCase.name);
                Assert::IsTrue(actual.empty(), L"Arrange must not contribute to the Act trace");

                PowerRenameSelectionCommands::SelectAll(*manager, testCase.count, testCase.selected);

                AssertTrace(testCase.expected, actual, testCase.name);
            }
        }
    };
}
