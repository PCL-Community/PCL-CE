// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PCL;

public partial class PageOnline
{
    public PageOnline()
    {
        InitializeComponent();
        PageEnter += RefreshAccountCard;
    }

    private void RefreshAccountCard()
    {
        if (PCL.Online.OnlineAccountService.IsLoggedIn)
        {
            PanNotLoggedIn.Visibility = Visibility.Collapsed;
            PanLoggedIn.Visibility = Visibility.Visible;
            LabUserName.Text = PCL.Online.OnlineAccountService.UserName ?? "未知";
            LabAccountType.Text = PCL.Online.OnlineAccountService.OwnsMinecraft
                ? "已拥有 Minecraft 正版" : "未拥有 Minecraft";
            var url = PCL.Online.OnlineAccountService.AvatarUrl;
            ImgAvatar.Source = null;
            if (!string.IsNullOrEmpty(url))
                try
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(Path.GetFullPath(url), UriKind.Absolute);
                    image.EndInit();
                    image.Freeze();
                    ImgAvatar.Source = image;
                }
                catch
                {
                    ImgAvatar.Source = null;
                }
        }
        else
        {
            PanNotLoggedIn.Visibility = Visibility.Visible;
            PanLoggedIn.Visibility = Visibility.Collapsed;
            ImgAvatar.Source = null;
        }
    }

    private void BtnLogin_Click(object sender, ModBase.RouteEventArgs e)
    {
        BtnLogin.IsEnabled = false;
        ModBase.RunInNewThread(() =>
        {
            var result = PCL.Online.OnlineAccountService.Login(prepareJson =>
            {
                var converter = new ModMain.MyMsgBoxConverter
                    { Content = prepareJson, ForceWait = true, Type = ModMain.MyMsgBoxType.Login };
                ModBase.RunInUi(() => ModMain.WaitingMyMsgBox.Add(converter));
                while (converter.Result is null) Thread.Sleep(100);
                return converter.Result;
            });

            ModBase.RunInUi(() =>
            {
                if (result.Success && result.OwnsMinecraft)
                    ModProfile.AddProfileFromOnline(result);
                ModMain.Hint(result.Message,
                    result.Success ? ModMain.HintType.Finish : ModMain.HintType.Critical);
                RefreshAccountCard();
                BtnLogin.IsEnabled = true;
            });
        }, "OnlineLogin");
    }

    private void BtnLogout_Click(object sender, ModBase.RouteEventArgs e)
    {
        PCL.Online.OnlineAccountService.Logout();
        RefreshAccountCard();
        ModMain.Hint("已退出登录", ModMain.HintType.Finish);
    }

}
