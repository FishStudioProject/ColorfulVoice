using Avalonia.Controls;
using SukiUI.Controls;
using ColorfulVoice.Views;

namespace ColorfulVoice;

/// <summary>桌面壳：真正的界面在 MainView，Android 单视图宿主直接复用它。</summary>
public partial class MainWindow : SukiWindow
{
    public MainWindow() => InitializeComponent();

    private MainView View => (MainView)Content!;

    public void OpenUst(string path) => View.OpenUst(path);

    public void SeekPreview(long tick) => View.SeekPreview(tick);

    public void SelectPreview(int index) => View.SelectPreview(index);
}
