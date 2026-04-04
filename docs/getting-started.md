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

If you are working from a local checkout already, move on to the setup validation step.

## 2. Validate Your Toolchain

Run the repository setup check:

```powershell
.\setup.ps1
```

The script verifies:

- `dotnet` is available
- a .NET 10 SDK is installed
- `nasm` is available when you target the x64 backend
- `vswhere.exe` can locate a Visual Studio installation with the required native target linker
- `link.exe` is present for the selected target architecture

If any requirement is missing, the script prints the missing dependency and exits with a non-zero status.

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

On Windows ARM64, the default build is native `win-arm64`. If you built `-TargetArchitecture x64`, Windows runs the sample through the x64 emulation layer instead.

The default sample is Dash Harvest, a small arcade loop used to exercise scenes, scripts, collision, the HTML/CSS HUD, and generated 8-bit SFX. The repository also includes Lantern Letters in `sample/visual-novel`, a dialogue-driven sample with generated character sprites, parallax backgrounds, save/load, skip mode, and its own 8-bit SFX set.

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
| `sample/visual-novel` | Visual novel sample project |
| `shell` | Build and setup automation |
| `docs` | Project documentation |

## Troubleshooting

### `nasm` is not found

Install NASM and either add it to `PATH` or place it in one of the locations already checked by the scripts if you want to build the x64 backend.

### `link.exe` or the MSVC toolchain is not found

Install Visual Studio 2026 or Visual Studio Build Tools with the Desktop development with C++ workload. The build scripts use `vswhere.exe` to locate the linker for the selected target, including `HostARM64\arm64\link.exe` for native ARM64 builds and `HostARM64\x64\link.exe` for x64 compatibility builds.

### `assemblycore.dll` is missing at runtime

Build through `shell/build.ps1` or `SampleGame.csproj` so the native DLL is produced and copied into the output folder.

### The ARM64 sample does not start on Windows

Rebuild with `-TargetArchitecture arm64` so the output includes the native ARM64 `assemblycore.dll`. If you are intentionally using the x64 compatibility build, install the x64 .NET runtime or SDK under `%ProgramFiles%\dotnet\x64`.

### The UI does not appear

Make sure the `ui` folder is present next to the built executable. The sample build copies `sample/basic/ui` into `build/output/ui`.

## Next Steps

- Read [architecture.md](architecture.md) for a deeper view of the current engine shape.
- Read [implementation-guide.md](implementation-guide.md) before adding new engine features.
- Read [project-goals.md](project-goals.md) for the current direction and roadmap.