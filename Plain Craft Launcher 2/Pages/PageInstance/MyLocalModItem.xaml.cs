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
        var currentName = Entry.compFile.fileName.Replace(".jar", "");
        var newestName = Entry.UpdateFile.fileName.Replace(".jar", "");
        // 简化名称对比
        var currentSegs = currentName.Split('-').ToList();
        var newestSegs = newestName.Split('-').ToList();
        var shortened = false;
        foreach (var Seg in currentSegs.ToList())
        {
            if (!newestSegs.Contains(Seg))
                continue;
            currentSegs.Remove(Seg);
            newestSegs.Remove(Seg);
            shortened = true;
        }

        if (shortened && currentSegs.Any() && newestSegs.Any())
        {
            currentName = currentSegs.Join("-");
            newestName = newestSegs.Join("-");
            Entry._Version = currentName; // 使用网络信息作为显示的版本号
        }

        return
            Lang.Text("Instance.Resource.Item.UpdateCompare", currentName, Lang.TimeSpan(Entry.compFile.releaseDate - DateTime.Now), newestName, Lang.TimeSpan(Entry.UpdateFile.releaseDate - DateTime.Now));
    }

    public void Refresh()
    {
        Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            // 更新
            if (Entry.CanUpdate)
            {
                BtnUpdate.Visibility = Visibility.Visible;
                BtnUpdate.ToolTip = $"{GetUpdateCompareDescription()}\r\n{Lang.Text("Instance.Resource.Item.UpdateToolTip")}";
            }
            else
            {
                BtnUpdate.Visibility = Visibility.Collapsed;
            }

            // 标题与描述
            string descFileName;
            if (Entry.IsFolder)
                // 文件夹项的特殊处理
                descFileName = Entry.Name;
            else
                switch (Entry.State)
                {
                    case ModLocalComp.LocalCompFile.LocalFileStatus.Fine:
                    {
                        descFileName = ModBase.GetFileNameWithoutExtentionFromPath(Entry.path);
                        break;
                    }
                    case ModLocalComp.LocalCompFile.LocalFileStatus.Disabled:
                    {
                        descFileName =
                            ModBase.GetFileNameWithoutExtentionFromPath(Entry.path.Replace(".disabled", "")
                                .Replace(".old", "")); // McMod.McModState.Unavailable
                        break;
                    }

                    default:
                    {
                        descFileName = ModBase.GetFileNameFromPath(Entry.path);
                        break;
                    }
                }

            string newDescription;
            var compTemp = Entry.Comp;
            if (Entry.IsFolder)
            {
                // 文件夹项的特殊显示
                Title = Entry.Name;
                newDescription = Entry.Description;
            }
            else if (Config.Download.Comp.UiCompNameSolution == 1)
            {
                // 标题显示文件名，详情显示译名
                // 标题
                Title = descFileName;
                SubTitle = "";
                // 描述
                if (Entry.Comp is null)
                {
                    newDescription = Entry.Name;
                }
                else
                {
                    var titles = await Task.Run(() => compTemp.GetControlTitle(false));
                    newDescription = titles.Key + titles.Value;
                }

                newDescription = newDescription.Replace("  |  ", " / ");
                if (Entry.Version is not null)
                    newDescription += $" ({Entry.Version})";
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
                    var titles = await Task.Run(() => compTemp.GetControlTitle(false));
                    Title = titles.Key;
                    SubTitle = titles.Value + (Entry.Version is null ? "" : "  |  " + Entry.Version);
                }

                // 描述
                newDescription = descFileName;
            }

            if (Entry.Comp is not null)
                newDescription += ": " + Entry.Comp.description.Replace("\r", "").Replace("\n", "");
            else if (Entry.Description is not null)
                newDescription += ": " + Entry.Description.Replace("\r", "").Replace("\n", "");
            else if (!Entry.IsFileAvailable) newDescription += ": " + Lang.Text("Instance.Resource.Item.InfoUnavailable");
            Description = newDescription;
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
                if (imgState is not null)
                {
                    Children.Remove(imgState);
                    imgState = null;
                }
            }
            else
            {
                if (imgState is null)
                {
                    imgState = new Image
                    {
                        Width = 20d,
                        Height = 20d,
                        Margin = new Thickness(0d, 0d, -5, -3),
                        IsHitTestVisible = false,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom
                    };
                    RenderOptions.SetBitmapScalingMode(imgState, BitmapScalingMode.HighQuality);
                    SetColumn(imgState, 1);
                    SetRow(imgState, 1);
                    SetRowSpan(imgState, 2);
                    Children.Add(imgState);
                    // <Image x:Name="ImgState" RenderOptions.BitmapScalingMode="HighQuality" Width="16" Height="16" Margin="0,0,-3,-1"
                    // Grid.Column="1" Grid.Row="1" Grid.RowSpan="2" IsHitTestVisible="False"
                    // HorizontalAlignment="Right" VerticalAlignment="Bottom"
                    // Source="/Images/Icons/Unavailable.png" />
                }

                imgState.Source = new MyBitmap(ModBase.pathImage + $"Icons/{Entry.State}.png");
            }

            // 标签
            if (Entry.IsFolder)
                // 为文件夹添加标签
                Tags = new List<string> { Lang.Text("Instance.Resource.Item.FolderTag") };
            else if (Entry.Comp is not null) Tags = Entry.Comp.tags;
        }));
    }

    public void RefreshColor(object sender, EventArgs e)
    {
        InitLate(sender, e);
        // 触发颜色动画
        var time = IsMouseOver ? 120 : 180;
        var ani = new List<ModAnimation.AniData>();
        // ButtonStack
        if (buttonStack is not null)
        {
            if (IsMouseOver)
            {
                ani.Add(ModAnimation.AaOpacity(buttonStack, 1d - buttonStack.Opacity, (int)Math.Round(time * 0.7d),
                    (int)Math.Round(time * 0.3d)));
                ani.Add(ModAnimation.AaDouble(
                    i => ColumnPaddingRight.Width =
                        new GridLength(Math.Max(0, ColumnPaddingRight.Width.Value + (double)i)),
                    5 + Buttons.Count() * 25 - ColumnPaddingRight.Width.Value, (int)Math.Round(time * 0.3d),
                    (int)Math.Round(time * 0.7d)));
            }
            else
            {
                ani.Add(ModAnimation.AaOpacity(buttonStack, -buttonStack.Opacity, (int)Math.Round(time * 0.4d)));
                ani.Add(ModAnimation.AaDouble(
                    i => ColumnPaddingRight.Width =
                        new GridLength(Math.Max(0, ColumnPaddingRight.Width.Value + (double)i)),
                    4d - ColumnPaddingRight.Width.Value, (int)Math.Round(time * 0.4d)));
            }
        }

        // RectBack
        if (IsMouseOver || Checked)
        {
            ani.AddRange(new[]
            {
                ModAnimation.AaColor(RectBack, Border.BackgroundProperty, isMouseDown ? "ColorBrush6" : "ColorBrushBg1",
                    time),
                ModAnimation.AaOpacity(RectBack, 1d - RectBack.Opacity, time, Ease: new ModAnimation.AniEaseOutFluent())
            });
            if (isMouseDown)
                ani.Add(ModAnimation.AaScaleTransform(RectBack,
                    0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX, (int)Math.Round(time * 1.2d),
                    Ease: new ModAnimation.AniEaseOutFluent()));
            else
                ani.Add(ModAnimation.AaScaleTransform(RectBack, 1d - ((ScaleTransform)RectBack.RenderTransform).ScaleX,
                    (int)Math.Round(time * 1.2d), Ease: new ModAnimation.AniEaseOutFluent()));
        }
        else
        {
            ani.AddRange(new[]
            {
                ModAnimation.AaOpacity(RectBack, -RectBack.Opacity, time),
                ModAnimation.AaScaleTransform(RectBack, 0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX,
                    time, Ease: new ModAnimation.AniEaseOutFluent()),
                ModAnimation.AaScaleTransform(RectBack, -0.196d, 1, After: true)
            });
        }

        ModAnimation.AniStart(ani, "LocalModItem Color " + uuid);
    }

    // 触发虚拟化内容
    private void InitLate(object sender, EventArgs e)
    {
        if (buttonHandler is not null)
        {
            buttonHandler((MyLocalCompItem)sender, e);
            buttonHandler = null;
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
            if (!Information.IsNumeric(Entry.Comp.id))
            {
                var modrinthUrl = Entry.changelogUrls.FirstOrDefault(x => x.Contains("modrinth.com"));
                if (modrinthUrl is not null)
                {
                    ModBase.OpenWebsite(modrinthUrl);
                    return;
                }
            }
            else
            {
                var curseForgeUrl = Entry.changelogUrls.FirstOrDefault(x => x.Contains("curseforge.com"));
                if (curseForgeUrl is not null)
                {
                    ModBase.OpenWebsite(curseForgeUrl);
                    return;
                }
            }
        }

        ModBase.Log(Lang.Text("Instance.Resource.Item.OpenChangelogFailed"), ModBase.LogLevel.Hint);
    }

    // 触发更新
    private void BtnUpdate_Click(object sender, EventArgs e)
    {
        switch (ModMain.MyMsgBox(
                    $"{Lang.Text("Instance.Resource.Item.UpdateConfirm.Message", Entry.Name)}\r\n\r\n{GetUpdateCompareDescription()}",
                    Lang.Text("Instance.Resource.Item.UpdateConfirm.Title"),
                    Lang.Text("Instance.Resource.Item.Update"), Lang.Text("Instance.Resource.Item.ViewChangelog"), Lang.Text("Common.Action.Cancel")))
        {
            case 1: // 更新
            {
                switch (Entry.Comp.type)
                {
                    case ModComp.CompType.Mod:
                    {
                        ModMain.frmInstanceMod ??= new PageInstanceCompResource(ModComp.CompType.Mod);
                        ModMain.frmInstanceMod.UpdateResource(new[] { Entry });
                        break;
                    }
                    case ModComp.CompType.ResourcePack:
                    {
                        ModMain.frmInstanceResourcePack ??= new PageInstanceCompResource(ModComp.CompType.ResourcePack);
                        ModMain.frmInstanceResourcePack.UpdateResource(new[] { Entry });
                        break;
                    }
                    case ModComp.CompType.Shader:
                    {
                        ModMain.frmInstanceShader ??= new PageInstanceCompResource(ModComp.CompType.Shader);
                        ModMain.frmInstanceShader.UpdateResource(new[] { Entry });
                        break;
                    }
                    case ModComp.CompType.DataPack:
                    {
                        ModMain.frmInstanceSavesDatapack ??= new PageInstanceSavesDatapack();
                        ModMain.frmInstanceSavesDatapack.UpdateResource(new[] { Entry });
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
        var currentCompressLevel =
            ColumnExtend.Width.IsStar ? 0 : ColumnTitle.Width.IsStar ? 2 : 1; // Subtitle 可能是 Collapsed
        var newCompressLevel = default(int);
        switch (currentCompressLevel)
        {
            case 0:
            {
                if (ColumnExtend.ActualWidth < 0.5d)
                    newCompressLevel = LabSubtitle.Visibility == Visibility.Collapsed ? 2 : 1;
                else
                    return;

                break;
            }
            case 1:
            {
                if (ColumnSubtitle.ActualWidth < 0.5d)
                    newCompressLevel = 2;
                else if (!LabSubtitle.IsTextTrimmed())
                    newCompressLevel = 0;
                else
                    return;

                break;
            }
            case 2:
            {
                if (!LabTitle.IsTextTrimmed())
                    newCompressLevel = LabSubtitle.Visibility == Visibility.Collapsed ? 0 : 1;
                else
                    return;

                break;
            }
        }

        switch (newCompressLevel)
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

    public int uuid = ModBase.GetUuid();

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
            var rawValue = value;
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
                    value += Lang.Text("Instance.Resource.Item.ErrorSuffix");
                    break;
                }
            }

            if ((LabTitle.Text ?? "") == (value ?? ""))
                return;
            LabTitle.Text = value;
            _Title = rawValue;
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
                var newTag = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(12, 0, 0, 0)),
                    Padding = new Thickness(3d, 1d, 3d, 1d),
                    CornerRadius = new CornerRadius(3d),
                    Margin = new Thickness(0d, 0d, 3d, 0d),
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = false
                };
                var tagTextBlock = new TextBlock
                {
                    Text = TagText,
                    Foreground = new SolidColorBrush(ThemeManager.IsDarkMode
                        ? Color.FromArgb(88, 255, 255, 255)
                        : Color.FromArgb(88, 136, 136, 136)),
                    FontSize = 11d
                };
                newTag.Child = tagTextBlock;
                PanTags.Children.Add(newTag);
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
        changed += RefreshColor;
        // Handles
        BtnUpdate.PreviewMouseRightButtonUp += BtnUpdate_PreviewMouseRightButtonUp;
        BtnUpdate.Click += BtnUpdate_Click;
        PanTitle.SizeChanged += PanTitle_SizeChanged;
    }

    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (isMouseDown)
        {
            Click?.Invoke(sender, e);
            if (e.Handled)
                return;
            ModBase.Log("[Control] 按下本地 Mod 列表项：" + LabTitle.Text);
        }
    }

    // 鼠标点击判定
    private bool isMouseDown;

    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsMouseDirectlyOver)
            return;
        isMouseDown = true;
        if (buttonStack is not null)
            buttonStack.IsHitTestVisible = false;
    }

    private void Button_MouseLeave(object sender, object e)
    {
        isMouseDown = false;
        if (buttonStack is not null)
            buttonStack.IsHitTestVisible = true;
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
        var index = ((StackPanel)Parent).Children.IndexOf(this);
        CurrentSwipe.Start = index;
        CurrentSwipe.End = index;
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
        var elements = ((StackPanel)Parent).Children;
        var index = elements.IndexOf(this);
        CurrentSwipe.Start =
            (int)Math.Round(ModBase.MathClamp(Math.Min(CurrentSwipe.Start, index), 0d, elements.Count - 1));
        CurrentSwipe.End =
            (int)Math.Round(ModBase.MathClamp(Math.Max(CurrentSwipe.End, index), 0d, elements.Count - 1));
        // 勾选所有范围中的项
        if (CurrentSwipe.Start == CurrentSwipe.End)
            return;
        for (int i = CurrentSwipe.Start, loopTo = CurrentSwipe.End; i <= loopTo; i++)
        {
            var item = (MyLocalCompItem)elements[i];
            item.InitLate(item, (EventArgs)e);
            item.Checked = CurrentSwipe.SwipeToState;
        }
    }

    // 勾选状态
    public event CheckEventHandler? Check;

    public delegate void CheckEventHandler(object sender, ModBase.RouteEventArgs e);

    public event ChangedEventHandler? changed;

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
                var rawValue = _Checked;
                if (value == _Checked)
                    return;
                _Checked = value;
                var changedEventArgs = new ModBase.RouteEventArgs();
                if (IsInitialized)
                {
                    changed?.Invoke(this, changedEventArgs);
                    if (changedEventArgs.handled)
                    {
                        _Checked = rawValue;
                        return;
                    }
                }

                if (value)
                {
                    var checkEventArgs = new ModBase.RouteEventArgs();
                    Check?.Invoke(this, checkEventArgs);
                    if (checkEventArgs.handled)
                        return;
                }

                // 更改动画
                if (this.IsVisibleInWindow(ModMain.frmMain))
                {
                    var anim = new List<ModAnimation.AniData>();
                    if (Checked)
                    {
                        // 由无变有
                        var delta = 32d - RectCheck.ActualHeight;
                        anim.Add(ModAnimation.AaHeight(RectCheck, delta * 0.4d, 200,
                            Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
                        anim.Add(ModAnimation.AaHeight(RectCheck, delta * 0.6d, 300,
                            Ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
                        anim.Add(ModAnimation.AaOpacity(RectCheck, 1d - RectCheck.Opacity, 30));
                        RectCheck.VerticalAlignment = VerticalAlignment.Center;
                        RectCheck.Margin = new Thickness(-3, 0d, 0d, 0d);
                        anim.Add(ModAnimation.AaColor(LabTitle, TextBlock.ForegroundProperty,
                            Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine
                                ? "ColorBrush2"
                                : "ColorBrush5", 200));
                    }
                    else
                    {
                        // 由有变无
                        anim.Add(ModAnimation.AaHeight(RectCheck, -RectCheck.ActualHeight, 120,
                            Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
                        anim.Add(ModAnimation.AaOpacity(RectCheck, -RectCheck.Opacity, 70, 40));
                        RectCheck.VerticalAlignment = VerticalAlignment.Center;
                        anim.Add(ModAnimation.AaColor(LabTitle, TextBlock.ForegroundProperty,
                            LabTitle.TextDecorations is null ? "ColorBrush1" : "ColorBrushGray4", 120));
                    }

                    ModAnimation.AniStart(anim, "MyLocalCompItem Checked " + uuid);
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

                    ModAnimation.AniStop("MyLocalCompItem Checked " + uuid);
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
    private Image imgState;

    // 指向背景
    private Border _RectBack;

    public Border RectBack
    {
        get
        {
            if (_RectBack is null)
            {
                var rect = new Border
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
                rect.SetResourceReference(Border.BackgroundProperty, "ColorBrush7");
                rect.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6");
                SetColumnSpan(rect, 999);
                SetRowSpan(rect, 999);
                Children.Insert(0, rect);
                _RectBack = rect;
                // <!--<corelocal:BlurBorder x:Name = "RectBack" CornerRadius="3" RenderTransformOrigin="0.5,0.5" SnapsToDevicePixels="True" 
                // IsHitTestVisible = "False" Opacity="0" BorderThickness="1" 
                // Grid.ColumnSpan = "4" Background="{DynamicResource ColorBrush7}" BorderBrush="{DynamicResource ColorBrush6}"/>-->
            }

            return _RectBack;
        }
    }

    // 按钮
    public Action<MyLocalCompItem, EventArgs> buttonHandler;
    public FrameworkElement buttonStack;
    private IEnumerable<MyIconButton> _Buttons;

    public IEnumerable<MyIconButton> Buttons
    {
        get => _Buttons;
        set
        {
            _Buttons = value;
            // 移除原 Stack
            if (buttonStack is not null)
            {
                Children.Remove(buttonStack);
                buttonStack = null;
            }

            if (!value.Any())
                return;
            // 添加新 Stack
            buttonStack = new StackPanel
            {
                Opacity = 0d,
                Margin = new Thickness(0d, 0d, 5d, 0d),
                SnapsToDevicePixels = false,
                Orientation = (Orientation)System.Windows.Forms.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                UseLayoutRounding = false
            };
            SetColumnSpan(buttonStack, 10);
            SetRowSpan(buttonStack, 10);
            // 构造按钮
            foreach (var Btn in value)
            {
                if (Btn.Height.Equals(double.NaN))
                    Btn.Height = 25d;
                if (Btn.Width.Equals(double.NaN))
                    Btn.Width = 25d;
                ((StackPanel)buttonStack).Children.Add(Btn);
            }

            Children.Add(buttonStack);
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
