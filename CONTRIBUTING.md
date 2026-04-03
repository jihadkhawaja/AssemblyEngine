# Contributing to AssemblyEngine

AssemblyEngine is intentionally small and readable. Contributions should preserve that quality bar while making the engine more capable.

## Before You Start

- Work on Windows x64 for now. That is the only supported runtime target today.
- Run the setup check before your first build:

```powershell
.\setup.ps1
```

- Build the full stack at least once before opening a pull request:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1
```

## Contribution Workflow

1. Keep each pull request focused on one feature, fix, or documentation update.
2. Update documentation when you change public APIs, engine structure, or build/setup steps.
3. Avoid unrelated reformatting while touching a file.
4. Build the sample game after native or runtime changes.
5. Add tests for managed code where practical.

## Code Expectations

- Keep code files under 400 lines when possible.
- Preserve clear module boundaries: one subsystem per assembly file, one primary class per C# file.
- Prefer small, root-cause fixes over broad changes.
- Keep naming consistent with the current codebase:
  - Native exports use the `ae_` prefix.
  - Managed wrappers stay in the appropriate `Core`, `Engine`, `Scripting`, or `UI` namespace.
- If you add a native function, wire it through all required layers instead of leaving partial work behind.

## Documentation Expectations

- Update `README.md` for changes that affect the project pitch, quick start, or public capabilities.
- Update files in `docs/` for architecture, extension patterns, or roadmap changes.
- Refresh Mermaid diagrams when architectural flow changes materially.

## Pull Request Checklist

- The project still builds on Windows x64.
- The sample game still runs.
- Documentation has been updated for any user-facing or contributor-facing change.
- New features include at least one usage example in code or documentation.
- Licensing and attribution remain intact.

## Good First Areas

- Add managed wrappers for existing native exports
- Improve sprite, audio, or math documentation
- Expand the HTML/CSS UI subset
- Add sample scenes or focused demos
- Prepare abstractions needed for future platform layers

## Questions to Ask Before Adding a Feature

- Does the feature belong in the native core or the managed runtime?
- Does it fit the current Windows x64-first milestone?
- Does it make the engine easier to understand and extend?
- Does it need a sample or documentation update to stay discoverable?

For technical extension patterns, see [docs/implementation-guide.md](docs/implementation-guide.md).