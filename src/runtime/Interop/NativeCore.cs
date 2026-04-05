using AssemblyEngine.Platform;
using System.Runtime.InteropServices;

namespace AssemblyEngine.Interop;

/// <summary>
/// P/Invoke bindings to the native engine core entry points exported by assemblycore.
/// The managed platform layer is responsible for any OS-specific translation above this boundary.
/// </summary>
internal static partial class NativeCore
{
    private const string DllName = "assemblycore";

    static NativeCore()
    {
        NativeLibraryBootstrap.EnsureInitialized(typeof(NativeCore).Assembly);
    }

    // --- Platform ---
    [LibraryImport(DllName, EntryPoint = "ae_init", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Init(int width, int height, string? title);

    [LibraryImport(DllName, EntryPoint = "ae_shutdown")]
    internal static partial void Shutdown();

    [LibraryImport(DllName, EntryPoint = "ae_poll_events")]
    internal static partial int PollEvents();

    [LibraryImport(DllName, EntryPoint = "ae_present")]
    internal static partial void Present();

    [LibraryImport(DllName, EntryPoint = "ae_set_vsync_enabled")]
    internal static partial void SetVSyncEnabled(int enabled);

    [LibraryImport(DllName, EntryPoint = "ae_resize_window")]
    internal static partial int ResizeWindow(int width, int height);

    [LibraryImport(DllName, EntryPoint = "ae_set_window_mode")]
    internal static partial int SetWindowMode(int windowMode);

    [LibraryImport(DllName, EntryPoint = "ae_get_window_mode")]
    internal static partial int GetWindowMode();

    [LibraryImport(DllName, EntryPoint = "ae_get_window_width")]
    internal static partial int GetWindowWidth();

    [LibraryImport(DllName, EntryPoint = "ae_get_window_height")]
    internal static partial int GetWindowHeight();

    [LibraryImport(DllName, EntryPoint = "ae_get_window_handle")]
    internal static partial nint GetWindowHandle();

    // --- Renderer ---
    [LibraryImport(DllName, EntryPoint = "ae_clear")]
    internal static partial void Clear(int r, int g, int b, int a);

    [LibraryImport(DllName, EntryPoint = "ae_draw_pixel")]
    internal static partial void DrawPixel(int x, int y, int r, int g, int b, int a);

    [LibraryImport(DllName, EntryPoint = "ae_draw_rect")]
    internal static partial void DrawRect(int x, int y, int w, int h, int r, int g, int b, int a);

    [LibraryImport(DllName, EntryPoint = "ae_draw_filled_rect")]
    internal static partial void DrawFilledRect(int x, int y, int w, int h, int r, int g, int b, int a);

    [LibraryImport(DllName, EntryPoint = "ae_draw_line")]
    internal static partial void DrawLine(int x1, int y1, int x2, int y2, int r, int g, int b, int a);

    [LibraryImport(DllName, EntryPoint = "ae_draw_circle")]
    internal static partial void DrawCircle(int cx, int cy, int radius, int r, int g, int b, int a);

    [LibraryImport(DllName, EntryPoint = "ae_copy_framebuffer")]
    internal static unsafe partial int CopyFramebuffer(byte* destination, int destinationLength);

    [LibraryImport(DllName, EntryPoint = "ae_upload_framebuffer")]
    internal static unsafe partial int UploadFramebuffer(byte* source, int sourceLength);

    // --- Sprites ---
    [LibraryImport(DllName, EntryPoint = "ae_load_sprite", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int LoadSprite(string path);

    [LibraryImport(DllName, EntryPoint = "ae_draw_sprite")]
    internal static partial void DrawSprite(int id, int x, int y, int flags);

    // --- Input ---
    [LibraryImport(DllName, EntryPoint = "ae_is_key_down")]
    internal static partial int IsKeyDown(int keycode);

    [LibraryImport(DllName, EntryPoint = "ae_is_key_pressed")]
    internal static partial int IsKeyPressed(int keycode);

    [LibraryImport(DllName, EntryPoint = "ae_get_mouse_x")]
    internal static partial int GetMouseX();

    [LibraryImport(DllName, EntryPoint = "ae_get_mouse_y")]
    internal static partial int GetMouseY();

    [LibraryImport(DllName, EntryPoint = "ae_is_mouse_down")]
    internal static partial int IsMouseDown(int button);

    [LibraryImport(DllName, EntryPoint = "ae_set_key_state")]
    internal static partial void SetKeyState(int keycode, int isDown);

    [LibraryImport(DllName, EntryPoint = "ae_set_mouse_position")]
    internal static partial void SetMousePosition(int x, int y);

    [LibraryImport(DllName, EntryPoint = "ae_set_mouse_button_state")]
    internal static partial void SetMouseButtonState(int button, int isDown);

    // --- Audio ---
    [LibraryImport(DllName, EntryPoint = "ae_load_sound", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int LoadSound(string path);

    [LibraryImport(DllName, EntryPoint = "ae_play_sound")]
    internal static partial int PlaySound(int id);

    [LibraryImport(DllName, EntryPoint = "ae_stop_sound")]
    internal static partial void StopSound();

    // --- Timer ---
    [LibraryImport(DllName, EntryPoint = "ae_get_delta_time")]
    internal static partial int GetDeltaTimeBits();

    [LibraryImport(DllName, EntryPoint = "ae_get_fps")]
    internal static partial int GetFps();

    [LibraryImport(DllName, EntryPoint = "ae_get_ticks")]
    internal static partial long GetTicks();

    // --- Memory ---
    [LibraryImport(DllName, EntryPoint = "ae_alloc")]
    internal static partial nint Alloc(int size);

    [LibraryImport(DllName, EntryPoint = "ae_free")]
    internal static partial void Free();

    // --- Math ---
    [LibraryImport(DllName, EntryPoint = "ae_math_clamp")]
    internal static partial int MathClamp(int value, int min, int max);

    [LibraryImport(DllName, EntryPoint = "ae_math_dist_sq")]
    internal static partial int MathDistSq(int x1, int y1, int x2, int y2);
}
