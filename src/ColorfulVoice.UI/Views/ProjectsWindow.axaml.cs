using Avalonia.Controls;
using SukiUI.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NanoFish.Interop;

namespace ColorfulVoice.Views;

public partial class ProjectsWindow : SukiWindow
{
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
            Close();
        }
    }

    private void OnOpenItemClick(object? sender, RoutedEventArgs e)
    {
        if (Selected() is { } p)
        {
            OpenRequested?.Invoke(p.Path);
            Close();
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
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出 WAV",
                SuggestedFileName = project.Name + ".wav",
                DefaultExtension = "wav",
            });
            if (file is null)
            {
                return;
            }
            WavWriter.Save(file.Path.LocalPath, pcm.Samples, (int)pcm.SampleRate);
            HintText.Text = $"已导出 {file.Path.LocalPath}";
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
