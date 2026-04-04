using System.Buffers.Binary;

namespace SampleShared;

internal enum ChipWaveform
{
    Square,
    Pulse25,
    Triangle,
    Noise,
    Silence
}

internal readonly record struct ChipTone(
    int DurationMs,
    float StartFrequency,
    float EndFrequency = 0f,
    float Volume = 0.45f,
    ChipWaveform Waveform = ChipWaveform.Square)
{
    public float FinalFrequency => EndFrequency <= 0f ? StartFrequency : EndFrequency;
}

internal static class EightBitSoundBuilder
{
    private const int SampleRate = 11025;
    private const int BitsPerSample = 8;
    private const int ChannelCount = 1;

    public static void WriteSound(string path, params ChipTone[] tones)
    {
        if (tones.Length == 0)
            throw new ArgumentException("At least one tone is required.", nameof(tones));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var sampleCount = 0;
        foreach (var tone in tones)
            sampleCount += Math.Max(1, (SampleRate * tone.DurationMs) / 1000);

        var pcm = new byte[sampleCount];
        var writeIndex = 0;
        uint noiseState = 0xA1C3E57Du;

        foreach (var tone in tones)
        {
            var toneSamples = Math.Max(1, (SampleRate * tone.DurationMs) / 1000);
            var fadeSamples = Math.Min(toneSamples / 4, SampleRate / 200);
            var phase = 0f;
            var noiseCountdown = 0;
            var heldNoise = 0f;

            for (var sampleIndex = 0; sampleIndex < toneSamples; sampleIndex++)
            {
                var progress = toneSamples == 1 ? 1f : sampleIndex / (float)(toneSamples - 1);
                var frequency = tone.StartFrequency + ((tone.FinalFrequency - tone.StartFrequency) * progress);
                var waveform = RenderSample(tone.Waveform, frequency, ref phase, ref noiseState, ref noiseCountdown, ref heldNoise);
                var envelope = 1f;

                if (fadeSamples > 0)
                {
                    if (sampleIndex < fadeSamples)
                        envelope *= sampleIndex / (float)fadeSamples;

                    var tailIndex = toneSamples - 1 - sampleIndex;
                    if (tailIndex < fadeSamples)
                        envelope *= tailIndex / (float)fadeSamples;
                }

                var value = 128f + (waveform * tone.Volume * envelope * 127f);
                pcm[writeIndex++] = (byte)Math.Clamp((int)MathF.Round(value), byte.MinValue, byte.MaxValue);
            }
        }

        File.WriteAllBytes(path, BuildWaveFile(pcm));
    }

    private static float RenderSample(
        ChipWaveform waveform,
        float frequency,
        ref float phase,
        ref uint noiseState,
        ref int noiseCountdown,
        ref float heldNoise)
    {
        if (waveform == ChipWaveform.Silence || frequency <= 0f)
            return 0f;

        switch (waveform)
        {
            case ChipWaveform.Noise:
                if (noiseCountdown <= 0)
                {
                    noiseState = AdvanceNoiseState(noiseState);
                    heldNoise = (noiseState & 1u) == 0u ? -1f : 1f;
                    noiseCountdown = Math.Max(1, (int)(SampleRate / Math.Max(40f, frequency)));
                }

                noiseCountdown--;
                return heldNoise;

            default:
                phase += frequency / SampleRate;
                phase -= MathF.Floor(phase);
                return waveform switch
                {
                    ChipWaveform.Pulse25 => phase < 0.25f ? 1f : -1f,
                    ChipWaveform.Triangle => 1f - (4f * MathF.Abs(((phase + 0.25f) % 1f) - 0.5f)),
                    _ => phase < 0.5f ? 1f : -1f
                };
        }
    }

    private static uint AdvanceNoiseState(uint state)
    {
        var lsb = state & 1u;
        state >>= 1;
        if (lsb != 0)
            state ^= 0xB400u;

        return state == 0 ? 0xACE1u : state;
    }

    private static byte[] BuildWaveFile(byte[] pcm)
    {
        const int fileHeaderSize = 12;
        const int formatChunkSize = 24;
        const int dataHeaderSize = 8;
        const int formatTagPcm = 1;
        const int bytesPerSample = BitsPerSample / 8;

        var blockAlign = (short)(ChannelCount * bytesPerSample);
        var byteRate = SampleRate * blockAlign;
        var fileSize = fileHeaderSize + formatChunkSize + dataHeaderSize + pcm.Length;
        var data = new byte[fileSize];

        data[0] = (byte)'R';
        data[1] = (byte)'I';
        data[2] = (byte)'F';
        data[3] = (byte)'F';
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4, 4), fileSize - 8);
        data[8] = (byte)'W';
        data[9] = (byte)'A';
        data[10] = (byte)'V';
        data[11] = (byte)'E';

        data[12] = (byte)'f';
        data[13] = (byte)'m';
        data[14] = (byte)'t';
        data[15] = (byte)' ';
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(20, 2), formatTagPcm);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(22, 2), ChannelCount);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(24, 4), SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(28, 4), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(32, 2), blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(34, 2), BitsPerSample);

        data[36] = (byte)'d';
        data[37] = (byte)'a';
        data[38] = (byte)'t';
        data[39] = (byte)'a';
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(40, 4), pcm.Length);
        Buffer.BlockCopy(pcm, 0, data, 44, pcm.Length);
        return data;
    }
}