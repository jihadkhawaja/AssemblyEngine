# Getting Started

This guide walks through the current Windows x64 setup for AssemblyEngine and gets the sample game running locally.

## Supported Environment

- Windows 10 or Windows 11 x64
- PowerShell 5.1 or later
- .NET 10 SDK and runtime
- NASM
- Visual Studio 2022 or Build Tools with the Desktop development with C++ workload

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
- `nasm` is available
- `vswhere.exe` can locate a Visual Studio installation with the C++ x64 toolchain
- `link.exe` is present

If any requirement is missing, the script prints the missing dependency and exits with a non-zero status.

## 3. Build the Engine and Sample Game

To build everything in one pass:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1
```

That script performs four stages:

1. Assemble each NASM module in `src/core`
2. Link `assemblycore.dll`
3. Build `AssemblyEngine.Runtime`
4. Build the sample game and copy the UI assets into `build/output/ui`

The expected runnable output lands in `build/output`.

## 4. Run the Sample

```powershell
.\build\output\SampleGame.exe
```

The current sample is Dash Harvest, a small arcade loop used to exercise scenes, scripts, collision, and the HTML/CSS HUD.

Controls:

- `WASD` or arrow keys move
- `Space` dashes
- `R` or `Enter` restarts after game over

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

On Windows, `SampleGame.csproj` invokes `shell/build_core.ps1` before the managed build, so the native DLL is rebuilt automatically.

## 6. Open the Workspace

You can work with either of these entry points:

- `AssemblyEngine.code-workspace` for VS Code
- `AssemblyEngine.slnx` for solution-oriented tooling

## Project Layout Cheat Sheet

| Path | Description |
| --- | --- |
| `src/core` | NASM native engine modules |
| `src/runtime` | Managed runtime, interop, scenes, scripts, and UI |
| `sample/basic` | Sample game project |
| `shell` | Build and setup automation |
| `docs` | Project documentation |

## Troubleshooting

### `nasm` is not found

Install NASM and either add it to `PATH` or place it in one of the locations already checked by the scripts.

### `link.exe` or the MSVC toolchain is not found

Install Visual Studio 2022 or Visual Studio Build Tools with the Desktop development with C++ workload. The build scripts use `vswhere.exe` to locate the x64 linker.

### `assemblycore.dll` is missing at runtime

Build through `shell/build.ps1` or `SampleGame.csproj` so the native DLL is produced and copied into the output folder.

### The UI does not appear

Make sure the `ui` folder is present next to the built executable. The sample build copies `sample/basic/ui` into `build/output/ui`.

## Next Steps

- Read [architecture.md](architecture.md) for a deeper view of the current engine shape.
- Read [implementation-guide.md](implementation-guide.md) before adding new engine features.
- Read [project-goals.md](project-goals.md) for the current direction and roadmap.