using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>正版验证弹窗的视图模型。</summary>
public partial class MyMsgLoginViewModel : ViewModelBase
{
    [ObservableProperty] private string _captionText = string.Empty;

    public string UserCode { get; private set; } // 需要用户在网页上输入的设备代码

    public string Website { get; private set; } // 验证网页的网址

    public void Load(JsonObject data)
    {
        UserCode = (string)data["user_code"];
        if (data["verification_uri_complete"] is not null)
        {
            Website = (string)data["verification_uri_complete"];
            CaptionText = Lang.Text("Launch.Account.LoginDialog.MicrosoftInstructions.WithAutoFill", UserCode, Website);
        }
        else
        {
            Website = (string)data["verification_uri"];
            CaptionText = Lang.Text("Launch.Account.LoginDialog.MicrosoftInstructions", UserCode, Website);
        }
    }
}
