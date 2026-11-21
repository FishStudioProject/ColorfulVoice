using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace ColorfulVoice.Services;

/// <summary>
/// UTAU 音源是「文件夹」：oto.ini + wav 采样 + character.txt。
/// Android 的文件夹选择器只有 content tree，不能直接给引擎用，
/// 这里把整个文件夹（一层）复制到应用缓存，返回缓存里的 oto.ini 路径。
/// </summary>
public static class VoicebankImporter
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".ini", ".txt", ".frq", ".bmp", ".png", ".jpg", ".jpeg", ".ust",
    };

    public static async Task<(string? OtoPath, int Copied, string? Error)> ImportAsync(IStorageFolder folder)
    {
        var local = folder.TryGetLocalPath();
        if (!string.IsNullOrEmpty(local))
        {
            var localOto = Path.Combine(local, "oto.ini");
            if (File.Exists(localOto))
            {
                return (localOto, 0, null); // 桌面：直接用原目录
            }
        }

        var target = Path.Combine(Path.GetTempPath(), "novavoice-banks", Sanitize(folder.Name));
        Directory.CreateDirectory(target);

        var copied = 0;
        try
        {
            // UTAU 音源可能把 wav 放在子目录（oto 里写 `wav\a.wav`），因此递归复制
            async Task WalkAsync(IStorageFolder dir, string relative, int depth)
            {
                await foreach (var item in dir.GetItemsAsync())
                {
                    if (item is IStorageFile file)
                    {
                        var ext = Path.GetExtension(file.Name);
                        if (!AllowedExtensions.Contains(ext))
                        {
                            continue;
                        }
                        var folderPath = Path.Combine(target, relative);
                        Directory.CreateDirectory(folderPath);
                        var dest = Path.Combine(folderPath, Sanitize(file.Name));
                        await using var src = await file.OpenReadAsync();
                        await using var dst = File.Create(dest);
                        await src.CopyToAsync(dst);
                        copied++;
                    }
                    else if (item is IStorageFolder sub && depth < 3)
                    {
                        await WalkAsync(sub, Path.Combine(relative, Sanitize(sub.Name)), depth + 1);
                    }
                }
            }

            await WalkAsync(folder, "", 0);
        }
        catch (Exception ex)
        {
            return (null, copied, $"读取文件夹失败：{ex.Message}");
        }

        var oto = Path.Combine(target, "oto.ini");
        return File.Exists(oto) ? (oto, copied, null) : (null, copied, "文件夹里没有 oto.ini");
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return string.IsNullOrWhiteSpace(name) ? Guid.NewGuid().ToString("N") : name;
    }
}
