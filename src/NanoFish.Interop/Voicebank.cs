using System.Runtime.InteropServices;
using System.Text;

namespace NanoFish.Interop;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NfVoicebankInfoNative
{
    public fixed byte Name[64];
    public fixed byte Image[128];
    public uint AliasCount;
    public uint Kind;
}

public enum BankKind
{
    Cv = 0,
    Vcv = 1,
    Cvvc = 2,
    Unknown = 3,
}

/// <summary>已导入的 UTAU 音源（目录 + oto.ini + character.txt）。</summary>
public sealed class Voicebank : IDisposable
{
    private IntPtr _handle;

    private Voicebank(IntPtr handle, string directory, string name, string? image,
        uint aliasCount, BankKind kind)
    {
        _handle = handle;
        Directory = directory;
        Name = name;
        ImagePath = image;
        AliasCount = (int)aliasCount;
        Kind = kind;
    }

    public string Directory { get; }
    public string Name { get; }
    public string? ImagePath { get; }
    public int AliasCount { get; }
    public BankKind Kind { get; }

    public string KindLabel => Kind switch
    {
        BankKind.Cv => "CV",
        BankKind.Vcv => "VCV",
        BankKind.Cvvc => "CVVC",
        _ => "未知",
    };

    public static Voicebank Open(string directory)
    {
        var handle = NativeMethods.nanofish_voicebank_open(directory);
        if (handle == IntPtr.Zero)
        {
            throw new NanoFishException(NfStatus.Io, NanoFish.LastError);
        }

        try
        {
            var status = (NfStatus)NativeMethods.nanofish_voicebank_info(handle, out var info);
            if (status != NfStatus.Ok)
            {
                throw new NanoFishException(status, NanoFish.LastError);
            }

            unsafe
            {
                var name = Encoding.UTF8.GetString(info.Name, 64).TrimEnd('\0');
                var image = Encoding.UTF8.GetString(info.Image, 128).TrimEnd('\0');
                return new Voicebank(handle, directory, name, string.IsNullOrEmpty(image) ? null : image,
                    info.AliasCount, (BankKind)info.Kind);
            }
        }
        catch
        {
            NativeMethods.nanofish_voicebank_free(handle);
            throw;
        }
    }

    /// <summary>按音素器解析当前歌词；没有匹配时返回 null。</summary>
    public string? Resolve(string? previousLyric, string lyric)
    {
        ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);
        var buffer = new byte[256];
        var status = NativeMethods.nanofish_voicebank_resolve(
            _handle, previousLyric, lyric, buffer, (nuint)buffer.Length);
        return status switch
        {
            0 => Encoding.UTF8.GetString(buffer).TrimEnd('\0'),
            1 => null,
            _ => null,
        };
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }
        NativeMethods.nanofish_voicebank_free(_handle);
        _handle = IntPtr.Zero;
    }
}
