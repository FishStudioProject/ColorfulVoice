using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NanoFish.Interop;

namespace ColorfulVoice.Controls;

/// <summary>
/// Skia 自绘钢琴卷帘（Phase 2.2）。
///
/// 交互对齐 PaintVoice：
/// * 空白处按下拖拽 = 直接画出音符并拖出时值；
/// * 拖音符本体 = 移动（吸附 1/4 拍与半音）；
/// * 拖右边缘 = 改时值；
/// * 双指捏合 = 缩放，双指拖动 = 平移；滚轮纵向滚动，Ctrl+滚轮横向缩放。
///
/// 实现要点：完全不使用 ItemsControl，所有可见元素在一次
/// <see cref="Render"/> 里提交；音符数量再多也只是多几个矩形。
/// </summary>
public sealed class PianoRoll : Control
{
    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#FBFBFD"));
    private static readonly IBrush RowWhite = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush RowBlack = new SolidColorBrush(Color.Parse("#F1F2F7"));
    private static readonly IBrush KeyboardWhite = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush KeyboardBlack = new SolidColorBrush(Color.Parse("#2B2B36"));
    private static readonly IBrush KeyLabel = new SolidColorBrush(Color.Parse("#8A8A96"));
    private static readonly IBrush NoteFill = new SolidColorBrush(Color.Parse("#7C8FD8"));
    private static readonly IBrush NoteSelected = new SolidColorBrush(Color.Parse("#F2B35C"));
    private static readonly IBrush NoteText = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush NoteTextOnSelected = new SolidColorBrush(Color.Parse("#101018"));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#E9EAF1")), 1);
    private static readonly Pen BeatPen = new(new SolidColorBrush(Color.Parse("#DCDEE9")), 1);
    private static readonly Pen BarPen = new(new SolidColorBrush(Color.Parse("#C6CAD9")), 1.5);
    private static readonly IBrush RulerText = new SolidColorBrush(Color.Parse("#8A8A96"));
    private static readonly Pen CurvePen = new(new SolidColorBrush(Color.Parse("#1F9E8F")), 1.6);
    private static readonly IBrush CurveHandle = new SolidColorBrush(Color.Parse("#1F9E8F"));
    private static readonly IBrush CurveHandleActive = new SolidColorBrush(Color.Parse("#E0873A"));
    private static readonly Pen PlayheadPen = new(new SolidColorBrush(Color.Parse("#F2B35C")), 1.5);
    private static readonly Pen EdgePen = new(new SolidColorBrush(Color.Parse("#5A5A6A")), 2);

    public static readonly StyledProperty<IReadOnlyList<PianoNote>> NotesProperty =
        AvaloniaProperty.Register<PianoRoll, IReadOnlyList<PianoNote>>(nameof(Notes), Array.Empty<PianoNote>());

    private enum DragMode { None, Move, Resize, Create, Pinch, BoxSelect, PitchPoint }

    private readonly PianoRollLayout _layout = new();
    private readonly Dictionary<int, Point> _pointers = new();
    private readonly Dictionary<string, FormattedText> _textCache = new();

    private DragMode _mode = DragMode.None;
    private PianoNote? _dragNote;
    private PianoNote? _created;
    private long _origStart;
    private long _origLength;
    private double _pinchStartDistance;
    private Point _pinchMid;
    private double _pinchStartScrollX;
    private double _pinchStartScrollY;
    private double _pinchStartPpt;
    private Point _dragOrigin;
    private Point _boxStart;
    private PianoNote? _curveNote;
    private PitchPoint? _curvePoint;
    private Rect _box;
    private int _nextId = 1000;
    private readonly List<PianoNote> _selection = new();
    private readonly List<(PianoNote Note, long Start, int Num)> _dragOriginals = new();
    private readonly Stack<Snapshot> _undo = new();
    private readonly Stack<Snapshot> _redo = new();
    private static List<PianoNote> _clipboard = new();
    private static readonly IBrush SelectionFill = new SolidColorBrush(Color.Parse("#337C8FD8"));
    private static readonly Pen SelectionPen = new(new SolidColorBrush(Color.Parse("#7C8FD8")), 1.5);

    private sealed record Snapshot(List<PianoNote> Notes, List<int> SelectedIds);

    public PianoRoll()
    {
        _timer.Tick += OnTimerTick;
    }

    static PianoRoll()
    {
        AffectsRender<PianoRoll>(NotesProperty);
        FocusableProperty.OverrideDefaultValue<PianoRoll>(true);
        ClipToBoundsProperty.OverrideDefaultValue<PianoRoll>(true);
    }

    public IReadOnlyList<PianoNote> Notes
    {
        get => GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    public PianoNote? Selected { get; private set; }

    /// <summary>工程 BPM，播放头推进用。</summary>
    public double Tempo { get; set; } = 120.0;

    /// <summary>播放头位置（tick）。</summary>
    public long PlayheadTick { get; private set; }

    public bool IsPlaying => _timer.IsEnabled;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private DateTime _lastTick = DateTime.UtcNow;

    /// <summary>网格吸附粒度（tick）：默认 1/4 拍。</summary>
    public long SnapTicks
    {
        get => _layout.SnapTicks;
        set => _layout.SnapTicks = Math.Max(1, value);
    }

    /// <summary>把视图滚动到音符范围（首次加载时调用）。</summary>
    public void FitToNotes()
    {
        if (Notes.Count == 0)
        {
            return;
        }
        var top = Notes.Max(n => n.NoteNum);
        var bottom = Notes.Min(n => n.NoteNum);
        _layout.ScrollY = 0;
        _layout.ScrollX = Math.Max(0, Notes.Min(n => n.StartTick) * _layout.PixelsPerTick - 24);

        // 让音符纵向填满约 85% 可视区（留出上方余量）
        if (_layout.ViewHeight > 40)
        {
            var span = Math.Max(8, top - bottom + 4);
            _layout.RowHeight = Math.Clamp(_layout.ViewHeight * 0.85 / span, 10, 28);
        }
        _layout.TopNote = top + 2;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        _layout.ViewWidth = w;
        _layout.ViewHeight = h;
        context.FillRectangle(Background, new Rect(0, 0, w, h));

        DrawRows(context, w, h);
        DrawGrid(context, w, h);
        DrawNotes(context, w, h);
        DrawKeyboard(context, h);
        DrawPitchCurves(context, w, h);

        // 小节号 + 播放头
        var barTicks = _layout.SnapTicks * 16;
        var firstBar = Math.Max(0, _layout.XToTick(_layout.KeyboardWidth) / barTicks * barTicks);
        for (var t = firstBar; t <= _layout.XToTick(w); t += barTicks)
        {
            context.DrawText(GetText($"{(t / barTicks) + 1}", 9, RulerText), new Point(_layout.TickToX(t) + 3, 2));
        }
        var px = _layout.TickToX(PlayheadTick);
        if (px >= _layout.KeyboardWidth && px <= w)
        {
            context.DrawLine(PlayheadPen, new Point(px, 0), new Point(px, h));
        }

        if (_mode == DragMode.BoxSelect && _box.Width > 0 && _box.Height > 0)
        {
            context.DrawRectangle(SelectionFill, SelectionPen, _box);
        }
    }

    private void DrawRows(DrawingContext context, double w, double h)
    {
        var topNote = _layout.TopNote;
        var rows = (int)Math.Ceiling((h + _layout.ScrollY) / _layout.RowHeight) + 1;
        for (var i = 0; i < rows; i++)
        {
            var note = topNote - i;
            if (note < 0 || note > 127)
            {
                continue;
            }
            var y = _layout.NoteToY(note);
            if (y > h)
            {
                break;
            }
            var brush = IsBlackKey(note) ? RowBlack : RowWhite;
            context.FillRectangle(brush, new Rect(0, y, w, _layout.RowHeight));
            context.DrawLine(GridPen, new Point(0, y + _layout.RowHeight), new Point(w, y + _layout.RowHeight));
        }
    }

    private void DrawGrid(DrawingContext context, double w, double h)
    {
        // 1/4 拍细线、1 拍中线、4 拍小节线
        var startTick = Math.Max(0, _layout.XToTick(_layout.KeyboardWidth));
        var endTick = _layout.XToTick(w);
        var step = _layout.SnapTicks;
        for (var tick = startTick / step * step; tick <= endTick; tick += step)
        {
            var x = Math.Floor(_layout.TickToX(tick)) + 0.5;
            if (x < _layout.KeyboardWidth)
            {
                continue;
            }
            var pen = tick % (step * 16) == 0 ? BarPen
                : tick % (step * 4) == 0 ? BeatPen
                : GridPen;
            context.DrawLine(pen, new Point(x, 0), new Point(x, h));
        }
    }

    private void DrawNotes(DrawingContext context, double w, double h)
    {
        var notes = Notes;
        for (var i = 0; i < notes.Count; i++)
        {
            var n = notes[i];
            var x = _layout.TickToX(n.StartTick);
            var right = _layout.TickToX(n.EndTick);
            if (right < _layout.KeyboardWidth || x > w)
            {
                continue;
            }
            var y = _layout.NoteToY(n.NoteNum);
            if (y + _layout.RowHeight < 0 || y > h)
            {
                continue;
            }

            var width = Math.Max(4, right - x);
            var rect = new Rect(x + 1, y + 1, width - 2, _layout.RowHeight - 2);
            var fill = _selection.Contains(n) ? NoteSelected : NoteFill;
            context.DrawRectangle(fill, null, rect, 4, 4);
            // 右边缘留一条深色把手，提示"可拖拽改时值"
            context.DrawLine(EdgePen, new Point(rect.Right - 2, rect.Top + 3), new Point(rect.Right - 2, rect.Bottom - 3));

            if (width > 18)
            {
                var text = GetText(n.Lyric, 11, _selection.Contains(n) ? NoteTextOnSelected : NoteText);
                if (text.Width < width - 10)
                {
                    context.DrawText(text, new Point(x + 5, y + (_layout.RowHeight - text.Height) / 2));
                }
                else
                {
                    using (context.PushClip(new Rect(rect.X, rect.Y, rect.Width, rect.Height)))
                    {
                        context.DrawText(text, new Point(x + 5, y + (_layout.RowHeight - text.Height) / 2));
                    }
                }
            }
        }
    }

    private double TicksPerMs => Tempo / 60.0 * 480.0 / 1000.0;

    private double CurveX(PianoNote note, double xMs) =>
        _layout.TickToX(note.StartTick + xMs * TicksPerMs);

    private double CurveY(PianoNote note, double semitones) =>
        _layout.NoteToY(note.NoteNum) + _layout.RowHeight / 2 - semitones * _layout.RowHeight;

    private void DrawPitchCurves(DrawingContext context, double w, double h)
    {
        foreach (var note in Notes)
        {
            var curve = note.Curve;
            if (curve is null || curve.Points.Count < 2)
            {
                continue;
            }
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var first = curve.Points[0];
                ctx.BeginFigure(new Point(CurveX(note, first.X), CurveY(note, first.Y)), false);
                for (var i = 1; i < curve.Points.Count; i++)
                {
                    var p = curve.Points[i];
                    ctx.LineTo(new Point(CurveX(note, p.X), CurveY(note, p.Y)));
                }
                ctx.EndFigure(false);
            }
            context.DrawGeometry(null, CurvePen, geometry);

            if (ReferenceEquals(note, Selected))
            {
                foreach (var p in curve.Points)
                {
                    var center = new Point(CurveX(note, p.X), CurveY(note, p.Y));
                    var brush = ReferenceEquals(p, _curvePoint) ? CurveHandleActive : CurveHandle;
                    context.DrawEllipse(brush, null, center, 4, 4);
                }
            }
        }
    }

    private void DrawKeyboard(DrawingContext context, double h)
    {
        var kw = _layout.KeyboardWidth;
        context.FillRectangle(new SolidColorBrush(Color.Parse("#F4F5F9")), new Rect(0, 0, kw, h));

        var topNote = _layout.TopNote;
        var rows = (int)Math.Ceiling((h + _layout.ScrollY) / _layout.RowHeight) + 1;
        for (var i = 0; i < rows; i++)
        {
            var note = topNote - i;
            if (note < 0 || note > 127)
            {
                continue;
            }
            var y = _layout.NoteToY(note);
            if (y > h)
            {
                break;
            }
            var black = IsBlackKey(note);
            // 白键铺满整列作为底，黑键是叠在上面的窄键
            context.FillRectangle(KeyboardWhite, new Rect(0, y, kw, _layout.RowHeight - 1));
            context.DrawLine(GridPen, new Point(0, y + _layout.RowHeight - 1), new Point(kw, y + _layout.RowHeight - 1));

            if (black)
            {
                // 黑键贴着卷帘一侧（右侧），像真实钢琴键盘
                var blackRect = new Rect(kw * 0.38, y, kw * 0.62, _layout.RowHeight - 1);
                context.DrawRectangle(KeyboardBlack, null, blackRect, 0, 0);
            }
            else
            {
                var ft = GetText(NoteName(note), 9, KeyLabel);
                context.DrawText(ft, new Point(kw - 8 - ft.Width, y + (_layout.RowHeight - ft.Height) / 2));
            }
        }
        context.DrawLine(BarPen, new Point(kw, 0), new Point(kw, h));
    }

    private FormattedText GetText(string text, double size = 11, IBrush? brush = null)
    {
        var key = $"{text}|{size}";
        if (_textCache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            size,
            brush ?? NoteText);
        _textCache[key] = ft;
        return ft;
    }

    private static bool IsBlackKey(int note)
    {
        var n = ((note % 12) + 12) % 12;
        return n is 1 or 3 or 6 or 8 or 10;
    }

    private static string NoteName(int note)
    {
        string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        var n = ((note % 12) + 12) % 12;
        return $"{names[n]}{note / 12 - 1}";
    }

    // ------------------------------------------------------------------
    // 交互
    // ------------------------------------------------------------------

    /// <summary>双击音符时触发（用于打开属性面板）。</summary>
    public event Action<PianoNote>? NoteDoubleClicked;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.ClickCount == 2)
        {
            var doublePos = e.GetPosition(this);
            var (doubleHit, _) = _layout.HitTest(Notes, doublePos.X, doublePos.Y);
            if (doubleHit is not null)
            {
                NoteDoubleClicked?.Invoke(doubleHit);
                e.Handled = true;
                return;
            }
        }
        var p = e.GetPosition(this);
        _pointers[e.Pointer.Id] = p;

        if (_pointers.Count == 2)
        {
            _mode = DragMode.Pinch;
            _pinchStartDistance = PinchDistance();
            _pinchMid = PinchMidpoint();
            _pinchStartPpt = _layout.PixelsPerTick;
            _pinchStartScrollX = _layout.ScrollX;
            _pinchStartScrollY = _layout.ScrollY;
            e.Pointer.Capture(this);
            return;
        }

        if (p.X < _layout.KeyboardWidth)
        {
            return; // 键盘区留给后续的试听
        }

        Focus();

        // 先判曲线点（选中音符才有把手）
        if (Selected?.Curve is { } selectedCurve)
        {
            foreach (var point in selectedCurve.Points)
            {
                var center = new Point(CurveX(Selected, point.X), CurveY(Selected, point.Y));
                if (Math.Abs(center.X - p.X) <= 8 && Math.Abs(center.Y - p.Y) <= 8)
                {
                    _curveNote = Selected;
                    _curvePoint = point;
                    _mode = DragMode.PitchPoint;
                    PushUndo();
                    e.Pointer.Capture(this);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }
        }

        var (hit, resize) = _layout.HitTest(Notes, p.X, p.Y);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (hit is null && shift)
        {
            _boxStart = p;
            _box = new Rect(p.X, p.Y, 0, 0);
            _mode = DragMode.BoxSelect;
            _selection.Clear();
            e.Pointer.Capture(this);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (hit is not null && e.ClickCount == 2)
        {
            if (hit.Curve is null || hit.Curve.Points.Count < 2)
            {
                PushUndo();
                var lengthMs = hit.LengthTicks / TicksPerMs;
                hit.Curve = new PitchCurve();
                hit.Curve.Points.Add(new PitchPoint { X = 0, Y = 0 });
                hit.Curve.Points.Add(new PitchPoint { X = lengthMs, Y = 0 });
            }
            Selected = hit;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (hit is not null)
        {
            if (ctrl)
            {
                if (!_selection.Remove(hit))
                {
                    _selection.Add(hit);
                }
            }
            else if (!_selection.Contains(hit))
            {
                _selection.Clear();
                _selection.Add(hit);
            }
            Selected = _selection.LastOrDefault();
            _dragNote = hit;
            _mode = resize ? DragMode.Resize : DragMode.Move;
            _origStart = hit.StartTick;
            _origLength = hit.LengthTicks;
            _dragOrigin = p;
            CaptureDragOriginals();
            PushUndo();
            e.Pointer.Capture(this);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // PaintVoice 式：在空白处按下就开始"画"一个音符
        PushUndo();
        var start = Math.Max(0, _layout.Snap(_layout.XToTick(p.X)));
        var created = new PianoNote
        {
            Id = _nextId++,
            StartTick = start,
            LengthTicks = _layout.SnapTicks,
            NoteNum = Math.Clamp(_layout.YToNote(p.Y), 0, 127),
        };
        if (Notes is List<PianoNote> list)
        {
            list.Add(created);
        }
        _created = created;
        _selection.Clear();
        _selection.Add(created);
        Selected = created;
        _dragNote = created;
        _mode = DragMode.Create;
        _origStart = start;
        _dragOrigin = p;
        e.Pointer.Capture(this);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointers.ContainsKey(e.Pointer.Id))
        {
            return;
        }
        var p = e.GetPosition(this);
        _pointers[e.Pointer.Id] = p;

        switch (_mode)
        {
            case DragMode.Pinch when _pointers.Count >= 2:
            {
                var distance = PinchDistance();
                if (_pinchStartDistance > 1)
                {
                    var factor = distance / _pinchStartDistance;
                    _layout.PixelsPerTick = Math.Clamp(_pinchStartPpt * factor, 0.02, 4.0);
                }
                // 双指同时平移：跟随两指中点位移
                var mid = PinchMidpoint();
                _layout.ScrollX = Math.Max(0, _pinchStartScrollX - (mid.X - _pinchMid.X));
                _layout.ScrollY = Math.Max(0, _pinchStartScrollY - (mid.Y - _pinchMid.Y));
                InvalidateVisual();
                break;
            }
            case DragMode.PitchPoint when _curveNote is not null && _curvePoint is not null:
            {
                var centerY = _layout.NoteToY(_curveNote.NoteNum) + _layout.RowHeight / 2;
                var semitones = (centerY - p.Y) / _layout.RowHeight;
                _curvePoint.Y = Math.Clamp(Math.Round(semitones * 2) / 2, -24, 24);
                InvalidateVisual();
                break;
            }
            case DragMode.BoxSelect:
            {
                var x = Math.Min(_boxStart.X, p.X);
                var y = Math.Min(_boxStart.Y, p.Y);
                _box = new Rect(x, y, Math.Abs(p.X - _boxStart.X), Math.Abs(p.Y - _boxStart.Y));
                _selection.Clear();
                foreach (var n in Notes)
                {
                    var nr = new Rect(
                        _layout.TickToX(n.StartTick),
                        _layout.NoteToY(n.NoteNum),
                        Math.Max(2, _layout.TickToX(n.EndTick) - _layout.TickToX(n.StartTick)),
                        _layout.RowHeight);
                    if (_box.Intersects(nr))
                    {
                        _selection.Add(n);
                    }
                }
                Selected = _selection.LastOrDefault();
                InvalidateVisual();
                break;
            }
            case DragMode.Move when _dragNote is not null:
            {
                var deltaTicks = (long)((p.X - _dragOrigin.X) / _layout.PixelsPerTick);
                var deltaNote = _layout.YToNote(p.Y) - _layout.YToNote(_dragOrigin.Y);
                foreach (var (note, start, num) in _dragOriginals)
                {
                    note.StartTick = Math.Max(0, _layout.Snap(start + deltaTicks));
                    note.NoteNum = Math.Clamp(num + deltaNote, 0, 127);
                }
                InvalidateVisual();
                break;
            }
            case DragMode.Resize when _dragNote is not null:
            {
                var end = _layout.Snap(_layout.XToTick(p.X));
                _dragNote.LengthTicks = Math.Max(_layout.SnapTicks, end - _dragNote.StartTick);
                InvalidateVisual();
                break;
            }
            case DragMode.Create when _created is not null:
            {
                var end = _layout.Snap(_layout.XToTick(p.X));
                _created.LengthTicks = Math.Max(_layout.SnapTicks, end - _created.StartTick);
                InvalidateVisual();
                break;
            }
            case DragMode.None:
            {
                // 单指空白拖动 = 平移（移动端习惯）
                if (_pointers.Count == 1 && p.X >= _layout.KeyboardWidth)
                {
                    _layout.ScrollX = Math.Max(0, _layout.ScrollX - (p.X - _dragOrigin.X));
                    _dragOrigin = p;
                    InvalidateVisual();
                }
                break;
            }
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _pointers.Remove(e.Pointer.Id);
        if (_mode == DragMode.Create && _created is not null)
        {
            // 点一下就松开：保留一个 1/4 拍的音符，方便继续编辑
            if (_created.LengthTicks < _layout.SnapTicks)
            {
                _created.LengthTicks = _layout.SnapTicks;
            }
        }
        _mode = DragMode.None;
        _dragNote = null;
        _created = null;
        _dragOriginals.Clear();
        _curveNote = null;
        _curvePoint = null;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var p = e.GetPosition(this);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var factor = e.Delta.Y > 0 ? 1.15 : 1 / 1.15;
            _layout.ZoomAt(p.X, factor);
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _layout.ScrollX = Math.Max(0, _layout.ScrollX - e.Delta.Y * 40);
        }
        else
        {
            _layout.ScrollY = Math.Max(0, _layout.ScrollY - e.Delta.Y * 48);
        }
        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>选中指定音符（调试 / 截图用）。</summary>
    public void SelectNote(PianoNote note)
    {
        _selection.Clear();
        _selection.Add(note);
        Selected = note;
        InvalidateVisual();
    }

    /// <summary>把播放头移到指定 tick（用于调试与截图）。</summary>
    public void Seek(long tick)
    {
        PlayheadTick = Math.Max(0, tick);
        InvalidateVisual();
    }

    /// <summary>开始 / 暂停预览（目前只有播放头，音频输出在 Phase 2.4）。</summary>
    public void TogglePlayback()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
            InvalidateVisual();
            return;
        }
        var end = Notes.Count == 0 ? 0 : Notes.Max(n => n.EndTick);
        if (end <= 0 || PlayheadTick >= end)
        {
            PlayheadTick = 0;
        }
        _lastTick = DateTime.UtcNow;
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var ms = (now - _lastTick).TotalMilliseconds;
        _lastTick = now;
        PlayheadTick += (long)PianoRollLayout.TicksForMs(ms, Tempo);
        var end = Notes.Count == 0 ? 0 : Notes.Max(n => n.EndTick);
        if (PlayheadTick >= end)
        {
            PlayheadTick = end;
            _timer.Stop();
        }
        // 播放头跑出视口时自动跟随
        var px = _layout.TickToX(PlayheadTick);
        if (px > _layout.ViewWidth - 40)
        {
            _layout.ScrollX = Math.Max(0, PlayheadTick * _layout.PixelsPerTick - _layout.ViewWidth * 0.4);
        }
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (e.Key == Key.Space)
        {
            TogglePlayback();
            e.Handled = true;
            return;
        }
        if (e.Key is Key.Delete or Key.Back)
        {
            DeleteSelection();
        }
        else if (ctrl && e.Key == Key.A)
        {
            _selection.Clear();
            _selection.AddRange(Notes);
            Selected = _selection.LastOrDefault();
            InvalidateVisual();
        }
        else if (ctrl && e.Key == Key.C)
        {
            _clipboard = _selection.Select(n => n.Clone()).ToList();
        }
        else if (ctrl && e.Key == Key.V)
        {
            Paste();
        }
        else if (ctrl && e.Key == Key.Z)
        {
            Undo();
        }
        else if (ctrl && e.Key == Key.Y)
        {
            Redo();
        }
        else if (e.Key == Key.Escape)
        {
            _selection.Clear();
            Selected = null;
            InvalidateVisual();
        }
        else
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
    }

    private void DeleteSelection()
    {
        if (_selection.Count == 0 || Notes is not List<PianoNote> list)
        {
            return;
        }
        PushUndo();
        foreach (var n in _selection.ToList())
        {
            list.Remove(n);
        }
        _selection.Clear();
        Selected = null;
        InvalidateVisual();
    }

    private void Paste()
    {
        if (_clipboard.Count == 0 || Notes is not List<PianoNote> list)
        {
            return;
        }
        PushUndo();
        var offset = 480L;
        _selection.Clear();
        foreach (var c in _clipboard)
        {
            var n = c.Clone();
            n.Id = _nextId++;
            n.StartTick += offset;
            list.Add(n);
            _selection.Add(n);
        }
        _clipboard = _selection.Select(n => n.Clone()).ToList();
        Selected = _selection.LastOrDefault();
        InvalidateVisual();
    }

    private void CaptureDragOriginals()
    {
        _dragOriginals.Clear();
        foreach (var n in _selection)
        {
            _dragOriginals.Add((n, n.StartTick, n.NoteNum));
        }
    }

    private Snapshot Capture() => new(
        Notes.Select(n => n.Clone()).ToList(),
        _selection.Select(n => n.Id).ToList());

    private void PushUndo()
    {
        _undo.Push(Capture());
        _redo.Clear();
    }

    private void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }
        _redo.Push(Capture());
        Restore(_undo.Pop());
    }

    private void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }
        _undo.Push(Capture());
        Restore(_redo.Pop());
    }

    private void Restore(Snapshot snapshot)
    {
        if (Notes is not List<PianoNote> list)
        {
            return;
        }
        list.Clear();
        list.AddRange(snapshot.Notes.Select(n => n.Clone()));
        _selection.Clear();
        foreach (var id in snapshot.SelectedIds)
        {
            var match = list.FirstOrDefault(n => n.Id == id);
            if (match is not null)
            {
                _selection.Add(match);
            }
        }
        Selected = _selection.LastOrDefault();
        InvalidateVisual();
    }

    private Point PinchMidpoint()
    {
        if (_pointers.Count < 2)
        {
            return default;
        }
        var pts = _pointers.Values.Take(2).ToArray();
        return new Point((pts[0].X + pts[1].X) / 2, (pts[0].Y + pts[1].Y) / 2);
    }

    private double PinchDistance()
    {
        if (_pointers.Count < 2)
        {
            return 0;
        }
        var pts = _pointers.Values.Take(2).ToArray();
        return Math.Sqrt(Math.Pow(pts[1].X - pts[0].X, 2) + Math.Pow(pts[1].Y - pts[0].Y, 2));
    }
}