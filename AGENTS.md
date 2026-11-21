# breakwindow

A Windows break reminder: C# with NativeAOT and plain Win32 through CsWin32, no UI framework.

## Layout

- `src/BreakWindow.Core`: schedule, clock in and out, work-time counting and break state. Plain C#,
  no Win32 calls; the clock and idle source are injected.
- `src/BreakWindow`: the exe. Message loop, tray icon, reminder and break windows (Direct2D,
  DirectWrite, DWM), settings file, autostart.
- `tests/BreakWindow.Core.Tests`: xUnit tests for the core.

## Commands

- Build: `dotnet build breakwindow.slnx`
- Test: `dotnet test --solution breakwindow.slnx` (Microsoft.Testing.Platform, set in `global.json`)
- Publish: `dotnet publish src/BreakWindow/BreakWindow.csproj -c Release -r win-x64`, which writes
  `src/BreakWindow/bin/Release/net10.0-windows/win-x64/publish/breakwindow.exe`
- Idle memory: `(Get-Process breakwindow).PrivateMemorySize64`, target under 10 MB.

NativeAOT publish needs the Visual Studio C++ build tools. If it fails with
`'vswhere.exe' is not recognized`, put `C:\Program Files (x86)\Microsoft Visual Studio\Installer`
on PATH and pass `-nodeReuse:false`, because a reused MSBuild node keeps its old environment.

`nuget.config` pins nuget.org as the only package source.

## Rules

- AOT and trim warnings are errors. No reflection: System.Text.Json runs on source generation.
- Win32 APIs come from CsWin32: add the name to `src/BreakWindow/NativeMethods.txt`. Marshaling is
  off, so COM interfaces are generated as structs called through pointers.
- One UI thread, one message loop: no FileSystemWatcher, Task.Run or System.Threading timers.
