using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace HuntersAccord.Platform.Windows;

/// <summary>
/// Low-latency sound output for short interface sounds. PlaySound opens the audio device on every call: the
/// device wakes up late, so short sounds are lost and long ones start late. This keeps one waveOut stream
/// open and feeds it 15 ms blocks from a thread, mixing the sounds that play (silence in between keeps the
/// device awake). The stream stops after a while without sounds and restarts on the next one or on
/// <see cref="KeepReady"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WaveOutMixer
{
    private const int Rate = 44100;
    private const int BlockSamples = Rate * 15 / 1000;
    private const int Blocks = 3;
    private static readonly TimeSpan IdleStop = TimeSpan.FromMinutes(2);

    private readonly Lock _gate = new();
    private readonly List<(short[] Samples, int Position)> _voices = [];
    private DateTime _lastUse = DateTime.UtcNow;
    private Thread? _thread;
    private bool _failed;

    public void Play(byte[] wav)
    {
        var samples = Pcm16Mono(wav);
        if (samples is null) return;
        lock (_gate)
        {
            _voices.Add((samples, 0));
            _lastUse = DateTime.UtcNow;
            EnsureRunning();
        }
    }

    /// <summary>Opens the stream ahead of the first sound (e.g. when the window shows), so it is not late.</summary>
    public void KeepReady()
    {
        lock (_gate)
        {
            _lastUse = DateTime.UtcNow;
            EnsureRunning();
        }
    }

    private void EnsureRunning()
    {
        if (_thread is not null || _failed) return;
        _thread = new Thread(Run) { IsBackground = true, Name = "HA sounds", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    private void Run()
    {
        using var ready = new AutoResetEvent(false);
        var format = new WaveFormat
        {
            FormatTag = 1, Channels = 1, SamplesPerSec = Rate, AvgBytesPerSec = Rate * 2, BlockAlign = 2, BitsPerSample = 16,
        };
        if (waveOutOpen(out var device, WaveMapper, ref format, ready.SafeWaitHandle.DangerousGetHandle(), 0, CallbackEvent) != 0)
        {
            lock (_gate)
            {
                _failed = true;
                _thread = null;
            }
            return;
        }

        var headers = new nint[Blocks];
        var mix = new short[BlockSamples];
        try
        {
            for (var i = 0; i < Blocks; i++)
            {
                headers[i] = Marshal.AllocHGlobal(HeaderSize);
                for (var b = 0; b < HeaderSize; b++) Marshal.WriteByte(headers[i], b, 0);
                Marshal.WriteIntPtr(headers[i], OffsetData, Marshal.AllocHGlobal(BlockSamples * 2));
                Marshal.WriteInt32(headers[i], OffsetLength, BlockSamples * 2);
                waveOutPrepareHeader(device, headers[i], HeaderSize);
                Fill(headers[i], mix);
                waveOutWrite(device, headers[i], HeaderSize);
            }

            while (true)
            {
                ready.WaitOne(50);
                lock (_gate)
                {
                    if (_voices.Count == 0 && DateTime.UtcNow - _lastUse > IdleStop)
                    {
                        _thread = null;
                        break;
                    }
                }
                foreach (var header in headers)
                {
                    if ((Marshal.ReadInt32(header, OffsetFlags) & HeaderDone) == 0) continue;
                    Fill(header, mix);
                    Marshal.WriteInt32(header, OffsetFlags, Marshal.ReadInt32(header, OffsetFlags) & ~HeaderDone);
                    waveOutWrite(device, header, HeaderSize);
                }
            }
        }
        finally
        {
            waveOutReset(device);
            foreach (var header in headers.Where(h => h != 0))
            {
                waveOutUnprepareHeader(device, header, HeaderSize);
                Marshal.FreeHGlobal(Marshal.ReadIntPtr(header, OffsetData));
                Marshal.FreeHGlobal(header);
            }
            waveOutClose(device);
        }
    }

    /// <summary>Mixes the next block of every playing sound into the header's buffer.</summary>
    private void Fill(nint header, short[] mix)
    {
        Array.Clear(mix);
        lock (_gate)
        {
            for (var v = _voices.Count - 1; v >= 0; v--)
            {
                var (samples, position) = _voices[v];
                var n = Math.Min(BlockSamples, samples.Length - position);
                for (var i = 0; i < n; i++)
                    mix[i] = (short)Math.Clamp(mix[i] + samples[position + i], short.MinValue, short.MaxValue);
                if (position + n >= samples.Length) _voices.RemoveAt(v);
                else _voices[v] = (samples, position + n);
            }
        }
        Marshal.Copy(mix, 0, Marshal.ReadIntPtr(header, OffsetData), BlockSamples);
    }

    /// <summary>Samples of a 16-bit mono 44.1 kHz WAV (what UiSounds makes); null for anything else.</summary>
    private static short[]? Pcm16Mono(byte[] wav)
    {
        if (wav.Length < 44 || BitConverter.ToInt16(wav, 22) != 1 || BitConverter.ToInt32(wav, 24) != Rate
            || BitConverter.ToInt16(wav, 34) != 16) return null;
        var samples = new short[(wav.Length - 44) / 2];
        Buffer.BlockCopy(wav, 44, samples, 0, samples.Length * 2);
        return samples;
    }

    // WAVEHDR (64-bit): lpData, dwBufferLength, dwBytesRecorded, dwUser, dwFlags, dwLoops, lpNext, reserved.
    private static int HeaderSize => IntPtr.Size == 8 ? 48 : 32;
    private const int OffsetData = 0;
    private static int OffsetLength => IntPtr.Size;
    private static int OffsetFlags => IntPtr.Size == 8 ? 24 : 16;
    private const int HeaderDone = 1;
    private const uint CallbackEvent = 0x50000;
    private const uint WaveMapper = unchecked((uint)-1);

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public short FormatTag;
        public short Channels;
        public int SamplesPerSec;
        public int AvgBytesPerSec;
        public short BlockAlign;
        public short BitsPerSample;
        public short Size;
    }

    [DllImport("winmm.dll")] private static extern int waveOutOpen(out nint device, uint deviceId, ref WaveFormat format, nint callback, nint instance, uint flags);
    [DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(nint device, nint header, int size);
    [DllImport("winmm.dll")] private static extern int waveOutUnprepareHeader(nint device, nint header, int size);
    [DllImport("winmm.dll")] private static extern int waveOutWrite(nint device, nint header, int size);
    [DllImport("winmm.dll")] private static extern int waveOutReset(nint device);
    [DllImport("winmm.dll")] private static extern int waveOutClose(nint device);
}
