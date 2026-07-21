# Bug Localization Answer: FileLocksmith

**CULPRIT_FILES:**
1. `src/modules/FileLocksmith/FileLocksmithLib/IPC.cpp` (primary)
2. `src/modules/FileLocksmith/FileLocksmithLibInterop/NativeMethods.cpp` (secondary)

**CULPRIT_FUNCTIONS:**
- `IPC.cpp::Writer::start()` (line 23, `std::ofstream(path)` without binary mode)
- `NativeMethods.cpp::StartAsElevated()` (line 100, `std::ofstream stream(paths_file())` without binary mode)

**FIX:**
Both `std::ofstream` constructors write UTF-16 paths to `last-run.log` but open in default text mode, which performs newline translation and corrupts UTF-16 bytes equal to 0x0A (LF). Open both streams with `std::ios::binary` flag: `std::ofstream(path, std::ios::binary)` in `IPC.cpp::Writer::start()` and `std::ofstream(paths_file(), std::ios::binary)` in `NativeMethods.cpp::StartAsElevated()`.

**CITED_FIX_PR:**
#47361

**CONFIDENCE:**
high

**USED_SKILL:**
The skill's regression "Non-ASCII / Unicode paths corrupted (esp. when restarting elevated)" directly matched the symptom, identifying that both IPC writers open `last-run.log` in text mode, corrupting UTF-16 bytes equal to 0x0A via newline translation. The guardrail is to open both writers with `std::ios::binary`.
