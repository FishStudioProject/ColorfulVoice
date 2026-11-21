using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ColorfulVoice.Views;

public partial class HomeView : UserControl
{
    public event Action? NewProject;
    public event Action? ContinueProject;
    public event Action? ImportProject;
    public event Action? VoicebankRequested;

    /// <summary>小于该宽度时切成移动端竖排布局。</summary>
    public const double CompactThreshold = 760;

    public bool IsCompact { get; private set; }

    public HomeView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateLayoutMode();
        AttachedToVisualTree += (_, _) => UpdateLayoutMode();
    }

    private void UpdateLayoutMode()
    {
        var compact = Bounds.Width > 0 && Bounds.Width < CompactThreshold;
        if (compact == IsCompact)
        {
            return;
        }
        IsCompact = compact;
        WideRoot.IsVisible = !compact;
        CompactRoot.IsVisible = compact;
    }

    private void OnNewClick(object? sender, RoutedEventArgs e) => NewProject?.Invoke();

    private void OnContinueClick(object? sender, RoutedEventArgs e) => ContinueProject?.Invoke();

    private void OnImportClick(object? sender, RoutedEventArgs e) => ImportProject?.Invoke();

    private void OnVoicebankClick(object? sender, RoutedEventArgs e) => VoicebankRequested?.Invoke();
}
