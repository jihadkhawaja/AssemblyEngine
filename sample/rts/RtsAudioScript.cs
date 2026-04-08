using AssemblyEngine.Scripting;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Runtime.Versioning;

namespace RtsSample;

[SupportedOSPlatform("windows")]
internal sealed class RtsAudioScript : GameScript
{
    private readonly string _audioDir;
    private readonly Dictionary<string, CachedCue> _cues = [];
    private readonly Dictionary<string, float> _lastPlayedAt = [];
    private CueMixer? _mixer;
    private float _elapsed;

    public RtsAudioScript(string audioDir)
    {
        _audioDir = audioDir;
    }

    public override void OnLoad()
    {
        LoadCue("mission-start");
        LoadCue("move-order");
        LoadCue("harvest-order");
        LoadCue("rally");
        LoadCue("queue-worker");
        LoadCue("queue-guard");
        LoadCue("queue-denied");
        LoadCue("unit-ready");
        LoadCue("mine");
        LoadCue("deposit");
        LoadCue("guard-fire");
        LoadCue("under-attack");
        LoadCue("raid-alert");
        LoadCue("victory");
        LoadCue("defeat");
    }

    public override void OnUpdate(float deltaTime)
    {
        _elapsed += deltaTime;
    }

    public override void OnUnload()
    {
        _mixer?.Dispose();
        _mixer = null;
        _cues.Clear();
    }

    public void PlayMissionStart() => PlayCue("mission-start", 0.5f);

    public void PlayOrder(bool harvest)
    {
        PlayCue(harvest ? "harvest-order" : "move-order", 0.06f);
    }

    public void PlayRally() => PlayCue("rally", 0.1f);

    public void PlayQueue(RtsUnitRole role)
    {
        PlayCue(role == RtsUnitRole.Worker ? "queue-worker" : "queue-guard", 0.05f);
    }

    public void PlayDenied() => PlayCue("queue-denied", 0.12f);
    public void PlayUnitReady() => PlayCue("unit-ready", 0.12f);
    public void PlayMine() => PlayCue("mine", 0.12f);
    public void PlayDeposit() => PlayCue("deposit", 0.1f);
    public void PlayGuardFire() => PlayCue("guard-fire", 0.04f);
    public void PlayUnderAttack() => PlayCue("under-attack", 0.15f);
    public void PlayRaidAlert() => PlayCue("raid-alert", 0.35f);
    public void PlayVictory() => PlayCue("victory", 0.45f);
    public void PlayDefeat() => PlayCue("defeat", 0.45f);

    private void LoadCue(string name)
    {
        var path = Path.Combine(_audioDir, name + ".wav");
        if (!File.Exists(path))
            throw new InvalidOperationException($"Missing RTS sound '{name}' at '{path}'.");

        var cue = new CachedCue(path);
        _mixer ??= new CueMixer(cue.WaveFormat);

        if (!_mixer.Supports(cue.WaveFormat))
            throw new InvalidOperationException($"Sound '{name}' does not match the RTS mixer format.");

        _cues[name] = cue;
    }

    private void PlayCue(string name, float minInterval)
    {
        if (!_cues.TryGetValue(name, out var cue))
            return;

        if (_lastPlayedAt.TryGetValue(name, out var lastPlayedAt) && (_elapsed - lastPlayedAt) < minInterval)
            return;

        _mixer?.Play(cue);
        _lastPlayedAt[name] = _elapsed;
    }

    private sealed class CueMixer : IDisposable
    {
        private readonly MixingSampleProvider _mixer;
        private readonly WaveOutEvent _output;

        public CueMixer(WaveFormat waveFormat)
        {
            _mixer = new MixingSampleProvider(waveFormat)
            {
                ReadFully = true
            };

            _output = new WaveOutEvent
            {
                DesiredLatency = 60,
                NumberOfBuffers = 2
            };

            _output.Init(_mixer);
            _output.Play();
        }

        public bool Supports(WaveFormat waveFormat)
        {
            return _mixer.WaveFormat.SampleRate == waveFormat.SampleRate
                && _mixer.WaveFormat.Channels == waveFormat.Channels
                && _mixer.WaveFormat.Encoding == waveFormat.Encoding;
        }

        public void Play(CachedCue cue)
        {
            _mixer.AddMixerInput(new CachedCueSampleProvider(cue));
        }

        public void Dispose()
        {
            _output.Stop();
            _output.Dispose();
        }
    }

    private sealed class CachedCue
    {
        public CachedCue(string path)
        {
            using var reader = new WaveFileReader(path);
            var sampleProvider = reader.ToSampleProvider();
            WaveFormat = sampleProvider.WaveFormat;

            var samples = new List<float>();
            var buffer = new float[WaveFormat.SampleRate * WaveFormat.Channels];
            int samplesRead;
            while ((samplesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var index = 0; index < samplesRead; index++)
                    samples.Add(buffer[index]);
            }

            AudioData = samples.ToArray();
        }

        public WaveFormat WaveFormat { get; }

        public float[] AudioData { get; }
    }

    private sealed class CachedCueSampleProvider : ISampleProvider
    {
        private readonly CachedCue _cue;
        private int _position;

        public CachedCueSampleProvider(CachedCue cue)
        {
            _cue = cue;
        }

        public WaveFormat WaveFormat => _cue.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var availableSamples = _cue.AudioData.Length - _position;
            if (availableSamples <= 0)
                return 0;

            var samplesToCopy = Math.Min(availableSamples, count);
            Array.Copy(_cue.AudioData, _position, buffer, offset, samplesToCopy);
            _position += samplesToCopy;
            return samplesToCopy;
        }
    }
}