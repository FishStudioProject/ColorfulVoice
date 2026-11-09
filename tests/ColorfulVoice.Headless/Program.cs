using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using ColorfulVoice;

// 无显示器环境下的 UI 验证：真 Skia 渲染一帧，存成 PNG。
// 用法: dotnet run -c Release --project tests/ColorfulVoice.Headless -- out.png
var path = args.Length > 0 ? args[0] : "phase0-window.png";

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();

var window = new MainWindow { Width = 960, Height = 420 };
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