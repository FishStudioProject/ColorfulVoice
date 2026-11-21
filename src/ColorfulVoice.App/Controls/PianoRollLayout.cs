using System;

namespace ColorfulVoice.Controls;

/// <summary>卷帘里的一个音符（UI 侧模型，与 UST 音符一一对应）。</summary>
public sealed class PianoNote
{
    public int Id { get; set; }
    public long StartTick { get; set; }
    public long LengthTicks { get; set; }
    public int NoteNum { get; set; }
    public string Lyric { get; set; } = "a";

    public long EndTick => StartTick + LengthTicks;

    public PianoNote Clone() => new()
    {
        Id = Id,
        StartTick = StartTick,
        LengthTicks = LengthTicks,
        NoteNum = NoteNum,
        Lyric = Lyric,
    };
}

/// <summary>
/// 卷帘的坐标系与吸附逻辑。刻意做成纯计算类，方便脱离 UI 单测：
/// tick ↔ 像素、音高 ↔ 像素、命中测试、网格吸附。
/// </summary>
public sealed class PianoRollLayout
{
    /// <summary>键盘列宽度（像素）。</summary>
    public double KeyboardWidth { get; set; } = 64;

    /// <summary>每 tick 的像素宽度（横向缩放）。</summary>
    public double PixelsPerTick { get; set; } = 0.16;

    /// <summary>每个半音的行高（纵向缩放）。</summary>
    public double RowHeight { get; set; } = 20;

    /// <summary>横向滚动量（像素）。</summary>
    public double ScrollX { get; set; }

    /// <summary>纵向滚动量（像素，向下为正）。</summary>
    public double ScrollY { get; set; }

    /// <summary>可视区顶部对应的 MIDI 音高。</summary>
    public int TopNote { get; set; } = 84;

    /// <summary>一格吸附 = 1/4 拍（120 tick，UTAU 480 tick/拍）。</summary>
    public long SnapTicks { get; set; } = 120;

    public double ViewWidth { get; set; } = 900;
    public double ViewHeight { get; set; } = 500;

    /// <summary>右侧拖拽改时值的感应宽度。</summary>
    public const double ResizeHandleWidth = 10;

    public double TickToX(double tick) => KeyboardWidth + tick * PixelsPerTick - ScrollX;

    public long XToTick(double x) =>
        (long)Math.Floor((x - KeyboardWidth + ScrollX) / Math.Max(1e-6, PixelsPerTick));

    public double NoteToY(int noteNum) => (TopNote - noteNum) * RowHeight - ScrollY;

    public int YToNote(double y) =>
        TopNote - (int)Math.Floor((y + ScrollY) / Math.Max(1e-6, RowHeight));

    public long Snap(long tick) =>
        (long)Math.Round(tick / (double)SnapTicks) * SnapTicks;

    public bool IsInsideGrid(double x, double y) =>
        x >= KeyboardWidth && x <= ViewWidth && y >= 0 && y <= ViewHeight;

    /// <summary>返回命中的音符以及是否点在右边缘（改时值）。</summary>
    public (PianoNote? Note, bool Resize) HitTest(System.Collections.Generic.IReadOnlyList<PianoNote> notes, double x, double y)
    {
        foreach (var n in notes)
        {
            var left = TickToX(n.StartTick);
            var right = TickToX(n.EndTick);
            var top = NoteToY(n.NoteNum);
            var bottom = top + RowHeight;
            if (x < left || x > right || y < top || y > bottom)
            {
                continue;
            }
            return (n, x >= right - ResizeHandleWidth);
        }
        return (null, false);
    }

    /// <summary>以某个屏幕点为中心缩放（滚轮 / 捏合）。</summary>
    public void ZoomAt(double screenX, double factor, double minPpt = 0.02, double maxPpt = 4.0)
    {
        var tickAtCursor = (screenX - KeyboardWidth + ScrollX) / Math.Max(1e-6, PixelsPerTick);
        PixelsPerTick = Math.Clamp(PixelsPerTick * factor, minPpt, maxPpt);
        ScrollX = tickAtCursor * PixelsPerTick - (screenX - KeyboardWidth);
    }
}