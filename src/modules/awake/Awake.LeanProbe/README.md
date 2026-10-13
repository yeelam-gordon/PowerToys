# Awake: lean C# NativeAOT feasibility probe

**Result:** A native-style C# design reached approximately the same startup time
and countdown CPU range as the Rust rewrite for the measured workloads. It did
not match Rust's executable size or private memory, and it is not feature-complete.

This standalone experimental project is intentionally absent from the PowerToys
solution and installer. It does not replace or modify the production Awake
executable. Its purpose is to test whether preserving C# is a viable optimization
direction before introducing another language.

## Final x64 measurements

Windows development machine; .NET 10 NativeAOT; Rust PR #51181 at `93a394bbcc`.
The default probe uses `OptimizationPreference=Size` and identical-method folding.
Three alternating rounds ran each workload; each round measured five launches and
one ten-second countdown CPU window after three seconds of settling.

| Metric | Lean C# NativeAOT | Rust |
|--------|------------------|------|
| CLI startup, median of three round medians | **28.1 ms** | **30.3 ms** |
| Settings startup, median of three round medians | **29.8 ms** | **30.5 ms** |
| CLI countdown CPU / 10 s, observed range | 3.3-4.0 million cycles | 2.8-3.7 million cycles |
| Settings countdown CPU / 10 s, observed range | 3.2-3.7 million cycles | 3.3-3.7 million cycles |
| CLI private memory | 4.0-4.3 MiB | 2.1 MiB |
| Settings private memory | 4.2-4.4 MiB | 2.1 MiB |
| CLI working set | 14.3-14.5 MiB | 11.9 MiB |
| Threads | 5 | 6 |
| Executable bytes | **2,529,280** | **938,496** |
| External runtime state-icon bytes | 54,506 | Embedded in executable |

The earlier, substantially unchanged C# architecture compiled with NativeAOT
measured 64.9 ms CLI startup and produced a 12,554,240-byte executable. That is a
historical reference, not a simultaneous third participant in this comparison.

The speed-optimized C# variant is also retained in the data, not discarded:
its executable is 3,083,776 bytes, its CLI round medians are 41.8/32.8/27.8 ms,
and its settings round medians are 29.5/29.5/27.8 ms. It did not provide a consistent
startup advantage over the smaller configuration. See the raw samples rather than
selecting its best run.

**Interpretation:** These observations support trying a lean C# NativeAOT
implementation. They do not establish statistical equivalence, a universal language
advantage, or the final footprint of a complete implementation. There is still
about a twofold private-memory difference and a 2.7-fold executable-size difference.

## What actually runs

- Direct Win32 hidden window and Shell notification icon using source-generated
  P/Invoke; no WPF, WinForms, `System.Drawing`, or WinRT projection use.
- One state-owning message-loop thread; native waiting for the bound process and
  Windows messages.
- Real `SetThreadExecutionState` system/display requests, native countdown timers,
  tooltip-only updates, and WTS lock/unlock notifications.
- Standalone `for`/`until` parsing reused from the parent C# branch.
- Awake-only source-generated JSON deserialization and `FileSystemWatcher`
  notifications dispatched back to the state-owning thread.
- Deadline and process-exit completion, icon deletion, and request release.

Timer-completion smoke checks exercise actual native calls and process lifetime.
An independent `powercfg /requests` check was **blocked by lack of elevation**;
no administrator prompt or policy change was attempted.

## Important missing production features

This is an architecture probe, not a full Rust-to-C# port:

| Omitted area | Why the result is not full feature parity |
|--------------|-------------------------------------------|
| GPO, telemetry, logging | No equivalent policy bridge, ETW emission, recording, or async log sink is included |
| Runner integration | No signed module contract, named exit event, secure single-instance objects, or watchdog |
| Tray UX | No popup menu, Explorer-restart recovery, theme/RTL handling, or full initial lock-state detection |
| Settings persistence | No writeback, presets, atomic merging, migration, or busy-file recovery; configuration expiry exits rather than persisting passive mode |
| CLI and localization | Only the benchmark subset is accepted; no complete help, console handoff, parent alias, or localization pipeline |
| Validation breadth | No ARM64 runtime, full UI suite, suspend/resume, installer, or failure-stress validation |

Adding these capabilities can increase code size, memory and startup work.
The C# probe also uses a managed file watcher rather than copying the Rust native
overlapped watcher. This comparison controls the workloads, not every implementation
detail.

## Measurement limitations

Startup means **time until the hidden `Awake.MessageWindow` exists**, as in the PR's
original `measure` harness. It is not a full functional-readiness or power-state
measurement. Both programs perform additional initialization around that milestone.
The three-second settling period precedes the footprint/CPU sample.

These are mixed cold/warm development-machine launches, not a rebooted cold-start
study. The harness prints memory as MB but scales by 1,048,576 bytes (MiB).
CPU-time readings have coarse granularity, so the report uses cycle counts instead
of interpreting a displayed `0.00 ms` as zero work. Cycle counts are not battery
energy measurements.

The Rust CLI reference is built without test hooks. Settings-mode Rust uses the
PR's test hooks to redirect files and kernel-object names to a generated temporary
fixture. The product logic remains intact, but this is not its exact shipped
configuration. No real PowerToys settings are overwritten.

## Reproduce

Build the C# probe from this directory:

```powershell
..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Release -ExtraArgs '/restore','/t:Publish'
```

The native executable is at
`x64\Release\AwakeLeanProbe\publish\PowerToys.Awake.LeanProbe.exe` relative to the
repository root. The project imports the normal shared .NET props but references
no PowerToys settings, UI, logging, or interop projects.

Create a separate Rust worktree at the final PR commit, then apply
`settings-fixture.patch` from this directory with `git apply`. This changes only the
measurement example: it writes a five-minute settings fixture, substitutes
`{settings}`, and starts the clock after fixture creation. It does not change Rust
product code.

From that worktree's `src\modules\awake\Awake` directory:

```powershell
cargo build --release --locked -p awake --target-dir target\shipped
cargo build --release --locked -p awake --features test-hooks
cargo build --release --locked -p awake --features test-hooks --example measure
```

Invoke `Measure-Comparison.ps1` with the resulting binaries:

```powershell
.\Measure-Comparison.ps1 `
  -HarnessPath '<RustWorkspace>\target\release\examples\measure.exe' `
  -RustCliPath '<RustWorkspace>\target\shipped\release\awake.exe' `
  -RustSettingsPath '<RustWorkspace>\target\release\awake.exe' `
  -ProbePath '<CSharpRepo>\x64\Release\AwakeLeanProbe\publish\PowerToys.Awake.LeanProbe.exe' `
  -OutputPath '<output-file.txt>' -Rounds 3
```

The wrapper has a 180-second timeout per measurement and captures output/error
streams. The harness has a 30-second window-start timeout per launch. Keep other
Awake instances closed.

`results\measurements.x64.json` contains all 24 final size/speed comparison rows,
binary hashes, compiler/environment metadata, and the methodology.
`results\size-comparison.x64.txt` and `results\speed-comparison.x64.txt` preserve
the original numeric output. Inspect these alongside the source before using the
summary to support a production decision.
