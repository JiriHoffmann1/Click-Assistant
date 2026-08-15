# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Build the whole solution:
```
dotnet build ClickAssistant.sln -c Debug
```

Run all tests:
```
dotnet test tests/ClickAssistant.Core.Tests
```

Run a single test (xUnit, filter by fully qualified name or a substring of it):
```
dotnet test tests/ClickAssistant.Core.Tests --filter "FullyQualifiedName~ClickSequenceExecutorTests"
dotnet test tests/ClickAssistant.Core.Tests --filter "FullyQualifiedName~ClickSequenceExecutorTests.SpecificTestMethodName"
```

Run the desktop app from source:
```
dotnet run --project src/ClickAssistant.App
```

Publish a self-contained single-file Windows executable (also available as `./publish-exe.ps1`):
```
dotnet publish src/ClickAssistant.App/ClickAssistant.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Only `ClickAssistant.Core.Tests` has tests; `ClickAssistant.App`/`Infrastructure` have no automated tests, so UI/hook changes need manual verification by running the app.

## Architecture

Four projects, dependencies flow one way only (`App` → `Infrastructure` → `Core`, and `App` → `Core` directly):

- **`src/ClickAssistant.Core`** — pure domain logic, zero external package references. Defines the ports (`IInputSimulator`, `IGlobalInputListener`, `IScreenCaptureProvider`, `IScreenInfoProvider`, `IProfileRepository`) and the engine that only depends on those interfaces: `ClickSequenceExecutor`, `BezierMovementPathGenerator`, point-order strategies, timing/position jitter, `ProfileRescaler`. Models are records (`ClickPoint`, `ClickProfile`, `TimingConfig`, `HumanizationConfig`, `ScreenSnapshot`, `HotkeyConfig`).
- **`src/ClickAssistant.Infrastructure`** — the only project allowed to depend on SharpHook/GDI. Implements the Core ports: `SharpHookGlobalListener` + `SharpHookInputSimulator` (global keyboard/mouse hooks and synthetic input via SharpHook), `WindowsScreenCaptureProvider` (GDI `CopyFromScreen`, Windows-only — returns `null` on other OSes by design), `JsonProfileRepository` (profiles as JSON under `%AppData%/ClickAssistant/profiles`, atomic write via temp file + rename).
- **`src/ClickAssistant.App`** — Avalonia UI, MVVM via CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`). `ViewModels/` hold state and logic, `Views/*.axaml` are the XAML templates.
- **`tests/ClickAssistant.Core.Tests`** — xUnit + NSubstitute, tests only `ClickAssistant.Core` (executor, Bézier path, jitter, order strategies) against fakes/mocks of the ports.

**No DI container despite the package reference.** `Microsoft.Extensions.DependencyInjection` is referenced but unused — every concrete implementation is wired up by hand in one place: the `MainWindow` constructor (`src/ClickAssistant.App/MainWindow.axaml.cs`). That constructor is the composition root; start there when tracing which concrete class backs an interface.

**`ClickSequenceExecutor.StartAsync` must stay fire-and-forget.** It launches the click loop via `Task.Run` and returns immediately; awaiting the inner loop directly would block until the sequence finishes, which never happens for infinite-repeat profiles and would deadlock `Stop()`. This was a real, previously-fixed bug — don't "simplify" it back to a direct await.

**Global hotkeys are two separate, independent bindings**, not one toggle: Start and Stop each get their own `HotkeyConfig` and their own subscriber id on `IGlobalInputListener`. Setting a Stop hotkey is mandatory — `MainWindowViewModel.CanStart` stays false (and `StartAsync` guards again internally) until `Editor.HasStopHotkey` is true.

**Startup has an async race to be careful of**: `MainWindow`'s `Opened` handler calls `MainWindowViewModel.InitializeAsync()`, which loads saved profiles from disk and then auto-selects/creates one. Because the window is already interactive before that `await` resolves, any code path that resets the editor here must first check whether the user has already started editing (`Editor.Steps.Count > 0`) — otherwise it silently discards in-progress edits.

**Avalonia XAML binding gotcha**: inside a nested `ItemsControl.ItemTemplate`, a typed cast like `#Root.((vm:SomeViewModel)DataContext).SomeCommand` fails to resolve the `xmlns:vm` type at runtime (Avalonia XAML resolver limitation for nested templates). Bind through the untyped path instead — `#Root.DataContext.SomeCommand` — since `DataContext` is `object` and the reflective binding resolves the command anyway.

**Screen capture is Windows-only and best-effort.** `WindowsScreenCaptureProvider.CaptureRegion` uses `Graphics.CopyFromScreen`, which captures whatever is actually composited on screen at those pixel coordinates — not a specific window's content. It returns `null` (never throws) on capture failure or non-Windows platforms; callers must treat a `null`/missing bitmap as a normal, expected case, not an error.

Resolution/multi-monitor changes are handled via `ScreenSnapshot` (captured per-profile) compared against the live snapshot from `IScreenInfoProvider`, both before starting a sequence and periodically while one is running; a mismatch triggers a rescale/continue/cancel dialog (`ResolutionMismatchDialog`, `ProfileRescaler`).
