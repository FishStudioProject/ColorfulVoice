using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ColorfulVoice.Services;

/// <summary>已导入的音源列表（参考 OpenUtau 的音源列表：名称 + oto 路径）。</summary>
public sealed class BankEntry
{
    public string Name { get; set; } = "";
    public string OtoPath { get; set; } = "";
    public int AliasCount { get; set; }
    public string Kind { get; set; } = "";
}

public static class BankLibrary
{
    private static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NovaVoice");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "banks.json");
        }
    }

    public static List<BankEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new List<BankEntry>();
            }
            return JsonSerializer.Deserialize<List<BankEntry>>(File.ReadAllText(FilePath))
                   ?? new List<BankEntry>();
        }
        catch
        {
            return new List<BankEntry>();
        }
    }

    public static void Add(BankEntry entry)
    {
        var list = Load();
        list.RemoveAll(e => string.Equals(e.OtoPath, entry.OtoPath, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, entry);
        Save(list);
    }

    public static void Remove(string otoPath)
    {
        var list = Load();
        list.RemoveAll(e => string.Equals(e.OtoPath, otoPath, StringComparison.OrdinalIgnoreCase));
        Save(list);
    }

    private static void Save(List<BankEntry> list)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }
}
