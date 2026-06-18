using PCL.Core.App.Localization;

namespace PCL;

internal static class MicrosoftLoginPolicyGate
{
    public static bool EnsureAccepted()
    {
        if (PCL.Online.FirstLaunchService.IsAccepted())
            return true;

        var legalText = PCL.Online.FirstLaunchService.LoadFullText();
        if (ModMain.MyMsgBoxMarkdown(legalText, Lang.Text("Main.Legal.Title"),
                Lang.Text("Main.Legal.Agree"), Lang.Text("Main.Legal.Decline"),
                isWarn: false, forceWait: true) != 1)
            return false;

        PCL.Online.FirstLaunchService.Accept();
        return true;
    }
}
