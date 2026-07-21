# AdvancedPaste Bug Triage — Symptom → Likely File/Function

Map the reported symptom to the most likely code path, then **confirm in source** before
committing to a hypothesis. If the symptom doesn't clearly match a row, reason from the
symptom — do not force-fit (a thin map can anchor you onto a wrong file).

| Symptom | Start here (file / function) | Notes / evidence |
|---|---|---|
| Keyboard "locked" / stuck Ctrl/Win/Shift/Alt after paste | `dllmain.cpp::try_to_paste_as_plain_text`, `try_inject_modifier_key_up/_restore` | Modifiers not released around `SendInput`. #42471, #45685 |
| Auto-copy does nothing in Teams/VS Code/browser | `dllmain.cpp::send_copy_selection`, `poll_clipboard_sequence` | Ctrl+C fallback; verify sequence number. #46486, #48327 |
| Clicking history item duplicates it / can't delete | `MainPage.xaml.cs` `ClipboardHistory_ItemInvoked` | Must use `Clipboard.SetHistoryItemAsContent`. #43945, #44212 |
| Custom action fires only when Settings open / wrong key | `dllmain.cpp` hotkey registration; `App.xaml.cs` activation | Activation parity with main hotkey. #43899, #44288 |
| "Doesn't work unless clipboard history is on" | clipboard-read paths, `OptionsViewModel` | OS clipboard-history coupling. #43814, #45243 |
| Paste-as-plain-text keeps CRLF / extra whitespace | `TransformHelpers.ToPlainTextAsync`, `PasteFormats` | Dedicated "plain text (trimmed)" action. #46007 |
| AI provider 400/403/SSL error | `AdvancedAIKernelService` connector reg.; `ErrorHelpers.TranslateErrorText` | Per-provider error mapping; `RequireEndpoint`. #47292, #45340, #45268 |
| Non-ASCII custom-action name breaks AzureOpenAI | custom-action `serviceId`/encoding path | #44480 |
| API key not persisted / lost | `EnhancedVaultCredentialsProvider` (`PasswordVault`) | Keys are per-service in vault, not settings. #42374 |
| AI paste UI missing / shows disabled provider | `OptionsViewModel` (`IsAllowedByGPO`, per-provider GPO) | `ShowAIPaste && IsAllowedByGPO`. #45242 |
| Endpoint URL not saved after migration | migration path, `AdvancedPasteProperties` | #43456, #43459, #43524 |
| JSON conversion returns wrong/empty result silently | `JsonHelper.ToJsonFromXmlOrCsvAsync` | Must log on error, return `string.Empty`. #48124 |
| Image paste / OCR memory growth or crash | `DataPackageHelpers.GetImageAsPngBytesAsync`, `OcrHelpers` | Dispose `SoftwareBitmap` in `using`. #44021 |
| Settings page / module crash | AdvancedPaste settings XAML/viewmodel bindings | #45060, #45067, #44835, #49114 |
| 13x WMC1506 binding warnings | `x:Bind` mode on `PasteFormat`/`ClipboardItem` | Use `Mode=OneTime`. #46726 |
| Extra tab stop between prompt and history | wrapping `ScrollViewer` `IsTabStop` | Set `IsTabStop="False"`. #43655, #43660 |
| String not localized / wrong text | `.resw` keys vs `x:Uid`; `ResourceLoaderInstance` | #43617, #43600 |

## Triage workflow

1. Reproduce and capture the exact symptom + whether AI/clipboard-history is involved.
2. Match to a row above; open the named file/function and **confirm** the code path exists
   and matches the symptom.
3. Check the linked PR/issue for how a similar bug was fixed and what guardrail was added.
4. If no row fits, search `PasteFormats.cs` (feature enum) and the Module Map in `SKILL.md`.
