using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SukiUI.Controls;

namespace ColorfulVoice.Views;

public partial class LyricsWindow : SukiWindow
{
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
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
