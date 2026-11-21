using System.Text.Json;

namespace ColorfulVoice;

public sealed class RecentProject
{
    public string Path { get; set; } = string.Empty;
    public DateTime LastOpened { get; set; }
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
}

/// <summary>最近打开的工程列表（存到应用数据目录的 recent.json）。</summary>
public static class RecentProjects
{
    private static readonly string FilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NovaVoice", "recent.json");

    private static List<RecentProject>? _items;

    public static IReadOnlyList<RecentProject> Items
    {
        get
        {
            _items ??= Load();
            return _items.OrderByDescending(p => p.LastOpened).ToList();
        }
    }

    public static void Add(string path)
    {
        _items ??= Load();
        _items.RemoveAll(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
        _items.Add(new RecentProject { Path = path, LastOpened = DateTime.Now });
        if (_items.Count > 20)
        {
            _items = _items.OrderByDescending(p => p.LastOpened).Take(20).ToList();
        }
        Save();
    }

    public static void Remove(string path)
    {
        _items ??= Load();
        _items.RemoveAll(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    private static List<RecentProject> Load()
    {
        try
        {
            if (System.IO.File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<List<RecentProject>>(System.IO.File.ReadAllText(FilePath))
                       ?? new List<RecentProject>();
            }
        }
        catch
        {
            // 损坏的列表不影响启动
        }
        return new List<RecentProject>();
    }

    private static void Save()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(FilePath)!;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(FilePath, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 只读环境忽略
        }
    }
}
