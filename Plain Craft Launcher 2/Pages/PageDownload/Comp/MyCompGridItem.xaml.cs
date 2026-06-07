using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL;

public partial class MyCompGridItem
{
    public bool CanInteraction { get; set; } = true;
    public int Uuid = ModBase.GetUuid();
    private string stateLast;

    public string Logo
    {
        get => PathLogo.Source;
        set => PathLogo.Source = value;
    }

    public string Title
    {
        get => LabTitle.Text;
        set
        {
            if ((LabTitle.Text ?? "") == (value ?? ""))
                return;
            LabTitle.Text = value;
        }
    }

    public string SubTitle
    {
        get => LabTitleRaw?.Text ?? "";
        set
        {
            if ((LabTitleRaw.Text ?? "") == (value ?? ""))
                return;
            LabTitleRaw.Text = value;
            LabTitleRaw.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

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

    public List<string> Tags
    {
        set
        {
            if (value.Count > 0)
            {
                Tag1.Text = value[0];
                TagBorder1.Visibility = Visibility.Visible;
            }
            if (value.Count > 1)
            {
                Tag2.Text = value[1];
                TagBorder2.Visibility = Visibility.Visible;
            }
        }
    }

    public bool ShowFavoriteBtn
    {
        set => PanButtons.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        get => PanButtons.Visibility == Visibility.Visible;
    }

    public event ClickEventHandler? Click;

    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e);

    public MyCompGridItem()
    {
        InitializeComponent();
        Click += (sender, e) => MyCompGridItem_Click((MyCompGridItem)sender, e);
        MouseLeftButtonUp += (sender, e) => 
        {
            if (CanInteraction)
            {
                var clickPosition = e.GetPosition(this);
                var isClickOnButton = false;

                if (PanButtons.Visibility == Visibility.Visible)
                {
                    var buttonBounds = new Rect(BtnDelete.TranslatePoint(new Point(0d, 0d), this), BtnDelete.RenderSize);
                    isClickOnButton = buttonBounds.Contains(clickPosition);
                }

                if (!isClickOnButton)
                {
                    Click?.Invoke(this, e);
                }
            }
        };
        MouseEnter += RefreshColor;
        MouseLeave += RefreshColor;
        BtnDelete.Click += BtnDelete_Click;
    }

    public void RefreshFavoriteStatus()
    {
        if (Tag is not ModComp.CompProject project) return;

        var isFavourite = ModComp.CompFavorites.IsFavourite(project.Id);
        BtnDelete.SvgIcon = isFavourite ? "lucide/heart-filled" : "lucide/heart";
        ShowFavoriteBtn = isFavourite;
    }

    private void BtnDelete_Click(object sender, EventArgs e)
    {
        if (PanButtons.Opacity > 0d && Tag is ModComp.CompProject)
        {
            var project = (ModComp.CompProject)Tag;
            ModComp.CompFavorites.ShowMenu(project, (UIElement)sender, () => RefreshFavoriteStatus());
        }
    }

    private void MyCompGridItem_Click(MyCompGridItem sender, EventArgs e)
    {
        var titles = new List<string>();
        if (ModMain.frmMain.pageCurrent.page == FormMain.PageType.CompDetail)
        {
            foreach (MyCard Card in ModMain.frmDownloadCompDetail.PanResults.Children)
                if (!string.IsNullOrEmpty(Card.Title) && !Card.IsSwapped)
                    titles.Add(Card.Title);
            ModBase.Log("[Comp] 记录当前已展开的卡片：" + string.Join("、", titles));
            var additional = ModMain.frmMain.pageCurrent.additional.Value;
            ModMain.frmMain.pageCurrent.additional = additional with { ExpandedTitles = titles };
        }

        var targetType = default(ModComp.CompType);
        string targetVersion = null;
        var targetLoader = ModComp.CompLoaderType.Any;
        if (ModMain.frmMain.pageCurrent.page == FormMain.PageType.Download)
        {
            if (ModMain.frmMain.PageCurrentSub == FormMain.PageSubType.DownloadCompFavorites)
            {
                targetVersion = "";
                targetLoader = ModComp.CompLoaderType.Any;
            }
            else
            {
                switch (ModMain.frmMain.PageCurrentSub)
                {
                    case FormMain.PageSubType.DownloadMod:
                        {
                            targetType = ModComp.CompType.Mod;
                            targetVersion = ModMain.frmDownloadMod.Content.loader.input.gameVersion;
                            targetLoader = ModMain.frmDownloadMod.Content.loader.input.modLoader;
                            break;
                        }
                    case FormMain.PageSubType.DownloadPack:
                        {
                            targetType = ModComp.CompType.ModPack;
                            targetVersion = ModMain.frmDownloadPack.Content.loader.input.gameVersion;
                            break;
                        }
                    case FormMain.PageSubType.DownloadDataPack:
                        {
                            targetType = ModComp.CompType.DataPack;
                            targetVersion = ModMain.frmDownloadDataPack.Content.loader.input.gameVersion;
                            break;
                        }
                    case FormMain.PageSubType.DownloadResourcePack:
                        {
                            targetType = ModComp.CompType.ResourcePack;
                            targetVersion = ModMain.frmDownloadResourcePack.Content.loader.input.gameVersion;
                            break;
                        }
                    case FormMain.PageSubType.DownloadShader:
                        {
                            targetType = ModComp.CompType.Shader;
                            targetVersion = ModMain.frmDownloadShader.Content.loader.input.gameVersion;
                            break;
                        }
                    case FormMain.PageSubType.DownloadWorld:
                        {
                            targetType = ModComp.CompType.World;
                            targetVersion = ModMain.frmDownloadWorld.Content.loader.input.gameVersion;
                            break;
                        }
                }
            }
        }
        else if (ModMain.frmMain.pageCurrent.page == FormMain.PageType.InstanceSetup)
        {
            targetType = ModComp.CompType.ModPack;
        }
        else
        {
            targetType = ModComp.CompType.Any;
            var additional = ModMain.frmMain.pageCurrent.additional.Value;
            targetVersion = additional.TargetVersion;
            targetLoader = additional.TargetLoader;
        }

        ModMain.frmMain.PageChange(new FormMain.PageStackData
        {
            page = FormMain.PageType.CompDetail,
            additional = ((ModComp.CompProject)sender.Tag, new List<string>(), targetVersion, targetLoader, targetType, null, null, null)
        });
    }

    public void RefreshColor(object sender, EventArgs e)
    {
        if (!CanInteraction)
            return;

        string stateNew = IsMouseOver ? "MouseOver" : "Idle";

        if ((stateLast ?? "") == (stateNew ?? ""))
            return;
        stateLast = stateNew;

        if (IsMouseOver)
        {
            if (PanButtons is not null && ShowFavoriteBtn)
                PanButtons.Opacity = 1;
            PanBack.BorderBrush = (Brush)FindResource("ColorBrush2");
            PanBack.BorderThickness = new Thickness(2);
        }
        else
        {
            if (PanButtons is not null)
                PanButtons.Opacity = 0;
            PanBack.BorderBrush = (Brush)FindResource("ColorBrush6");
            PanBack.BorderThickness = new Thickness(1);
        }
    }
}
