using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Controls;
using NanoFish.Interop;

namespace ColorfulVoice.Views;

public partial class BankWindow : UserControl
{
    /// <summary>浮层关闭时触发（true = 确认）。</summary>
    public event Action<bool>? Closed;

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
        RefreshLibrary();
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

            var (sampleMissing, sampleTotal, sampleExample) = CheckSamples();
            SummaryText.Text = $"{_entries.Count} 条 · 工程缺失别名 {missing.Count} 个 · 采样缺失 {sampleMissing}/{sampleTotal}"
                + (sampleExample is null ? "" : $"（如 {sampleExample}）");
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
        var folders = await (TopLevel.GetTopLevel(this)?.StorageProvider ?? throw new InvalidOperationException("no storage"))!.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择音源库根目录",
            AllowMultiple = false,
        });
        if (folders.Count == 0)
        {
            return;
        }
        var root = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(root))
        {
            SummaryText.Text = "Android 请改用「导入音源文件夹…」";
            return;
        }
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

    private void RefreshLibrary()
    {
        var saved = Services.BankLibrary.Load();
        LibraryList.ItemsSource = saved.Select(e => new LibraryEntry(
            string.IsNullOrEmpty(e.Kind) ? e.Name : $"{e.Name}（{e.Kind} · {e.AliasCount} 别名）",
            e.OtoPath)).ToList();
        LibraryList.IsVisible = saved.Count > 0;
    }

    /// <summary>检查 oto 里引用的 wav 是否真的在音源目录里（大小写不敏感）。</summary>
    private (int Missing, int Total, string? Example) CheckSamples()
    {
        var dir = VoiceDir;
        if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir))
        {
            return (_entries.Count, _entries.Count, "目录不存在");
        }

        var files = System.IO.Directory
            .EnumerateFiles(dir, "*", System.IO.SearchOption.AllDirectories)
            .Select(f => System.IO.Path.GetRelativePath(dir, f))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = 0;
        string? example = null;
        foreach (var entry in _entries)
        {
            var name = entry.Wav.Replace('\\', '/').TrimStart('/');
            if (!files.Contains(name))
            {
                missing++;
                example ??= name;
            }
        }
        return (missing, _entries.Count, example);
    }

    /// <summary>导入 zip 音源：Android 上目录枚举不可靠时的兜底路径。</summary>
    private async void OnImportZipClick(object? sender, RoutedEventArgs e)
    {
        var files = await (TopLevel.GetTopLevel(this)?.StorageProvider
            ?? throw new InvalidOperationException("no storage")).OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择音源压缩包（zip）",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("zip") { Patterns = new[] { "*.zip" } } },
        });
        if (files.Count == 0)
        {
            return;
        }

        try
        {
            var zipPath = await Services.FileStaging.StageReadAsync(files[0]);
            var target = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "novavoice-banks",
                System.IO.Path.GetFileNameWithoutExtension(files[0].Name));
            System.IO.Directory.CreateDirectory(target);
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, target, overwriteFiles: true);

            var oto = System.IO.Directory
                .EnumerateFiles(target, "oto.ini", System.IO.SearchOption.AllDirectories)
                .FirstOrDefault();
            if (oto is null)
            {
                SummaryText.Text = "压缩包里没有 oto.ini";
                return;
            }

            SetBank(oto);
            Services.BankLibrary.Add(new Services.BankEntry
            {
                Name = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(oto) ?? oto),
                OtoPath = oto,
                AliasCount = _entries.Count,
                Kind = "zip 导入",
            });
            RefreshLibrary();
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"解压失败：{ex.Message}";
        }
    }

    private async void OnImportFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await (TopLevel.GetTopLevel(this)?.StorageProvider
            ?? throw new InvalidOperationException("no storage")).OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择 UTAU 音源文件夹（含 oto.ini 与 wav）",
            AllowMultiple = false,
        });
        if (folders.Count == 0)
        {
            return;
        }

        var (oto, copied, error) = await Services.VoicebankImporter.ImportAsync(folders[0]);
        if (oto is null)
        {
            SummaryText.Text = error ?? "导入失败";
            return;
        }
        SetBank(oto);
        Services.BankLibrary.Add(new Services.BankEntry
        {
            Name = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(oto) ?? oto),
            OtoPath = oto,
            AliasCount = _entries.Count,
            Kind = _entries.Count > 0 ? "已导入" : "空",
        });
        RefreshLibrary();
        if (copied > 0)
        {
            SummaryText.Text = (SummaryText.Text ?? "") + $"｜已复制 {copied} 个文件到缓存";
        }
    }

    private string? _voicebank;

    private async void OnPickClick(object? sender, RoutedEventArgs e)
    {
        if (OperatingSystem.IsAndroid())
        {
            // 只选 oto.ini 拿不到同目录的 wav，Android 上必须整目录导入
            SummaryText.Text = "Android 上请用「导入音源文件夹…」，只选 oto.ini 找不到采样文件";
            return;
        }

        var files = await (TopLevel.GetTopLevel(this)?.StorageProvider ?? throw new InvalidOperationException("no storage"))!.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 oto.ini",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("oto.ini") { Patterns = new[] { "oto.ini", "*.ini" } } },
        });
        if (files.Count > 0)
        {
            SetBank(await Services.FileStaging.StageReadAsync(files[0]));
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Closed?.Invoke(true);
}
