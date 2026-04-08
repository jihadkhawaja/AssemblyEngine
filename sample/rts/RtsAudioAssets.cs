using SampleShared;

namespace RtsSample;

internal static class RtsAudioAssets
{
    public static void EnsureAssets(string audioDir)
    {
        Directory.CreateDirectory(audioDir);

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "mission-start.wav"),
            new ChipTone(18, 420f, 620f, 0.18f, ChipWaveform.Square),
            new ChipTone(24, 740f, 980f, 0.14f, ChipWaveform.Pulse25),
            new ChipTone(30, 1180f, 1420f, 0.10f, ChipWaveform.Triangle));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "move-order.wav"),
            new ChipTone(14, 760f, 900f, 0.18f, ChipWaveform.Pulse25),
            new ChipTone(18, 980f, 840f, 0.12f, ChipWaveform.Square));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "harvest-order.wav"),
            new ChipTone(16, 520f, 660f, 0.20f, ChipWaveform.Square),
            new ChipTone(18, 760f, 940f, 0.14f, ChipWaveform.Triangle));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "rally.wav"),
            new ChipTone(18, 580f, 740f, 0.18f, ChipWaveform.Triangle),
            new ChipTone(20, 900f, 1220f, 0.14f, ChipWaveform.Pulse25));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "queue-worker.wav"),
            new ChipTone(16, 700f, 940f, 0.20f, ChipWaveform.Square),
            new ChipTone(22, 1080f, 1320f, 0.12f, ChipWaveform.Pulse25));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "queue-guard.wav"),
            new ChipTone(18, 420f, 540f, 0.20f, ChipWaveform.Square),
            new ChipTone(22, 620f, 760f, 0.18f, ChipWaveform.Square),
            new ChipTone(24, 940f, 1180f, 0.12f, ChipWaveform.Pulse25));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "queue-denied.wav"),
            new ChipTone(18, 360f, 240f, 0.24f, ChipWaveform.Square),
            new ChipTone(44, 210f, 126f, 0.18f, ChipWaveform.Noise));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "unit-ready.wav"),
            new ChipTone(18, 620f, 620f, 0.20f, ChipWaveform.Square),
            new ChipTone(18, 880f, 880f, 0.16f, ChipWaveform.Square),
            new ChipTone(28, 1180f, 1460f, 0.12f, ChipWaveform.Triangle));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "mine.wav"),
            new ChipTone(10, 1040f, 960f, 0.16f, ChipWaveform.Pulse25),
            new ChipTone(12, 660f, 620f, 0.12f, ChipWaveform.Noise));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "deposit.wav"),
            new ChipTone(14, 640f, 820f, 0.18f, ChipWaveform.Square),
            new ChipTone(18, 980f, 1240f, 0.16f, ChipWaveform.Pulse25));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "guard-fire.wav"),
            new ChipTone(8, 1320f, 980f, 0.16f, ChipWaveform.Pulse25),
            new ChipTone(10, 760f, 620f, 0.10f, ChipWaveform.Noise));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "under-attack.wav"),
            new ChipTone(20, 420f, 340f, 0.24f, ChipWaveform.Square),
            new ChipTone(32, 220f, 160f, 0.18f, ChipWaveform.Noise));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "raid-alert.wav"),
            new ChipTone(22, 320f, 520f, 0.22f, ChipWaveform.Square),
            new ChipTone(22, 620f, 460f, 0.20f, ChipWaveform.Square),
            new ChipTone(34, 820f, 1040f, 0.14f, ChipWaveform.Pulse25));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "victory.wav"),
            new ChipTone(18, 520f, 520f, 0.18f, ChipWaveform.Square),
            new ChipTone(18, 660f, 660f, 0.18f, ChipWaveform.Square),
            new ChipTone(18, 880f, 1100f, 0.16f, ChipWaveform.Pulse25),
            new ChipTone(44, 1320f, 1560f, 0.12f, ChipWaveform.Triangle));

        EightBitSoundBuilder.WriteSound(
            Path.Combine(audioDir, "defeat.wav"),
            new ChipTone(28, 420f, 320f, 0.22f, ChipWaveform.Square),
            new ChipTone(30, 280f, 180f, 0.18f, ChipWaveform.Triangle),
            new ChipTone(48, 160f, 92f, 0.14f, ChipWaveform.Noise));
    }
}