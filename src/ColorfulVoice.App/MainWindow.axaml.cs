using Avalonia.Controls;
using NanoFish.Interop;

namespace ColorfulVoice;

public partial class MainWindow : Window
{
    private AudioBuffer? _buffer;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            // 宿主只持有 dispose 权；样本数据始终留在 Rust 分配的堆上。
            _buffer = NanoFish.Interop.NanoFish.GenerateSine(440f, 48_000, 1_200);
            Waveform.Buffer = _buffer;
            // 1.2s 的 440Hz 正弦在 960px 宽度下会糊成一条实心带，
            // 这里放大前 20ms（约 9 个周期）让波形细节可见。
            Waveform.VisibleSamples = 960;

            var seconds = _buffer.Length / (double)_buffer.SampleRate;
            SubtitleText.Text = $"NanoFish {NanoFish.Interop.NanoFish.Version} · 440 Hz 正弦波 · 波形放大前 20 ms";
            StatusText.Text =
                $"{_buffer.Length:N0} 帧 @ {_buffer.SampleRate} Hz ({seconds:F2}s) · " +
                $"RMS {_buffer.Rms():F4}（Rust 端计算，C# 端零拷贝映射）";
        }
        catch (NanoFishException ex)
        {
            StatusText.Text = $"引擎错误：{ex.Status} — {ex.Message}";
        }
        catch (DllNotFoundException)
        {
            StatusText.Text = "找不到 libnanofish.so：请先运行 scripts/build.sh 或设置 NANOFISH_LIB";
        }

        Closed += (_, _) =>
        {
            _buffer?.Dispose();
            _buffer = null;
            Waveform.Buffer = null;
        };
    }
}