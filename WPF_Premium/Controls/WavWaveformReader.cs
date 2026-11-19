using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
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
/// Parses multi-channel RIFF/WAVE and Media Foundation-supported audio files
/// into normalized per-channel peak envelopes, with a byte-level fallback.
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
            // Fall through to Media Foundation / raw byte envelope.
        }

        try
        {
            if (TryReadWithMediaFoundation(path, bucketCount, out var mfData))
            {
                return mfData;
            }
        }
        catch
        {
            // Fall back to raw byte envelope if Media Foundation cannot decode the file.
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
                if (channels > 0 && sampleRate > 0 && bitsPerSample > 0)
                {
                    break;
                }
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

        // Support PCM (1), IEEE Float (3), G.711 A-law (6), and G.711 mu-law (7);
        // other compressed WAV formats (e.g. ADPCM) fall through to Media Foundation.
        if (audioFormat is not (1 or 3 or 6 or 7))
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
        if (audioFormat == 6)
        {
            return Math.Abs(DecodeALaw(sample[0]) / 32768f);
        }
        if (audioFormat == 7)
        {
            return Math.Abs(DecodeMuLaw(sample[0]) / 32768f);
        }
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

    private static short DecodeALaw(byte alaw)
    {
        alaw ^= 0x55;
        var sign = (alaw & 0x80) != 0 ? -1 : 1;
        var exponent = (alaw & 0x70) >> 4;
        var mantissa = alaw & 0x0F;
        var sample = exponent == 0 ? (mantissa << 4) + 8 : ((mantissa << 4) + 0x108) << (exponent - 1);
        return (short)(sign * sample);
    }

    private static short DecodeMuLaw(byte mulaw)
    {
        mulaw = (byte)~mulaw;
        var sign = (mulaw & 0x80) != 0 ? -1 : 1;
        var exponent = (mulaw & 0x70) >> 4;
        var mantissa = mulaw & 0x0F;
        var sample = (((mantissa << 3) + 0x84) << exponent) - 0x84;
        return (short)(sign * sample);
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

    private static bool TryReadWithMediaFoundation(string path, int bucketCount, out WavWaveformData result)
    {
        result = default!;
        var mfStarted = false;
        IMFSourceReader? reader = null;
        IMFMediaType? partialType = null;
        IMFMediaType? currentType = null;

        try
        {
            if (MFStartup(MfVersion, 0) != 0)
            {
                return false;
            }
            mfStarted = true;

            if (MFCreateSourceReaderFromURL(path, IntPtr.Zero, out reader) != 0 || reader is null)
            {
                return false;
            }

            if (MFCreateMediaType(out partialType) != 0 || partialType is null)
            {
                return false;
            }

            var majorType = MfMtMajorType;
            var audioType = MfMediaTypeAudio;
            var subtype = MfMtSubtype;
            var floatFormat = MfAudioFormatFloat;
            if (partialType.SetGUID(ref majorType, ref audioType) != 0 ||
                partialType.SetGUID(ref subtype, ref floatFormat) != 0 ||
                reader.SetCurrentMediaType(MfSourceReaderFirstAudioStream, IntPtr.Zero, partialType) != 0)
            {
                return false;
            }

            if (reader.GetCurrentMediaType(MfSourceReaderFirstAudioStream, out currentType) != 0 || currentType is null)
            {
                return false;
            }

            var channelsKey = MfMtAudioNumChannels;
            var sampleRateKey = MfMtAudioSamplesPerSecond;
            var bitsKey = MfMtAudioBitsPerSample;
            if (currentType.GetUINT32(ref channelsKey, out var channelCountU) != 0 ||
                currentType.GetUINT32(ref sampleRateKey, out var sampleRateU) != 0)
            {
                return false;
            }

            var bitsPerSample = currentType.GetUINT32(ref bitsKey, out var bitsU) == 0 ? (int)bitsU : 32;
            var channels = (int)channelCountU;
            var sampleRate = (int)sampleRateU;
            if (channels <= 0 || sampleRate <= 0)
            {
                return false;
            }

            var framesPerWindow = Math.Max(64, sampleRate / 200);
            var windowPeaks = new List<float[]>(2048);
            var currentWindow = new float[channels];
            var framesInCurrentWindow = 0;
            long totalFrames = 0;
            var floatBuffer = Array.Empty<float>();

            while (true)
            {
                if (reader.ReadSample(MfSourceReaderFirstAudioStream, 0, out _, out var flags, out _, out var sample) != 0)
                {
                    break;
                }

                try
                {
                    if ((flags & MfSourceReaderFlagEndOfStream) != 0)
                    {
                        break;
                    }

                    if (sample is null)
                    {
                        continue;
                    }

                    if (sample.ConvertToContiguousBuffer(out var buffer) != 0 || buffer is null)
                    {
                        continue;
                    }

                    try
                    {
                        if (buffer.Lock(out var dataPtr, out _, out var byteLength) != 0)
                        {
                            continue;
                        }

                        try
                        {
                            var floatCount = (int)(byteLength / sizeof(float));
                            var frameCount = floatCount / channels;
                            if (frameCount <= 0)
                            {
                                continue;
                            }

                            var needed = frameCount * channels;
                            if (floatBuffer.Length < needed)
                            {
                                floatBuffer = new float[needed];
                            }

                            Marshal.Copy(dataPtr, floatBuffer, 0, needed);
                            for (var f = 0; f < frameCount; f++)
                            {
                                var baseIdx = f * channels;
                                for (var ch = 0; ch < channels; ch++)
                                {
                                    var amp = floatBuffer[baseIdx + ch];
                                    if (float.IsFinite(amp))
                                    {
                                        var abs = Math.Clamp(MathF.Abs(amp), 0f, 1f);
                                        if (abs > currentWindow[ch])
                                        {
                                            currentWindow[ch] = abs;
                                        }
                                    }
                                }

                                framesInCurrentWindow++;
                                if (framesInCurrentWindow >= framesPerWindow)
                                {
                                    windowPeaks.Add(currentWindow);
                                    currentWindow = new float[channels];
                                    framesInCurrentWindow = 0;
                                }
                            }

                            totalFrames += frameCount;
                        }
                        finally
                        {
                            buffer.Unlock();
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(buffer);
                    }
                }
                finally
                {
                    if (sample is not null)
                    {
                        Marshal.ReleaseComObject(sample);
                    }
                }
            }

            if (framesInCurrentWindow > 0)
            {
                windowPeaks.Add(currentWindow);
            }

            if (totalFrames <= 0 || windowPeaks.Count == 0)
            {
                return false;
            }

            var peaks = new float[channels][];
            for (var ch = 0; ch < channels; ch++)
            {
                peaks[ch] = new float[bucketCount];
            }

            var windowCount = windowPeaks.Count;
            for (var b = 0; b < bucketCount; b++)
            {
                var startIdx = (int)((long)b * windowCount / bucketCount);
                var endIdx = (int)Math.Max(startIdx + 1, (long)(b + 1) * windowCount / bucketCount);
                for (var w = startIdx; w < endIdx && w < windowCount; w++)
                {
                    var win = windowPeaks[w];
                    for (var ch = 0; ch < channels; ch++)
                    {
                        if (win[ch] > peaks[ch][b])
                        {
                            peaks[ch][b] = win[ch];
                        }
                    }
                }
            }

            var duration = TimeSpan.FromSeconds((double)totalFrames / sampleRate);
            result = new WavWaveformData(channels, sampleRate, bitsPerSample, duration, peaks, false);
            return true;
        }
        finally
        {
            if (currentType is not null) Marshal.ReleaseComObject(currentType);
            if (partialType is not null) Marshal.ReleaseComObject(partialType);
            if (reader is not null) Marshal.ReleaseComObject(reader);
            if (mfStarted) MFShutdown();
        }
    }

    private const uint MfVersion = 0x00020070;
    private const uint MfSourceReaderFirstAudioStream = 0xFFFFFFFD;
    private const uint MfSourceReaderFlagEndOfStream = 0x00000002;

    private static readonly Guid MfMtMajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    private static readonly Guid MfMtSubtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    private static readonly Guid MfMediaTypeAudio = new("73647561-0000-0010-8000-00aa00389b71");
    private static readonly Guid MfAudioFormatFloat = new("00000003-0000-0010-8000-00aa00389b71");
    private static readonly Guid MfMtAudioNumChannels = new("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    private static readonly Guid MfMtAudioSamplesPerSecond = new("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    private static readonly Guid MfMtAudioBitsPerSample = new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFStartup(uint version, uint dwFlags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFShutdown();

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateMediaType(out IMFMediaType ppMFType);

    [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int MFCreateSourceReaderFromURL(string pwszURL, IntPtr pAttributes, out IMFSourceReader ppSourceReader);

    [ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFAttributes
    {
        int GetItem(ref Guid guidKey, IntPtr pValue);
        int GetItemType(ref Guid guidKey, out int pType);
        int CompareItem(ref Guid guidKey, IntPtr Value, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        int Compare(IMFAttributes pTheirs, int MatchType, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        int GetUINT32(ref Guid guidKey, out uint punValue);
        int GetUINT64(ref Guid guidKey, out ulong punValue);
        int GetDouble(ref Guid guidKey, out double pfValue);
        int GetGUID(ref Guid guidKey, out Guid pguidValue);
        int GetStringLength(ref Guid guidKey, out uint pcchLength);
        int GetString(ref Guid guidKey, IntPtr pwszValue, uint cchBufSize, out uint pcchLength);
        int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out uint pcchLength);
        int GetBlobSize(ref Guid guidKey, out uint pcbBlobSize);
        int GetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize, out uint pcbBlobSize);
        int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out uint pcbSize);
        int GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
        int SetItem(ref Guid guidKey, IntPtr Value);
        int DeleteItem(ref Guid guidKey);
        int DeleteAllItems();
        int SetUINT32(ref Guid guidKey, uint unValue);
        int SetUINT64(ref Guid guidKey, ulong unValue);
        int SetDouble(ref Guid guidKey, double fValue);
        int SetGUID(ref Guid guidKey, ref Guid guidValue);
        int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        int SetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize);
        int SetUnknown(ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        int LockStore();
        int UnlockStore();
        int GetCount(out uint pcItems);
        int GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
        int CopyAllItems(IMFAttributes pDest);
    }

    [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaType : IMFAttributes
    {
        new int GetItem(ref Guid guidKey, IntPtr pValue);
        new int GetItemType(ref Guid guidKey, out int pType);
        new int CompareItem(ref Guid guidKey, IntPtr Value, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        new int Compare(IMFAttributes pTheirs, int MatchType, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        new int GetUINT32(ref Guid guidKey, out uint punValue);
        new int GetUINT64(ref Guid guidKey, out ulong punValue);
        new int GetDouble(ref Guid guidKey, out double pfValue);
        new int GetGUID(ref Guid guidKey, out Guid pguidValue);
        new int GetStringLength(ref Guid guidKey, out uint pcchLength);
        new int GetString(ref Guid guidKey, IntPtr pwszValue, uint cchBufSize, out uint pcchLength);
        new int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out uint pcchLength);
        new int GetBlobSize(ref Guid guidKey, out uint pcbBlobSize);
        new int GetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize, out uint pcbBlobSize);
        new int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out uint pcbSize);
        new int GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
        new int SetItem(ref Guid guidKey, IntPtr Value);
        new int DeleteItem(ref Guid guidKey);
        new int DeleteAllItems();
        new int SetUINT32(ref Guid guidKey, uint unValue);
        new int SetUINT64(ref Guid guidKey, ulong unValue);
        new int SetDouble(ref Guid guidKey, double fValue);
        new int SetGUID(ref Guid guidKey, ref Guid guidValue);
        new int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        new int SetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize);
        new int SetUnknown(ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        new int LockStore();
        new int UnlockStore();
        new int GetCount(out uint pcItems);
        new int GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
        new int CopyAllItems(IMFAttributes pDest);
    }

    [ComImport, Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSourceReader
    {
        int GetStreamSelection(uint dwStreamIndex, [MarshalAs(UnmanagedType.Bool)] out bool pfSelected);
        int SetStreamSelection(uint dwStreamIndex, [MarshalAs(UnmanagedType.Bool)] bool fSelected);
        int GetNativeMediaType(uint dwStreamIndex, uint dwMediaTypeIndex, out IMFMediaType ppMediaType);
        int GetCurrentMediaType(uint dwStreamIndex, out IMFMediaType ppMediaType);
        int SetCurrentMediaType(uint dwStreamIndex, IntPtr pdwReserved, IMFMediaType pMediaType);
        int SetCurrentPosition(ref Guid guidTimeFormat, IntPtr varPosition);
        int ReadSample(uint dwStreamIndex, uint dwControlFlags, out uint pdwActualStreamIndex, out uint pdwStreamFlags, out long pllTimestamp, out IMFSample? ppSample);
        int Flush(uint dwStreamIndex);
        int GetServiceForStream(uint dwStreamIndex, ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
        int GetPresentationAttribute(uint dwStreamIndex, ref Guid guidAttribute, IntPtr pvarAttribute);
    }

    [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSample : IMFAttributes
    {
        new int GetItem(ref Guid guidKey, IntPtr pValue);
        new int GetItemType(ref Guid guidKey, out int pType);
        new int CompareItem(ref Guid guidKey, IntPtr Value, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        new int Compare(IMFAttributes pTheirs, int MatchType, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        new int GetUINT32(ref Guid guidKey, out uint punValue);
        new int GetUINT64(ref Guid guidKey, out ulong punValue);
        new int GetDouble(ref Guid guidKey, out double pfValue);
        new int GetGUID(ref Guid guidKey, out Guid pguidValue);
        new int GetStringLength(ref Guid guidKey, out uint pcchLength);
        new int GetString(ref Guid guidKey, IntPtr pwszValue, uint cchBufSize, out uint pcchLength);
        new int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out uint pcchLength);
        new int GetBlobSize(ref Guid guidKey, out uint pcbBlobSize);
        new int GetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize, out uint pcbBlobSize);
        new int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out uint pcbSize);
        new int GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
        new int SetItem(ref Guid guidKey, IntPtr Value);
        new int DeleteItem(ref Guid guidKey);
        new int DeleteAllItems();
        new int SetUINT32(ref Guid guidKey, uint unValue);
        new int SetUINT64(ref Guid guidKey, ulong unValue);
        new int SetDouble(ref Guid guidKey, double fValue);
        new int SetGUID(ref Guid guidKey, ref Guid guidValue);
        new int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        new int SetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize);
        new int SetUnknown(ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        new int LockStore();
        new int UnlockStore();
        new int GetCount(out uint pcItems);
        new int GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
        new int CopyAllItems(IMFAttributes pDest);
        int GetSampleFlags(out uint pdwSampleFlags);
        int SetSampleFlags(uint dwSampleFlags);
        int GetSampleTime(out long phnsSampleTime);
        int SetSampleTime(long hnsSampleTime);
        int GetSampleDuration(out long phnsSampleDuration);
        int SetSampleDuration(long hnsSampleDuration);
        int GetBufferCount(out uint pdwBufferCount);
        int GetBufferByIndex(uint dwIndex, out IMFMediaBuffer ppBuffer);
        int ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
    }

    [ComImport, Guid("045fa593-8799-42b8-bc8d-8968c6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaBuffer
    {
        int Lock(out IntPtr ppbBuffer, out uint pcbMaxLength, out uint pcbCurrentLength);
        int Unlock();
        int GetCurrentLength(out uint pcbCurrentLength);
        int SetCurrentLength(uint cbCurrentLength);
        int GetMaxLength(out uint pcbMaxLength);
    }
}
