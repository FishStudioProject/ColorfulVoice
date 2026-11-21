using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ColorfulVoice.Views;

public partial class LyricsWindow : UserControl
{
    /// <summary>浮层关闭时触发（true = 确认）。</summary>
    public event Action<bool>? Closed;

    public string[] Syllables { get; private set; } = Array.Empty<string>();

    public LyricsWindow()
    {
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        Syllables = (LyricsBox.Text ?? string.Empty)
            .Split(new[] { ' ', '\t', '\r', '\n', ',', '，', '。', '.', '/', '|', '、' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().Trim('"').Trim('\''))
            .Where(s => s.Length > 0)
            .ToArray();
        Closed?.Invoke(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Closed?.Invoke(false);
}
