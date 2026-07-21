# AdvancedPaste Regression Catalog (progressive disclosure)

Fuller regression history for `src/modules/AdvancedPaste/`. Load this only when the touched
area matches. Each entry: symptom → root cause → fix PR → guardrail. Confirm in source.

## Keystroke / input injection

- **Stuck modifier keys after paste** (#42471, #45685, #43220, #42875 swapped Ctrl/Alt,
  #40374 PowerPoint, #46874). Root: Ctrl+V synthesized while activation-hotkey modifiers held.
  Fix: release L/R Ctrl/Win/Shift/Alt via `try_inject_modifier_key_up`, paste,
  `try_inject_modifier_key_restore`, dummy `0xFF` key-up (`dllmain.cpp`). Guardrail: every new
  `SendInput` path releases+restores, guarded by `GetAsyncKeyState`.
- **Auto-copy fails on Electron/Chromium** (cf. #48327). Root: Ctrl+C fallback didn't release
  modifiers and success wasn't verified. Fix [#46486](https://github.com/microsoft/PowerToys/pull/46486):
  release modifiers, verify clipboard sequence number advanced, warn-log on failure.

## Clipboard handling

- **History item duplicated on click** (#43945). Root: `SetContentWithOptions` created a new
  entry. Fix [#44212](https://github.com/microsoft/PowerToys/pull/44212):
  `Clipboard.SetHistoryItemAsContent(item)` in try/catch.
- **"Doesn't work unless clipboard history is on"** (#43814, #45243, v0.96.0). OS
  clipboard-history APIs coupled into activation. Guardrail: track this coupling on
  clipboard-read paths.
- **Paste-as-plain-text didn't trim CRLF** (#46007). Fix: dedicated "plain text (trimmed)"
  action wired through `TransformHelpers.ToPlainTextAsync`, `PasteFormats`, interop
  `Constants`, Settings — not a silent behavior change.
- **JSON conversion swallowed errors silently** (#48124). Fix: `ToJsonFromXmlOrCsvAsync` wraps
  `GetTextAsync()`, returns `string.Empty`, logs — matching sibling parse branches.

## AI providers / endpoints / credentials

- **OpenAI 403 for Responses-API models** (#47292 gpt-5-mini, #40210). Hotfix
  [#43766](https://github.com/microsoft/PowerToys/pull/43766) removed unsupported
  prompt-execution-settings; [#43768](https://github.com/microsoft/PowerToys/pull/43768)
  lengthened output.
- **Foundry Local 400 / model-not-in-catalog** (#45340). Fixes: don't prefix displayed model
  with `fl://`, auto-start Foundry service
  ([#43529](https://github.com/microsoft/PowerToys/pull/43529)); re-configure hint when a
  catalog model disappears ([#43600](https://github.com/microsoft/PowerToys/pull/43600)).
- **Gemini/Gemma 400** (#45268, #46311), **Azure AI Inference** (#44295), **AOAI SSL**
  (#43951), **Ollama LAN address** (#45959), **non-ASCII custom-action name w/ AzureOpenAI**
  (#44480 — encoding/`serviceId`), **Anthropic removal** (#43429). Guardrail: error text mapped
  by service type (`ErrorHelpers.TranslateErrorText`); new providers need their own mapping +
  clear "reconfigure model/endpoint" messaging.
- **Legacy single-OpenAI-key migration** to multi-provider model, then migration code deleted
  ([#43459](https://github.com/microsoft/PowerToys/pull/43459),
  [#43524](https://github.com/microsoft/PowerToys/pull/43524),
  [#43564](https://github.com/microsoft/PowerToys/pull/43564)); endpoint-url not persisting
  (#43456, #44243); stale doc claiming OpenAI key required (#44044).

## UI / bindings / accessibility / crashes

- **Settings-page / module crashes** (#45060, #45067, #44835 Priority-1, #44883, #49114); GPO
  info-bar mis-placement for clipboard history (#45029).
- **XAML binding warnings** — `PasteFormat`/`ClipboardItem` lack `INotifyPropertyChanged`;
  `Mode=OneWay` `x:Bind` produced 13 WMC1506 warnings; fixed to `Mode=OneTime` refreshed via
  `Bindings.Update()` ([#46726](https://github.com/microsoft/PowerToys/pull/46726)).
- **Accessibility tab-stop sink** (#43655). A wrapping `ScrollViewer` became an extra tab stop
  between PromptBox and ListViews. Fix [#43660](https://github.com/microsoft/PowerToys/pull/43660):
  `IsTabStop="False"`, per [WCAG 2.1.1 Keyboard](https://www.w3.org/WAI/WCAG21/Understanding/keyboard.html).
- **Enabled-state flicker** — `is_enabled_by_default()` mismatched `EnabledModules.cs`; fixed
  in [#47144](https://github.com/microsoft/PowerToys/pull/47144).
- **Localization** — `LocalModelBadge` → `LocalModelBadge.Text` rename broke an `x:Uid`
  binding (#43617); "Do not localize" resource-comment convention (#43600).

## Testing & build

- Test projects: `AdvancedPaste.UnitTests` (Helpers/Converters/Services, mocks in `Mocks/`),
  `AdvancedPaste.FuzzTests`, `UITest-AdvancedPaste`
  ([#40803](https://github.com/microsoft/PowerToys/pull/40803)); run under MTP
  ([#37651](https://github.com/microsoft/PowerToys/pull/37651)). New image/AI helpers shipped
  without tests — add coverage (#44021).
- CI: "Verify XAML formatting" + StyleCop (SA1505, WMC1506); prefer `[GeneratedRegex]` without
  redundant `RegexOptions.Compiled` (#43990). C++ `.vcxproj` (`PlatformToolset`,
  `Microsoft.Cpp.Default.props` order) centrally managed — document deviations (#44639).
