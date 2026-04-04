using System.Runtime.InteropServices;

namespace AssemblyEngine.NativeArm64;

internal static unsafe partial class NativeExports
{
    [UnmanagedCallersOnly(EntryPoint = "ae_is_key_down")]
    public static int IsKeyDown(int keycode)
    {
        var state = NativeContext.Engine;
        return keycode is >= 0 and < 256 && state.Keys[keycode] != 0 ? 1 : 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_is_key_pressed")]
    public static int IsKeyPressed(int keycode)
    {
        var state = NativeContext.Engine;
        return keycode is >= 0 and < 256 && state.Keys[keycode] != 0 && state.PrevKeys[keycode] == 0 ? 1 : 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_get_mouse_x")]
    public static int GetMouseX() => NativeContext.Engine.MouseX;

    [UnmanagedCallersOnly(EntryPoint = "ae_get_mouse_y")]
    public static int GetMouseY() => NativeContext.Engine.MouseY;

    [UnmanagedCallersOnly(EntryPoint = "ae_is_mouse_down")]
    public static int IsMouseDown(int button)
    {
        var mask = button switch
        {
            0 => 1,
            1 => 2,
            2 => 4,
            _ => 0
        };

        return mask != 0 && (NativeContext.Engine.MouseButtons & mask) != 0 ? 1 : 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_set_key_state")]
    public static void SetKeyState(int keycode, int isDown)
    {
        if (keycode is < 0 or >= 256)
            return;

        NativeContext.Engine.Keys[keycode] = isDown != 0 ? (byte)1 : (byte)0;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_set_mouse_position")]
    public static void SetMousePosition(int x, int y)
    {
        NativeContext.Engine.MouseX = x;
        NativeContext.Engine.MouseY = y;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_set_mouse_button_state")]
    public static void SetMouseButtonState(int button, int isDown)
    {
        var mask = button switch
        {
            0 => 1,
            1 => 2,
            2 => 4,
            _ => 0
        };

        if (mask == 0)
            return;

        if (isDown != 0)
        {
            NativeContext.Engine.MouseButtons |= mask;
            return;
        }

        NativeContext.Engine.MouseButtons &= ~mask;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_get_delta_time")]
    public static int GetDeltaTimeBits() => BitConverter.SingleToInt32Bits(NativeContext.Engine.DeltaTime);

    [UnmanagedCallersOnly(EntryPoint = "ae_get_fps")]
    public static int GetFps() => NativeContext.Engine.Fps;

    [UnmanagedCallersOnly(EntryPoint = "ae_get_ticks")]
    public static long GetTicks() => NativeContext.Engine.CurrentTick;

    [UnmanagedCallersOnly(EntryPoint = "ae_alloc")]
    public static nint Alloc(int size)
    {
        var state = NativeContext.Engine;
        if (size <= 0)
            return 0;

        state.EnsureArena();
        if (state.ArenaBase is null)
            return 0;

        var alignedSize = (nuint)((size + 15) & ~15);
        var nextOffset = state.ArenaOffset + alignedSize;
        if (nextOffset > state.ArenaSize)
            return 0;

        var result = state.ArenaBase + state.ArenaOffset;
        state.ArenaOffset = nextOffset;
        return (nint)result;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_free")]
    public static void Free()
    {
        NativeContext.Engine.ArenaOffset = 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_math_clamp")]
    public static int MathClamp(int value, int min, int max)
    {
        if (value < min)
            return min;

        return value > max ? max : value;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_math_lerp")]
    public static float MathLerp(float a, float b, float t)
    {
        return a + ((b - a) * t);
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_math_min")]
    public static int MathMin(int a, int b) => a < b ? a : b;

    [UnmanagedCallersOnly(EntryPoint = "ae_math_max")]
    public static int MathMax(int a, int b) => a > b ? a : b;

    [UnmanagedCallersOnly(EntryPoint = "ae_math_abs")]
    public static int MathAbs(int value)
    {
        return value == int.MinValue ? int.MinValue : Math.Abs(value);
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_math_sqrt_int")]
    public static int MathSqrtInt(int value)
    {
        return value <= 0 ? 0 : (int)MathF.Sqrt(value);
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_math_dist_sq")]
    public static int MathDistSq(int x1, int y1, int x2, int y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return (dx * dx) + (dy * dy);
    }
}