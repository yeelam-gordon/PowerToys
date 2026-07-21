# AdvancedPaste PR Review Checklist

Apply after reading the diff cold. Cross-check only the code paths the PR actually touches.
Prioritize the **security/privacy** checks — they are the highest-consequence and most
frequently missed.

## Security & Privacy (highest priority)

- [ ] **API keys / secrets** routed through `IAICredentialsProvider` /
      `EnhancedVaultCredentialsProvider` (Windows `PasswordVault`) — **never** a new field in
      `AdvancedPasteProperties` / `settings.json`. (#42374)
- [ ] **New AI provider or paste path:** the online-AI GPO gate
      (`getAllowedAdvancedPasteOnlineAIModelsValue`) still applies, and the AI UI stays behind
      `ShowAIPaste && IsAllowedByGPO` incl. the **per-provider** GPO. (#45242, #35026)
- [ ] **Prompt moderation reachability** consciously decided for the touched path
      (`ShouldModerateAdvancedAI` → OpenAI/Azure only). Clipboard content is
      attacker-influenced + may hold secrets. (#35902)
- [ ] **Telemetry** logs service type/model/duration only — no clipboard content or prompt text.

## Clipboard & keystroke injection

- [ ] Every `Clipboard.*` call wrapped in try/catch + `Logger.LogError`. (#44212)
- [ ] Clipboard writes go through `ClipboardHelper` (UI-thread flush + retry) — not raw
      `Clipboard.*` off-thread.
- [ ] New `SendInput` path releases + restores L/R Ctrl/Win/Shift/Alt via
      `try_inject_modifier_key_up`/`_restore`, guarded by `GetAsyncKeyState`, with the dummy
      `0xFF` key-up. (#42471, #46486)
- [ ] Copy/paste success verified via clipboard **sequence number** — "unchanged" = failure.
- [ ] Read helpers never throw: parse branches return `string.Empty` and log. (#48124)
- [ ] `SoftwareBitmap`/`IDisposable` image resources scoped in `using`. (#44021)

## AI routing & providers

- [ ] `KernelFunctionDescription` edits reviewed as **functional** changes; language stays
      non-restrictive; router prompt not hand-edited to force a choice. (#44021)
- [ ] New provider has its own `ErrorHelpers.TranslateErrorText` mapping + "reconfigure
      model/endpoint" message; `RequireEndpoint` enforced where needed.
- [ ] Encoding / `serviceId` safe for non-ASCII custom-action names. (#44480)

## Settings, i18n, build

- [ ] New setting round-trips all three layers (`AdvancedPasteProperties.cs` → Settings page →
      `IUserSettings`/`OptionsViewModel`), combines with GPO, defaults to prior behavior;
      `is_enabled_by_default()` matches `EnabledModules.cs`. (#47144)
- [ ] All user-facing strings in `.resw`/`ms-resource`; `x:Uid` matches `.resw` keys. (#43617)
- [ ] `x:Bind` uses `Mode=OneTime` for `PasteFormat`/`ClipboardItem` (no `INotifyPropertyChanged`). (#46726)
- [ ] No empty catch blocks; specific exceptions caught + logged. (#43990)
- [ ] Module-interface `.vcxproj`: `Microsoft.Cpp.Default.props` include order /
      `PlatformToolset` untouched unless justified. (#44639)
- [ ] Accessibility: non-interactive wrappers (e.g. `ScrollViewer`) set `IsTabStop="False"`. (#43655)
- [ ] Tests added for new helpers/AI paths (`AdvancedPaste.UnitTests`, FuzzTests, UITest). (#44021)
