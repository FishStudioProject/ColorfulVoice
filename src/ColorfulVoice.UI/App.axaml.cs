using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ColorfulVoice.Views;
using Avalonia.Markup.Xaml;
using SukiUI.Controls;

namespace ColorfulVoice;

public partial class App : Application
{
    public override void Initialize()
    {
        Trace("App.Initialize start");
        try
        {
            AvaloniaXamlLoader.Load(this);
            Trace("App.Initialize done");
        }
        catch (Exception ex)
        {
            Trace("App.Initialize failed: " + ex);
            throw;
        }
    }

    /// <summary>宿主可注入的启动追踪钩子（Android 侧写入日志文件）。</summary>
    public static Action<string>? TraceHook;

    private static void Trace(string message)
    {
        try
        {
            TraceHook?.Invoke(message);
        }
        catch
        {
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Trace("OnFrameworkInitialization start");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // Android / 移动端：没有窗口概念，界面挂在单视图上
            singleView.MainView = new MainView();
        }

        Trace("MainView assigned");
        base.OnFrameworkInitializationCompleted();
    }
}