# Click Assistant

A Windows desktop autoclicker (.NET 8 / Avalonia UI). Define a sequence of points on screen (mouse clicks,
optionally key presses), and the app plays them back automatically in the chosen order and interval -
optionally with "humanized" mouse movement (curved paths, randomized position and timing) so the action
doesn't look robotic.

## Main features

- **Sequence editor** - click points and key-press steps, sequential/random order, custom order by
  dragging, repeat N times or forever, base interval with random jitter.
- **Movement humanization** - curved (Bézier) mouse paths between points, randomized target position,
  randomized movement duration, a chance of "overshooting" the target - all optional and configurable.
- **Global hotkeys** - Start and Stop are two independent hotkeys, and work even outside the app window.
- **Monitor map** - a visual editor for the layout of all connected monitors; both points and monitors can
  be dragged with the mouse, monitors can't be dragged through each other or off the map (collisions push
  the other monitors aside or snap to the nearest free position).
- **Live point preview** - the selected point can show a real screenshot of the area around the click,
  which refreshes itself when whatever is under it changes.
- **Resolution/monitor change detection** - on start and while running, the app compares the saved screen
  snapshot against the current one and offers to rescale the profile's coordinates on a mismatch.
- **Single instance** - launching the app again while it's already running just brings the existing window
  to front (maximized) instead of opening a second copy.
- **Localization** - the full UI is available in 40 languages (see
  `src/ClickAssistant.App/Localization/Strings`), switchable at runtime without a restart.
- **Light/dark/automatic theme.**

## Requirements

- Windows (screen capture and input simulation are tied to Windows APIs).
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build from source.

## Run from source

```
dotnet run --project src/ClickAssistant.App
```

## Tests

Automated tests cover only `ClickAssistant.Core` (pure domain logic with no UI/OS dependencies):

```
dotnet test tests/ClickAssistant.Core.Tests
```

## Build a standalone .exe

```
./publish-exe.ps1
```

(or the equivalent `dotnet publish` command inside the script) produces a self-contained single-file
`publish/ClickAssistant.App.exe` that doesn't need .NET installed to run.

## Where the app stores data

Profiles and settings are stored outside the repository, in `%AppData%/ClickAssistant/` (each profile as
its own JSON file under `profiles/`, app settings in `settings.json`).

## Project structure and architecture

- `src/ClickAssistant.Core` - pure domain logic (engine, models, port interfaces), no dependency on
  Windows/UI.
- `src/ClickAssistant.Infrastructure` - port implementations on top of SharpHook (global input) and GDI
  (screenshots).
- `src/ClickAssistant.App` - Avalonia UI (MVVM).
- `tests/ClickAssistant.Core.Tests` - xUnit tests over `Core`.

A detailed description of the architecture, key classes, and non-obvious gotchas lives in
[`CLAUDE.md`](CLAUDE.md). `technicalExplanation.md` is additionally a Czech-language C#/.NET learning
document for someone coming from PHP.
