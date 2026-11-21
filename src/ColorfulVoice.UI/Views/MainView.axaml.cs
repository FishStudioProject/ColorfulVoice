using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ColorfulVoice.Controls;
using NanoFish.Interop;

namespace ColorfulVoice.Views;

public partial class MainView : UserControl
{
    private AudioBuffer? _buffer;
    private AudioBuffer? _rendered;
    private AudioPlayback? _player;
    private readonly Avalonia.Threading.DispatcherTimer _playTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private string? _ustPath;
    private string? _otoPath;
    private string? _voiceDir;
    private readonly List<PianoNote> _notes = new();
    private readonly Dictionary<int, string> _extras = new();

    private IStorageProvider StorageProvider =>
        TopLevel.GetTopLevel(this)?.StorageProvider
        ?? throw new InvalidOperationException("没有可用的 StorageProvider");

    public MainView()
    {
        InitializeComponent();

        Home.NewProject += OnNewProject;
        Home.ContinueProject += OnContinueProject;
        Home.ImportProject += async () => await PickAndOpenUst();
        Home.VoicebankRequested += () => OnBankClick(this, new RoutedEventArgs());

        BuildDemoNotes();
        Roll.Notes = _notes;
        Roll.NoteDoubleClicked += OnNoteDoubleClicked;
        UpdateHeaderInfo();
        AttachedToVisualTree += (_, _) => Roll.FitToNotes();

        LoadEngineDemo();

        SizeChanged += (_, _) => UpdateResponsiveChrome();

        _playTimer.Tick += OnPlayTimer;

        DetachedFromVisualTree += (_, _) =>
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
        if (compact)
        {
            Toolbar.Classes.Add("compact");
        }
        else
        {
            Toolbar.Classes.Remove("compact");
        }

        // 窄屏只留"主页 / 预览 / ⋯"
        MoreButton.IsVisible = true;
        SaveButton.IsVisible = !compact;
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
        UpdateHeaderInfo();
        StatusText.Text = "在卷帘空白处拖拽即可画音符";
        ShowEditor();
    }

    private async void OnContinueProject()
    {
        var window = new ProjectsWindow();
        window.OpenRequested += path =>
        {
            OpenUst(path);
            ShowEditor();
        };
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await window.ShowDialog(owner);
        }
        else
        {
            StatusText.Text = "移动端暂不支持项目列表弹窗";
        }
    }

    private void ShowEditor()
    {
        Home.IsVisible = false;
        Editor.IsVisible = true;
        Roll.FitToNotes();
        UpdateHeaderInfo();
    }

    private async void OnBankClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new Views.BankWindow(_otoPath, _notes.Select(n => n.Lyric));
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            StatusText.Text = "移动端暂不支持弹窗式音源管理器";
            return;
        }
        if (dialog.OtoPath is not null && dialog.OtoPath != _otoPath)
        {
            _otoPath = dialog.OtoPath;
            _voiceDir = dialog.VoiceDir;
            _rendered?.Dispose();
            _rendered = null;
            UpdateHeaderInfo();
            StatusText.Text = $"已切换音源：{System.IO.Path.GetFileName(_voiceDir)}";
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "保存 UST",
                SuggestedFileName = _ustPath is null ? "untitled.ust" : System.IO.Path.GetFileName(_ustPath),
                DefaultExtension = "ust",
            });
            if (file is null)
            {
                return;
            }

            var notes = _notes
                .OrderBy(n => n.StartTick)
                .Select(n => new UstNoteData
                {
                    StartTick = (uint)n.StartTick,
                    LengthTicks = (uint)n.LengthTicks,
                    NoteNum = n.NoteNum,
                    Lyric = n.Lyric,
                    IsRest = n.Lyric == "R",
                    Extras = WithVelocity(
                        PitchCurve.ReplaceLines(_extras.GetValueOrDefault(n.Id, string.Empty), n.Curve),
                        n.Velocity, n.VibratoDepth, n.VibratoPeriod),
                })
                .ToList();

            UstWriter.Save(file.Path.LocalPath, (float)Roll.Tempo, notes);
            UpdateHeaderInfo();
            StatusText.Text = $"已保存 {System.IO.Path.GetFileName(file.Path.LocalPath)} · {notes.Count} 个音符";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"保存失败：{ex.Message}";
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
    /// <summary>刷新顶栏的音源名与工程信息。</summary>
    private void UpdateHeaderInfo()
    {
        var bank = "未选择音源";
        if (!string.IsNullOrEmpty(_voiceDir))
        {
            bank = System.IO.Path.GetFileName(_voiceDir.TrimEnd('/', '\\'));
            try
            {
                using var vb = NanoFish.Interop.Voicebank.Open(_voiceDir!);
                if (!string.IsNullOrWhiteSpace(vb.Name) && vb.Name != "未命名音源")
                {
                    bank = vb.Name;
                }
            }
            catch
            {
                // 音源信息只是展示，失败就退回目录名
            }
        }
        else if (!string.IsNullOrEmpty(_otoPath))
        {
            bank = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(_otoPath) ?? _otoPath);
        }
        BankChipText.Text = bank;
        InfoText.Text = $"{_notes.Count} 个音符 · Tempo {Roll.Tempo:0}";
    }

    /// <summary>把 Velocity 写进 UST 附加行（去掉旧的同名行，避免重复）。</summary>
    private static string WithVelocity(string extras, int velocity, double vibratoDepth, double vibratoPeriod)
    {
        var lines = extras
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !l.TrimStart().StartsWith("Velocity=", StringComparison.OrdinalIgnoreCase)
                        && !l.TrimStart().StartsWith("VBR=", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (velocity != 100)
        {
            lines.Add($"Velocity={velocity}");
        }
        if (vibratoDepth > 0)
        {
            // VBR=length,period,depth,in,out,shift,drift
            lines.Add($"VBR={vibratoPeriod:0},{vibratoPeriod:0},{vibratoDepth:0},20,20,0,0");
        }
        return string.Join("\n", lines);
    }

    private async void OnNoteDoubleClicked(PianoNote note)
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                StatusText.Text = "移动端暂不支持属性面板，请用桌面版编辑";
                return;
            }
            var dialog = new Views.NotePropertiesWindow(note);
            var ok = await dialog.ShowDialog<bool>(owner);
            if (ok)
            {
                Roll.InvalidateVisual();
                StatusText.Text = $"音符 #{note.Id} 已更新（{note.Lyric} · {note.NoteNum} · {note.LengthTicks} tick · vel {note.Velocity}）";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"打开属性失败：{ex.Message}";
        }
    }

    private void OnBreathToggle(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var on = (sender as MenuItem)?.IsChecked == true;
        NanoFish.Interop.NanoFish.SetBreathAlias(on ? "br" : null);
        StatusText.Text = on ? "呼吸音已开启（休止处插 br）" : "呼吸音已关闭";
    }

    private async void OnLyricsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                StatusText.Text = "移动端暂不支持批量歌词弹窗";
                return;
            }
            var dialog = new Views.LyricsWindow();
            if (!await dialog.ShowDialog<bool>(owner) || dialog.Syllables.Length == 0)
            {
                return;
            }
            var targets = _notes.Where(n => !n.Lyric.Equals("R", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n.StartTick).ToList();
            var written = 0;
            foreach (var note in targets)
            {
                if (written >= dialog.Syllables.Length)
                {
                    break;
                }
                note.Lyric = dialog.Syllables[written++];
            }
            Roll.InvalidateVisual();
            StatusText.Text = $"已填入 {written} 个音符的歌词";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"批量歌词失败：{ex.Message}";
        }
    }

    public void SeekPreview(long tick) => Roll.Seek(tick);

    /// <summary>无头截图用：选中第 N 个音符（展示音高曲线把手）。</summary>
    public void SelectPreview(int index)
    {
        if (index >= 0 && index < _notes.Count)
        {
            Roll.SelectNote(_notes[index]);
        }
    }

    /// <summary>从磁盘加载 UST（解析在 Rust 侧完成，经 FFI 取回扁平音符表）。</summary>
    public void OpenUst(string path)
    {
        try
        {
            var doc = UstLoader.Load(path);
            _notes.Clear();
            _extras.Clear();
            var id = 0;
            foreach (var n in doc.Notes)
            {
                _extras[id] = n.Extras;
                _notes.Add(new PianoNote
                {
                    Id = id++,
                    StartTick = n.StartTick,
                    LengthTicks = n.LengthTicks,
                    NoteNum = n.NoteNum,
                    Lyric = n.IsRest ? "R" : n.Lyric,
                    Curve = n.Curve?.Clone(),
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
            UpdateHeaderInfo();
            StatusText.Text = _otoPath is null
                ? $"已加载 {doc.Notes.Count} 个音符 · 未找到 oto.ini，无法试听"
                : $"已加载 {doc.Notes.Count} 个音符 · 双击空白添加音符";
            RecentProjects.Add(path);
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

            UpdateHeaderInfo();
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