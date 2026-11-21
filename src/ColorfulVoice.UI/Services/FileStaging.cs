using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace ColorfulVoice.Services;

/// <summary>
/// Android 的文件选择器返回 content:// URI，引擎只认真实路径。
/// 这里统一把选中文件复制到缓存目录（读），或把生成结果写回所选 URI（写）。
/// </summary>
public static class FileStaging
{
    private static string CacheDir
    {
        get
        {
            var dir = Path.Combine(Path.GetTempPath(), "novavoice-import");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>返回一个可被引擎直接打开的本地路径。</summary>
    public static async Task<string> StageReadAsync(IStorageFile file)
    {
        var local = file.TryGetLocalPath();
        if (!string.IsNullOrEmpty(local) && File.Exists(local))
        {
            return local;
        }

        var target = Path.Combine(CacheDir, Sanitize(file.Name));
        await using (var src = await file.OpenReadAsync())
        await using (var dst = File.Create(target))
        {
            await src.CopyToAsync(dst);
        }
        return target;
    }

    /// <summary>把本地生成的文件写回用户选定的目标（SAF 或普通路径）。</summary>
    public static async Task WriteBackAsync(IStorageFile file, string localPath)
    {
        var local = file.TryGetLocalPath();
        if (!string.IsNullOrEmpty(local))
        {
            File.Copy(localPath, local, overwrite: true);
            return;
        }

        await using var src = File.OpenRead(localPath);
        await using var dst = await file.OpenWriteAsync();
        dst.SetLength(0);
        await src.CopyToAsync(dst);
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
