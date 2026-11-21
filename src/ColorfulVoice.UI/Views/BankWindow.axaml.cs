using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NanoFish.Interop;

namespace ColorfulVoice.Views;

public partial class BankWindow : Window
{
    private readonly List<string> _projectLyrics;
    private List<OtoEntryData> _entries = new();

    /// <summary>用户选择的 oto.ini（取消/未选时为 null）。</summary>
    public string? OtoPath { get; private set; }

    public string? VoiceDir { get; private set; }

    public BankWindow()
    {
        _projectLyrics = new List<string>();
        InitializeComponent();
    }

    public BankWindow(string? currentOtoPath, IEnumerable<string> lyrics) : this()
    {
        _projectLyrics = lyrics.Where(l => !string.IsNullOrWhiteSpace(l) && l != "R").Distinct().ToList();
        if (!string.IsNullOrEmpty(currentOtoPath) && System.IO.File.Exists(currentOtoPath))
        {
            SetBank(currentOtoPath);
        }
    }

    public void SetBank(string otoPath)
    {
        try
        {
            _entries = OtoLoader.Load(otoPath).ToList();
            OtoPath = otoPath;
            VoiceDir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(otoPath));

            PathText.Text = otoPath;
            var aliases = _entries.Select(e => e.Alias).ToHashSet();
            var missing = _projectLyrics.Where(l => !aliases.Contains(l)).ToList();

            SummaryText.Text = $"{_entries.Count} 条 · 工程需要 {_projectLyrics.Count} 个别名 · 缺失 {missing.Count} 个";
            MissingTitle.Text = missing.Count == 0 ? "当前工程没有缺失别名 ✓" : $"当前工程缺失 {missing.Count} 个别名";
            EntriesList.ItemsSource = _entries;
            MissingList.ItemsSource = missing;
        }
        catch (Exception ex)
        {
            PathText.Text = otoPath;
            SummaryText.Text = $"读取失败：{ex.Message}";
        }
    }

    private async void OnPickClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 oto.ini",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("oto.ini") { Patterns = new[] { "oto.ini", "*.ini" } } },
        });
        if (files.Count > 0)
        {
            SetBank(files[0].Path.LocalPath);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
