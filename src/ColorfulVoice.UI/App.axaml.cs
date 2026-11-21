using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ColorfulVoice.Views;
using Avalonia.Markup.Xaml;

namespace ColorfulVoice;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // Android / 移动端：没有窗口概念，界面挂在单视图上
            singleView.MainView = new MainView();
        }

        base.OnFrameworkInitializationCompleted();
    }
}