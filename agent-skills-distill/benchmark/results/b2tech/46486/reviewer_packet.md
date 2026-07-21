# PR #46486 — Fix AdvancedPaste auto-copy failing on Electron/Chromium apps

Base: main  Head: user/yeelam/FixAutoCopyIssue

## Description

## Summary

Fixes AdvancedPaste custom action auto-copy failing on Electron/Chromium apps (e.g. Teams, VS Code, browsers).

## Problem

The Ctrl+C fallback in \send_copy_selection()\ injected keystrokes without first releasing modifier keys held from the hotkey combination. Target apps received e.g. \Win+Shift+Ctrl+C\ instead of \Ctrl+C\, so the copy never triggered.

## Changes

- **Release modifier keys before Ctrl+C** and restore them after, matching the existing \	ry_to_paste_as_plain_text()\ pattern for Ctrl+V
- **Extract helpers** \send_ctrl_c_input()\ and \poll_clipboard_sequence()\ from inline code
- **Add warn-level logging** on failure paths for customer diagnostics
- **Check WM_COPY result** — verify clipboard actually changed before declaring success

## Validation

- Tested auto-copy custom action on Electron apps (Teams) — now works
- Confirmed warn-level logs appear on failure for diagnostics
- No regressions in standard Win32 app copy behavior

## Changed files (unified diff)

### src/modules/AdvancedPaste/AdvancedPasteModuleInterface/dllmain.cpp  (+121/-67)
```diff
@@ -496,23 +496,119 @@ class AdvancedPaste : public PowertoyModuleIface
 
         if (!GetGUIThreadInfo(0, &gui_info))
         {
+            Logger::warn(L"Auto-copy: GetGUIThreadInfo failed (error={})", GetLastError());
             return false;
         }
 
         HWND target = gui_info.hwndFocus ? gui_info.hwndFocus : gui_info.hwndActive;
         if (!target)
         {
+            Logger::warn(L"Auto-copy: no focused or active window found");
             return false;
         }
 
         DWORD_PTR result = 0;
-        return SendMessageTimeout(target,
-                                  WM_COPY,
-                                  0,
-                                  0,
-                                  SMTO_ABORTIFHUNG | SMTO_BLOCK,
-                                  50,
-                                  &result) != 0;
+        auto sendResult = SendMessageTimeout(target, WM_COPY, 0, 0, SMTO_ABORTIFHUNG | SMTO_BLOCK, 50, &result);
+        return sendResult != 0;
+    }
+
+    // Helper: poll clipboard sequence number for a change from initial_sequence.
+    // Returns true if the sequence number changed within the given number of polls.
+    bool poll_clipboard_sequence(DWORD initial_sequence, int poll_attempts, std::chrono::milliseconds poll_delay)
+    {
+        for (int poll = 0; poll < poll_attempts; ++poll)
+        {
+            if (GetClipboardSequenceNumber() != initial_sequence)
+            {
+                return true;
+            }
+            std::this_thread::sleep_for(poll_delay);
+        }
+        return false;
+    }
+
+    // Helper: send Ctrl+C via SendInput, releasing any held modifier keys first
+    // (the hotkey combination may still have modifiers physically pressed).
+    bool send_ctrl_c_input()
+    {
+        std::vector<INPUT> inputs;
+
+        // Release all modifier keys that are currently held down from the hotkey.
+        // Without this, the target app sees e.g. Win+Shift+Ctrl+C instead of just Ctrl+C.
+        try_inject_modifier_key_up(inputs, VK_LCONTROL);
+        try_inject_modifier_key_up(inputs, VK_RCONTROL);
+        try_inject_modifier_key_up(inputs, VK_LWIN);
+        try_inject_modifier_key_up(inputs, VK_RWIN);
+        try_inject_modifier_key_up(inputs, VK_LSHIFT);
+        try_inject_modifier_key_up(inputs, VK_RSHIFT);
+        try_inject_modifier_key_up(inputs, VK_LMENU);
+        try_inject_modifier_key_up(inputs, VK_RMENU);
+
+        // Ctrl down
+        {
+            INPUT input_event = {};
+            input_event.type = INPUT_KEYBOARD;
+            input_event.ki.wVk = VK_CONTROL;
+            input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
+            inputs.push_back(input_event);
+        }
+
+        // C down
+        {
+            INPUT input_event = {};
+            input_event.type = INPUT_KEYBOARD;
+            input_event.ki.wVk = 0x43; // C
+            input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
+            inputs.push_back(input_event);
+        }
+
+        // C up
+        {
+            INPUT input_event = {};
+            input_event.type = INPUT_KEYBOARD;
+            input_event.ki.wVk = 0x43; // C
+            input_event.ki.dwFlags = KEYEVENTF_KEYUP;
+            input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
+            inputs.push_back(input_event);
+        }
+
+        // Ctrl up
+        {
+            INPUT input_event = {};
+            input_event.type = INPUT_KEYBOARD;
+            input_event.ki.wVk = VK_CONTROL;
+            input_event.ki.dwFlags = KEYEVENTF_KEYUP;
+            input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
+            inputs.push_back(input_event);
+        }
+
+        // Restore modifiers that were held down
+        try_inject_modifier_key_restore(inputs, VK_LCONTROL);
+        try_inject_modifier_key_restore(inputs, VK_RCONTROL);
+        try_inject_modifier_key_restore(inputs, VK_LWIN);
+        try_inject_modifier_key_restore(inputs, VK_RWIN);
+        try_inject_modifier_key_restore(inputs, VK_LSHIFT);
+        try_inject_modifier_key_restore(inputs, VK_RSHIFT);
+        try_inject_modifier_key_restore(inputs, VK_LMENU);
+        try_inject_modifier_key_restore(inputs, VK_RMENU);
+
+        // Prevent Start Menu from activating after Win key release/restore
+        INPUT dummyEvent = {};
+        dummyEvent.type = INPUT_KEYBOARD;
+        dummyEvent.ki.wVk = 0xFF;
+        dummyEvent.ki.dwFlags = KEYEVENTF_KEYUP;
+        inputs.push_back(dummyEvent);
+
+        auto uSent = SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
+        if (uSent != inputs.size())
+        {
+            DWORD errorCode = GetLastError();
+            auto errorMessage = get_last_error_message(errorCode);
+            Logger::error(L"SendInput failed for Ctrl+C. Expected to send {} inputs and sent only {}. {}", inputs.size(), uSent, errorMessage.has_value() ? errorMessage.value() : L"");
+            Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"input.SendInput");
+            return false;
+        }
+        return true;
     }
 
     bool send_copy_selection()
@@ -526,78 +622,30 @@ class AdvancedPaste : public PowertoyModuleIface
         for (int attempt = 0; attempt < copy_attempts; ++attempt)
         {
             const auto initial_sequence = GetClipboardSequenceNumber();
-            copy_succeeded = try_send_copy_message();
 
-            if (!copy_succeeded)
-            {
-                std::vector<INPUT> inputs;
+            // Strategy 1: Try WM_COPY message (works for standard Win32 controls)
+            bool wm_copy_sent = try_send_copy_message();
 
-                // send Ctrl+C (key downs and key ups)
-                {
-                    INPUT input_event = {};
-                    input_event.type = INPUT_KEYBOARD;
-                    input_event.ki.wVk = VK_CONTROL;
-                    input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
-                    inputs.push_back(input_event);
-                }
-
-                {
-                    INPUT input_event = {};
-                    input_event.type = INPUT_KEYBOARD;
-                    input_event.ki.wVk = 0x43; // C
-                    // Avoid triggering detection by the centralized keyboard hook.
-                    input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
-                    inputs.push_back(input_event);
-                }
-
-                {
-                    INPUT input_event = {};
-                    input_event.type = INPUT_KEYBOARD;
-                    input_event.ki.wVk = 0x43; // C
-                    input_event.ki.dwFlags = KEYEVENTF_KEYUP;
-                    // Avoid triggering detection by the centralized keyboard hook.
-                    input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
-                    inputs.push_back(input_event);
-                }
-
-                {
-                    INPUT input_event = {};
-                    input_event.type = INPUT_KEYBOARD;
-                    input_event.ki.wVk = VK_CONTROL;
-                    input_event.ki.dwFlags = KEYEVENTF_KEYUP;
-                    input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
-                    inputs.push_back(input_event);
-                }
-
-                auto uSent = SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
-                if (uSent != inputs.size())
-                {
-                    DWORD errorCode = GetLastError();
-                    auto errorMessage = get_last_error_message(errorCode);
-                    Logger::error(L"SendInput failed for Ctrl+C. Expected to send {} inputs and sent only {}. {}", inputs.size(), uSent, errorMessage.has_value() ? errorMessage.value() : L"");
-                    Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"input.SendInput");
-                }
-                else
+            if (wm_copy_sent)
+            {
+                if (poll_clipboard_sequence(initial_sequence, clipboard_poll_attempts, clipboard_poll_delay))
                 {
                     copy_succeeded = true;
                 }
             }
 
-            if (copy_succeeded)
+            // Strategy 2: If WM_COPY didn't work, try SendInput Ctrl+C (works for Electron, browsers, etc.)
+            if (!copy_succeeded)
             {
-                bool sequence_changed = false;
-                for (int poll_attempt = 0; poll_attempt < clipboard_poll_attempts; ++poll_attempt)
+                const auto sequence_before_ctrl_c = GetClipboardSequenceNumber();
+
+                if (send_ctrl_c_input())
                 {
-                    if (GetClipboardSequenceNumber() != initial_sequence)
+                    if (poll_clipboard_sequence(sequence_before_ctrl_c, clipboard_poll_attempts, clipboard_poll_delay))
                     {
-                        sequence_changed = true;
-                        break;
+                        copy_succeeded = true;
                     }
-
-                    std::this_thread::sleep_for(clipboard_poll_delay);
                 }
-
-                copy_succeeded = sequence_changed;
             }
 
             if (copy_succeeded)
@@ -611,6 +659,11 @@ class AdvancedPaste : public PowertoyModuleIface
             }
         }
 
+        if (!copy_succeeded)
+        {
+            Logger::warn(L"Auto-copy: all {} copy attempts failed — the target application did not update the clipboard after WM_COPY and Ctrl+C", copy_attempts);
+        }
+
         return copy_succeeded;
     }
 
@@ -977,6 +1030,7 @@ class AdvancedPaste : public PowertoyModuleIface
             {
                 if (!send_copy_selection())
                 {
+                    Logger::warn(L"Auto-copy: failed to copy selection for custom action index {} — aborting action", custom_action_index);
                     return false;
                 }
             }
```