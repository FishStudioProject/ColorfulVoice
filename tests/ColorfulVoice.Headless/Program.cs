using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using ColorfulVoice;
using ColorfulVoice.Controls;

// 无显示器环境下的 UI 验证：真 Skia 渲染一帧，存成 PNG。
// 用法: dotnet run -c Release --project tests/ColorfulVoice.Headless -- out.png
if (args.Contains("--self-test"))
{
    return SelfTest();
}

if (args.Contains("--font-test"))
{
    return FontTest();
}

if (args.Contains("--bank-test"))
{
    return BankTest(args);
}

if (args.Contains("--bank-window"))
{
    return BankShot(args);
}

if (args.Contains("--pitch-test"))
{
    return PitchTest();
}

if (args.Contains("--save-test"))
{
    return SaveTest();
}

if (args.Contains("--audio-test"))
{
    return AudioTest();
}

var path = args.Length > 0 ? args[0] : "phase0-window.png";

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();

var width = 1080.0;
var height = 640.0;
var sizeIndex = Array.IndexOf(args, "--size");
if (sizeIndex >= 0 && sizeIndex + 1 < args.Length)
{
    var parts = args[sizeIndex + 1].Split('x');
    if (parts.Length == 2 && double.TryParse(parts[0], out var w) && double.TryParse(parts[1], out var h))
    {
        width = w;
        height = h;
    }
}
var window = new MainWindow { Width = width, Height = height };
window.Show();
if (args.Length > 1)
{
    window.OpenUst(args[1]);
}
if (args.Length > 2 && long.TryParse(args[2], out var seekTick))
{
    window.SeekPreview(seekTick);
}
var selectIndex = Array.IndexOf(args, "--select");
if (selectIndex >= 0 && selectIndex + 1 < args.Length && int.TryParse(args[selectIndex + 1], out var noteIndex))
{
    window.SelectPreview(noteIndex);
}

// 推进布局 + 渲染定时器，确保绘制真的发生
Dispatcher.UIThread.RunJobs();
AvaloniaHeadlessPlatform.ForceRenderTimerTick();
Dispatcher.UIThread.RunJobs();

var frame = window.CaptureRenderedFrame();
if (frame is null)
{
    Console.Error.WriteLine("渲染失败：CaptureRenderedFrame 返回 null");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
frame.Save(path);
Console.WriteLine($"已保存 {Path.GetFullPath(path)} ({frame.PixelSize.Width}x{frame.PixelSize.Height})");
return 0;
static int SelfTest()
{
    var failures = 0;
    void Check(bool ok, string what)
    {
        Console.WriteLine($"  {(ok ? "ok  " : "FAIL")} {what}");
        if (!ok) failures++;
    }

    var layout = new PianoRollLayout
    {
        PixelsPerTick = 0.2,
        RowHeight = 20,
        KeyboardWidth = 64,
        TopNote = 84,
        SnapTicks = 120,
        ViewWidth = 900,
        ViewHeight = 600,
    };

    Check(layout.XToTick(layout.TickToX(480)) == 480, "tick -> x -> tick 往返");
    Check(layout.YToNote(layout.NoteToY(60)) == 60, "note -> y -> note 往返");
    Check(layout.Snap(130) == 120 && layout.Snap(190) == 240, "1/4 拍吸附");

    var notes = new List<PianoNote>
    {
        new() { Id = 1, StartTick = 0, LengthTicks = 240, NoteNum = 60, Lyric = "a" },
    };
    var inside = layout.HitTest(notes, 70, 490);
    Check(ReferenceEquals(inside.Note, notes[0]) && !inside.Resize, "命中音符本体");
    var edge = layout.HitTest(notes, 108, 490);
    Check(ReferenceEquals(edge.Note, notes[0]) && edge.Resize, "命中右边缘 = 改时值");
    Check(layout.HitTest(notes, 400, 100).Note is null, "空白处不命中");

    var before = layout.XToTick(160);
    layout.ZoomAt(160, 2.0);
    Check(layout.XToTick(160) == before, "缩放时光标下的 tick 不变");
    Check(layout.PixelsPerTick > 0.2, "缩放确实放大了");

    // 120 BPM：1 拍 = 0.5s = 480 tick
    // 双指平移：手指向右下拖 → 内容跟随 → 滚动量减小
    layout.ScrollX = 100;
    layout.ScrollY = 100;
    layout.PanBy(40, 30);
    Check(Math.Abs(layout.ScrollX - 60) < 0.001 && Math.Abs(layout.ScrollY - 70) < 0.001, "双指平移跟随位移");
    layout.PanBy(1000, 1000);
    Check(layout.ScrollX == 0 && layout.ScrollY == 0, "平移夹在 0 以上");

    // 触控热区：右边缘 16px 内都算改时值
    Check(PianoRollLayout.ResizeHandleWidth >= 16, "改时值热区 ≥16px");

    Check(Math.Abs(PianoRollLayout.TicksForMs(500, 120) - 480) < 0.5, "播放头推进：500ms @ 120BPM = 480 tick");
    Check(Math.Abs(PianoRollLayout.TicksForMs(1000, 60) - 480) < 0.5, "播放头推进：1s @ 60BPM = 480 tick");

    Console.WriteLine(failures == 0 ? "钢琴卷帘几何自检通过" : $"{failures} 项失败");
    return failures == 0 ? 0 : 1;
}

static int AudioTest()
{
    var root = "/root/workspace/NovaVoice/NanoFish/tests/data";
    var ust = Path.Combine(root, "demo.ust");
    var oto = Path.Combine(root, "voicebank", "oto.ini");
    var dir = Path.Combine(root, "voicebank");

    using var buffer = NanoFish.Interop.NanoFish.RenderUst(ust, oto, dir);
    Console.WriteLine($"渲染: {buffer.Length} 帧 @ {buffer.SampleRate} Hz (RMS {buffer.Rms():F3})");

    using var player = new NanoFish.Interop.AudioPlayback();
    Console.WriteLine($"后端: {(player.IsNullBackend ? "null（无声卡）" : "真实设备")}");
    player.Load(buffer.Samples);
    player.Start();
    Thread.Sleep(400);
    player.Pump();
    Console.WriteLine($"已播: {player.PositionSeconds:F3}s  队列: {player.QueuedFrames} 帧");

    if (player.PositionSeconds <= 0.01)
    {
        Console.Error.WriteLine("播放位置没有推进");
        return 1;
    }
    Console.WriteLine("音频链路 OK");
    return 0;
}

static int SaveTest()
{
    var src = "/root/workspace/NovaVoice/NanoFish/tests/data/handshake.ust";
    var dst = "/tmp/ui-roundtrip.ust";
    var doc = NanoFish.Interop.UstLoader.Load(src);
    Console.WriteLine($"加载: {doc.Notes.Count} 个音符, tempo {doc.Tempo}");
    NanoFish.Interop.UstWriter.Save(dst, doc.Tempo, doc.Notes);
    var back = NanoFish.Interop.UstLoader.Load(dst);
    Console.WriteLine($"重载: {back.Notes.Count} 个音符");

    var ok = back.Notes.Count == doc.Notes.Count
             && back.Notes.Any(n => n.Extras.Contains("PBS="))
             && back.Notes.Any(n => n.Extras.Contains("Envelope="))
             && back.Notes[0].Lyric == "か";
    Console.WriteLine(ok ? "保存往返 OK（PBS/Envelope 保留）" : "保存往返失败");
    return ok ? 0 : 1;
}

static int BankShot(string[] args)
{
    var outPath = args[0];
    var oto = args[Array.IndexOf(args, "--bank-window") + 1];

    AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .WithInterFont()
        .SetupWithoutStarting();

    var lyrics = new[] { "- か", "a か", "i か", "x 缺失" };
    var window = new ColorfulVoice.Views.BankWindow(oto, lyrics) { Width = 860, Height = 580 };
    window.Show();
    Dispatcher.UIThread.RunJobs();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    Dispatcher.UIThread.RunJobs();

    var frame = window.CaptureRenderedFrame();
    if (frame is null)
    {
        Console.Error.WriteLine("渲染失败");
        return 1;
    }
    frame.Save(outPath);
    Console.WriteLine($"已保存 {outPath}");
    return 0;
}

static int PitchTest()
{
    const string extras = "Envelope=0,5,35,0,100,100,0,%\nPBS=-40;0.5\nPBW=60,90\nPBY=0.5,-0.3\nPBM=,r";
    var curve = NanoFish.Interop.PitchCurve.FromExtras(extras);
    if (curve is null || curve.Points.Count != 3)
    {
        Console.Error.WriteLine("解析失败");
        return 1;
    }
    var ok = Math.Abs(curve.Points[0].X + 40) < 0.001
             && Math.Abs(curve.Points[2].X - 110) < 0.001
             && Math.Abs(curve.Points[2].Y + 0.3) < 0.001
             && curve.Points[1].Shape == 'r';

    var rebuilt = NanoFish.Interop.PitchCurve.ReplaceLines(extras, curve);
    var again = NanoFish.Interop.PitchCurve.FromExtras(rebuilt);
    ok &= again is not null && again.Points.Count == 3 && Math.Abs(again.Points[1].X - 20) < 0.001;
    ok &= rebuilt.Contains("Envelope=");

    Console.WriteLine(ok ? "音高曲线往返 OK" : $"音高曲线往返失败:\n{rebuilt}");
    return ok ? 0 : 1;
}

static int BankTest(string[] args)
{
    var dir = args[Array.IndexOf(args, "--bank-test") + 1];
    using var bank = NanoFish.Interop.Voicebank.Open(dir);
    Console.WriteLine($"音源: {bank.Name} ({bank.KindLabel}) aliases={bank.AliasCount}");

    string? prev = null;
    foreach (var lyric in new[] { "a", "i", "u", "e", "o" })
    {
        Console.WriteLine($"  {lyric} -> {bank.Resolve(prev, lyric) ?? "(none)"}");
        prev = lyric;
    }
    return 0;
}

static int FontTest()
{
    AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .WithInterFont()
        .SetupWithoutStarting();

    var gsf = new Avalonia.Media.FontFamily("avares://ColorfulVoice.UI/Assets/Fonts#Google Sans Flex");
    var ok = Avalonia.Media.FontManager.Current.TryGetGlyphTypeface(new Avalonia.Media.Typeface(gsf), out var glyph);
    Console.WriteLine($"embedded: {(ok ? glyph.FamilyName : "NOT FOUND")}");

    // 比较默认字体与 GSF 的排版宽度，判断是否真的生效
    double Measure(Avalonia.Media.FontFamily? family)
    {
        var tf = new Avalonia.Media.Typeface(family);
        var ft = new Avalonia.Media.FormattedText("ColorfulVoice 0123456789", System.Globalization.CultureInfo.InvariantCulture,
            Avalonia.Media.FlowDirection.LeftToRight, tf, 22, Avalonia.Media.Brushes.Black);
        return ft.Width;
    }
    var d = Measure(null);
    var g = Measure(gsf);
    Console.WriteLine($"default width={d:F2}  gsf width={g:F2}  same={Math.Abs(d-g)<0.5}");
    return ok ? 0 : 1;
}
