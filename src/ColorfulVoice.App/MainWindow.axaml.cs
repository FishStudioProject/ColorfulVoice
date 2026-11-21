using Avalonia.Controls;
using ColorfulVoice.Controls;
using NanoFish.Interop;

namespace ColorfulVoice;

public partial class MainWindow : Window
{
    private AudioBuffer? _buffer;
    private readonly List<PianoNote> _notes = new();

    public MainWindow()
    {
        InitializeComponent();

        BuildDemoNotes();
        Roll.Notes = _notes;
        Opened += (_, _) => Roll.FitToNotes();

        LoadEngineDemo();

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