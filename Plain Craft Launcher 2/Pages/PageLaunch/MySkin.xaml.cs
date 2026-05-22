using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Network;

namespace PCL;

public partial class MySkin
{
    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e);

    // 皮肤储存
    private string _Address;
    private bool IsChanging;

    // 点击
    private bool IsSkinMouseDown;
    public ModLoader.LoaderTask<ModBase.EqualableList<string>, string> Loader;

    public MySkin()
    {
        InitializeComponent();
        MouseEnter += PanSkin_MouseEnter;
        MouseLeave += PanSkin_MouseLeave;
        MouseLeftButtonDown += PanSkin_MouseLeftButtonDown;
        MouseLeftButtonUp += PanSkin_MouseLeftButtonUp;
        // Handles
        BtnSkinSave.Click += BtnSkinSave_Click;
        BtnSkinSave.Checked += BtnSkinSave_Checked;
        BtnSkinRefresh.Click += RefreshClick;
        BtnSkinCape.Click += BtnSkinCape_Click;
    }

    public string Address
    {
        get => _Address;
        set
        {
            _Address = value;
            ToolTip = string.IsNullOrEmpty(_Address)
                ? Lang.Text("Common.State.Loading")
                : Lang.Text("Launch.Skin.Change.ToolTip");
        }
    }

    // 披风
    public bool HasCape
    {
        get => BtnSkinCape.Visibility == Visibility.Collapsed;
        set => BtnSkinCape.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    // 事件
    public event ClickEventHandler? Click;

    // 控件动画
    private void PanSkin_MouseEnter(object sender, MouseEventArgs e)
    {
        ModAnimation.AniStart(ModAnimation.AaOpacity(ShadowSkin, 0.8d - ShadowSkin.Opacity, 200, 100), "Skin Shadow");
    }

    private void PanSkin_MouseLeave(object sender, MouseEventArgs e)
    {
        ModAnimation.AniStart(ModAnimation.AaOpacity(ShadowSkin, 0.2d - ShadowSkin.Opacity, 200), "Skin Shadow");
        IsSkinMouseDown = false;
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(this, 1d - ((ScaleTransform)RenderTransform).ScaleX, 60,
                Ease: new ModAnimation.AniEaseOutFluent()), "Skin Scale");
    }

    private void PanSkin_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        IsSkinMouseDown = true;
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(this, 0.9d - ((ScaleTransform)RenderTransform).ScaleX, 60,
                Ease: new ModAnimation.AniEaseOutFluent()), "Skin Scale");
    }

    private void PanSkin_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(this, 1d - ((ScaleTransform)RenderTransform).ScaleX, 60,
                Ease: new ModAnimation.AniEaseOutFluent()), "Skin Scale");
        if (!IsSkinMouseDown) return;
        IsSkinMouseDown = false;
        Click?.Invoke(sender, e);
    }

    // 保存皮肤
    public void BtnSkinSave_Click(object sender, RoutedEventArgs e)
    {
        Save(Loader);
    }

    public static void Save(ModLoader.LoaderTask<ModBase.EqualableList<string>, string> Loader)
    {
        var Address = Loader.Output;
        if (Loader.State != ModBase.LoadState.Finished)
        {
            ModMain.Hint(Lang.Text("Launch.Skin.Fetching"), ModMain.HintType.Critical);
            if (Loader.State != ModBase.LoadState.Loading)
                Loader.Start();
            return;
        }

        try
        {
            var FileAddress = SystemDialogs.SelectSaveFile(Lang.Text("Launch.Skin.SaveDialog.Title"),
                ModBase.GetFileNameFromPath(Address),
                Lang.Text("Launch.Skin.SaveDialog.Filter"));
            if (!FileAddress.Contains(@"\")) return;
            File.Delete(FileAddress);
            if (Address.StartsWith(ModBase.PathImage))
            {
                var Image = new MyBitmap(Address);
                Image.Save(FileAddress);
            }
            else
            {
                ModBase.CopyFile(Address, FileAddress);
            }

            ModMain.Hint(Lang.Text("Launch.Skin.SaveSuccess"), ModMain.HintType.Finish);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Launch.Skin.Save.Error"), ModBase.LogLevel.Hint);
        }
    }

    private void BtnSkinSave_Checked(object sender, RoutedEventArgs e)
    {
        ((MyMenuItem)sender).IsEnabled = string.IsNullOrEmpty(Address);
    }

    /// <summary>
    ///     载入皮肤。
    /// </summary>
    public void Load()
    {
        try
        {
            // 检查文件存在
            Address = Loader.Output;
            if (string.IsNullOrEmpty(Address))
                throw new Exception("皮肤加载器 " + Loader.Name + " 没有输出");
            if (!Address.StartsWith(ModBase.PathImage) && !File.Exists(Address))
                throw new FileNotFoundException("皮肤文件未找到", Address);
            // 加载
            MyBitmap Image;
            try
            {
                Image = new MyBitmap(Address);
            }
            catch (Exception ex) // #2272
            {
                ModBase.Log(ex, Lang.Text("Launch.Skin.Load.Error.Corrupted", Address), ModBase.LogLevel.Hint);
                File.Delete(Address);
                return;
            }

            ImgBack.Tag = Address;
            // 大小检查
            var Scale = (int)Math.Round(Image.Pic.Width / 64d);
            if (Image.Pic.Width < 32 || Image.Pic.Height < 32)
            {
                ImgFore.Source = null;
                ImgBack.Source = null;
                throw new Exception("图片大小不足，长为 " + Image.Pic.Height + "，宽为 " + Image.Pic.Width);
            }

            MyBitmap SkinHead = null;
            // 头发层（附加层）
            if (Image.Pic.Width >= 64 && Image.Pic.Height >= 32)
            {
                if (Image.Pic.GetPixel(1, 1).A == 0 ||
                    Image.Pic.GetPixel(Image.Pic.Width - 1, Image.Pic.Height - 1).A == 0 ||
                    Image.Pic.GetPixel(Image.Pic.Width - 2, (int)Math.Round(Image.Pic.Height / 2d - 2d)).A == 0 ||
                    (Image.Pic.GetPixel(1, 1) != Image.Pic.GetPixel(Scale * 41, Scale * 9) &&
                     Image.Pic.GetPixel(Image.Pic.Width - 1, Image.Pic.Height - 1) !=
                     Image.Pic.GetPixel(Scale * 41, Scale * 9) &&
                     Image.Pic.GetPixel(Image.Pic.Width - 2, (int)Math.Round(Image.Pic.Height / 2d - 2d)) !=
                     Image.Pic.GetPixel(Scale * 41, Scale * 9))) // 如果图片中有任何透明像素（避免纯色白底）
                    // 或是头部颜色和透明区均不一样
                {
                    ImgFore.Source = Image.Clip(Scale * 40, Scale * 8, Scale * 8, Scale * 8);
                    SkinHead = Image.Clip(Scale * 40, Scale * 8, Scale * 8, Scale * 8);
                }
                else
                {
                    ImgFore.Source = null;
                }
            }
            else
            {
                ImgFore.Source = null;
            }

            // 脸层
            ImgBack.Source = Image.Clip(Scale * 8, Scale * 8, Scale * 8, Scale * 8);
            // 用于显示档案列表头像的图片
            var SkinHeadId = Address.Between(new[] { Address.Contains("Images/Skins/") ? "Skins/" : @"Skin\" }[0],
                ".png");
            var CachePath = ModBase.PathTemp + $@"Cache\Skin\Head\{SkinHeadId}.png";
            ModProfile.SelectedProfile.SkinHeadId = SkinHeadId;
            ModProfile.SaveProfile();
            var CompleteHead = new Bitmap(56, 56);
            using (var g = Graphics.FromImage(CompleteHead))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                using (Bitmap FaceBitmap = Image.Clip(Scale * 8, Scale * 8, Scale * 8, Scale * 8))
                {
                    g.DrawImage(FaceBitmap, new Rectangle(4, 4, 48, 48));
                }

                if (ImgFore.Source is not null)
                {
                    using Bitmap HairBitmap = Image.Clip(Scale * 40, Scale * 8, Scale * 8, Scale * 8);
                    g.DrawImage(HairBitmap, new Rectangle(0, 0, 56, 56));
                }
            }

            if (!Directory.Exists(ModBase.PathTemp + @"Cache\Skin\Head"))
                Directory.CreateDirectory(ModBase.PathTemp + @"Cache\Skin\Head");
            CompleteHead.Save(CachePath, ImageFormat.Png);
            ModBase.Log("[Skin] 载入头像成功：" + Loader.Name);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Launch.Skin.Load.Error.Avatar", (Address ?? "null") + "," + Loader.Name), ModBase.LogLevel.Hint);
        }
    }

    private object ScaleToSize(Bitmap Bitmap, int Width, int Height)
    {
        var ScaledBitmap = new Bitmap(Width, Height);
        using var g = Graphics.FromImage(ScaledBitmap);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(Bitmap, 0, 0, Width, Height);

        return ScaledBitmap;
    }

    /// <summary>
    ///     清空皮肤。
    /// </summary>
    public void Clear()
    {
        Address = "";
        ImgFore.Source = null;
        ImgBack.Source = null;
    }

    // 刷新缓存
    public void RefreshClick(object sender, RoutedEventArgs e)
    {
        RefreshCache(Loader);
    }

    /// <summary>
    ///     刷新皮肤缓存。
    /// </summary>
    public static void RefreshCache(ModLoader.LoaderTask<ModBase.EqualableList<string>, string> sender = null)
    {
        var HasLoaderRunning =
            PageLaunchLeft.SkinLoaders.Any(SkinLoader => SkinLoader.State == ModBase.LoadState.Loading);

        if (ModMain.FrmLaunchLeft is not null && HasLoaderRunning)
            // 由于 Abort 不是实时的，暂时不会释放文件，会导致删除报错，故只能取消执行
            ModMain.Hint(Lang.Text("Launch.Skin.Refresh.Busy"));
        else
            // 清空缓存
            // 刷新控件
            ModBase.RunInThread(() =>
            {
                try
                {
                    ModMain.Hint(Lang.Text("Launch.Skin.Refreshing"));
                    ModBase.Log("[Skin] 正在清空皮肤缓存");
                    if (Directory.Exists(ModBase.PathTemp + @"Cache\Skin"))
                        ModBase.DeleteDirectory(ModBase.PathTemp + @"Cache\Skin");
                    if (Directory.Exists(ModBase.PathTemp + @"Cache\Uuid"))
                        ModBase.DeleteDirectory(ModBase.PathTemp + @"Cache\Uuid");
                    ModBase.IniClearCache(ModBase.PathTemp + @"Cache\Skin\IndexMs.ini");
                    ModBase.IniClearCache(ModBase.PathTemp + @"Cache\Skin\IndexAuth.ini");
                    ModBase.IniClearCache(ModBase.PathTemp + @"Cache\Uuid\Mojang.ini");
                    foreach (var SkinLoader in sender is not null
                                 ? new[] { sender }
                                 : new[] { PageLaunchLeft.SkinLegacy, PageLaunchLeft.SkinMs })
                        SkinLoader.WaitForExit(IsForceRestart: true);
                    ModMain.Hint(Lang.Text("Launch.Skin.RefreshSuccess"), ModMain.HintType.Finish);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, Lang.Text("Launch.Skin.Refresh.Error"), ModBase.LogLevel.Msgbox);
                }
            });
    }

    /// <summary>
    ///     在更换正版皮肤后，刷新正版皮肤。
    /// </summary>
    /// <param name="SkinAddress">新的正版皮肤完整地址。</param>
    public static void ReloadCache(string SkinAddress)
    {
        // 更新缓存
        // 刷新控件
        // 完成提示
        ModBase.RunInThread(() =>
        {
            try
            {
                ModBase.WriteIni(ModBase.PathTemp + @"Cache\Skin\IndexMs.ini", ModProfile.SelectedProfile.Uuid,
                    SkinAddress);
                ModBase.Log($"[Skin] 已写入皮肤地址缓存 {ModProfile.SelectedProfile.Uuid} -> {SkinAddress}");
                foreach (var SkinLoader in new[] { PageLaunchLeft.SkinMs, PageLaunchLeft.SkinLegacy })
                    SkinLoader.WaitForExit(IsForceRestart: true);
                ModMain.Hint(Lang.Text("Launch.Skin.ChangeSuccess"), ModMain.HintType.Finish);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, Lang.Text("Launch.Skin.Change.Error.MsRefresh"), ModBase.LogLevel.Feedback);
            }
        });
    }

    public void BtnSkinCape_Click(object sender, RoutedEventArgs e)
    {
        // 检查条件，获取新披风
        if (IsChanging)
        {
            ModMain.Hint(Lang.Text("Launch.Skin.Cape.Changing"));
            return;
        }

        if (ModLaunch.McLoginMsLoader.State == ModBase.LoadState.Failed)
        {
            ModMain.Hint(Lang.Text("Launch.Skin.Cape.LoginFailed"), ModMain.HintType.Critical);
            return;
        }

        ModMain.Hint(Lang.Text("Launch.Skin.Cape.FetchingList"));
        IsChanging = true;
        // 开始实际获取
        ModBase.RunInNewThread(() =>
        {
            try
            {
                // 获取登录信息
                if (ModLaunch.McLoginMsLoader.State != ModBase.LoadState.Finished)
                    ModLaunch.McLoginMsLoader.WaitForExit(ModProfile.GetLoginData());
                if (ModLaunch.McLoginMsLoader.State != ModBase.LoadState.Finished)
                {
                    ModMain.Hint(Lang.Text("Launch.Skin.Cape.LoginFailed"), ModMain.HintType.Critical);
                    return;
                }

                var AccessToken = ModLaunch.McLoginMsLoader.Output.AccessToken;
                var Uuid = ModLaunch.McLoginMsLoader.Output.Uuid;
                var SkinData = (JObject)ModBase.GetJson(ModLaunch.McLoginMsLoader.Output.ProfileJson);
                foreach (var itemSkin in SkinData["capes"])
                {
                    if (itemSkin["url"] is null)
                        continue;
                    var localFile = $@"{ModBase.PathTemp}Cache\Capes\{itemSkin["alias"]}.png";
                    var capeFrontFile = $@"{ModBase.PathTemp}Cache\Capes\{itemSkin["alias"]}-front.png";
                    if (File.Exists(localFile) && File.Exists(capeFrontFile))
                    {
                        itemSkin["url"] = capeFrontFile;
                        continue;
                    }

                    FileDownloader.DownloadByLoader(itemSkin["url"].ToString(), localFile);
                    var capeFrontRegion = new Rectangle(1, 0, 11, 17);
                    var capeFront = new Bitmap(capeFrontRegion.Width, capeFrontRegion.Height);
                    var capeImage = Image.FromFile(localFile);
                    var gra = Graphics.FromImage(capeFront);
                    gra.DrawImage(capeImage, capeFrontRegion, capeFrontRegion, GraphicsUnit.Pixel);
                    capeFront.Save(capeFrontFile);
                    itemSkin["url"] = capeFrontFile;
                }

                // 获取玩家的所有披风
                int? SelId = null;
                ModBase.RunInUiWait(() =>
                {
                    try
                    {
                        var SelectionControl = new List<IMyRadio>
                        {
                            new MyListItem
                            {
                                Title = Lang.Text("Launch.Skin.Cape.None"),
                                Info = "Null"
                            }
                        };
                        SelectionControl.AddRange(from Cape in SkinData["capes"]
                            let CapeAlias = Cape["alias"].ToString()
                            let CapeName = _GetCapeDisplayName(CapeAlias)
                            let state = Cape["state"]
                            let active = state is not null & state.ToString().ToUpper().Equals("ACTIVE")
                            select new MyListItem
                            {
                                Title = CapeName,
                                Info = Cape["alias"].ToString(),
                                Checked = active,
                                Type = MyListItem.CheckType.RadioBox,
                                Logo = (string)Cape["url"],
                                LogoScale = 0.8d
                            });

                        SelId = ModMain.MyMsgBoxSelect(SelectionControl, Lang.Text("Launch.Skin.Cape.SelectTitle"),
                            Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel"));
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, Lang.Text("Launch.Skin.Cape.Error.List"), ModBase.LogLevel.Feedback);
                    }
                });
                if (SelId is null)
                    return;
                // 发送请求
                var Result = Requester.Fetch("https://api.minecraftservices.com/minecraft/profile/capes/active",
                    new FetchParam
                    {
                        Method = SelId is 0 ? "DELETE" : "PUT",
                        Content = SelId is 0
                            ? ""
                            : new JObject(new JProperty("capeId", SkinData["capes"][SelId - 1]["id"])).ToString(0),
                        ContentType = "application/json",
                        Headers = new Dictionary<string, string> { { "Authorization", "Bearer " + AccessToken } }
                    }
                );
                if (Result.Contains("\"errorMessage\""))
                    ModMain.Hint(
                        Lang.Text("Launch.Skin.Cape.ChangeFailedWithReason",
                            ((JObject)ModBase.GetJson(Result))["errorMessage"]), ModMain.HintType.Critical);
                else
                    ModMain.Hint(Lang.Text("Launch.Skin.Cape.ChangeSuccess"), ModMain.HintType.Finish);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, Lang.Text("Launch.Skin.Cape.ChangeFailed"), ModBase.LogLevel.Hint);
            }
            finally
            {
                IsChanging = false;
            }
        }, "Cape Change");
    }

    private static string _GetCapeDisplayName(string capeAlias)
    {
        var safeName = capeAlias
            .Replace("-", "")
            .Replace(" ", "")
            .Replace("'", "");
        var key = $"Launch.Skin.Cape.Name.{safeName}";
        var name = Lang.Text(key);
        if (name == $"!{key}!" || name == key)
            return capeAlias;
        return name;
    }
}