using Avalonia.Controls;
using SukiUI.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NanoFish.Interop;

namespace ColorfulVoice.Views;

public partial class BankWindow : SukiWindow
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

    /// <summary>扫描一个音源库根目录：根目录与一层子目录里含 oto.ini 的都算音源。</summary>
    private async void OnScanLibraryClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择音源库根目录",
            AllowMultiple = false,
        });
        if (folders.Count == 0)
        {
            return;
        }
        var root = folders[0].Path.LocalPath;
        var banks = new List<LibraryEntry>();
        void Add(string dir)
        {
            var oto = System.IO.Path.Combine(dir, "oto.ini");
            if (System.IO.File.Exists(oto))
            {
                banks.Add(new LibraryEntry(System.IO.Path.GetFileName(dir), oto));
            }
        }
        Add(root);
        try
        {
            foreach (var dir in System.IO.Directory.EnumerateDirectories(root))
            {
                Add(dir);
            }
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"扫描失败：{ex.Message}";
            return;
        }

        LibraryList.ItemsSource = banks;
        LibraryList.IsVisible = banks.Count > 0;
        SummaryText.Text = banks.Count > 0 ? $"音源库：找到 {banks.Count} 个音源，点击切换" : "该目录下没有找到音源";
    }

    private void OnLibrarySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LibraryList.SelectedItem is LibraryEntry entry)
        {
            SetBank(entry.OtoPath);
            SummaryText.Text = $"{SummaryText.Text}｜已切换到 {entry.Name}";
        }
    }

    public sealed record LibraryEntry(string Name, string OtoPath);

    private async void OnImportFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择 UTAU 音源文件夹（含 oto.ini）",
            AllowMultiple = false,
        });
        if (folders.Count == 0)
        {
            return;
        }
        var dir = folders[0].Path.LocalPath;
        try
        {
            using var bank = Voicebank.Open(dir);
            _voicebank = bank.Name;
            SetBank(System.IO.Path.Combine(dir, "oto.ini"));
            SummaryText.Text += $"  ·  音源「{bank.Name}」（{bank.KindLabel}，{bank.AliasCount} 个别名）";

            var preview = new List<string>();
            string? prev = null;
            foreach (var lyric in _projectLyrics.Take(8))
            {
                var alias = bank.Resolve(prev, lyric);
                preview.Add($"{lyric}→{alias ?? "∅"}");
                prev = lyric;
            }
            PhonemeText.Text = preview.Count == 0
                ? "音素器：工程里还没有歌词"
                : "音素预览：" + string.Join("  ·  ", preview);
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"导入失败：{ex.Message}";
        }
    }

    private string? _voicebank;

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
