using AssemblyEngine.Interop;
using System.Runtime.InteropServices;

namespace AssemblyEngine.Core;

/// <summary>
/// High-level audio API wrapping the native assembly audio system.
/// </summary>
public static partial class Audio
{
    private const int ManagedSoundIdBase = 1_000_000;
    private static readonly Dictionary<int, ManagedSoundBuffer> ManagedSounds = [];
    private static readonly Lock ManagedSoundLock = new();

    public static int LoadSound(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var nativeId = NativeCore.LoadSound(path);
        if (nativeId >= 0)
            return nativeId;

        if (!File.Exists(path))
            return -1;

        var data = File.ReadAllBytes(path);
        if (data.Length == 0)
            return -1;

        lock (ManagedSoundLock)
        {
            var managedId = ManagedSoundIdBase + ManagedSounds.Count;
            ManagedSounds[managedId] = new ManagedSoundBuffer(data);
            return managedId;
        }
    }

    public static bool PlaySound(int id)
    {
        ManagedSoundBuffer? managedSound;
        lock (ManagedSoundLock)
            ManagedSounds.TryGetValue(id, out managedSound);

        if (managedSound is not null)
            return ManagedAudioFallback.Play(managedSound.Pointer);

        return NativeCore.PlaySound(id) != 0;
    }

    public static void StopAll()
    {
        NativeCore.StopSound();
        ManagedAudioFallback.Stop();
    }

    private static partial class ManagedAudioFallback
    {
        private const uint SndAsync = 0x0001;
        private const uint SndNodefault = 0x0002;
        private const uint SndMemory = 0x0004;

        public static bool Play(IntPtr soundData)
        {
            return PlaySoundMemory(soundData, IntPtr.Zero, SndMemory | SndAsync | SndNodefault) != 0;
        }

        public static void Stop()
        {
            PlaySoundMemory(IntPtr.Zero, IntPtr.Zero, 0);
        }

        [LibraryImport("winmm", EntryPoint = "PlaySoundA")]
        private static partial int PlaySoundMemory(IntPtr soundName, IntPtr module, uint flags);
    }

    private sealed class ManagedSoundBuffer : IDisposable
    {
        private readonly GCHandle _handle;

        public ManagedSoundBuffer(byte[] data)
        {
            Data = data;
            _handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        }

        public byte[] Data { get; }
        public IntPtr Pointer => _handle.AddrOfPinnedObject();

        public void Dispose()
        {
            if (_handle.IsAllocated)
                _handle.Free();
        }
    }
}
