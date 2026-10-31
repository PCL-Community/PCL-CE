using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.Minecraft;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「Java」页的视图模型。</summary>
public class PageSetupJavaViewModel : ViewModelBase
{
    public static string BuildTitle(JavaEntry entry)
    {
        return $"{(entry.Installation.IsJre ? "JRE" : "JDK")} {entry.Installation.MajorVersion}";
    }

    public static string BuildInfo(JavaEntry entry)
    {
        return entry.Installation.JavaFolder;
    }

    public static List<string> BuildTags(JavaEntry entry)
    {
        return [entry.Installation.Is64Bit ? "64 Bit" : "32 Bit", entry.Installation.Brand.ToString()];
    }

    public static string BuildInfoMessage(JavaEntry entry)
    {
        return Lang.Text("Setup.Java.Info.Format",
            entry.Installation.IsJre ? "JRE" : "JDK",
            entry.Installation.Version.ToString(),
            entry.Installation.Architecture.ToString(),
            entry.Installation.Is64Bit ? "64 Bit" : "32 Bit",
            entry.Installation.Brand.ToString(),
            entry.Installation.JavaFolder);
    }

    public static bool EnsureAvailable(JavaEntry entry)
    {
        if (entry.Installation.IsStillAvailable)
            return true;
        HintService.Show(Hints.Info(Lang.Text("Setup.Java.Unavailable")));
        return false;
    }

    public bool TrySelectJava(JavaEntry entry)
    {
        if (!entry.Installation.IsStillAvailable)
        {
            HintService.Show(Hints.Info(Lang.Text("Setup.Java.Unavailable")));
            return true;
        }

        if (entry.IsEnabled)
        {
            Config.Launch.SelectedJava = entry.Installation.JavaExePath;
            return true;
        }

        HintService.Show(Hints.Info(Lang.Text("Setup.Java.EnableBeforeSelect")));
        return false;
    }

    public bool? ToggleJavaEnabled(JavaEntry entry)
    {
        try
        {
            var target = JavaRuntime.Javas.AddOrGet(entry.Installation.JavaExePath);
            if (target is null)
            {
                HintService.Show(Hints.Info(Lang.Text("Setup.Java.Unavailable")));
                return null;
            }

            if (target.IsEnabled && Config.Launch.SelectedJava == target.Installation.JavaExePath)
            {
                HintService.Show(Hints.Info(Lang.Text("Setup.Java.DeselectBeforeDisable")));
                return null;
            }

            target.IsEnabled = !target.IsEnabled;
            JavaRuntime.Javas.SaveConfig();
            return target.IsEnabled;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Setup.Java.EnableFailed"),
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Setup.Java.EnableFailed"));
            return null;
        }
    }

    public async Task ScanJavaAsync()
    {
        await RunAsync(ct => Task.Run(async () =>
        {
            await JavaService.JavaManager.ScanJavaAsync(force: true);
            JavaService.JavaManager.CheckAllAvailability();
        }, ct));
    }

    public async Task<bool> AddJavaAsync(string javaExePath)
    {
        await RunAsync(ct => Task.Run(() =>
        {
            JavaRuntime.Javas.AddOrGet(javaExePath);
            JavaRuntime.Javas.SaveConfig();
        }, ct));
        return JavaRuntime.Javas.Exist(javaExePath);
    }
}
