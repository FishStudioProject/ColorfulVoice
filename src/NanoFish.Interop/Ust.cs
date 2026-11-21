using System.Runtime.InteropServices;
using System.Text;

namespace NanoFish.Interop;

/// <summary>与 Rust 侧 `NfNote` 对应（歌词为定长 64 字节 UTF-8）。</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NfNoteNative
{
    public uint StartTick;
    public uint LengthTicks;
    public int NoteNum;
    public uint Flags;
    public fixed byte Lyric[64];
}

/// <summary>与 Rust 侧 `NfUstData` 对应。</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NfUstDataNative
{
    public IntPtr Notes;
    public UIntPtr NoteCount;
    public UIntPtr Capacity;
    public float Tempo;
}

public sealed class UstNoteData
{
    public uint StartTick { get; init; }
    public uint LengthTicks { get; init; }
    public int NoteNum { get; init; }
    public bool IsRest { get; init; }
    public string Lyric { get; init; } = string.Empty;
}

public sealed class UstDocument
{
    public float Tempo { get; init; }
    public IReadOnlyList<UstNoteData> Notes { get; init; } = Array.Empty<UstNoteData>();
}

/// <summary>通过 FFI 读取 UST 工程（解析始终发生在 Rust 侧）。</summary>
public static class UstLoader
{
    public static UstDocument Load(string path)
    {
        var status = (NfStatus)NativeMethods.nanofish_load_ust(path, out var native);
        if (status != NfStatus.Ok)
        {
            throw new NanoFishException(status, NanoFish.LastError);
        }

        try
        {
            var count = checked((int)native.NoteCount);
            var notes = new List<UstNoteData>(count);
            unsafe
            {
                var ptr = (NfNoteNative*)native.Notes;
                for (var i = 0; i < count; i++)
                {
                    var n = ptr[i];
                    var lyric = Encoding.UTF8.GetString(n.Lyric, 64).TrimEnd('\0');
                    notes.Add(new UstNoteData
                    {
                        StartTick = n.StartTick,
                        LengthTicks = n.LengthTicks,
                        NoteNum = n.NoteNum,
                        IsRest = (n.Flags & 1) != 0,
                        Lyric = lyric,
                    });
                }
            }
            return new UstDocument { Tempo = native.Tempo, Notes = notes };
        }
        finally
        {
            NativeMethods.nanofish_free_ust(ref native);
        }
    }
}
