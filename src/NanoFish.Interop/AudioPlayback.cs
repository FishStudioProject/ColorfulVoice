using System.Runtime.InteropServices;

namespace NanoFish.Interop;

/// <summary>
/// 原生播放器（miniaudio）。没有声卡时自动回退 null 后端，
/// 位置仍按真实时钟推进，因此播放头逻辑在任何机器上都能工作。
/// </summary>
public sealed class AudioPlayback : IDisposable
{
    private IntPtr _player;
    private float[] _samples = Array.Empty<float>();
    private int _cursor;
    private bool _disposed;

    public const int SampleRate = 48_000;

    public AudioPlayback()
    {
        _player = NativeMethods.nanofish_player_create(SampleRate, 1);
        if (_player == IntPtr.Zero)
        {
            throw new InvalidOperationException("无法创建音频设备（无可用后端）");
        }
    }

    public bool IsNullBackend => NativeMethods.nanofish_player_is_null(_player) == 1;

    public bool IsPlaying { get; private set; }

    /// <summary>已播放秒数（设备时钟）。</summary>
    public double PositionSeconds =>
        NativeMethods.nanofish_player_position(_player) / (double)SampleRate;

    public int QueuedFrames => (int)NativeMethods.nanofish_player_queued(_player);

    /// <summary>装载待播 PCM（单声道），会复制一份供分块推送。</summary>
    public void Load(ReadOnlySpan<float> samples)
    {
        _samples = samples.ToArray();
        _cursor = 0;
    }

    public void Start()
    {
        if (_disposed || IsPlaying)
        {
            return;
        }
        Pump();
        IsPlaying = NativeMethods.nanofish_player_start(_player) == 0;
    }

    public void Stop()
    {
        if (_disposed || !IsPlaying)
        {
            return;
        }
        NativeMethods.nanofish_player_stop(_player);
        IsPlaying = false;
    }

    /// <summary>补充环形缓冲，由 UI 定时器周期调用。</summary>
    public void Pump()
    {
        if (_disposed || _cursor >= _samples.Length)
        {
            return;
        }
        var chunk = Math.Min(SampleRate / 4, _samples.Length - _cursor);
        unsafe
        {
            fixed (float* p = &_samples[_cursor])
            {
                var written = NativeMethods.nanofish_player_push(_player, p, (uint)chunk);
                _cursor += (int)written;
            }
        }
    }

    public bool Finished => _cursor >= _samples.Length && QueuedFrames == 0;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Stop();
        if (_player != IntPtr.Zero)
        {
            NativeMethods.nanofish_player_destroy(_player);
            _player = IntPtr.Zero;
        }
    }
}
