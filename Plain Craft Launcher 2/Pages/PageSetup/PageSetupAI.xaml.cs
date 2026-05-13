using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;

namespace PCL;

public partial class PageSetupAI
{
    private new bool IsLoaded;

    public PageSetupAI()
    {
        InitializeComponent();
        Loaded += PageSetupAI_Loaded;
        Loaded += (_, _) => Reload();
    }

    private void PageSetupAI_Loaded(object sender, RoutedEventArgs e)
    {
        PanBack.ScrollToHome();
        if (IsLoaded)
            return;
        IsLoaded = true;

        ModAnimation.AniControlEnabled += 1;
        Reload();
        ModAnimation.AniControlEnabled -= 1;
    }

    public void Reload()
    {
        CheckAiAnalysis.Checked = Config.Tool.AI.Enabled;
        ComboAiApiType.SelectedIndex = Config.Tool.AI.ApiType == 1 ? 1 : 0;
        TextAiBaseUrl.Text = Config.Tool.AI.BaseUrl;
        TextAiModelId.Text = Config.Tool.AI.ModelId;
        TextAiApiKey.Text = Config.Tool.AI.ApiKey;
    }

    public void Reset()
    {
        try
        {
            Config.Tool.AI.Reset();
            ModBase.Log("[Setup] 已初始化 AI 分析页设置");
            ModMain.Hint("已初始化 AI 分析页设置！", ModMain.HintType.Finish, false);
            Reload();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化 AI 分析页设置失败", ModBase.LogLevel.Msgbox);
        }

        Reload();
    }

    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.Checked);
    }

    private void TextBoxChange(object senderRaw, TextChangedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.Text);
    }

    private void ComboChange(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.SelectedIndex);
    }
}
