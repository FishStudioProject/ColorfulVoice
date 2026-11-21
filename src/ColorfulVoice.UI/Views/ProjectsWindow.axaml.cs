using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Controls;
using NanoFish.Interop;

namespace ColorfulVoice.Views;

public partial class ProjectsWindow : UserControl
{
    /// <summary>浮层关闭时触发（true = 确认）。</summary>
    public event Action<bool>? Closed;

    public event Action<string>? OpenRequested;

    public ProjectsWindow()
    {
        InitializeComponent();
        ProjectList.ItemsSource = RecentProjects.Items.ToList();
        ProjectList.DoubleTapped += OnListDoubleTapped;
    }

    private void OnListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ProjectList.SelectedItem is RecentProject p)
        {
            OpenRequested?.Invoke(p.Path);
            Closed?.Invoke(true);
        }
    }

    private void OnOpenItemClick(object? sender, RoutedEventArgs e)
    {
        if (Selected() is { } p)
        {
            OpenRequested?.Invoke(p.Path);
            Closed?.Invoke(true);
        }
    }

    private async void OnExportItemClick(object? sender, RoutedEventArgs e)
    {
        if (Selected() is not { } project)
        {
            return;
        }
        try
        {
            var dir = System.IO.Path.GetDirectoryName(project.Path)!;
            var oto = new[] { System.IO.Path.Combine(dir, "oto.ini"), System.IO.Path.Combine(dir, "voicebank", "oto.ini") }
                .FirstOrDefault(System.IO.File.Exists);
            if (oto is null)
            {
                HintText.Text = "这个工程找不到 oto.ini，无法渲染";
                return;
            }

            using var pcm = NanoFish.Interop.NanoFish.RenderUst(project.Path, oto, System.IO.Path.GetDirectoryName(oto)!);
            var file = await (TopLevel.GetTopLevel(this)?.StorageProvider ?? throw new InvalidOperationException("no storage"))!.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出 WAV",
                SuggestedFileName = project.Name + ".wav",
                DefaultExtension = "wav",
            });
            if (file is null)
            {
                return;
            }
            var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "colorfulvoice-export.wav");
            WavWriter.Save(temp, pcm.Samples, (int)pcm.SampleRate);
            await Services.FileStaging.WriteBackAsync(file, temp);
            HintText.Text = $"已导出 {file.Name}";
        }
        catch (Exception ex)
        {
            HintText.Text = $"导出失败：{ex.Message}";
        }
    }

    private void OnRemoveItemClick(object? sender, RoutedEventArgs e)
    {
        if (Selected() is { } p)
        {
            RecentProjects.Remove(p.Path);
            ProjectList.ItemsSource = RecentProjects.Items.ToList();
        }
    }

    private RecentProject? Selected() => ProjectList.SelectedItem as RecentProject;
}
