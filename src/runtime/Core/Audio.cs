using System.Runtime.InteropServices;

namespace AssemblyEngine.Core;

/// <summary>
/// High-level audio API using managed WAV playback.
/// </summary>
public static partial class Audio
{
    private static readonly Dictionary<int, ManagedSoundBuffer> ManagedSounds = [];
    private static readonly Lock ManagedSoundLock = new();
    private static int _nextId;

    public static int LoadSound(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
            return -1;

        var data = File.ReadAllBytes(path);
        if (data.Length == 0)
            return -1;

        lock (ManagedSoundLock)
        {
            var id = _nextId++;
            ManagedSounds[id] = new ManagedSoundBuffer(data);
            return id;
        }
    }

    public static bool PlaySound(int id)
    {
        ManagedSoundBuffer? sound;
        lock (ManagedSoundLock)
            ManagedSounds.TryGetValue(id, out sound);

        if (sound is null)
            return false;

        return ManagedAudioPlayer.Play(sound.Pointer);
    }

    public static void StopAll()
    {
        ManagedAudioPlayer.Stop();
    }

    private static partial class ManagedAudioPlayer
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
