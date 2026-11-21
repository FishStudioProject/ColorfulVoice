using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ColorfulVoice.Controls;
using NanoFish.Interop;

namespace ColorfulVoice;

public partial class MainWindow : Window
{
    private AudioBuffer? _buffer;
    private AudioBuffer? _rendered;
    private AudioPlayback? _player;
    private readonly Avalonia.Threading.DispatcherTimer _playTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private string? _ustPath;
    private string? _otoPath;
    private string? _voiceDir;
    private readonly List<PianoNote> _notes = new();

    public MainWindow()
    {
        InitializeComponent();

        Home.NewProject += OnNewProject;
        Home.ContinueProject += OnContinueProject;
        Home.ImportProject += async () => await PickAndOpenUst();

        BuildDemoNotes();
        Roll.Notes = _notes;
        Opened += (_, _) => Roll.FitToNotes();

        LoadEngineDemo();

        SizeChanged += (_, _) => UpdateResponsiveChrome();

        _playTimer.Tick += OnPlayTimer;

        Closed += (_, _) =>
        {
            _buffer?.Dispose();
            _buffer = null;
            Waveform.Buffer = null;
        };
    }

    /// <summary>先放一段 demo 旋律，等 UST 加载接进 UI 后再换成真实工程。</summary>
    private void BuildDemoNotes()
    {
        string[] lyrics = ["a", "i", "u", "e", "o", "a", "i", "u"];
        int[] pitches = [60, 62, 64, 65, 67, 69, 71, 72];
        for (var i = 0; i < lyrics.Length; i++)
        {
            _notes.Add(new PianoNote
            {
                Id = i,
                StartTick = i * 240L,
                LengthTicks = 240,
                NoteNum = pitches[i],
                Lyric = lyrics[i],
            });
        }
    }

    private bool _compactChrome;

    private void UpdateResponsiveChrome()
    {
        var compact = Bounds.Width > 0 && Bounds.Width < 780;
        BadgeText.IsVisible = Bounds.Width >= 820;
        if (compact == _compactChrome)
        {
            return;
        }
        _compactChrome = compact;
        HeaderGrid.RowDefinitions = new RowDefinitions(compact ? "Auto,Auto" : "Auto");
        Grid.SetRow(Toolbar, compact ? 1 : 0);
        Grid.SetColumn(Toolbar, compact ? 0 : 1);
        Grid.SetColumnSpan(Toolbar, compact ? 2 : 1);
        Toolbar.HorizontalAlignment = compact ? Avalonia.Layout.HorizontalAlignment.Left : Avalonia.Layout.HorizontalAlignment.Right;
        Toolbar.Margin = compact ? new Avalonia.Thickness(0, 8, 0, 0) : default;
        TitleBlock.Margin = compact ? new Avalonia.Thickness(0, 0, 0, 0) : default;
    }
    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        Editor.IsVisible = false;
        Home.IsVisible = true;
    }

    private void OnNewProject()
    {
        _notes.Clear();
        Roll.Notes = _notes;
        SubtitleText.Text = "未命名作品 · 0 个音符";
        StatusText.Text = "在卷帘空白处拖拽即可画音符";
        ShowEditor();
    }

    private void OnContinueProject()
    {
        if (_notes.Count == 0)
        {
            BuildDemoNotes();
            Roll.Notes = _notes;
        }
        SubtitleText.Text = $"继续项目 · {_notes.Count} 个音符";
        ShowEditor();
    }

    private void ShowEditor()
    {
        Home.IsVisible = false;
        Editor.IsVisible = true;
        Roll.FitToNotes();
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_ustPath is null || _otoPath is null || _voiceDir is null)
        {
            StatusText.Text = "没有可导出的音源路径";
            return;
        }
        try
        {
            _rendered ??= NanoFish.Interop.NanoFish.RenderUst(_ustPath, _otoPath, _voiceDir);
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出 WAV",
                SuggestedFileName = "novavoice-preview.wav",
                DefaultExtension = "wav",
            });
            if (file is null)
            {
                return;
            }
            var path = file.Path.LocalPath;
            WavWriter.Save(path, _rendered.Samples, (int)_rendered.SampleRate);
            StatusText.Text = $"已导出 {path}（{_rendered.Length / (double)_rendered.SampleRate:F1}s）";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"导出失败：{ex.Message}";
        }
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e) => TogglePlayback();

    private void TogglePlayback()
    {
        if (_player is { IsPlaying: true })
        {
            StopPlayback();
            return;
        }
        if (_ustPath is null || _otoPath is null || _voiceDir is null)
        {
            StatusText.Text = "当前工程没有音源路径（需要 UST 同级或 voicebank/ 下的 oto.ini）";
            return;
        }

        try
        {
            _rendered ??= NanoFish.Interop.NanoFish.RenderUst(_ustPath, _otoPath, _voiceDir);
            _player = new AudioPlayback();
            _player.Load(_rendered.Samples);
            _player.Start();
            _playTimer.Start();
            PlayButton.Content = "⏸ 停止";
            Roll.Seek(0);
            StatusText.Text = _player.IsNullBackend
                ? $"试听中（无可用声卡，null 后端；PCM {_rendered.Length:N0} 帧）"
                : $"试听中 · {_rendered.Length / (double)AudioPlayback.SampleRate:F1}s";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"试听失败：{ex.Message}";
            StopPlayback();
        }
    }

    private void OnPlayTimer(object? sender, EventArgs e)
    {
        if (_player is null)
        {
            return;
        }
        _player.Pump();
        var ticks = _player.PositionSeconds * (Roll.Tempo / 60.0) * 480.0;
        Roll.Seek((long)ticks);
        if (_player.Finished)
        {
            StopPlayback();
            StatusText.Text = "播放结束";
        }
    }

    private void StopPlayback()
    {
        _playTimer.Stop();
        _player?.Dispose();
        _player = null;
        PlayButton.Content = "▶ 预览";
    }

    private async void OnOpenUstClick(object? sender, RoutedEventArgs e) => await PickAndOpenUst();

    private async Task PickAndOpenUst()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开 UST 工程",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("UST 工程") { Patterns = new[] { "*.ust" } } },
        });
        if (files.Count > 0)
        {
            OpenUst(files[0].Path.LocalPath);
            ShowEditor();
        }
    }

    /// <summary>无头截图用：把播放头放到指定 tick。</summary>
    public void SeekPreview(long tick) => Roll.Seek(tick);

    /// <summary>从磁盘加载 UST（解析在 Rust 侧完成，经 FFI 取回扁平音符表）。</summary>
    public void OpenUst(string path)
    {
        try
        {
            var doc = UstLoader.Load(path);
            _notes.Clear();
            var id = 0;
            foreach (var n in doc.Notes)
            {
                _notes.Add(new PianoNote
                {
                    Id = id++,
                    StartTick = n.StartTick,
                    LengthTicks = n.LengthTicks,
                    NoteNum = n.NoteNum,
                    Lyric = n.IsRest ? "R" : n.Lyric,
                });
            }
            Roll.Tempo = Math.Max(20, doc.Tempo);
            _ustPath = path;
            var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!;
            var candidates = new[]
            {
                System.IO.Path.Combine(dir, "oto.ini"),
                System.IO.Path.Combine(dir, "voicebank", "oto.ini"),
            };
            _otoPath = candidates.FirstOrDefault(System.IO.File.Exists);
            _voiceDir = _otoPath is null ? null : System.IO.Path.GetDirectoryName(_otoPath);
            _rendered?.Dispose();
            _rendered = null;
            Roll.FitToNotes();
            SubtitleText.Text = $"{System.IO.Path.GetFileName(path)} · {doc.Notes.Count} 个音符 · Tempo {doc.Tempo:0.##}";
            StatusText.Text = _otoPath is null
                ? $"已加载 {doc.Notes.Count} 个音符；未找到 oto.ini，无法试听"
                : $"已加载 {doc.Notes.Count} 个音符；音源 {_voiceDir}";
            ShowEditor();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载失败：{ex.Message}";
        }
    }

    private void LoadEngineDemo()
    {
        try
        {
            // 宿主只持有 dispose 权；样本数据始终留在 Rust 分配的堆上。
            _buffer = NanoFish.Interop.NanoFish.GenerateSine(440f, 48_000, 1_200);
            Waveform.Buffer = _buffer;
            Waveform.VisibleSamples = 960;

            SubtitleText.Text = $"NanoFish {NanoFish.Interop.NanoFish.Version} · 8 个音符的卷帘 Demo";
            StatusText.Text =
                "拖动空白处画音符 · 拖音符移动 · 拖右边缘改时值 · 双指/滚轮缩放 · " +
                $"波形 {_buffer.Length:N0} 帧 @ {_buffer.SampleRate} Hz（零拷贝映射，RMS {_buffer.Rms():F4}）";
        }
        catch (NanoFishException ex)
        {
            StatusText.Text = $"引擎错误：{ex.Status} — {ex.Message}";
        }
        catch (DllNotFoundException)
        {
            StatusText.Text = "找不到 libnanofish.so：请先运行 scripts/build.sh 或设置 NANOFISH_LIB";
        }
    }
}