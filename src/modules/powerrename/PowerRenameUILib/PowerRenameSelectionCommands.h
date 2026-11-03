#pragma once

#include <atlbase.h>
#include <PowerRenameInterfaces.h>

namespace PowerRenameSelectionCommands
{
    inline void SelectAll(IPowerRenameManager& manager, UINT itemCount, bool selected)
    {
        for (UINT i = 0; i < itemCount; i++)
        {
            CComPtr<IPowerRenameItem> spItem;
            if (SUCCEEDED(manager.GetItemByIndex(i, &spItem)))
            {
                spItem->PutSelected(selected);
            }
        }
    }
}
