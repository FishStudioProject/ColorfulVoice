using System.Text;

namespace NanoFish.Interop;

/// <summary>把 PCM 写成 16-bit 单声道 WAV（导出试听用）。</summary>
public static class WavWriter
{
    public static void Save(string path, ReadOnlySpan<float> samples, int sampleRate)
    {
        using var stream = File.Create(path);
        using var w = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        var dataBytes = samples.Length * 2;

        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataBytes);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);          // PCM
        w.Write((short)1);          // mono
        w.Write(sampleRate);
        w.Write(sampleRate * 2);    // byte rate
        w.Write((short)2);          // block align
        w.Write((short)16);         // bits
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataBytes);

        foreach (var s in samples)
        {
            var v = (short)Math.Round(Math.Clamp(s, -1f, 1f) * 32767f);
            w.Write(v);
        }
    }
}
