using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualBasic;
using PCL.Core.App;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.App.Localization;

namespace PCL;

public partial class MyLocalCompItem
{
    private string GetUpdateCompareDescription()
    {
        var CurrentName = Entry.CompFile.FileName.Replace(".jar", "");
        var NewestName = Entry.UpdateFile.FileName.Replace(".jar", "");
        // 简化名称对比
        var CurrentSegs = CurrentName.Split('-').ToList();
        var NewestSegs = NewestName.Split('-').ToList();
        var Shortened = false;
        foreach (var Seg in CurrentSegs.ToList())
        {
            if (!NewestSegs.Contains(Seg))
                continue;
            CurrentSegs.Remove(Seg);
            NewestSegs.Remove(Seg);
            Shortened = true;
        }

        if (Shortened && CurrentSegs.Any() && NewestSegs.Any())
        {
            CurrentName = CurrentSegs.Join("-");
            NewestName = NewestSegs.Join("-");
            Entry._Version = CurrentName; // 使用网络信息作为显示的版本号
        }

        return
            $"当前版本：{CurrentName}（{Lang.TimeSpan(Entry.CompFile.ReleaseDate - DateTime.Now)}）{"\r\n"}最新版本：{NewestName}（{Lang.TimeSpan(Entry.UpdateFile.ReleaseDate - DateTime.Now)}）";
    }

    public void Refresh()
    {
        Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            // 更新
            if (Entry.CanUpdate)
            {
                BtnUpdate.Visibility = Visibility.Visible;
                BtnUpdate.ToolTip = $"{GetUpdateCompareDescription()}{"\r\n"}点击以更新，右键查看更新日志。";
            }
            else
            {
                BtnUpdate.Visibility = Visibility.Collapsed;
            }

            // 标题与描述
            string DescFileName;
            if (Entry.IsFolder)
                // 文件夹项的特殊处理
                DescFileName = Entry.Name;
            else
                switch (Entry.State)
                {
                    case ModLocalComp.LocalCompFile.LocalFileStatus.Fine:
                    {
                        DescFileName = ModBase.GetFileNameWithoutExtentionFromPath(Entry.Path);
                        break;
                    }
                    case ModLocalComp.LocalCompFile.LocalFileStatus.Disabled:
                    {
                        DescFileName =
                            ModBase.GetFileNameWithoutExtentionFromPath(Entry.Path.Replace(".disabled", "")
                                .Replace(".old", "")); // McMod.McModState.Unavailable
                        break;
                    }

                    default:
                    {
                        DescFileName = ModBase.GetFileNameFromPath(Entry.Path);
                        break;
                    }
                }

            string NewDescription;
            var compTemp = Entry.Comp;
            if (Entry.IsFolder)
            {
                // 文件夹项的特殊显示
                Title = Entry.Name;
                NewDescription = Entry.Description;
            }
            else if (Config.Download.Comp.UiCompNameSolution == 1)
            {
                // 标题显示文件名，详情显示译名
                // 标题
                Title = DescFileName;
                SubTitle = "";
                // 描述
                if (Entry.Comp is null)
                {
                    NewDescription = Entry.Name;
                }
                else
                {
                    var Titles = await Task.Run(() => compTemp.GetControlTitle(false));
                    NewDescription = Titles.Key + Titles.Value;
                }

                NewDescription = NewDescription.Replace("  |  ", " / ");
                if (Entry.Version is not null)
                    NewDescription += $" ({Entry.Version})";
            }
            else
            {
                // 标题显示译名，详情显示文件名
                // 标题
                if (Entry.Comp is null)
                {
                    Title = Entry.Name;
                    SubTitle = Entry.Version is null ? "" : "  |  " + Entry.Version;
                }
                else
                {
                    var Titles = await Task.Run(() => compTemp.GetControlTitle(false));
                    Title = Titles.Key;
                    SubTitle = Titles.Value + (Entry.Version is null ? "" : "  |  " + Entry.Version);
                }

                // 描述
                NewDescription = DescFileName;
            }

            if (Entry.Comp is not null)
                NewDescription += ": " + Entry.Comp.Description.Replace("\r", "").Replace("\n", "");
            else if (Entry.Description is not null)
                NewDescription += ": " + Entry.Description.Replace("\r", "").Replace("\n", "");
            else if (!Entry.IsFileAvailable) NewDescription += ": " + "存在错误，无法获取信息";
            Description = NewDescription;
            if (Checked)
                LabTitle.SetResourceReference(TextBlock.ForegroundProperty,
                    Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine ? "ColorBrush2" : "ColorBrush5");
            else
                LabTitle.SetResourceReference(TextBlock.ForegroundProperty,
                    Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine ? "ColorBrush1" : "ColorBrushGray4");
            // 主 Logo
            Logo = Entry.GetLogo();

            // 图标右下角的 Logo
            if (Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine)
            {
                if (ImgState is not null)
                {
                    Children.Remove(ImgState);
                    ImgState = null;
                }
            }
            else
            {
                if (ImgState is null)
                {
                    ImgState = new Image
                    {
                        Width = 20d,
                        Height = 20d,
                        Margin = new Thickness(0d, 0d, -5, -3),
                        IsHitTestVisible = false,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom
                    };
                    RenderOptions.SetBitmapScalingMode(ImgState, BitmapScalingMode.HighQuality);
                    SetColumn(ImgState, 1);
                    SetRow(ImgState, 1);
                    SetRowSpan(ImgState, 2);
                    Children.Add(ImgState);
                    // <Image x:Name="ImgState" RenderOptions.BitmapScalingMode="HighQuality" Width="16" Height="16" Margin="0,0,-3,-1"
                    // Grid.Column="1" Grid.Row="1" Grid.RowSpan="2" IsHitTestVisible="False"
                    // HorizontalAlignment="Right" VerticalAlignment="Bottom"
                    // Source="/Images/Icons/Unavailable.png" />
                }

                ImgState.Source = new MyBitmap(ModBase.PathImage + $"Icons/{Entry.State}.png");
            }

            // 标签
            if (Entry.IsFolder)
                // 为文件夹添加标签
                Tags = new List<string> { "文件夹" };
            else if (Entry.Comp is not null) Tags = Entry.Comp.Tags;
        }));
    }

    public void RefreshColor(object sender, EventArgs e)
    {
        InitLate(sender, e);
        // 触发颜色动画
        var Time = IsMouseOver ? 120 : 180;
        var Ani = new List<ModAnimation.AniData>();
        // ButtonStack
        if (ButtonStack is not null)
        {
            if (IsMouseOver)
            {
                Ani.Add(ModAnimation.AaOpacity(ButtonStack, 1d - ButtonStack.Opacity, (int)Math.Round(Time * 0.7d),
                    (int)Math.Round(Time * 0.3d)));
                Ani.Add(ModAnimation.AaDouble(
                    i => ColumnPaddingRight.Width =
                        new GridLength(Math.Max(0, ColumnPaddingRight.Width.Value + (double)i)),
                    5 + Buttons.Count() * 25 - ColumnPaddingRight.Width.Value, (int)Math.Round(Time * 0.3d),
                    (int)Math.Round(Time * 0.7d)));
            }
            else
            {
                Ani.Add(ModAnimation.AaOpacity(ButtonStack, -ButtonStack.Opacity, (int)Math.Round(Time * 0.4d)));
                Ani.Add(ModAnimation.AaDouble(
                    i => ColumnPaddingRight.Width =
                        new GridLength(Math.Max(0, ColumnPaddingRight.Width.Value + (double)i)),
                    4d - ColumnPaddingRight.Width.Value, (int)Math.Round(Time * 0.4d)));
            }
        }

        // RectBack
        if (IsMouseOver || Checked)
        {
            Ani.AddRange(new[]
            {
                ModAnimation.AaColor(RectBack, Border.BackgroundProperty, IsMouseDown ? "ColorBrush6" : "ColorBrushBg1",
                    Time),
                ModAnimation.AaOpacity(RectBack, 1d - RectBack.Opacity, Time, Ease: new ModAnimation.AniEaseOutFluent())
            });
            if (IsMouseDown)
                Ani.Add(ModAnimation.AaScaleTransform(RectBack,
                    0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX, (int)Math.Round(Time * 1.2d),
                    Ease: new ModAnimation.AniEaseOutFluent()));
            else
                Ani.Add(ModAnimation.AaScaleTransform(RectBack, 1d - ((ScaleTransform)RectBack.RenderTransform).ScaleX,
                    (int)Math.Round(Time * 1.2d), Ease: new ModAnimation.AniEaseOutFluent()));
        }
        else
        {
            Ani.AddRange(new[]
            {
                ModAnimation.AaOpacity(RectBack, -RectBack.Opacity, Time),
                ModAnimation.AaScaleTransform(RectBack, 0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX,
                    Time, Ease: new ModAnimation.AniEaseOutFluent()),
                ModAnimation.AaScaleTransform(RectBack, -0.196d, 1, After: true)
            });
        }

        ModAnimation.AniStart(Ani, "LocalModItem Color " + Uuid);
    }

    // 触发虚拟化内容
    private void InitLate(object sender, EventArgs e)
    {
        if (ButtonHandler is not null)
        {
            ButtonHandler((MyLocalCompItem)sender, e);
            ButtonHandler = null;
        }
    }

    // 显示更新日志
    private void BtnUpdate_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShowUpdateLog();
    }

    private void ShowUpdateLog()
    {
        if (Entry.Comp is not null)
        {
            if (!Information.IsNumeric(Entry.Comp.Id))
            {
                var modrinthUrl = Entry.ChangelogUrls.FirstOrDefault(x => x.Contains("modrinth.com"));
                if (modrinthUrl is not null)
                {
                    ModBase.OpenWebsite(modrinthUrl);
                    return;
                }
            }
            else
            {
                var curseForgeUrl = Entry.ChangelogUrls.FirstOrDefault(x => x.Contains("curseforge.com"));
                if (curseForgeUrl is not null)
                {
                    ModBase.OpenWebsite(curseForgeUrl);
                    return;
                }
            }
        }

        ModBase.Log("打开更新日志出现错误", ModBase.LogLevel.Hint);
    }

    // 触发更新
    private void BtnUpdate_Click(object sender, EventArgs e)
    {
        switch (ModMain.MyMsgBox(
                    $"是否要更新 {Entry.Name}？{"\r\n"}{"\r\n"}{GetUpdateCompareDescription()}", "更新确认",
                    "更新", "查看更新日志", Lang.Text("Common.Action.Cancel")))
        {
            case 1: // 更新
            {
                switch (Entry.Comp.Type)
                {
                    case ModComp.CompType.Mod:
                    {
                        ModMain.FrmInstanceMod.UpdateResource(new[] { Entry });
                        break;
                    }
                    case ModComp.CompType.ResourcePack:
                    {
                        ModMain.FrmInstanceResourcePack.UpdateResource(new[] { Entry });
                        break;
                    }
                    case ModComp.CompType.Shader:
                    {
                        ModMain.FrmInstanceShader.UpdateResource(new[] { Entry });
                        break;
                    }
                    case ModComp.CompType.DataPack:
                    {
                        ModMain.FrmInstanceSavesDatapack.UpdateResource(new[] { Entry });
                        break;
                    }
                }

                break;
            }
            case 2: // 查看更新日志
            {
                ShowUpdateLog();
                break;
            }
            case 3: // 取消
            {
                break;
            }
        }
    }

    // 自适应（#4465）
    private void PanTitle_SizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
    {
        // 0：全部舒展：Auto - Auto - (Auto) - 1*
        // 1：压缩 Subtitle：Auto - 1* - (Auto) - 0
        // 2：继续压缩 Title：1* - 0 - (Auto) - 0
        var CurrentCompressLevel =
            ColumnExtend.Width.IsStar ? 0 : ColumnTitle.Width.IsStar ? 2 : 1; // Subtitle 可能是 Collapsed
        var NewCompressLevel = default(int);
        switch (CurrentCompressLevel)
        {
            case 0:
            {
                if (ColumnExtend.ActualWidth < 0.5d)
                    NewCompressLevel = LabSubtitle.Visibility == Visibility.Collapsed ? 2 : 1;
                else
                    return;

                break;
            }
            case 1:
            {
                if (ColumnSubtitle.ActualWidth < 0.5d)
                    NewCompressLevel = 2;
                else if (!LabSubtitle.IsTextTrimmed())
                    NewCompressLevel = 0;
                else
                    return;

                break;
            }
            case 2:
            {
                if (!LabTitle.IsTextTrimmed())
                    NewCompressLevel = LabSubtitle.Visibility == Visibility.Collapsed ? 0 : 1;
                else
                    return;

                break;
            }
        }

        switch (NewCompressLevel)
        {
            case 0:
            {
                // 全部舒展：Auto - Auto - (Auto) - 1*
                ColumnTitle.Width = GridLength.Auto;
                ColumnSubtitle.Width = GridLength.Auto;
                ColumnExtend.Width = new GridLength(1d, GridUnitType.Star);
                break;
            }
            case 1:
            {
                // 压缩 Subtitle：Auto - 1* - (Auto) - 0
                ColumnTitle.Width = GridLength.Auto;
                ColumnSubtitle.Width = new GridLength(1d, GridUnitType.Star);
                ColumnExtend.Width = new GridLength(0d, GridUnitType.Pixel);
                break;
            }
            case 2:
            {
                // 继续压缩 Title：1* - 0 - (Auto) - 0
                ColumnTitle.Width = new GridLength(1d, GridUnitType.Star);
                ColumnSubtitle.Width = new GridLength(0d, GridUnitType.Pixel);
                ColumnExtend.Width = new GridLength(0d, GridUnitType.Pixel);
                break;
            }
        }
    }

    #region 基础属性

    public int Uuid = ModBase.GetUuid();

    // Logo
    public string Logo
    {
        get => PathLogo.Source;
        set => PathLogo.Source = value;
    }

    // 标题
    private string _Title;

    public string Title
    {
        get => _Title;
        set
        {
            var RawValue = value;
            switch (Entry.State)
            {
                case ModLocalComp.LocalCompFile.LocalFileStatus.Fine:
                {
                    LabTitle.TextDecorations = null;
                    break;
                }
                case ModLocalComp.LocalCompFile.LocalFileStatus.Disabled:
                {
                    LabTitle.TextDecorations = TextDecorations.Strikethrough;
                    break;
                }
                case ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable:
                {
                    LabTitle.TextDecorations = TextDecorations.Strikethrough;
                    value += " [错误]";
                    break;
                }
            }

            if ((LabTitle.Text ?? "") == (value ?? ""))
                return;
            LabTitle.Text = value;
            _Title = RawValue;
        }
    }

    // 副标题
    public string SubTitle
    {
        get => LabSubtitle?.Text ?? "";
        set
        {
            if ((LabSubtitle.Text ?? "") == (value ?? ""))
                return;
            LabSubtitle.Text = value;
            LabSubtitle.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    // 描述
    public string Description
    {
        get => LabInfo.Text;
        set
        {
            if ((LabInfo.Text ?? "") == (value ?? ""))
                return;
            LabInfo.Text = value;
        }
    }

    // Tag
    public List<string> Tags
    {
        set
        {
            PanTags.Children.Clear();
            PanTags.Visibility = value.Any() ? Visibility.Visible : Visibility.Collapsed;
            foreach (var TagText in value)
            {
                var NewTag = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(12, 0, 0, 0)),
                    Padding = new Thickness(3d, 1d, 3d, 1d),
                    CornerRadius = new CornerRadius(3d),
                    Margin = new Thickness(0d, 0d, 3d, 0d),
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = false
                };
                var TagTextBlock = new TextBlock
                {
                    Text = TagText,
                    Foreground = new SolidColorBrush(ThemeManager.IsDarkMode
                        ? Color.FromArgb(88, 255, 255, 255)
                        : Color.FromArgb(88, 136, 136, 136)),
                    FontSize = 11d
                };
                NewTag.Child = TagTextBlock;
                PanTags.Children.Add(NewTag);
            }
        }
    }

    // 相关联的 Mod
    public ModLocalComp.LocalCompFile Entry
    {
        get => (ModLocalComp.LocalCompFile)Tag;
        set => Tag = value;
    }

    #endregion

    #region 点击与勾选

    // 触发点击事件
    public event ClickEventHandler? Click;

    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e);

    public MyLocalCompItem()
    {
        InitializeComponent();
        PreviewMouseLeftButtonUp += Button_MouseUp;
        PreviewMouseLeftButtonDown += Button_MouseDown;
        MouseLeave += Button_MouseLeave;
        PreviewMouseLeftButtonUp += Button_MouseLeave;
        MouseLeftButtonDown += Button_MouseSwipeStart;
        MouseEnter += Button_MouseSwipe;
        MouseLeave += Button_MouseSwipe;
        MouseLeftButtonUp += Button_MouseSwipe;
        Loaded += (_, _) => Refresh();
        MouseEnter += RefreshColor;
        MouseLeave += RefreshColor;
        MouseLeftButtonDown += RefreshColor;
        MouseLeftButtonUp += RefreshColor;
        Changed += RefreshColor;
        // Handles
        BtnUpdate.PreviewMouseRightButtonUp += BtnUpdate_PreviewMouseRightButtonUp;
        BtnUpdate.Click += BtnUpdate_Click;
        PanTitle.SizeChanged += PanTitle_SizeChanged;
    }

    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (IsMouseDown)
        {
            Click?.Invoke(sender, e);
            if (e.Handled)
                return;
            ModBase.Log("[Control] 按下本地 Mod 列表项：" + LabTitle.Text);
        }
    }

    // 鼠标点击判定
    private bool IsMouseDown;

    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsMouseDirectlyOver)
            return;
        IsMouseDown = true;
        if (ButtonStack is not null)
            ButtonStack.IsHitTestVisible = false;
    }

    private void Button_MouseLeave(object sender, object e)
    {
        IsMouseDown = false;
        if (ButtonStack is not null)
            ButtonStack.IsHitTestVisible = true;
    }

    // 滑动选中
    public class SwipeSelect
    {
        private bool _Swiping;
        public int Start { get; set; }
        public int End { get; set; }

        public bool Swiping
        {
            get => _Swiping;
            set
            {
                _Swiping = value;
                if (TargetFrm is not null)
                    try
                    {
                        var cardSelect = Interaction.CallByName(TargetFrm, "CardSelect", CallType.Get);
                        Interaction.CallByName(cardSelect, "IsHitTestVisible", CallType.Set, !value);
                    }
                    catch
                    {
                    }
            }
        }

        public bool SwipeToState { get; set; }
        public object TargetFrm { get; set; }
    }

    public SwipeSelect CurrentSwipe { get; set; }

    private void Button_MouseSwipeStart(object sender, object e)
    {
        if (Parent is null)
            return; // Mod 可能已被删除（#3824）
        // 开始滑动
        var Index = ((StackPanel)Parent).Children.IndexOf(this);
        CurrentSwipe.Start = Index;
        CurrentSwipe.End = Index;
        CurrentSwipe.Swiping = true;
        CurrentSwipe.SwipeToState = !Checked;
    }

    private void Button_MouseSwipe(object sender, object e)
    {
        if (Parent is null)
            return; // Mod 可能已被删除（#3824）
        // 结束滑动
        if (Mouse.LeftButton != MouseButtonState.Pressed || !(Mouse.DirectlyOver is MyLocalCompItem)) // #5771
        {
            CurrentSwipe.Swiping = false;
            return;
        }

        // 计算滑动范围
        var Elements = ((StackPanel)Parent).Children;
        var Index = Elements.IndexOf(this);
        CurrentSwipe.Start =
            (int)Math.Round(ModBase.MathClamp(Math.Min(CurrentSwipe.Start, Index), 0d, Elements.Count - 1));
        CurrentSwipe.End =
            (int)Math.Round(ModBase.MathClamp(Math.Max(CurrentSwipe.End, Index), 0d, Elements.Count - 1));
        // 勾选所有范围中的项
        if (CurrentSwipe.Start == CurrentSwipe.End)
            return;
        for (int i = CurrentSwipe.Start, loopTo = CurrentSwipe.End; i <= loopTo; i++)
        {
            var Item = (MyLocalCompItem)Elements[i];
            Item.InitLate(Item, (EventArgs)e);
            Item.Checked = CurrentSwipe.SwipeToState;
        }
    }

    // 勾选状态
    public event CheckEventHandler? Check;

    public delegate void CheckEventHandler(object sender, ModBase.RouteEventArgs e);

    public event ChangedEventHandler? Changed;

    public delegate void ChangedEventHandler(object sender, ModBase.RouteEventArgs e);

    private bool _Checked;

    public bool Checked
    {
        get => _Checked;
        set
        {
            try
            {
                // 触发属性值修改
                var RawValue = _Checked;
                if (value == _Checked)
                    return;
                _Checked = value;
                var ChangedEventArgs = new ModBase.RouteEventArgs();
                if (IsInitialized)
                {
                    Changed?.Invoke(this, ChangedEventArgs);
                    if (ChangedEventArgs.Handled)
                    {
                        _Checked = RawValue;
                        return;
                    }
                }

                if (value)
                {
                    var CheckEventArgs = new ModBase.RouteEventArgs();
                    Check?.Invoke(this, CheckEventArgs);
                    if (CheckEventArgs.Handled)
                        return;
                }

                // 更改动画
                if (this.IsVisibleInWindow(ModMain.FrmMain))
                {
                    var Anim = new List<ModAnimation.AniData>();
                    if (Checked)
                    {
                        // 由无变有
                        var Delta = 32d - RectCheck.ActualHeight;
                        Anim.Add(ModAnimation.AaHeight(RectCheck, Delta * 0.4d, 200,
                            Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
                        Anim.Add(ModAnimation.AaHeight(RectCheck, Delta * 0.6d, 300,
                            Ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
                        Anim.Add(ModAnimation.AaOpacity(RectCheck, 1d - RectCheck.Opacity, 30));
                        RectCheck.VerticalAlignment = VerticalAlignment.Center;
                        RectCheck.Margin = new Thickness(-3, 0d, 0d, 0d);
                        Anim.Add(ModAnimation.AaColor(LabTitle, TextBlock.ForegroundProperty,
                            Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine
                                ? "ColorBrush2"
                                : "ColorBrush5", 200));
                    }
                    else
                    {
                        // 由有变无
                        Anim.Add(ModAnimation.AaHeight(RectCheck, -RectCheck.ActualHeight, 120,
                            Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
                        Anim.Add(ModAnimation.AaOpacity(RectCheck, -RectCheck.Opacity, 70, 40));
                        RectCheck.VerticalAlignment = VerticalAlignment.Center;
                        Anim.Add(ModAnimation.AaColor(LabTitle, TextBlock.ForegroundProperty,
                            LabTitle.TextDecorations is null ? "ColorBrush1" : "ColorBrushGray4", 120));
                    }

                    ModAnimation.AniStart(Anim, "MyLocalCompItem Checked " + Uuid);
                }
                else
                {
                    // 不在窗口上时直接设置
                    RectCheck.VerticalAlignment = VerticalAlignment.Center;
                    RectCheck.Margin = new Thickness(-3, 0d, 0d, 0d);
                    if (Checked)
                    {
                        RectCheck.Height = 32d;
                        RectCheck.Opacity = 1d;
                        LabTitle.SetResourceReference(TextBlock.ForegroundProperty,
                            Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine
                                ? "ColorBrush2"
                                : "ColorBrush5");
                    }
                    else
                    {
                        RectCheck.Height = 0d;
                        RectCheck.Opacity = 0d;
                        LabTitle.SetResourceReference(TextBlock.ForegroundProperty,
                            Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine
                                ? "ColorBrush1"
                                : "ColorBrushGray4");
                    }

                    ModAnimation.AniStop("MyLocalCompItem Checked " + Uuid);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "设置 Checked 失败");
            }
        }
    }

    #endregion

    #region 后加载内容

    // 右下角状态指示图标
    private Image ImgState;

    // 指向背景
    private Border _RectBack;

    public Border RectBack
    {
        get
        {
            if (_RectBack is null)
            {
                var Rect = new Border
                {
                    Name = "RectBack",
                    CornerRadius = new CornerRadius(3d),
                    RenderTransform = new ScaleTransform(0.8d, 0.8d),
                    RenderTransformOrigin = new Point(0.5d, 0.5d),
                    BorderThickness = new Thickness(ModBase.GetWPFSize(1d)),
                    SnapsToDevicePixels = true,
                    IsHitTestVisible = false,
                    Opacity = 0d
                };
                Rect.SetResourceReference(Border.BackgroundProperty, "ColorBrush7");
                Rect.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6");
                SetColumnSpan(Rect, 999);
                SetRowSpan(Rect, 999);
                Children.Insert(0, Rect);
                _RectBack = Rect;
                // <!--<corelocal:BlurBorder x:Name = "RectBack" CornerRadius="3" RenderTransformOrigin="0.5,0.5" SnapsToDevicePixels="True" 
                // IsHitTestVisible = "False" Opacity="0" BorderThickness="1" 
                // Grid.ColumnSpan = "4" Background="{DynamicResource ColorBrush7}" BorderBrush="{DynamicResource ColorBrush6}"/>-->
            }

            return _RectBack;
        }
    }

    // 按钮
    public Action<MyLocalCompItem, EventArgs> ButtonHandler;
    public FrameworkElement ButtonStack;
    private IEnumerable<MyIconButton> _Buttons;

    public IEnumerable<MyIconButton> Buttons
    {
        get => _Buttons;
        set
        {
            _Buttons = value;
            // 移除原 Stack
            if (ButtonStack is not null)
            {
                Children.Remove(ButtonStack);
                ButtonStack = null;
            }

            if (!value.Any())
                return;
            // 添加新 Stack
            ButtonStack = new StackPanel
            {
                Opacity = 0d,
                Margin = new Thickness(0d, 0d, 5d, 0d),
                SnapsToDevicePixels = false,
                Orientation = (Orientation)System.Windows.Forms.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                UseLayoutRounding = false
            };
            SetColumnSpan(ButtonStack, 10);
            SetRowSpan(ButtonStack, 10);
            // 构造按钮
            foreach (var Btn in value)
            {
                if (Btn.Height.Equals(double.NaN))
                    Btn.Height = 25d;
                if (Btn.Width.Equals(double.NaN))
                    Btn.Width = 25d;
                ((StackPanel)ButtonStack).Children.Add(Btn);
            }

            Children.Add(ButtonStack);
        }
    }

    // 勾选条
    private Border _RectCheck;

    public Border RectCheck
    {
        get
        {
            if (_RectCheck is null)
            {
                _RectCheck = new Border
                {
                    Width = 5d,
                    Height = Checked ? double.NaN : 0d,
                    CornerRadius = new CornerRadius(2d, 2d, 2d, 2d),
                    VerticalAlignment = Checked ? VerticalAlignment.Stretch : VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    UseLayoutRounding = false,
                    SnapsToDevicePixels = false,
                    Margin = Checked ? new Thickness(-3, 6d, 0d, 6d) : new Thickness(-3, 0d, 0d, 0d)
                };
                _RectCheck.SetResourceReference(Border.BackgroundProperty, "ColorBrush3");
                SetRowSpan(_RectCheck, 10);
                Children.Add(_RectCheck);
            }

            return _RectCheck;
        }
    }

    #endregion
}
