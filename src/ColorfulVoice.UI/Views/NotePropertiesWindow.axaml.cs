using Avalonia.Controls;
using Avalonia.Interactivity;
using ColorfulVoice.Controls;

namespace ColorfulVoice.Views;

public partial class NotePropertiesWindow : UserControl
{
    /// <summary>浮层关闭时触发（true = 确认）。</summary>
    public event Action<bool>? Closed;

    private readonly PianoNote _note;

    public NotePropertiesWindow(PianoNote note)
    {
        InitializeComponent();
        _note = note;
        LyricBox.Text = note.Lyric;
        PitchBox.Value = note.NoteNum;
        LengthBox.Value = note.LengthTicks;
        VelocityBox.Value = note.Velocity;
        VibratoDepthBox.Value = (decimal)note.VibratoDepth;
        VibratoPeriodBox.Value = (decimal)note.VibratoPeriod;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        _note.Lyric = string.IsNullOrWhiteSpace(LyricBox.Text) ? "a" : LyricBox.Text!.Trim();
        _note.NoteNum = (int)(PitchBox.Value ?? _note.NoteNum);
        _note.LengthTicks = (long)(LengthBox.Value ?? _note.LengthTicks);
        _note.Velocity = (int)(VelocityBox.Value ?? _note.Velocity);
        _note.VibratoDepth = (double)(VibratoDepthBox.Value ?? (decimal)_note.VibratoDepth);
        _note.VibratoPeriod = (double)(VibratoPeriodBox.Value ?? (decimal)_note.VibratoPeriod);
        Closed?.Invoke(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Closed?.Invoke(false);
}
