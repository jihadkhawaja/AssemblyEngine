using SampleShared;

namespace VisualNovelSample;

internal static class VisualNovelAudioAssets
{
    public static void EnsureAssets(string audioDir)
    {
        Directory.CreateDirectory(audioDir);

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "advance.wav"),
            new ChipTone(18, 840f, 1020f, 0.18f, ChipWaveform.Pulse25),
            new ChipTone(24, 1260f, 1180f, 0.12f, ChipWaveform.Square));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "reveal.wav"),
            new ChipTone(26, 620f, 880f, 0.16f, ChipWaveform.Triangle),
            new ChipTone(18, 1120f, 980f, 0.10f, ChipWaveform.Pulse25));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "iris-talk.wav"),
            new ChipTone(40, 660f, 880f, 0.75f, ChipWaveform.Square),
            new ChipTone(60, 880f, 440f, 0.45f, ChipWaveform.Triangle));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "rowan-talk.wav"),
            new ChipTone(40, 280f, 360f, 0.75f, ChipWaveform.Square),
            new ChipTone(60, 360f, 200f, 0.45f, ChipWaveform.Triangle));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "skip-on.wav"),
            new ChipTone(22, 520f, 720f, 0.18f, ChipWaveform.Square),
            new ChipTone(26, 860f, 1080f, 0.16f, ChipWaveform.Pulse25));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "skip-off.wav"),
            new ChipTone(22, 900f, 700f, 0.18f, ChipWaveform.Square),
            new ChipTone(24, 660f, 480f, 0.14f, ChipWaveform.Triangle));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "save.wav"),
            new ChipTone(22, 660f, 660f, 0.16f, ChipWaveform.Square),
            new ChipTone(22, 990f, 990f, 0.16f, ChipWaveform.Square),
            new ChipTone(44, 1320f, 1540f, 0.12f, ChipWaveform.Triangle));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "load.wav"),
            new ChipTone(24, 1180f, 920f, 0.16f, ChipWaveform.Pulse25),
            new ChipTone(26, 760f, 920f, 0.14f, ChipWaveform.Square),
            new ChipTone(36, 1100f, 1320f, 0.10f, ChipWaveform.Triangle));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "restart.wav"),
            new ChipTone(22, 440f, 620f, 0.16f, ChipWaveform.Square),
            new ChipTone(32, 720f, 980f, 0.14f, ChipWaveform.Pulse25),
            new ChipTone(38, 1120f, 1320f, 0.10f, ChipWaveform.Triangle));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "chapter-end.wav"),
            new ChipTone(42, 520f, 620f, 0.18f, ChipWaveform.Square),
            new ChipTone(52, 780f, 980f, 0.16f, ChipWaveform.Triangle),
            new ChipTone(86, 1180f, 1420f, 0.12f, ChipWaveform.Pulse25));
    }
}