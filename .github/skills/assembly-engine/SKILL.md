---
name: assembly-engine
description: 'AssemblyEngine 2D game engine knowledge: x86-64 NASM native core, C# scripting, HTML/CSS UI. Use for: adding native functions, creating components, building scenes, configuring UI, build pipeline, debugging render/input/audio, adding P/Invoke bindings, extending the ECS.'
---

# AssemblyEngine - Skill Reference

## Overview
AssemblyEngine is a 2D game engine with an x86-64 assembly native core (NASM), a C# (.NET 10) scripting runtime, and an HTML/CSS UI overlay system. It targets Windows x64.

## Architecture Layers

### 1. Native Core (`src/core/`)
NASM x86-64 assembly compiled to `assemblycore.dll`. Uses Win64 calling convention throughout.

**Key files:**
- `platform_win64.asm` — Window creation, message loop, engine lifecycle
- `renderer.asm` — Framebuffer clear, pixel, rect, line, circle drawing
- `sprite.asm` — BMP sprite loading and rendering with alpha blending
- `input.asm` — Keyboard/mouse state queries
- `timer.asm` — High-precision timing via QueryPerformanceCounter
- `memory.asm` — Arena allocator (64MB via VirtualAlloc)
- `audio.asm` — WAV file loading and PlaySound playback
- `math.asm` — clamp, lerp, min, max, abs, sqrt, distance

**Include files (`src/core/include/`):**
- `constants.inc` — All defines, macros, Win32 constants
- `engine_state.inc` — Shared EngineState struct used by all modules
- `win64api.inc` — Win32 API extern declarations

**Exported API pattern:** All functions prefixed `ae_` with C ABI.

### 2. C# Runtime (`src/runtime/`)
.NET 10 class library `AssemblyEngine.Runtime.dll`.

**Interop** (`Interop/NativeCore.cs`):
- P/Invoke via `LibraryImport` source generator to `assemblycore.dll`

**Core** (`Core/`):
- `Primitives.cs` — Color, Vector2, Rectangle value types
- `Graphics.cs` — Static graphics wrapper over native renderer
- `InputSystem.cs` — Static input queries
- `Audio.cs` — Static audio wrapper
- `Time.cs` — DeltaTime, FPS access
- `Input.cs` — KeyCode and MouseButton enums

**Engine** (`Engine/`):
- `GameEngine.cs` — Main engine class: init, game loop, shutdown
- `Entity.cs` — Game entity with component list
- `Component.cs` — Base component class
- `Scene.cs` — Entity container with lifecycle
- `SceneManager.cs` — Scene transitions
- `BuiltInComponents.cs` — SpriteComponent, BoxCollider, VelocityComponent

**Scripting** (`Scripting/`):
- `GameScript.cs` — Base class for user game logic
- `ScriptManager.cs` — Discovers and manages script instances

**UI** (`UI/`):
- `HtmlParser.cs` — Minimal HTML parser → UIElement tree
- `CssParser.cs` — CSS parser → selector → UIStyle map
- `UIDocument.cs` — Combined HTML+CSS document with style cascade
- `UIElement.cs` — DOM node (tag, id, class, children, computed style)
- `UIStyle.cs` — CSS properties (position, box model, color, flex)
- `UILayoutEngine.cs` — Block and flex layout computation
- `UIRenderer.cs` — Renders UI tree via engine graphics primitives

### 3. Game Projects (`sample/`)
.NET 10 console app referencing `AssemblyEngine.Runtime`.
Users subclass `GameScript` and `Scene` to build games.

## Build Process
1. NASM assembles each `.asm` to `.obj` (win64 format)
2. MSVC `link.exe` links `.obj` files into `assemblycore.dll`
3. `dotnet build` compiles C# runtime and game
4. `build.bat` orchestrates the full pipeline

## Conventions
- Assembly files: NASM Intel syntax, one module per subsystem
- C# files: max 400 lines, one primary class per file
- Native functions: `ae_` prefix, Win64 calling convention (rcx, rdx, r8, r9)
- Engine state: all modules access `g_engine` global struct
- UI: vanilla HTML/CSS files parsed at runtime, no JavaScript
- Input keycodes: Windows VK_ constants

## Common Task Patterns

### Adding a new native function:
1. Add implementation in relevant `.asm` file
2. Add `global` declaration and export in `exports.def`
3. Add P/Invoke in `NativeCore.cs`
4. Add C# wrapper in appropriate `Core/` static class

### Adding a new component:
1. Create class extending `Component` in `Engine/`
2. Override `Update()` and/or `Draw()`
3. Attach via `entity.AddComponent<T>()`

### Adding a new scene:
1. Create class extending `Scene`
2. Override `OnLoad()` to create entities
3. Register with `engine.Scenes.Register("name", scene)`

### Adding UI elements:
1. Edit HTML file to add elements with IDs
2. Edit CSS file to position/style them
3. Use `engine.UI.UpdateText("id", "value")` in scripts
