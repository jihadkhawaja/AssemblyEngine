# Getting Started

This guide walks through the current Windows x64 and Windows ARM64 setup for AssemblyEngine.

## Supported Environment

- Windows 10 or Windows 11 x64
- Windows 11 ARM64
- PowerShell 5.1 or later
- .NET 10 SDK and runtime
- NASM for x64 backend builds
- Visual Studio 2026 or Build Tools with the Desktop development with C++ workload

If you are on Windows ARM64 and also want the compatibility x64 build, install the x64 .NET runtime or SDK under `%ProgramFiles%\dotnet\x64`.

AssemblyEngine currently links the native core with `link.exe`, so the MSVC toolchain is required even if you primarily work from VS Code.

## 1. Clone the Repository

```powershell
git clone https://github.com/jihadkhawaja/AssemblyEngine.git
cd AssemblyEngine
```

If you are working from a local checkout already, move on to the setup step.

## 2. Bootstrap Your Toolchain

Run the repository setup script:

```powershell
.\setup.ps1
```

The script installs any missing prerequisites and then restores the managed solution dependencies. It covers:

- the .NET 10 SDK
- NASM for the x64 assembly backend
- Visual Studio Build Tools or Visual Studio with the required C++ workloads and linker targets
- the x64 .NET runtime on Windows ARM64 so the compatibility win-x64 sample can run under emulation
- a `dotnet restore` pass for the solution

If you only want to audit the machine state without changing it, run:

```powershell
.\setup.ps1 -CheckOnly
```

If you want to skip the restore step, run:

```powershell
.\setup.ps1 -SkipRestore
```

## 3. Build the Engine and Sample Game

To build everything in one pass:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1
```

That script builds the native core for the selected target architecture, then builds the managed runtime and sample game. On Windows ARM64, the default target is native `arm64`.

To build the x64 backend explicitly:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1 -TargetArchitecture x64
```

To publish the visual novel sample instead of Dash Harvest:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1 -Sample visual-novel
```

To publish the 3D FPS sample instead:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1 -Sample fps
```

To publish the RTS sample instead:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1 -Sample rts
```

To publish self-contained bundles for every sample into isolated folders:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\publish_samples.ps1 -TargetArchitecture x64
```

To publish the same bundle layout for Windows ARM64:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\publish_samples.ps1 -TargetArchitecture arm64
```

Those outputs land in `build/sample-publish/<architecture>` and contain runnable sample binaries, not source or standalone engine SDK artifacts.

To build the native ARM64 backend explicitly:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1 -TargetArchitecture arm64
```

The expected runnable output lands in `build/output`.

## 4. Run the Sample

```powershell
.\build\output\SampleGame.exe
```

Or, after using `-Sample visual-novel`:

```powershell
.\build\output\VisualNovelSample.exe
```

Or, after using `-Sample fps`:

```powershell
.\build\output\FpsSample.exe
```

Or, after using `-Sample rts`:

```powershell
.\build\output\RtsSample.exe
```

On Windows ARM64, the default build is native `win-arm64`. If you built `-TargetArchitecture x64`, Windows runs the sample through the x64 emulation layer instead.

The default sample is Dash Harvest, a small arcade loop used to exercise scenes, scripts, collision, the HTML/CSS HUD, and generated 8-bit SFX. The repository also includes Citadel Breach in `sample/fps`, a 3D FPS arena sample built from cubes, camera control, hitscan shooting, and a HUD overlay, Frontier Foundry in `sample/rts`, a top-down RTS sample with harvesting, production queues, rally points, and escalating raids, plus Lantern Letters in `sample/visual-novel`, a dialogue-driven sample with generated character sprites, parallax backgrounds, save/load, skip mode, and its own 8-bit SFX set.

Controls:

- `WASD` or arrow keys move
- `Space` dashes
- `R` or `Enter` restarts
- `F1` opens the display settings panel

Lantern Letters controls:

- `Space`, `Enter`, or `Right Arrow` advance dialogue
- `Tab` toggles skip mode
- Hold `Shift` or `Control` to fast reveal the current line
- `F5` saves and `F9` loads the current dialogue state
- `Home` restarts the chapter

Citadel Breach controls:

- `WASD` move
- Mouse or `Left` and `Right` arrows look
- Left mouse or `Space` fires
- Hold `Shift` to sprint
- `F1` toggles the help panel
- `R` or `Enter` restarts after mission clear or failure

Frontier Foundry controls:

- Left drag selects units; hold Shift to add or Ctrl to remove
- Right click issues move or harvest orders
- Right click with no selection moves the HQ rally point
- Left click the minimap instantly recenters the camera
- Middle click snaps the camera to the cursor position
- `Q` queues a worker, `E` queues a guard, and `R` or `T` starts structure placement so you can click a highlighted pad for a structure or defense tower
- `1`, `2`, and `3` select workers, guards, or all units, and `Space` focuses the current selection or HQ
- Arrow keys or moving the cursor to the screen edge pans the camera
- `F1` toggles the command brief and `R` or `Enter` restarts after victory or defeat

The sample stores its display preferences in `sample-settings.json` next to the executable. `Window mode`, `Resolution`, `VSync`, and `UI scale` all apply from the in-game settings panel, and maximize or restore events resize the engine surface dynamically.

## 5. Iterate During Development

You have two common loops:

### Full build loop

Use this when you touched both assembly and managed code:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1
```

### IDE or project build loop

Use this when working from Visual Studio, `dotnet build`, or VS Code task runners:

```powershell
dotnet build .\sample\basic\SampleGame.csproj -c Release
```

Or for the visual novel sample:

```powershell
dotnet build .\sample\visual-novel\VisualNovelSample.csproj -c Release
```

Or for the FPS sample:

```powershell
dotnet build .\sample\fps\FpsSample.csproj -c Release
```

Or for the RTS sample:

```powershell
dotnet build .\sample\rts\RtsSample.csproj -c Release
```

On Windows, both sample projects invoke `shell/build_core.ps1` before the managed build, so the native DLL is rebuilt automatically. Use the `ARM64` platform in the solution or pass `-p:Platform=ARM64` on the command line to target the native ARM64 backend.

## 6. Open the Workspace

You can work with either of these entry points:

- `AssemblyEngine.code-workspace` for VS Code
- `AssemblyEngine.slnx` for solution-oriented tooling

## Project Layout Cheat Sheet

| Path | Description |
| --- | --- |
| `src/core` | NASM native engine modules |
| `src/nativearm64` | NativeAOT ARM64 backend |
| `src/runtime` | Managed runtime, interop, scenes, scripts, and UI |
| `sample/basic` | Sample game project |
| `sample/fps` | 3D FPS sample project |
| `sample/rts` | Top-down RTS sample project |
| `sample/visual-novel` | Visual novel sample project |
| `shell` | Build and setup automation |
| `docs` | Project documentation |

## Troubleshooting

### `nasm` is not found

Run `.\setup.ps1` again so it can install NASM through `winget`, or install NASM manually and ensure it is discoverable on `PATH`.

### `link.exe` or the MSVC toolchain is not found

Run `.\setup.ps1` again so it can install or modify Visual Studio Build Tools with the required C++ workloads and linker targets. The build scripts use `vswhere.exe` to locate the linker for the selected target, including `HostARM64\arm64\link.exe` for native ARM64 builds and `HostARM64\x64\link.exe` for x64 compatibility builds.

### `assemblycore.dll` is missing at runtime

Build through `shell/build.ps1` or `SampleGame.csproj` so the native DLL is produced and copied into the output folder.

### The ARM64 sample does not start on Windows

Rebuild with `-TargetArchitecture arm64` so the output includes the native ARM64 `assemblycore.dll`. If you are intentionally using the x64 compatibility build, rerun `.\setup.ps1` so it can install the x64 .NET runtime under `%ProgramFiles%\dotnet\x64`.

### The UI does not appear

Make sure the `ui` folder is present next to the built executable. The sample build copies the selected sample's `ui` folder into `build/output/ui`.

## Next Steps

- Read [architecture.md](architecture.md) for a deeper view of the current engine shape.
- Read [implementation-guide.md](implementation-guide.md) before adding new engine features.
- Read [project-goals.md](project-goals.md) for the current direction and roadmap.