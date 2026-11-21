using System.Runtime.InteropServices;

namespace NanoFish.Interop;

/// <summary>与 Rust 侧 <c>NfAudioBuffer</c> 字段顺序严格一致（size_t 用 UIntPtr 表达）。</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NfAudioBufferNative
{
    public IntPtr Data;
    public UIntPtr Len;
    public UIntPtr Capacity;
    public uint SampleRate;
}

/// <summary>与 Rust 侧返回码一致。</summary>
public enum NfStatus
{
    Ok = 0,
    InvalidArgument = 1,
    NullPointer = 2,
}

internal static class NativeMethods
{
    internal const string Lib = "nanofish";

    [DllImport(Lib, EntryPoint = "nanofish_version", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr nanofish_version();

    [DllImport(Lib, EntryPoint = "nanofish_last_error", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr nanofish_last_error();

    [DllImport(Lib, EntryPoint = "nanofish_generate_sine", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nanofish_generate_sine(
        float freqHz, uint sampleRate, uint durationMs, out NfAudioBufferNative output);

    [DllImport(Lib, EntryPoint = "nanofish_free_audio", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void nanofish_free_audio(ref NfAudioBufferNative buffer);

    [DllImport(Lib, EntryPoint = "nanofish_rms", CallingConvention = CallingConvention.Cdecl)]
    internal static extern float nanofish_rms(IntPtr data, UIntPtr len);

    [DllImport(Lib, EntryPoint = "nanofish_load_ust", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nanofish_load_ust(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path, out NfUstDataNative data);

    [DllImport(Lib, EntryPoint = "nanofish_free_ust", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void nanofish_free_ust(ref NfUstDataNative data);
}