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

var path = args.Length > 0 ? args[0] : "phase0-window.png";

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();

var window = new MainWindow { Width = 1080, Height = 640 };
window.Show();

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

    Console.WriteLine(failures == 0 ? "钢琴卷帘几何自检通过" : $"{failures} 项失败");
    return failures == 0 ? 0 : 1;
}
