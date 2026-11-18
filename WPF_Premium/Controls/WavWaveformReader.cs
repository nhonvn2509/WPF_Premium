using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace WPF_Premium.Controls;

public sealed record WavWaveformData(
    int ChannelCount,
    int SampleRate,
    int BitsPerSample,
    TimeSpan Duration,
    float[][] ChannelPeaks,
    bool IsWav);

/// <summary>
/// Parses multi-channel RIFF/WAVE audio files into normalized per-channel peak envelopes,
/// with a byte-level fallback for compressed audio formats.
/// </summary>
public static class WavWaveformReader
{
    public static WavWaveformData Read(string path, int bucketCount = 800)
    {
        bucketCount = Math.Clamp(bucketCount, 64, 2000);
        try
        {
            if (TryReadWav(path, bucketCount, out var wavData))
            {
                return wavData;
            }
        }
        catch
        {
            // Fall back to raw byte envelope if the file is malformed or non-standard.
        }

        return ReadFallbackEnvelope(path, bucketCount);
    }

    public static bool HasWavHeader(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length < 12) return false;
            Span<byte> header = stackalloc byte[12];
            if (stream.Read(header) < 12) return false;
            return header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
                && header[8] == 'W' && header[9] == 'A' && header[10] == 'V' && header[11] == 'E';
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadWav(string path, int bucketCount, out WavWaveformData result)
    {
        result = default!;
        using var stream = File.OpenRead(path);
        if (stream.Length < 44) return false;

        Span<byte> riffHeader = stackalloc byte[12];
        if (stream.Read(riffHeader) < 12) return false;
        if (Encoding.ASCII.GetString(riffHeader[..4]) != "RIFF" ||
            Encoding.ASCII.GetString(riffHeader[8..12]) != "WAVE")
        {
            return false;
        }

        ushort audioFormat = 0;
        ushort channels = 0;
        int sampleRate = 0;
        ushort blockAlign = 0;
        ushort bitsPerSample = 0;
        long dataOffset = -1;
        long dataLength = 0;

        Span<byte> chunkHeader = stackalloc byte[8];
        while (stream.Position + 8 <= stream.Length)
        {
            if (stream.Read(chunkHeader) < 8) break;
            var chunkId = Encoding.ASCII.GetString(chunkHeader[..4]);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..8]);
            var chunkStart = stream.Position;

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                var fmtBytes = new byte[Math.Min(chunkSize, 64)];
                var read = stream.Read(fmtBytes, 0, fmtBytes.Length);
                if (read >= 16)
                {
                    audioFormat = BinaryPrimitives.ReadUInt16LittleEndian(fmtBytes.AsSpan(0, 2));
                    channels = BinaryPrimitives.ReadUInt16LittleEndian(fmtBytes.AsSpan(2, 2));
                    sampleRate = BinaryPrimitives.ReadInt32LittleEndian(fmtBytes.AsSpan(4, 4));
                    blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(fmtBytes.AsSpan(12, 2));
                    bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(fmtBytes.AsSpan(14, 2));

                    // WAVE_FORMAT_EXTENSIBLE (0xFFFE) stores actual SubFormat GUID at offset 24
                    if (audioFormat == 0xFFFE && read >= 26)
                    {
                        audioFormat = BinaryPrimitives.ReadUInt16LittleEndian(fmtBytes.AsSpan(24, 2));
                    }
                }
            }
            else if (chunkId == "data")
            {
                dataOffset = chunkStart;
                dataLength = Math.Min(chunkSize, stream.Length - chunkStart);
                break;
            }

            var nextPos = chunkStart + chunkSize + (chunkSize & 1);
            if (nextPos > stream.Length) break;
            stream.Position = nextPos;
        }

        if (channels == 0 || sampleRate <= 0 || bitsPerSample == 0 || dataOffset < 0 || dataLength <= 0)
        {
            return false;
        }

        var bytesPerSample = Math.Max(1, bitsPerSample / 8);
        var frameSize = blockAlign > 0 ? blockAlign : channels * bytesPerSample;
        if (frameSize <= 0) return false;

        // Only PCM (1) and IEEE Float (3) are decoded sample-by-sample; otherwise use fallback.
        if (audioFormat is not (1 or 3))
        {
            return false;
        }

        var totalFrames = dataLength / frameSize;
        if (totalFrames <= 0) return false;

        var duration = TimeSpan.FromSeconds((double)totalFrames / sampleRate);
        var peaks = new float[channels][];
        for (var ch = 0; ch < channels; ch++)
        {
            peaks[ch] = new float[bucketCount];
        }

        stream.Position = dataOffset;
        const int bufferFrames = 4096;
        var buffer = new byte[bufferFrames * frameSize];
        long frameIndex = 0;

        while (frameIndex < totalFrames)
        {
            var remainingFrames = totalFrames - frameIndex;
            var toReadFrames = (int)Math.Min(bufferFrames, remainingFrames);
            var bytesRead = stream.Read(buffer, 0, toReadFrames * frameSize);
            if (bytesRead < frameSize) break;

            var framesRead = bytesRead / frameSize;
            for (var f = 0; f < framesRead; f++)
            {
                var bucket = (int)((frameIndex + f) * bucketCount / totalFrames);
                if (bucket >= bucketCount) bucket = bucketCount - 1;

                var frameOffset = f * frameSize;
                for (var ch = 0; ch < channels; ch++)
                {
                    var sampleOffset = frameOffset + ch * bytesPerSample;
                    if (sampleOffset + bytesPerSample > bytesRead) break;

                    var amp = ReadNormalizedSample(buffer.AsSpan(sampleOffset, bytesPerSample), audioFormat, bitsPerSample);
                    if (amp > peaks[ch][bucket])
                    {
                        peaks[ch][bucket] = amp;
                    }
                }
            }

            frameIndex += framesRead;
        }

        result = new WavWaveformData(channels, sampleRate, bitsPerSample, duration, peaks, true);
        return true;
    }

    private static float ReadNormalizedSample(ReadOnlySpan<byte> sample, ushort audioFormat, ushort bitsPerSample)
    {
        if (audioFormat == 3)
        {
            if (bitsPerSample == 32 && sample.Length >= 4)
            {
                var val = BinaryPrimitives.ReadSingleLittleEndian(sample);
                return float.IsFinite(val) ? Math.Clamp(MathF.Abs(val), 0f, 1f) : 0f;
            }
            if (bitsPerSample == 64 && sample.Length >= 8)
            {
                var val = BinaryPrimitives.ReadDoubleLittleEndian(sample);
                return double.IsFinite(val) ? (float)Math.Clamp(Math.Abs(val), 0d, 1d) : 0f;
            }
        }

        return bitsPerSample switch
        {
            8 => Math.Abs((sample[0] - 128) / 128f),
            16 => Math.Abs(BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768f),
            24 => Math.Abs(ReadInt24(sample) / 8388608f),
            32 => Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(sample) / 2147483648f),
            _ => 0f
        };
    }

    private static int ReadInt24(ReadOnlySpan<byte> sample)
    {
        var value = sample[0] | (sample[1] << 8) | (sample[2] << 16);
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }
        return value;
    }

    private static WavWaveformData ReadFallbackEnvelope(string path, int bucketCount)
    {
        var peaks = new float[1][];
        peaks[0] = new float[bucketCount];

        using var audio = File.OpenRead(path);
        var length = audio.Length;
        if (length > 0)
        {
            for (var i = 0; i < bucketCount; i++)
            {
                audio.Position = (long)i * length / bucketCount;
                var b = Math.Max(0, audio.ReadByte());
                peaks[0][i] = Math.Clamp(b / 255f, 0.05f, 1f);
            }
        }

        return new WavWaveformData(1, 0, 0, TimeSpan.Zero, peaks, false);
    }
}
