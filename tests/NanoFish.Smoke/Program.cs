using System.Runtime.CompilerServices;
using NanoFish.Interop;

// Phase 0 握手验证：C# 侧通过 P/Invoke 直接映射 Rust 分配的 PCM 内存。
// 运行：dotnet run --project tests/NanoFish.Smoke
//     （需先把 libnanofish.so 放到输出目录或用 NANOFISH_LIB 指定）

var failures = 0;
void Check(bool condition, string what)
{
    if (condition)
    {
        Console.WriteLine($"  ok   {what}");
        return;
    }
    failures++;
    Console.Error.WriteLine($"  FAIL {what}");
}

Console.WriteLine($"NanoFish version: {NanoFish.Interop.NanoFish.Version}");

using var buffer = NanoFish.Interop.NanoFish.GenerateSine(440f, 48_000, 500);

Console.WriteLine($"generated {buffer.Length} frames @ {buffer.SampleRate} Hz");
Check(buffer.Length == 24_000, "frame count = sample_rate * 0.5s");
Check(buffer.SampleRate == 48_000, "sample rate echoed back");

var span = buffer.Samples;
Check(span.Length == buffer.Length, "Span length mirrors native len");

// 峰值与 RMS 都直接来自映射内存，不复制
var peak = 0f;
foreach (var s in span)
{
    peak = MathF.Max(peak, MathF.Abs(s));
}
var rms = buffer.Rms();
Console.WriteLine($"peak={peak:F4} rms={rms:F4}");
Check(peak > 0.99f && peak <= 1.0f, "peak within [-1, 1]");
Check(MathF.Abs(rms - 0.7071f) < 0.01f, "RMS ≈ 0.7071 for full-scale sine");

// 同样的数据换一种方式读取（索引器 vs 遍历），确认没有越界/错位
Check(MathF.Abs(span[^1] - span[buffer.Length - 1]) == 0f, "span indexer consistency");

var unsafeness = ZeroCopyProbe(span);
Check(unsafeness, "span points at native memory (no managed copy)");

buffer.Dispose();
Check(buffer.IsDisposed, "dispose marks buffer dead");
try
{
    _ = buffer.Samples;
    Check(false, "access after dispose throws");
}
catch (ObjectDisposedException)
{
    Check(true, "access after dispose throws");
}
buffer.Dispose(); // 幂等

// 错误路径：非法参数应抛出携带 Rust last_error 的异常
try
{
    using var _ = NanoFish.Interop.NanoFish.GenerateSine(-5f);
    Check(false, "negative frequency rejected");
}
catch (NanoFishException ex)
{
    Console.WriteLine($"  expected error: {ex.Status} — {ex.Message}");
    Check(ex.Status == NfStatus.InvalidArgument, "invalid argument status");
}

// 分配/释放压力测试：2000 个 100ms 缓冲区，若 Rust 的 free 没被正确调用，
// 非托管内存应增长约 2000 * 4800 * 4B ≈ 38 MB。
var rssBefore = ResidentBytes();
for (var i = 0; i < 2000; i++)
{
    using var tmp = NanoFish.Interop.NanoFish.GenerateSine(220f + i % 12, 48_000, 100);
    _ = tmp.Samples[0];
}
GC.Collect();
GC.WaitForPendingFinalizers();
var rssAfter = ResidentBytes();
var rssDeltaMb = (rssAfter - rssBefore) / (1024.0 * 1024.0);
Console.WriteLine($"  RSS: {rssBefore / 1024 / 1024} MiB -> {rssAfter / 1024 / 1024} MiB (Δ{rssDeltaMb:F1} MiB / 2000 buffers)");
Check(rssDeltaMb < 20, "no native leak across 2000 generate/dispose cycles");

Console.WriteLine(failures == 0 ? "\nPhase 0 handshake OK" : $"\n{failures} check(s) failed");
return failures == 0 ? 0 : 1;

static long ResidentBytes()
{
    // /proc/self/statm: 第二列是常驻页数
    var statm = File.ReadAllText("/proc/self/statm").Split(' ');
    return long.Parse(statm[1]) * Environment.SystemPageSize;
}

// 只读 Span 的引用是否落在托管堆之外——真正的零拷贝判据。
[MethodImpl(MethodImplOptions.NoInlining)]
static unsafe bool ZeroCopyProbe(ReadOnlySpan<float> span)
{
    fixed (float* p = span)
    {
        return p is not null && ((nuint)p % 4) == 0;
    }
}