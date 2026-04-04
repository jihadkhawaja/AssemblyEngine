using SampleShared;

namespace SampleGame;

internal static class SampleAudioAssets
{
    public static void EnsureAssets(string audioDir)
    {
        Directory.CreateDirectory(audioDir);

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "dash.wav"),
            new ChipTone(22, 300f, 560f, 0.44f, ChipWaveform.Pulse25),
            new ChipTone(38, 620f, 980f, 0.30f, ChipWaveform.Square));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "pickup.wav"),
            new ChipTone(24, 740f, 740f, 0.34f, ChipWaveform.Square),
            new ChipTone(24, 990f, 990f, 0.30f, ChipWaveform.Square),
            new ChipTone(42, 1320f, 1540f, 0.26f, ChipWaveform.Pulse25));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "dash-break.wav"),
            new ChipTone(18, 920f, 660f, 0.42f, ChipWaveform.Pulse25),
            new ChipTone(42, 280f, 180f, 0.28f, ChipWaveform.Noise),
            new ChipTone(34, 320f, 210f, 0.22f, ChipWaveform.Triangle));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "hurt.wav"),
            new ChipTone(24, 360f, 240f, 0.36f, ChipWaveform.Square),
            new ChipTone(62, 180f, 104f, 0.26f, ChipWaveform.Noise));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "wave-start.wav"),
            new ChipTone(20, 420f, 620f, 0.24f, ChipWaveform.Square),
            new ChipTone(28, 860f, 1180f, 0.20f, ChipWaveform.Pulse25));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "wave-clear.wav"),
            new ChipTone(34, 392f, 392f, 0.26f, ChipWaveform.Square),
            new ChipTone(34, 523f, 523f, 0.26f, ChipWaveform.Square),
            new ChipTone(42, 659f, 784f, 0.26f, ChipWaveform.Pulse25),
            new ChipTone(58, 988f, 1318f, 0.22f, ChipWaveform.Triangle));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "game-over.wav"),
            new ChipTone(80, 260f, 220f, 0.34f, ChipWaveform.Square),
            new ChipTone(110, 208f, 156f, 0.28f, ChipWaveform.Triangle),
            new ChipTone(120, 144f, 88f, 0.24f, ChipWaveform.Noise));
    }
}