// Unable to use.
// Because the code contains references to other parts of the project that are not provided, I cannot complete the code in a meaningful way.
// We need more refactoring work to make this code snippet self-contained and functional.

//namespace PCL.Core.App;

//public class Feedback
//{
//    public static void Feedback(bool ShowMsgbox = true, bool ForceOpenLog = false)
//    {
//        // On Error Resume Next
//        FeedbackInfo();
//        string currentDate;
//        currentDate = Strings.Format(DateTime.Now, "yyyy-M-dd");

//        if (ForceOpenLog || (ShowMsgbox &&
//                             ModMain.MyMsgBox(
//                                 "若你在汇报一个 Bug，请点击 打开文件夹 按钮，并上传 Launch-" + currentDate + "-[一串数字].log 中包含错误信息的文件。" +
//                                 "\r\n" + "游戏崩溃一般与启动器无关，请不要因为游戏崩溃而提交反馈。", "反馈提交提醒", "打开文件夹", "不需要") ==
//                             1))
//        {
//            Basics.OpenPath(Path.Combine(Basics.ExecutableDirectory, "PCL", "Log"));
//        }

//        ShellUtils.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/issues/");
//    }

//    public static bool CanFeedback(bool ShowHint)
//    {
//        var stat = ModSecret.GetVersionStatus();
//        if (stat != ModSecret.VersionStatus.Latest)
//        {
//            if (ShowHint)
//                if (ModMain.MyMsgBox(
//                        stat == ModSecret.VersionStatus.NotLatest
//                            ? $"你的 PCL 不是最新版，因此无法提交反馈。{"\r\n"}请在更新后，确认该问题在最新版中依然存在，然后再提交反馈。"
//                            : $"你的 PCL 检查更新失败，因此无法提交反馈。{"\r\n"}请连接到互联网，在检查更新后，确认该问题在最新版中依然存在，然后再提交反馈。",
//                        "无法提交反馈", stat == ModSecret.VersionStatus.NotLatest ? "更新" : "重新检查更新", "取消") == 1)
//                    ModMain.FrmMain.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupUpdate);

//            return false;
//        }

//        return true;
//    }

//    /// <summary>
//    /// 在日志中输出系统诊断信息。
//    /// </summary>
//    public static void FeedbackInfo()
//    {
//        try
//        {
//            // Get system memory info
//            var phyRam = KernelInterop.GetPhysicalMemoryBytes();

//            // Calculate memory and DPI scale
//            var availableMb = phyRam.Available / 1024 / 1024;
//            var totalMb = phyRam.Total / 1024 / 1024;
//            var dpiScale = Math.Round(DPI / 96.0, 2);

//            // Build diagnostic information string
//            var info = $"[System] Diagnostic Information:{"\r\n"}" +
//                       $"OS: {RuntimeInformation.OSDescription} (32-bit: {Is32BitSystem}){"\r\n"}" +
//                       $"Memory: {availableMb} MB / {totalMb} MB{"\r\n"}" +
//                       $"DPI: {DPI} ({dpiScale * 100}%){"\r\n"}" +
//                       $"MC Folder: {ModMinecraft.McFolderSelected ?? "Nothing"}{"\r\n"}" +
//                       $"Executable Path: {Basics.ExecutableDirectory}";

//            LogWrapper.Info(info);
//        }
//        catch (Exception ex)
//        {
//            // Basic fail-safe to replace "On Error Resume Next"
//            LogWrapper.Error(ex, "Failed to collect feedback information");
//        }
//    }
//}