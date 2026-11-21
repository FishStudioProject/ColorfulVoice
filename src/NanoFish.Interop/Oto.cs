using System.Runtime.InteropServices;
using System.Text;

namespace NanoFish.Interop;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NfOtoEntryNative
{
    public fixed byte Alias[64];
    public fixed byte Wav[128];
    public float OffsetMs;
    public float ConsonantMs;
    public float CutoffMs;
    public float PreutteranceMs;
    public float OverlapMs;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NfOtoBankNative
{
    public IntPtr Entries;
    public UIntPtr Count;
    public UIntPtr Capacity;
}

public sealed class OtoEntryData
{
    public string Alias { get; init; } = string.Empty;
    public string Wav { get; init; } = string.Empty;
    public float OffsetMs { get; init; }
    public float ConsonantMs { get; init; }
    public float CutoffMs { get; init; }
    public float PreutteranceMs { get; init; }
    public float OverlapMs { get; init; }

    public string Summary =>
        $"offset {OffsetMs:0} · cons {ConsonantMs:0} · cutoff {CutoffMs:0} · pre {PreutteranceMs:0} · ovl {OverlapMs:0}";
}

public static class OtoLoader
{
    public static IReadOnlyList<OtoEntryData> Load(string path)
    {
        var status = (NfStatus)NativeMethods.nanofish_load_oto(path, out var native);
        if (status != NfStatus.Ok)
        {
            throw new NanoFishException(status, NanoFish.LastError);
        }

        try
        {
            var count = checked((int)native.Count);
            var list = new List<OtoEntryData>(count);
            unsafe
            {
                var ptr = (NfOtoEntryNative*)native.Entries;
                for (var i = 0; i < count; i++)
                {
                    var e = ptr[i];
                    list.Add(new OtoEntryData
                    {
                        Alias = Encoding.UTF8.GetString(e.Alias, 64).TrimEnd('\0'),
                        Wav = Encoding.UTF8.GetString(e.Wav, 128).TrimEnd('\0'),
                        OffsetMs = e.OffsetMs,
                        ConsonantMs = e.ConsonantMs,
                        CutoffMs = e.CutoffMs,
                        PreutteranceMs = e.PreutteranceMs,
                        OverlapMs = e.OverlapMs,
                    });
                }
            }
            return list;
        }
        finally
        {
            NativeMethods.nanofish_free_oto(ref native);
        }
    }
}
