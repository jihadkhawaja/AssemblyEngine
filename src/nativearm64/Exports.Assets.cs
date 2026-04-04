using System.Runtime.InteropServices;

namespace AssemblyEngine.NativeArm64;

internal static unsafe partial class NativeExports
{
    [UnmanagedCallersOnly(EntryPoint = "ae_load_sprite")]
    public static int LoadSprite(byte* path)
    {
        var value = Utf8ToString(path);
        if (string.IsNullOrWhiteSpace(value) || !BmpLoader.TryLoad(value, out var sprite) || sprite is null)
            return -1;

        var state = NativeContext.Engine;
        state.Sprites.Add(sprite);
        return state.Sprites.Count - 1;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_draw_sprite")]
    public static void DrawSprite(int id, int x, int y, int flags)
    {
        var state = NativeContext.Engine;
        if ((uint)id >= (uint)state.Sprites.Count || state.Framebuffer is null)
            return;

        var sprite = state.Sprites[id];
        var alphaBlend = (flags & 1) != 0 && sprite.HasAlpha;

        fixed (byte* sourceBase = sprite.Pixels)
        {
            for (var row = 0; row < sprite.Height; row++)
            {
                var destY = y + row;
                if ((uint)destY >= (uint)state.Height)
                    continue;

                for (var column = 0; column < sprite.Width; column++)
                {
                    var destX = x + column;
                    if ((uint)destX >= (uint)state.Width)
                        continue;

                    var source = sourceBase + (row * sprite.Pitch) + (column * 4);
                    var destination = (uint*)(state.Framebuffer + (destY * state.Stride) + (destX * 4));

                    if (alphaBlend)
                    {
                        *destination = BlendPixel(*destination, source[0], source[1], source[2], source[3]);
                    }
                    else if (source[3] != 0)
                    {
                        *destination = (uint)(source[0] | (source[1] << 8) | (source[2] << 16) | (source[3] << 24));
                    }
                }
            }
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_load_sound")]
    public static int LoadSound(byte* path)
    {
        var value = Utf8ToString(path);
        if (string.IsNullOrWhiteSpace(value) || !File.Exists(value))
            return -1;

        var state = NativeContext.Engine;
        state.Sounds.Add(new SoundAsset { Path = value });
        return state.Sounds.Count - 1;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_play_sound")]
    public static int PlaySound(int id)
    {
        var state = NativeContext.Engine;
        if ((uint)id >= (uint)state.Sounds.Count)
            return 0;

        return Win32.PlaySound(state.Sounds[id].Path, IntPtr.Zero, Win32.SND_FILENAME | Win32.SND_ASYNC | Win32.SND_NODEFAULT) != 0 ? 1 : 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_stop_sound")]
    public static void StopSound()
    {
        StopSoundImpl();
    }

    private static void StopSoundImpl()
    {
        Win32.PlaySound(null, IntPtr.Zero, 0);
    }
}