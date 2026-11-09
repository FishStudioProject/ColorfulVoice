using Avalonia;

namespace ColorfulVoice;

internal static class Program
{
    // Avalonia 初始化前不要使用任何依赖 SynchronizationContext 的 API。
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // 供设计器与后续移动端（Android/iOS）入口复用。
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}