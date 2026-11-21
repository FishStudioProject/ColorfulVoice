using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace ColorfulVoice.Android;

[Activity(
    Label = "ColorfulVoice",
    Theme = "@style/Theme.AppCompat.Light.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    static MainActivity()
    {
        global::ColorfulVoice.App.TraceHook = Trace;
    }

    public MainActivity()
    {
        global::Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => CrashLog(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => CrashLog(e.ExceptionObject as Exception);
    }

    public static void Trace(string message)
    {
        var text = $"[{DateTime.Now:O}] TRACE {message}\n";
        try
        {
            var dir = global::Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
            if (dir is not null)
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "cv-crash.log"), text);
            }
        }
        catch
        {
        }
        try
        {
            const string dir = "/sdcard/Download/DSH-Go";
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "cv-crash.log"), text);
        }
        catch
        {
        }
    }

    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        Trace("OnCreate start");
        try
        {
            base.OnCreate(savedInstanceState);
            Trace("OnCreate done");
        }
        catch (Exception ex)
        {
            CrashLog(ex);
            Trace("OnCreate failed: " + ex.GetType().Name);
            throw;
        }
    }

    private static void CrashLog(Exception? ex)
    {
        if (ex is null)
        {
            return;
        }
        var text = $"[{DateTime.Now:O}] {ex}\n\n";
        try
        {
            var dir = global::Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
            if (dir is not null)
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "cv-crash.log"), text);
            }
        }
        catch
        {
        }
        try
        {
            const string dir = "/sdcard/Download/DSH-Go";
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "cv-crash.log"), text);
        }
        catch
        {
        }
    }
}
