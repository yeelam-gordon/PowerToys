# Mouse Without Borders — Bug Triage (symptom → likely file/function)

Use the Module Map in SKILL.md as hypotheses to confirm in source. First decide the area (crypto /
socket / pool / input / clipboard / service), then localize.

| Symptom | Area | Start here (file · symbol) |
|---|---|---|
| Two machines won't connect after one updated | wire/compat | `Encryption.cs` `Get*Stream` header, `Common.cs` `SendOrReceiveARandomDataBlockPerInitialIV`, `SocketStuff.cs` framing (version mismatch is expected — no negotiation) |
| Same plaintext looks identical on the wire / crypto concern | security | `Encryption.cs` `GetEncryptedStream`/`GenLegalKey` (verify random salt+IV); tests in `EncryptionTests.cs` |
| Packets from a peer rejected / wrong key accepted | security | `Encryption.cs` `Get24BitHash`/`MagicNumber`; `SocketStuff.cs` `ProcessReceivedDataEx` |
| "Adding a PC makes the others vanish" / can't add machine | pool | `MachinePool.cs` (`list.Count >= 4` throw), `MachineStuff.cs` `AddToMachinePool` |
| Cursor won't cross to the neighbour machine | pool/edge | `MachineStuff.cs` `MoveToMyNeighbourIfNeeded`, `MoveLeft`/`MoveRight`, `NewDesMachineID` |
| Easy Mouse edge-cross doesn't trigger | input | `InputHook.cs` `UpdateEasyMouseKeyDown`/`EasyMouseKeyDown`, `EasyMouseOption.cs`, `HotKeyToggleEasyMouse` |
| Modifier key stuck / keyboard sync bug on remote | input | `InputSimulation.cs` `SendInputEx`/`keybd_event`, `InputHook.cs` key up/down, `KEYBDDATA.cs` |
| AltGr not working across machines | input | `InputSimulation.cs` (LCONTROL+RMENU handling), `VK.cs` |
| Keystrokes dropped over an elevated app (e.g. admin Terminal) | elevation | `Service.cs` `StartMouseWithoutBordersService`, `ModuleInterface/dllmain.cpp` service registration (UIPI — needs the service) |
| "Cannot run as administrator" / can't close admin mode | elevation | `ModuleInterface/dllmain.cpp` `launch_process`/`register_service`, `Core/Launch.cs` |
| Clipboard content corrupted (Excel→picture, rich text) | clipboard | `Core/Clipboard.cs`, `Core/ClipboardPostAction.cs`, `Class/IClipboardHelper.cs` |
| Dragged file transfer fails | clipboard | `Core/DragDrop.cs` |
| Sharing clipboard crashes network stack | clipboard/socket | `Core/Clipboard.cs` transfer path + `SocketStuff.cs`/`TcpServer.cs` framing |
| "Exception saving settings" / settings.json locked | settings | `App/Class/Setting.cs` (sharing/retry; multi-process contention) |
| Non-ASCII device name won't connect | pool/naming | `MachineStuff.cs` machine-name handling, `MachinePool.cs`, `Setting.cs` |
| CmdPal can't toggle move / reconnect / KBM | integration | CmdPal MWB extension (PR #45350), IPC in `Core/IpcChannelHelper.cs` |

## Cross-cutting checks
- MWB runs several processes (per-desktop + service); `static` state is per-process, settings.json is shared.
- If the symptom is version-dependent connectivity, suspect a **wire-format change** first (no negotiation).
- If it only happens with elevated targets, suspect **UIPI / service**, not the injection code.
- Confirm the shared key (`MyKey`) matches on both machines before blaming crypto.
