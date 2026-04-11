---
name: assembly-engine
description: 'AssemblyEngine game engine knowledge: managed C# with Silk.NET windowing/input, Vulkan rendering, C# scripting, HTML/CSS UI. Use for: creating components, building scenes, configuring UI, build pipeline, debugging render/input/audio, extending the ECS.'
---

# AssemblyEngine - Skill Reference

## Overview
AssemblyEngine is a game engine built entirely in managed C# (.NET 10) with Silk.NET for cross-platform windowing and input, Vulkan rendering support, and an HTML/CSS UI overlay system. It targets Windows x64 and Windows ARM64.

## Architecture Layers

### 1. Platform Host (`src/runtime/Platform/EngineHost.cs`)
Manages the Silk.NET window, input context, and frame timing. Provides:
- Window creation and lifecycle via `Silk.NET.Windowing`
- Keyboard and mouse input via `Silk.NET.Input`
- Frame timing via `System.Diagnostics.Stopwatch`
- Window mode management (windowed, fullscreen, borderless)
- Input injection for MCP diagnostics

### 2. C# Runtime (`src/runtime/`)
.NET 10 class library `AssemblyEngine.Runtime.dll`.

**Platform** (`Platform/`):
- `EngineHost.cs` — Silk.NET window, input, and timing management

**Core** (`Core/`):
- `Primitives.cs` — Color, Vector2, Rectangle value types
- `Graphics.cs` — Static graphics wrapper for the unified renderer
- `InputSystem.cs` — Static input queries (delegates to EngineHost)
- `Audio.cs` — Static audio wrapper (managed winmm P/Invoke)
- `Time.cs` — DeltaTime, FPS access (delegates to EngineHost)
- `Input.cs` — KeyCode and MouseButton enums

**Engine** (`Engine/`):
- `GameEngine.cs` — Main engine class: init, game loop, shutdown
- `Entity.cs` — Game entity with component list
- `Component.cs` — Base component class
- `Scene.cs` — Entity container with lifecycle
- `SceneManager.cs` — Scene transitions
- `BuiltInComponents.cs` — SpriteComponent, BoxCollider, VelocityComponent

**Rendering** (`Rendering/`):
- `UnifiedRenderer.cs` — Unified 2D/3D managed render surface
- `VulkanPresenter.cs` — Vulkan swapchain presentation
- `SoftwareWindowPresenter.cs` — GDI fallback presentation

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
1. `dotnet build` compiles C# runtime
2. `dotnet publish` builds sample game as self-contained
3. `shell/build.ps1` orchestrates the full pipeline

## Conventions
- C# files: max 400 lines, one primary class per file
- Engine state managed through `EngineHost` static class
- UI: vanilla HTML/CSS files parsed at runtime, no JavaScript
- Input keycodes: engine `KeyCode` enum mapped to Silk.NET `Key` enum

## Common Task Patterns

### Adding a new engine feature:
1. Add implementation in the appropriate runtime area
2. Add a high-level C# wrapper in the matching `Core/` static class
3. Update documentation

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
