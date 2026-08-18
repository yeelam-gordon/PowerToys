---
name: powertoys-advancedpaste-knowledge
description: 'Engineering knowledge for the PowerToys AdvancedPaste module (WinUI 3 / .NET UI + C++ module interface) that turns clipboard content into plain text, Markdown, JSON, OCR, files, transcode, or AI-transformed output via Semantic Kernel. Use when planning, fixing, triaging, or reviewing AdvancedPaste PRs/bugs touching clipboard handling, SendInput keystroke/modifier injection, paste-as-plain-text, clipboard-history, AI action chaining, custom actions, credential vault (API keys), prompt moderation, online-AI GPO gating, named-pipe IPC, PasteFormats, or provider/endpoint errors. Keywords: AdvancedPaste, clipboard, paste, Ctrl+V, SendInput, stuck modifier keys, Semantic Kernel, OpenAI, Azure OpenAI, Foundry Local, Ollama, PasswordVault, PromptModeration, GPO, OCR, transcode.'
license: Complete terms in LICENSE.txt
---

# PowerToys AdvancedPaste Module Knowledge

Distilled, source-verified engineering knowledge for `src/modules/AdvancedPaste/` — the
recurring regressions, security/privacy guardrails, and review conventions the maintainers
already established. Use it to plan, fix, triage, and review faster without re-reading
thousands of PR threads.

## When to Use This Skill

- Fixing or triaging an AdvancedPaste bug (stuck modifier keys, auto-copy fails, clipboard
  history duplication, AI endpoint/credential errors, settings/module crashes).
- Reviewing an AdvancedPaste PR — especially anything touching clipboard I/O, `SendInput`
  keystroke injection, AI providers, API-key storage, prompt moderation, or GPO gating.
- Planning a new paste format, AI provider, or custom action and wanting the prior art.
- Onboarding onto the module's two-process (C++ ↔ WinUI 3) architecture.

## Module Map (feature → file)

Two processes joined by a **named pipe**. C++ interface is loaded by the Runner; the WinUI 3
app does the transforms. Verify these in source before trusting them (see anti-anchoring).

| Feature | File / function |
|---|---|
| Module load, hotkeys, IPC host | `AdvancedPasteModuleInterface/dllmain.cpp` |
| Paste-as-plain-text keystroke injection | `dllmain.cpp::try_to_paste_as_plain_text()` (writes clipboard w/ `no_clipboard_history_or_roaming`, synthesizes Ctrl+V) |
| **Modifier release/restore around SendInput** | `dllmain.cpp::try_inject_modifier_key_up` / `try_inject_modifier_key_restore` (~L467-493), guarded by `GetAsyncKeyState` |
| Auto-copy current selection | `dllmain.cpp::send_copy_selection()` → `WM_COPY` then `send_ctrl_c_input()` + `poll_clipboard_sequence()` (~L517) |
| **Online-AI GPO gating** | `dllmain.cpp::gpo_policy_enabled_configuration()` / `is_ai_enabled()` (`getConfiguredAdvancedPasteEnabledValue`, `getAllowedAdvancedPasteOnlineAIModelsValue`) |
| Process lifetime / telemetry | `AdvancedPasteProcessManager.cpp`, `trace.cpp` |
| UI entry, IPC dispatch | `AdvancedPasteXAML/App.xaml.cs::OnLaunched` → `NamedPipeProcessor.ProcessNamedPipeAsync` |
| UI (prompt + history) | `AdvancedPasteXAML/Pages/MainPage.xaml.cs`, `Controls/PromptBox.xaml`, `Controls/ClipboardHistoryItemPreviewControl` |
| **Format source of truth** | `Models/PasteFormats.cs` (enum + `[PasteFormatMetadata]`: `ResourceId`, `IPCKey`, `SupportedClipboardFormats`, `IsCoreAction`, `RequiresAIService`, `KernelFunctionDescription`) |
| Clipboard write + flush | `Helpers/ClipboardHelper.cs` |
| Clipboard-history items, hex-color | `Helpers/ClipboardItemHelper.cs` |
| Read text/HTML/image bytes | `Helpers/DataPackageHelpers.cs` (`GetTextOrHtmlTextAsync`, `GetImageAsPngBytesAsync`) |
| Format converters | `Helpers/JsonHelper.cs`, `MarkdownHelper.cs`, `OcrHelpers.cs`, `TranscodeHelpers.cs`, `TransformHelpers.cs` |
| Clipboard sequence number (P/Invoke) | `Helpers/NativeMethods.cs::GetClipboardSequenceNumber` |
| Format execution | `Services/PasteFormatExecutor.cs` (`IPasteFormatExecutor`) |
| AI kernel | `Services/KernelServiceBase.cs` → `Services/AdvancedAIKernelService.cs` |
| Custom actions / providers | `Services/CustomActions/*` (`CustomActionTransformService`, `SemanticKernelPasteProvider`, `FoundryLocalPasteProvider`, `LocalModelPasteProvider`, `PasteAIProviderFactory`) |
| **Credential vault (API keys)** | `Services/EnhancedVaultCredentialsProvider.cs` (`PasswordVault`, keyed per service) |
| **Prompt moderation** | `Services/OpenAI/PromptModerationService.cs::ValidateAsync` |
| **AI action chaining** | `Models/ActionChainItem.cs`, `KernelExtensions.GetOrAddActionChain()`, `KernelServiceBase.ExecuteCachedActionChain`, `CustomActionKernelQueryCacheService` (persists chain, `Models/KernelQueryCache/*`) |
| Settings ↔ GPO binding, clipboard watch | `ViewModels/OptionsViewModel.cs` (`GetClipboardSequenceNumber`) |
| Settings serialization | `Settings.UI.Library/AdvancedPasteProperties.cs` |
| Error text mapping | `Helpers/ErrorHelpers.cs::TranslateErrorText` |

## Regression Playbooks (rule-by-rule)

Read the diff first (see anti-anchoring), then confirm the touched path here.

### Stuck modifier keys after paste
- **Symptom:** keyboard "locked", Win+Shift/Ctrl+Alt stuck after paste-as-plain-text
  (#42471, #45685, #43220, #42875, #40374, #46874).
- **Where:** `dllmain.cpp::try_to_paste_as_plain_text` / `send_ctrl_c_input`.
- **Root cause:** synthesizing Ctrl+V while the activation hotkey's modifiers are still
  physically held, so the target sees e.g. Win+Shift+Ctrl+V.
  [`SendInput`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
  injects into the shared input stream and can't see physical key state without
  `GetAsyncKeyState`.
- **Guardrail:** any new `SendInput` path must release L/R Ctrl/Win/Shift/Alt via
  `try_inject_modifier_key_up`, act, `try_inject_modifier_key_restore`, plus a dummy VK
  `0xFF` key-up to suppress the Start menu. Guard every release with `GetAsyncKeyState`.

### Auto-copy fails on Electron/Chromium apps
- **Symptom:** nothing copied from Teams / VS Code / browsers (cf. #48327).
- **Where:** `dllmain.cpp::send_copy_selection` / `poll_clipboard_sequence`.
- **Root cause:** Ctrl+C fallback injected keys without releasing hotkey modifiers, and
  success was never verified.
- **Fix / guardrail:** [PR #46486](https://github.com/microsoft/PowerToys/pull/46486) —
  release modifiers before Ctrl+C, then verify the
  [clipboard sequence number](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboardsequencenumber)
  advanced. **Treat "sequence number unchanged" as failure, not success**, and warn-log.

### Clipboard-to-LLM: unmoderated / non-gated AI path (security/privacy)
- **Symptom:** untrusted clipboard content (may hold secrets) is sent to a cloud LLM; a new
  provider/path ships without moderation or enterprise control.
- **Where:** `AdvancedAIKernelService.ShouldModerateAdvancedAI()` (~L88, base
  `KernelServiceBase.cs`), `PromptModerationService.ValidateAsync`, GPO
  `getAllowedAdvancedPasteOnlineAIModelsValue` (`dllmain.cpp`).
- **Root cause:** clipboard text/images become the prompt — attacker-influenced and possibly
  sensitive ([OWASP LLM01 Prompt Injection / LLM02 Sensitive Info Disclosure](https://owasp.org/www-project-top-10-for-large-language-model-applications/)).
  Moderation is reachable **only** for OpenAI/Azure OpenAI (`ModerationEnabled && (OpenAI ||
  AzureOpenAI)`); local/other providers are unmoderated by design.
- **Guardrail:** when adding a provider or AI paste path, confirm the online-AI GPO gate
  still applies (introduced [#35026](https://github.com/microsoft/PowerToys/pull/35026)) and
  consciously decide whether moderation is reachable. Moderation + Semantic Kernel landed in
  [#35902](https://github.com/microsoft/PowerToys/pull/35902).

### API keys must live in the OS credential vault
- **Symptom:** a new provider stores its key in `settings.json` / `AdvancedPasteProperties`.
- **Where:** `EnhancedVaultCredentialsProvider.LoadKey` (`PasswordVault().Retrieve(Resource,
  Username)`), keyed per `AIServiceType`.
- **Root cause:** secrets in plaintext settings
  ([OWASP Secrets Management](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html),
  [Windows Credential Locker](https://learn.microsoft.com/en-us/windows/uwp/security/credential-locker)).
  Cloud keys added in [#42374](https://github.com/microsoft/PowerToys/pull/42374).
- **Guardrail:** route every secret through `IAICredentialsProvider`; `settings.json` holds
  only endpoint/model. Local providers (FoundryLocal/Ollama/ML/Onnx) return `null` — no key.

### Clipboard history item duplicated / undeletable
- **Symptom:** clicking a history item creates a *new* entry; original can't be deleted
  (#43945).
- **Where:** `MainPage.xaml.cs` `ClipboardHistory_ItemInvoked`.
- **Root cause:** used `ClipboardHelper.SetTextContent/SetImageContent` →
  `Clipboard.SetContentWithOptions`, minting a new entry.
- **Fix / guardrail:** [PR #44212](https://github.com/microsoft/PowerToys/pull/44212) — use
  `Clipboard.SetHistoryItemAsContent(item)` to re-promote the existing entry, wrapped in
  try/catch + `Logger.LogError` (Win32 clipboard is a contended shared resource that throws).

### Custom-action / hotkey dead unless Settings open
- **Symptom:** custom-action shortcut only fires when the Settings page is open; conflicts,
  wrong-key firing (#43899, #43665, #45231, #45686, #45089).
- **Where:** hotkey registration in `dllmain.cpp`; activation parity in `App.xaml.cs`.
- **Root cause:** custom-action activation didn't match the main-hotkey activation path.
- **Fix / guardrail:** [PR #44288](https://github.com/microsoft/PowerToys/pull/44288). New
  activation code must behave identically whether or not the UI is already started.

### AI endpoint / credential error family
- **Symptom:** provider-specific 400/403/SSL failures (OpenAI Responses-API models #47292/#40210,
  Foundry Local #45340, Gemini/Gemma #45268/#46311, Azure AI Inference #44295, AOAI SSL #43951,
  Ollama LAN #45959, non-ASCII custom-action name w/ AzureOpenAI #44480).
- **Where:** `AdvancedAIKernelService` connector registration; `ErrorHelpers.TranslateErrorText`.
- **Guardrail:** error text is mapped by service type — a new provider needs its own error
  mapping and a clear "reconfigure model/endpoint" message. `RequireEndpoint` throws if the
  endpoint is blank for Azure/AzureAIInference/Ollama. Watch encoding/`serviceId` for
  non-ASCII names.

See [references/regression-catalog.md](./references/regression-catalog.md) for the fuller
list (paste-as-plain-text CRLF trimming, clipboard-history coupling, settings/module
crashes, XAML binding warnings, accessibility tab-stop sink, migration bugs).

## Review Rules (imperative)

Generic rules link an authoritative reference **plus** the app-specific hook and PR evidence.

- **Store API keys in the OS credential store, never in settings JSON**
  ([OWASP Secrets Management](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html),
  [Credential Locker](https://learn.microsoft.com/en-us/windows/uwp/security/credential-locker)):
  route through `IAICredentialsProvider`/`EnhancedVaultCredentialsProvider`; never add a key
  field to `AdvancedPasteProperties` (#42374).
- **Moderate clipboard-derived prompts and let enterprises disable online AI**
  ([OWASP LLM Top 10](https://owasp.org/www-project-top-10-for-large-language-model-applications/)):
  verify the online-AI GPO gate and moderation reachability when adding a provider/path
  (#35902, #35026).
- **Release held modifiers around every `SendInput`**
  ([SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)):
  new injection paths must release+restore L/R Ctrl/Win/Shift/Alt guarded by
  `GetAsyncKeyState`, or keys stick (#42471, #46486).
- **Wrap every Windows clipboard call in try/catch + `Logger.LogError`**
  ([about the clipboard](https://learn.microsoft.com/en-us/windows/win32/dataxchg/about-the-clipboard)):
  `SetHistoryItemAsContent` and friends throw on access-denied/removed-item/COM (#44212).
- **Clipboard-read helpers must not throw** — wrap parse branches, return `string.Empty`,
  and always log; matches `JsonHelper.ToJsonFromXmlOrCsvAsync` (#48124).
- **Dispose `SoftwareBitmap`/`IDisposable` clipboard resources** with `using`
  ([Dispose pattern](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose)) —
  image transforms allocate unmanaged bitmaps (#44021).
- **No empty catch blocks; catch specific exceptions and log** (#43990).
- **Treat `KernelFunctionDescription` strings as behavior, not copy** — they are what the
  model reads to route; keep language non-restrictive; review edits as functional (#44021).
  Don't hand-edit the router prompt to force a choice — let the descriptions drive routing.
- **Localize all user-facing strings via `.resw`/`ms-resource`**
  ([WinUI resources](https://learn.microsoft.com/en-us/windows/apps/design/globalizing/use-windows-app-sdk-resources)):
  keep `.resw` keys in sync with `x:Uid` (rename broke a binding #43617); literals are
  blocked at review.
- **Route settings through all three layers with GPO combine + back-compat default**
  (`AdvancedPasteProperties.cs` → Settings page → `IUserSettings`/`OptionsViewModel`); keep
  `is_enabled_by_default()` in sync with `EnabledModules.cs` (#47144).
- **Don't reorder `Microsoft.Cpp.Default.props` includes / `PlatformToolset`** in the
  module-interface `.vcxproj` — centrally managed and order-sensitive (#44639).

## Gotchas

- **AI paste UI shows only when `ShowAIPaste && IsAllowedByGPO`.** There is a global online-AI
  GPO *and* a per-provider GPO (`...OpenAIValue`, `...AzureOpenAIValue`, `...MistralValue`,
  `...GoogleValue`, `...OllamaValue`, `...FoundryLocalValue`). Missing the per-provider check
  leaks a disabled provider into the UI (#45242).
- **Moderation is OpenAI/Azure-only by design.** Do not assume local/Mistral/Google/Ollama
  traffic is moderated — there's no moderation endpoint for them.
- **PNG-only assumptions are safe *by construction*.** `GetImageAsPngBytesAsync` transcodes
  any clipboard image to PNG first, so downstream "we only support PNG" is intentional — don't
  add redundant format branches.
- **Clipboard writes must run on the UI thread with a retry loop.** `ClipboardHelper` flushes
  via `TaskScheduler.FromCurrentSynchronizationContext()` and retries because `Flush()` "fails
  from time to time when directly activated via hotkey". **Never** call `Clipboard.*` from
  arbitrary threads.
- **Some flows depend on Windows clipboard history being ON** — disabling it broke activation
  (#43814, #45243). Track this coupling when touching clipboard-read paths.
- **`PasteFormat`/`ClipboardItem` don't implement `INotifyPropertyChanged`** — use `x:Bind
  Mode=OneTime` and refresh via `Bindings.Update()`; `OneWay` emits WMC1506 warnings (#46726).
- **Telemetry must never log clipboard content or prompts** — endpoint-usage events record
  service type/model/duration only.

## Using This Skill in PR Review (Anti-Anchoring)

Benchmark-derived: reading this file first and hunting the diff for its themes *lowers* your
catch rate on the PR's actual issues. Instead:

1. **Read the diff cold first.** Form your own concern list from what actually changed.
2. **Then** cross-check only the touched code paths against the Regression Playbooks and
   Review Rules above — targeted retrieval, not the whole file.
3. Treat the Module Map as **hypotheses to confirm in source**, not ground truth. If a symptom
   doesn't clearly map to a listed area, reason from the symptom and verify against source —
   a thin map can anchor you onto a confident, wrong file.

This file is most valuable for **planning, onboarding, and bug-fixing**; least valuable as a
flat pre-read for expert review.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — AdvancedPaste PR review checklist.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source: [`src/modules/AdvancedPaste/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/AdvancedPaste)
