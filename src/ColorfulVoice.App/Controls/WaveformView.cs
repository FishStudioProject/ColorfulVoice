using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NanoFish.Interop;

namespace ColorfulVoice.Controls;

/// <summary>
/// 波形预览：直接读取 Rust 分配的 PCM（不复制），按像素列做 min/max 抽取，
/// 每列一条竖线。几千到几百万样本都只走一次 <see cref="StreamGeometry"/>。
/// Phase 2 的钢琴卷帘会沿用同样的"先聚合到像素、再一次性提交"思路。
/// </summary>
public sealed class WaveformView : Control
{
    public static readonly StyledProperty<AudioBuffer?> BufferProperty =
        AvaloniaProperty.Register<WaveformView, AudioBuffer?>(nameof(Buffer));

    /// <summary>只显示前 N 个样本（0 = 全部），用于放大观察波形细节。</summary>
    public static readonly StyledProperty<int> VisibleSamplesProperty =
        AvaloniaProperty.Register<WaveformView, int>(nameof(VisibleSamples));

    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#12121A"));
    private static readonly IBrush WaveBrush = new SolidColorBrush(Color.Parse("#4FD1C5"));
    private static readonly IBrush CenterBrush = new SolidColorBrush(Color.Parse("#2A2A38"));
    private static readonly Pen WavePen = new(WaveBrush, 1);
    private static readonly Pen CenterPen = new(CenterBrush, 1);

    static WaveformView()
    {
        AffectsRender<WaveformView>(BufferProperty, VisibleSamplesProperty);
    }

    public AudioBuffer? Buffer
    {
        get => GetValue(BufferProperty);
        set => SetValue(BufferProperty, value);
    }

    public int VisibleSamples
    {
        get => GetValue(VisibleSamplesProperty);
        set => SetValue(VisibleSamplesProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        context.FillRectangle(Background, new Rect(0, 0, width, height));

        var buffer = Buffer;
        if (buffer is null || buffer.IsDisposed || buffer.Length == 0 || width < 1 || height < 1)
        {
            return;
        }

        var centerY = height / 2;
        context.DrawLine(CenterPen, new Point(0, centerY), new Point(width, centerY));

        var samples = buffer.Samples;
        var visible = VisibleSamples > 0 ? Math.Min(samples.Length, VisibleSamples) : samples.Length;
        var columns = (int)Math.Floor(width);
        var amplitude = height * 0.45;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // 每像素不足 8 个样本：逐样本折线，能看清波形细节（放大视图）。
            if (visible <= columns * 8)
            {
                ctx.BeginFigure(new Point(0, centerY - samples[0] * amplitude), false);
                for (var i = 1; i < visible; i++)
                {
                    var px = (double)i / Math.Max(1, visible - 1) * (columns - 1);
                    ctx.LineTo(new Point(px, centerY - samples[i] * amplitude));
                }
                ctx.EndFigure(false);
                context.DrawGeometry(null, WavePen, geometry);
                return;
            }

            // 样本比像素多：每列取 min/max，画一条竖线（波形概览）。
            for (var x = 0; x < columns; x++)
            {
                var start = (int)((long)x * visible / columns);
                var end = (int)((long)(x + 1) * visible / columns);
                if (end <= start)
                {
                    end = start + 1;
                }

                var min = float.MaxValue;
                var max = float.MinValue;
                for (var i = start; i < end && i < visible; i++)
                {
                    var s = samples[i];
                    if (s < min) min = s;
                    if (s > max) max = s;
                }
                if (min > max)
                {
                    continue;
                }

                var px = x + 0.5;
                ctx.BeginFigure(new Point(px, centerY - max * amplitude), false);
                ctx.LineTo(new Point(px, centerY - min * amplitude));
                ctx.EndFigure(false);
            }
        }

        // 只描边不填充：开放图形若参与填充会被隐式闭合，整片波形会糊成实心色块。
        context.DrawGeometry(null, WavePen, geometry);
    }
}