# PR #44767 — feat(advancedpaste): add auto-copy selection for custom action hotkeys

Base: main  Head: user/yeelam/SingleCopyAndPaste

## Description

<!-- Enter a brief description/summary of your PR here. What does it fix/what does it change/how was it tested (even manually, if necessary)? -->
## Summary of the Pull Request
Boosting productivity #2x. Customer mentioned with Custom Action (Shortcut trigger) "We should not need to do two keyboard actions to finish this awesome AI data transformation, instead, just single shortcut should do copy + advanced paste."

This pull request introduces a new feature to the Advanced Paste module that allows users to automatically copy the current selection when triggering a custom action hotkey. The changes include backend logic for sending the copy command, updates to configuration and settings management, and UI additions to expose this option to users.

### Feature Addition: Auto-Copy Selection for Custom Action Hotkeys

* Added a new boolean setting, `AutoCopySelectionForCustomActionHotkey`, to both the backend (`dllmain.cpp`, `AdvancedPasteProperties.cs`) and the settings UI, allowing users to enable or disable automatic copying of the current selection when a custom action hotkey is pressed. [[1]](diffhunk://#diff-3866eb99ffe4453e0d03248e11d3560f7f15f4b982e323519d45e282f0fe898dR63) [[2]](diffhunk://#diff-3866eb99ffe4453e0d03248e11d3560f7f15f4b982e323519d45e282f0fe898dR106) [[3]](diffhunk://#diff-7f5d34989db7593fa4969a79cf97f709d210c157343d474650d5df4b9bf18114R31) [[4]](diffhunk://#diff-7f5d34989db7593fa4969a79cf97f709d210c157343d474650d5df4b9bf18114R83-R85) [[5]](diffhunk://#diff-09c575763019d9108b85a2e7b87a3bb6ed23a835970bf511b1a6bbe9a9f53835R174-R178) [[6]](diffhunk://#diff-0f8bf95882c074d687f6c4f2673cf9c8b1a904b117c11f75d0c892d809f3cd42R558-R570)

### Backend Logic and Integration

* Implemented the `send_copy_selection()` and `try_send_copy_message()` methods in `dllmain.cpp` to send a WM_COPY message or simulate a Ctrl+C keystroke, ensuring the selected content is copied before executing the custom action.
* Integrated the new setting into the hotkey handler logic so that when a custom action hotkey is pressed and the setting is enabled, the copy operation is triggered before running the custom action.

### Configuration and State Management

* Updated serialization/deserialization and property synchronization logic to support the new setting, ensuring its value is correctly loaded, saved, and reflected in the UI and runtime behavior. [[1]](diffhunk://#diff-3866eb99ffe4453e0d03248e11d3560f7f15f4b982e323519d45e282f0fe898dR353-R357) [[2]](diffhunk://#diff-0f8bf95882c074d687f6c4f2673cf9c8b1a904b117c11f75d0c892d809f3cd42R1235-R1240)

### UI and Localization

* Added a new checkbox to the Advanced Paste settings page in XAML to allow users to toggle the auto-copy feature.
* Provided localized strings for the new setting, including header and description, in the resource file for user clarity.

### Refactoring for Hotkey Logic

* Refactored hotkey handling code to correctly calculate indices for additional and custom actions

## Changed files (unified diff)

### src/modules/AdvancedPaste/AdvancedPasteModuleInterface/dllmain.cpp  (+155/-2)
```diff
@@ -19,6 +19,7 @@
 
 #include <algorithm>
 #include <cwctype>
+#include <chrono>
 #include <thread>
 #include <vector>
 
@@ -60,6 +61,7 @@ namespace
     const wchar_t JSON_KEY_IS_AI_ENABLED[] = L"IsAIEnabled";
     const wchar_t JSON_KEY_IS_OPEN_AI_ENABLED[] = L"IsOpenAIEnabled";
     const wchar_t JSON_KEY_SHOW_CUSTOM_PREVIEW[] = L"ShowCustomPreview";
+    const wchar_t JSON_KEY_AUTO_COPY_SELECTION_CUSTOM_ACTION[] = L"AutoCopySelectionForCustomActionHotkey";
     const wchar_t JSON_KEY_PASTE_AI_CONFIGURATION[] = L"paste-ai-configuration";
     const wchar_t JSON_KEY_PROVIDERS[] = L"providers";
     const wchar_t JSON_KEY_SERVICE_TYPE[] = L"service-type";
@@ -102,6 +104,7 @@ class AdvancedPaste : public PowertoyModuleIface
     bool m_is_ai_enabled = false;
     bool m_is_advanced_ai_enabled = false;
     bool m_preview_custom_format_output = true;
+    bool m_auto_copy_selection_custom_action = false;
 
     // Event listening for external triggers (e.g., from CmdPal extension)
     EventWaiter m_triggerEventWaiter;
@@ -348,6 +351,11 @@ class AdvancedPaste : public PowertoyModuleIface
             {
                 m_preview_custom_format_output = propertiesObject.GetNamedObject(JSON_KEY_SHOW_CUSTOM_PREVIEW).GetNamedBoolean(JSON_KEY_VALUE);
             }
+
+            if (propertiesObject.HasKey(JSON_KEY_AUTO_COPY_SELECTION_CUSTOM_ACTION))
+            {
+                m_auto_copy_selection_custom_action = propertiesObject.GetNamedObject(JSON_KEY_AUTO_COPY_SELECTION_CUSTOM_ACTION).GetNamedBoolean(JSON_KEY_VALUE, false);
+            }
         }
 
         if (old_data_migrated)
@@ -481,6 +489,131 @@ class AdvancedPaste : public PowertoyModuleIface
         }
     }
 
+    bool try_send_copy_message()
+    {
+        GUITHREADINFO gui_info = {};
+        gui_info.cbSize = sizeof(GUITHREADINFO);
+
+        if (!GetGUIThreadInfo(0, &gui_info))
+        {
+            return false;
+        }
+
+        HWND target = gui_info.hwndFocus ? gui_info.hwndFocus : gui_info.hwndActive;
+        if (!target)
+        {
+            return false;
+        }
+
+        DWORD_PTR result = 0;
+        return SendMessageTimeout(target,
+                                  WM_COPY,
+                                  0,
+                                  0,
+                                  SMTO_ABORTIFHUNG | SMTO_BLOCK,
+                                  50,
+                                  &result) != 0;
+    }
+
+    bool send_copy_selection()
+    {
+        constexpr int copy_attempts = 2;
+        constexpr auto copy_retry_delay = std::chrono::milliseconds(100);
+        constexpr int clipboard_poll_attempts = 5;
+        constexpr auto clipboard_poll_delay = std::chrono::milliseconds(30);
+
+        bool copy_succeeded = false;
+        for (int attempt = 0; attempt < copy_attempts; ++attempt)
+        {
+            const auto initial_sequence = GetClipboardSequenceNumber();
+            copy_succeeded = try_send_copy_message();
+
+            if (!copy_succeeded)
+            {
+                std::vector<INPUT> inputs;
+
+                // send Ctrl+C (key downs and key ups)
+                {
+                    INPUT input_event = {};
+                    input_event.type = INPUT_KEYBOARD;
+                    input_event.ki.wVk = VK_CONTROL;
+                    input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
+                    inputs.push_back(input_event);
+                }
+
+                {
+                    INPUT input_event = {};
+                    input_event.type = INPUT_KEYBOARD;
+                    input_event.ki.wVk = 0x43; // C
+                    // Avoid triggering detection by the centralized keyboard hook.
+                    input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
+                    inputs.push_back(input_event);
+                }
+
+                {
+                    INPUT input_event = {};
+                    input_event.type = INPUT_KEYBOARD;
+                    input_event.ki.wVk = 0x43; // C
+                    input_event.ki.dwFlags = KEYEVENTF_KEYUP;
+                    // Avoid triggering detection by the centralized keyboard hook.
+                    input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
+                    inputs.push_back(input_event);
+                }
+
+                {
+                    INPUT input_event = {};
+                    input_event.type = INPUT_KEYBOARD;
+                    input_event.ki.wVk = VK_CONTROL;
+                    input_event.ki.dwFlags = KEYEVENTF_KEYUP;
+                    input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
+                    inputs.push_back(input_event);
+                }
+
+                auto uSent = SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
+                if (uSent != inputs.size())
+                {
+                    DWORD errorCode = GetLastError();
+                    auto errorMessage = get_last_error_message(errorCode);
+                    Logger::error(L"SendInput failed for Ctrl+C. Expected to send {} inputs and sent only {}. {}", inputs.size(), uSent, errorMessage.has_value() ? errorMessage.value() : L"");
+                    Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"input.SendInput");
+                }
+                else
+                {
+                    copy_succeeded = true;
+                }
+            }
+
+            if (copy_succeeded)
+            {
+                bool sequence_changed = false;
+                for (int poll_attempt = 0; poll_attempt < clipboard_poll_attempts; ++poll_attempt)
+                {
+                    if (GetClipboardSequenceNumber() != initial_sequence)
+                    {
+                        sequence_changed = true;
+                        break;
+                    }
+
+                    std::this_thread::sleep_for(clipboard_poll_delay);
+                }
+
+                copy_succeeded = sequence_changed;
+            }
+
+            if (copy_succeeded)
+            {
+                break;
+            }
+
+            if (attempt + 1 < copy_attempts)
+            {
+                std::this_thread::sleep_for(copy_retry_delay);
+            }
+        }
+
+        return copy_succeeded;
+    }
+
     void try_to_paste_as_plain_text()
     {
         std::wstring clipboard_text;
@@ -826,6 +959,28 @@ class AdvancedPaste : public PowertoyModuleIface
         Logger::trace(L"AdvancedPaste hotkey pressed");
         if (m_enabled)
         {
+            size_t additional_action_index = 0;
+            size_t custom_action_index = 0;
+            bool is_custom_action_hotkey = false;
+
+            if (hotkeyId >= NUM_DEFAULT_HOTKEYS)
+            {
+                additional_action_index = hotkeyId - NUM_DEFAULT_HOTKEYS;
+                if (additional_action_index >= m_additional_actions.size())
+                {
+                    custom_action_index = additional_action_index - m_additional_actions.size();
+                    is_custom_action_hotkey = custom_action_index < m_custom_actions.size();
+                }
+            }
+
+            if (is_custom_action_hotkey && m_auto_copy_selection_custom_action)
+            {
+                if (!send_copy_selection())
+                {
+                    return false;
+                }
+            }
+
             m_process_manager.start();
 
             // hotkeyId in same order as set by get_hotkeys
@@ -868,7 +1023,6 @@ class AdvancedPaste : public PowertoyModuleIface
             }
 
 
-            const auto additional_action_index = hotkeyId - NUM_DEFAULT_HOTKEYS;
             if (additional_action_index < m_additional_actions.size())
             {
                 const auto& id = m_additional_actions.at(additional_action_index).id;
@@ -881,7 +1035,6 @@ class AdvancedPaste : public PowertoyModuleIface
                 return true;
             }
 
-            const auto custom_action_index = additional_action_index - m_additional_actions.size();
             if (custom_action_index < m_custom_actions.size())
             {
                 const auto id = m_custom_actions.at(custom_action_index).id;
```
### src/settings-ui/Settings.UI.Library/AdvancedPasteProperties.cs  (+4/-0)
```diff
@@ -28,6 +28,7 @@ public AdvancedPasteProperties()
             ShowCustomPreview = true;
             CloseAfterLosingFocus = false;
             EnableClipboardPreview = true;
+            AutoCopySelectionForCustomActionHotkey = false;
             PasteAIConfiguration = new();
         }
 
@@ -79,6 +80,9 @@ public bool TryConsumeLegacyAdvancedAIEnabled(out bool value)
         [JsonConverter(typeof(BoolPropertyJsonConverter))]
         public bool EnableClipboardPreview { get; set; }
 
+        [JsonConverter(typeof(BoolPropertyJsonConverter))]
+        public bool AutoCopySelectionForCustomActionHotkey { get; set; }
+
         [JsonPropertyName("advanced-paste-ui-hotkey")]
         public HotkeySettings AdvancedPasteUIShortcut { get; set; }
 
```
### src/settings-ui/Settings.UI/SettingsXAML/Views/AdvancedPastePage.xaml  (+3/-0)
```diff
@@ -171,6 +171,9 @@
                                 <tkcontrols:SettingsCard Name="AdvancedPasteEnableClipboardPreview" ContentAlignment="Left">
                                     <controls:CheckBoxWithDescriptionControl x:Uid="AdvancedPaste_EnableClipboardPreview" IsChecked="{x:Bind ViewModel.EnableClipboardPreview, Mode=TwoWay}" />
                                 </tkcontrols:SettingsCard>
+                                <tkcontrols:SettingsCard Name="AdvancedPasteAutoCopySelectionCustomAction" ContentAlignment="Left">
+                                    <controls:CheckBoxWithDescriptionControl x:Uid="AdvancedPaste_AutoCopySelectionForCustomActionHotkey" IsChecked="{x:Bind ViewModel.AutoCopySelectionForCustomActionHotkey, Mode=TwoWay}" />
+                                </tkcontrols:SettingsCard>
                                 <tkcontrols:SettingsCard
                                     Name="AdvancedPasteClipboardHistoryEnabledSettingsCard"
                                     ContentAlignment="Left"
```
### src/settings-ui/Settings.UI/Strings/en-us/Resources.resw  (+8/-0)
```diff
@@ -4655,6 +4655,14 @@ Activate by holding the key for the character you want to add an accent to, then
     <value>Show clipboard preview</value>
     <comment>Enables display of clipboard contents preview in the Advanced Paste window</comment>
   </data>
+  <data name="AdvancedPaste_AutoCopySelectionForCustomActionHotkey.Header" xml:space="preserve">
+    <value>Auto-copy selection for custom action hotkeys</value>
+    <comment>Advanced Paste is a product name</comment>
+  </data>
+  <data name="AdvancedPaste_AutoCopySelectionForCustomActionHotkey.Description" xml:space="preserve">
+    <value>Attempts to copy the current selection before running a custom action shortcut</value>
+    <comment>Advanced Paste is a product name</comment>
+  </data>
   <data name="GPO_CommandNotFound_ForceDisabled.Title" xml:space="preserve">
     <value>The Command Not Found module is disabled by your organization.</value>
     <comment>"Command Not Found" is a product name</comment>
```
### src/settings-ui/Settings.UI/ViewModels/AdvancedPasteViewModel.cs  (+19/-0)
```diff
@@ -569,6 +569,19 @@ public bool EnableClipboardPreview
             }
         }
 
+        public bool AutoCopySelectionForCustomActionHotkey
+        {
+            get => _advancedPasteSettings.Properties.AutoCopySelectionForCustomActionHotkey;
+            set
+            {
+                if (value != _advancedPasteSettings.Properties.AutoCopySelectionForCustomActionHotkey)
+                {
+                    _advancedPasteSettings.Properties.AutoCopySelectionForCustomActionHotkey = value;
+                    NotifySettingsChanged();
+                }
+            }
+        }
+
         public bool IsConflictingCopyShortcut =>
             _customActions.Select(customAction => customAction.Shortcut)
                           .Concat([PasteAsPlainTextShortcut, AdvancedPasteUIShortcut, PasteAsMarkdownShortcut, PasteAsJsonShortcut])
@@ -1233,6 +1246,12 @@ private void ApplyExternalProperties(AdvancedPasteProperties source)
                 OnPropertyChanged(nameof(EnableClipboardPreview));
             }
 
+            if (target.AutoCopySelectionForCustomActionHotkey != source.AutoCopySelectionForCustomActionHotkey)
+            {
+                target.AutoCopySelectionForCustomActionHotkey = source.AutoCopySelectionForCustomActionHotkey;
+                OnPropertyChanged(nameof(AutoCopySelectionForCustomActionHotkey));
+            }
+
             var incomingConfig = source.PasteAIConfiguration ?? new PasteAIConfiguration();
             if (ShouldReplacePasteAIConfiguration(target.PasteAIConfiguration, incomingConfig))
             {
```