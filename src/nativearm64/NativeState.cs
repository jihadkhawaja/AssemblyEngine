using System.Runtime.InteropServices;

namespace AssemblyEngine.NativeArm64;

internal static unsafe class NativeContext
{
    public const int ArenaSize = 64 * 1024 * 1024;
    public const string WindowClassName = "AE_WindowClass";
    public const string DefaultTitle = "AssemblyEngine";
    public static readonly EngineState Engine = new();
}

internal unsafe sealed class EngineState
{
    public IntPtr Instance;
    public IntPtr Window;
    public IntPtr DeviceContext;
    public byte* Framebuffer;
    public int Width;
    public int Height;
    public int Stride;
    public bool Running;
    public bool VSyncEnabled = true;
    public WindowMode WindowMode = WindowMode.Windowed;
    public Win32.RECT RestoreWindowRect;
    public bool HasRestoreWindowRect;

    public readonly byte[] Keys = new byte[256];
    public readonly byte[] PrevKeys = new byte[256];
    public int MouseX;
    public int MouseY;
    public int MouseButtons;
    public int PrevMouseButtons;

    public long PerfFrequency;
    public long LastTick;
    public long CurrentTick;
    public float DeltaTime;
    public int Fps;
    public int FrameCount;
    public float FpsAccumulator;

    public readonly List<SpriteAsset> Sprites = [];
    public readonly List<SoundAsset> Sounds = [];

    public byte* ArenaBase;
    public nuint ArenaOffset;
    public nuint ArenaSize;

    public Win32.BITMAPINFO BitmapInfo;
    public IntPtr ClassNamePtr;

    public void EnsureClassName()
    {
        if (ClassNamePtr == IntPtr.Zero)
            ClassNamePtr = Marshal.StringToHGlobalUni(NativeContext.WindowClassName);
    }

    public void EnsureArena()
    {
        if (ArenaBase is not null)
            return;

        ArenaBase = (byte*)NativeMemory.AllocZeroed((nuint)NativeContext.ArenaSize);
        ArenaOffset = 0;
        ArenaSize = ArenaBase is null ? 0u : (nuint)NativeContext.ArenaSize;
    }

    public void ReleaseArena()
    {
        if (ArenaBase is null)
            return;

        NativeMemory.Free(ArenaBase);
        ArenaBase = null;
        ArenaOffset = 0;
        ArenaSize = 0;
    }

    public void ResetFrameInput()
    {
        Array.Copy(Keys, PrevKeys, Keys.Length);
        PrevMouseButtons = MouseButtons;
    }

    public void ClearAssets()
    {
        Sprites.Clear();

        foreach (var sound in Sounds)
            sound.Dispose();

        Sounds.Clear();
    }
}

internal sealed class SpriteAsset
{
    public required byte[] Pixels { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Pitch { get; init; }
    public required bool HasAlpha { get; init; }
}

internal sealed class SoundAsset
{
    public required byte[] Data { get; init; }
    public required GCHandle DataHandle { get; init; }

    public IntPtr Pointer => DataHandle.AddrOfPinnedObject();

    public void Dispose()
    {
        if (DataHandle.IsAllocated)
            DataHandle.Free();
    }
}