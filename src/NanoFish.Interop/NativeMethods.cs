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

    [DllImport(Lib, EntryPoint = "nanofish_load_oto", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nanofish_load_oto(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path, out NfOtoBankNative bank);

    [DllImport(Lib, EntryPoint = "nanofish_free_oto", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void nanofish_free_oto(ref NfOtoBankNative bank);

    [DllImport(Lib, EntryPoint = "nanofish_save_ust", CallingConvention = CallingConvention.Cdecl)]
    internal static extern unsafe int nanofish_save_ust(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        float tempo,
        NfNoteNative* notes,
        nuint count,
        byte* extras,
        nuint extrasLen);

    [DllImport(Lib, EntryPoint = "nanofish_render_ust", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nanofish_render_ust(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string ustPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string otoPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string voiceDir,
        uint sampleRate,
        out NfAudioBufferNative output);

    [DllImport(Lib, EntryPoint = "nanofish_player_create", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr nanofish_player_create(uint sampleRate, ushort channels);

    [DllImport(Lib, EntryPoint = "nanofish_player_push", CallingConvention = CallingConvention.Cdecl)]
    internal static extern unsafe uint nanofish_player_push(IntPtr player, float* data, uint frames);

    [DllImport(Lib, EntryPoint = "nanofish_player_start", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nanofish_player_start(IntPtr player);

    [DllImport(Lib, EntryPoint = "nanofish_player_stop", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void nanofish_player_stop(IntPtr player);

    [DllImport(Lib, EntryPoint = "nanofish_player_destroy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void nanofish_player_destroy(IntPtr player);

    [DllImport(Lib, EntryPoint = "nanofish_player_position", CallingConvention = CallingConvention.Cdecl)]
    internal static extern ulong nanofish_player_position(IntPtr player);

    [DllImport(Lib, EntryPoint = "nanofish_player_queued", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint nanofish_player_queued(IntPtr player);

    [DllImport(Lib, EntryPoint = "nanofish_player_is_null", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int nanofish_player_is_null(IntPtr player);
}