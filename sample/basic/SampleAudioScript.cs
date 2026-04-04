using AssemblyEngine.Core;
using AssemblyEngine.Scripting;

namespace SampleGame;

internal sealed class SampleAudioScript : GameScript
{
    private readonly string _audioDir;
    private readonly Dictionary<string, int> _soundIds = [];
    private readonly Dictionary<string, float> _lastPlayedAt = [];
    private float _elapsed;

    public SampleAudioScript(string audioDir)
    {
        _audioDir = audioDir;
    }

    public override void OnLoad()
    {
        LoadCue("dash");
        LoadCue("pickup");
        LoadCue("dash-break");
        LoadCue("hurt");
        LoadCue("wave-start");
        LoadCue("wave-clear");
        LoadCue("game-over");
    }

    public override void OnUpdate(float deltaTime)
    {
        _elapsed += deltaTime;
    }

    public override void OnUnload()
    {
        Audio.StopAll();
    }

    public void PlayDash() => PlayCue("dash", 0.06f);
    public void PlayPickup() => PlayCue("pickup", 0.03f);
    public void PlayDashBreak() => PlayCue("dash-break", 0.05f);
    public void PlayHit() => PlayCue("hurt", 0.08f);
    public void PlayWaveStart() => PlayCue("wave-start", 0.2f);
    public void PlayWaveClear() => PlayCue("wave-clear", 0.25f);
    public void PlayGameOver() => PlayCue("game-over", 0.4f);

    private void LoadCue(string name)
    {
        var soundId = Audio.LoadSound(Path.Combine(_audioDir, name + ".wav"));
        if (soundId < 0)
            throw new InvalidOperationException($"Failed to load sample sound '{name}'.");

        _soundIds[name] = soundId;
    }

    private void PlayCue(string name, float minInterval)
    {
        if (!_soundIds.TryGetValue(name, out var soundId))
            return;

        if (_lastPlayedAt.TryGetValue(name, out var lastPlayedAt) && (_elapsed - lastPlayedAt) < minInterval)
            return;

        Audio.PlaySound(soundId);
        _lastPlayedAt[name] = _elapsed;
    }
}