using CommunityToolkit.Mvvm.ComponentModel;
using DotNet.Globbing;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实例导出页的视图模型。</summary>
public partial class PageInstanceExportViewModel : ViewModelBase
{
    [ObservableProperty] private string _currentVersion = "";

    [ObservableProperty] private string _exportName = "";

    [ObservableProperty] private string _exportNameHint = "";

    [ObservableProperty] private string _exportVersion = "";

    [ObservableProperty] private string _exportVersionHint = "1.0.0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OptionsVisible))]
    private bool _hasOverrides;

    [ObservableProperty] private bool _hintOptiFineVisible = true;

    [ObservableProperty] private bool _includePclEnabled = true;

    [ObservableProperty] private bool _modrinthEnabled = true;

    [ObservableProperty] private bool _otherFoldersVisible = true;

    public bool OptionsVisible => !HasOverrides;

    public void RefreshHeader(McInstance instance)
    {
        HintOptiFineVisible = instance.Info.HasOptiFine;
        CurrentVersion = instance.PathInstance ?? "";
        ExportName = "";
        ExportNameHint = instance.Name;
        ExportVersion = "";
        ExportVersionHint = "1.0.0";
    }

    public void RefreshOtherFolders(int optionCount)
    {
        OtherFoldersVisible = optionCount > 0;
    }

    /// <summary>
    ///     对文本行进行标准化处理，以便使用 Like 进行匹配。
    /// </summary>
    public static IEnumerable<string> StandardizeLines(IEnumerable<string> raw, bool addSuffixStarToFolderPath)
    {
        foreach (var IgnoreLineRaw in raw)
        {
            var ignoreLine = IgnoreLineRaw;
            ignoreLine = ignoreLine.Trim();
            if (string.IsNullOrEmpty(ignoreLine) || ignoreLine.StartsWithF("#") || ignoreLine.StartsWithF("="))
                continue;
            ignoreLine = ignoreLine.Replace("/", @"\");
            yield return ignoreLine + (ignoreLine.EndsWithF(@"\") && addSuffixStarToFolderPath ? "**" : "");
        }
    }

    public static bool LikeString(string input, string pattern)
    {
        pattern = pattern.Replace("#", "[0-9]");
        var options = new GlobOptions { Evaluation = { CaseInsensitive = true } };
        var glob = Glob.Parse(pattern, options);
        return glob.IsMatch(input);
    }
}
