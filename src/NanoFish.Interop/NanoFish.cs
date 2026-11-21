using System.Reflection;
using System.Runtime.InteropServices;

namespace NanoFish.Interop;

/// <summary>NanoFish 引擎异常，携带 Rust 端 last_error 文本。</summary>
public sealed class NanoFishException : Exception
{
    public NfStatus Status { get; }

    public NanoFishException(NfStatus status, string message) : base(message) => Status = status;
}

/// <summary>
/// 对 NanoFish C ABI 的薄封装。
/// 加载顺序：环境变量 <c>NANOFISH_LIB</c> → 程序目录 → 系统默认搜索路径。
/// </summary>
public static class NanoFish
{
    private static int _resolverInstalled;

    static NanoFish() => EnsureResolver();

    private static void EnsureResolver()
    {
        if (Interlocked.Exchange(ref _resolverInstalled, 1) != 0)
        {
            return;
        }

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(NanoFish).Assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // 已有其他模块安装了解析器，退回到默认行为。
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, NativeMethods.Lib, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        return TryLoadFrom(Environment.GetEnvironmentVariable("NANOFISH_LIB"))
            ?? TryLoadFrom(Path.Combine(AppContext.BaseDirectory, "libnanofish.so"))
            ?? TryLoadFrom(Path.Combine(AppContext.BaseDirectory, "libnanofish.dylib"))
            ?? TryLoadFrom(Path.Combine(AppContext.BaseDirectory, "nanofish.dll"))
            ?? IntPtr.Zero;
    }

    private static IntPtr? TryLoadFrom(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }
        return NativeLibrary.TryLoad(path, out var handle) ? handle : null;
    }

    /// <summary>引擎版本号，例如 <c>0.1.0</c>。</summary>
    public static string Version =>
        Marshal.PtrToStringUTF8(NativeMethods.nanofish_version()) ?? string.Empty;

    /// <summary>当前线程最近一次 FFI 调用的错误描述。</summary>
    public static string LastError =>
        Marshal.PtrToStringUTF8(NativeMethods.nanofish_last_error()) ?? string.Empty;

    /// <summary>调用 Rust 渲染整首 UST 为 PCM；返回的缓冲区由调用方 Dispose。</summary>
    public static AudioBuffer RenderUst(string ustPath, string otoPath, string voiceDir,
        uint sampleRate = 48_000)
    {
        var status = (NfStatus)NativeMethods.nanofish_render_ust(
            ustPath, otoPath, voiceDir, sampleRate, out var native);
        if (status != NfStatus.Ok)
        {
            throw new NanoFishException(status, LastError);
        }
        return new AudioBuffer(native);
    }

    /// <summary>生成单声道正弦波，返回的缓冲区由调用方 Dispose。</summary>
    public static AudioBuffer GenerateSine(float frequencyHz, uint sampleRate = 48_000, uint durationMs = 100)
    {
        var status = (NfStatus)NativeMethods.nanofish_generate_sine(
            frequencyHz, sampleRate, durationMs, out var native);

        if (status != NfStatus.Ok)
        {
            throw new NanoFishException(status, LastError);
        }
        return new AudioBuffer(native);
    }
}

/// <summary>
/// 引擎分配、宿主只读的音频缓冲区。
/// <see cref="Samples"/> 直接映射非托管内存，不产生拷贝；必须 <see cref="Dispose"/>。
/// </summary>
public sealed class AudioBuffer : IDisposable
{
    private NfAudioBufferNative _native;
    private bool _disposed;

    internal AudioBuffer(NfAudioBufferNative native)
    {
        _native = native;
        // 让 GC 感知这块非托管内存（4 字节/样本）
        GC.AddMemoryPressure(checked((long)native.Len * sizeof(float)));
    }

    public uint SampleRate => _native.SampleRate;

    public int Length => checked((int)_native.Len);

    public bool IsDisposed => _disposed;

    /// <summary>零拷贝样本视图；缓冲区释放后访问会抛 <see cref="ObjectDisposedException"/>。</summary>
    public unsafe ReadOnlySpan<float> Samples
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new ReadOnlySpan<float>((void*)_native.Data, checked((int)_native.Len));
        }
    }

    /// <summary>直接调用 Rust 端计算 RMS，用于交叉验证映射内容。</summary>
    public float Rms()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return NativeMethods.nanofish_rms(_native.Data, _native.Len);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.RemoveMemoryPressure(checked((long)_native.Len * sizeof(float)));
        NativeMethods.nanofish_free_audio(ref _native); // Rust 端把结构体清零
        _native = default;
    }
}