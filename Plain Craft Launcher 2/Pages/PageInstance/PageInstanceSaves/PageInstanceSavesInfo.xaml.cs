using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Humanizer;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Saves;
using PCL.Core.Minecraft.Saves.Editing;
using PCL.Core.UI;

namespace PCL;

public partial class PageInstanceSavesInfo : IRefreshable
{
    // 存档管理器 —— 在 Init 中创建，后续复用
    private SaveManager? _saveManager;

    // 当前正在显示的存档路径
    private string? _currentSavePath;

    // 当前存档的 Info，用于困难度修改时判断格式版本
    private SaveInfo? _currentSaveInfo;

    // 用于在 SelectionChanged 中抑制重复保存的标记
    private bool _suppressEvents;

    public PageInstanceSavesInfo()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
    }

    void IRefreshable.Refresh()
    {
        IRefreshable_Refresh();
    }

    private void IRefreshable_Refresh()
    {
        Refresh();
    }

    public void Refresh()
    {
        RefreshInfo();
    }

    private void Init()
    {
        PanBack.ScrollToHome();
        _saveManager ??= new SaveManager();
        RefreshInfo();
    }

    private async void RefreshInfo()
    {
        // 确保 _saveManager 已初始化
        _saveManager ??= new SaveManager();

        try
        {
            var saveFolder = PageInstanceSavesLeft.currentSave;
            if (string.IsNullOrEmpty(saveFolder) || !Directory.Exists(saveFolder))
                return;

            _currentSavePath = saveFolder;

            // 使用 SaveManager 加载存档信息
            var saveInfo = await _saveManager!.LoadSaveAsync(saveFolder);
            _currentSaveInfo = saveInfo;

            ClearInfoTable();
            PanSettingsList.Children.Clear();
            PanSettingsList.RowDefinitions.Clear();

            // 清空所有版本提示
            Hintversion1_9.Visibility = Visibility.Collapsed;
            Hintversion1_8.Visibility = Visibility.Collapsed;
            Hintversion1_3.Visibility = Visibility.Collapsed;
            PanSettings.Visibility = Visibility.Collapsed;

            // 显示版本提示（仅当无版本名时）
            if (saveInfo.VersionName is null)
            {
                ShowVersionHint();
            }
            else
            {
                AddInfoTable(Lang.Text("Instance.Saves.Info.Version"),
                    $"{saveInfo.VersionName} ({saveInfo.VersionId})");
            }

            // 控制数据包按钮可见性（1.9 = DataVersion 1444 起）
            if (ModMain.frmInstanceSavesLeft?.ItemDatapack is not null)
                ModMain.frmInstanceSavesLeft.ItemDatapack.Visibility =
                    !saveInfo.VersionId.HasValue || saveInfo.VersionId < 1444
                        ? Visibility.Collapsed
                        : Visibility.Visible;

            // 显示种子
            var seedText = saveInfo.Seed?.ToString() ?? Lang.Text("Instance.Saves.Info.GetFailed");
            AddInfoTable(Lang.Text("Instance.Saves.Info.Seed"), seedText, true, saveInfo.VersionName, true);

            // 构建设置面板
            BuildSettingsPanel();

            // 最后游玩时间
            AddInfoTable(Lang.Text("Instance.Saves.Info.LastPlayed"),
                Lang.Date(saveInfo.LastPlayedUtc.ToLocalTime(), "g"));

            // 出生点
            if (saveInfo.Spawn.HasValue)
            {
                var s = saveInfo.Spawn.Value;
                AddInfoTable(Lang.Text("Instance.Saves.Info.SpawnPoint"), $"{s.X} / {s.Y} / {s.Z}");
            }

            // 游戏模式
            var gameTypeName = ResolveGameModeName(saveInfo);
            AddInfoTable(Lang.Text("Instance.Saves.Info.GameMode"), gameTypeName);

            // 难度信息（仅当存档有难度时才显示）
            if (saveInfo.Difficulty.HasValue)
            {
                var difficultyName = ResolveDifficultyName(saveInfo.Difficulty.Value);
                var lockText = saveInfo.IsHardcore
                    ? Lang.Text("Common.Option.Yes")
                    : saveInfo.IsDifficultyLocked
                        ? Lang.Text("Common.Option.Yes")
                        : Lang.Text("Common.Option.No");
                if (Hintversion1_8.Visibility != Visibility.Visible)
                    AddInfoTable(Lang.Text("Instance.Saves.Info.Hardness"),
                        $"{difficultyName}  |  {Lang.Text("Instance.Saves.Info.DifficultyLocked", lockText)}");
            }

            // 游玩时长
            var formattedPlayTime = Lang.TimeSpan(
                saveInfo.PlayTime,
                precision: 3,
                addAffixes: false,
                maxUnit: TimeUnit.Day,
                minUnit: TimeUnit.Second);
            AddInfoTable(Lang.Text("Instance.Saves.Info.PlayTime"), formattedPlayTime);

            PanContent.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "获取存档信息失败");
            PanContent.Visibility = Visibility.Collapsed;
            PanSettings.Visibility = Visibility.Collapsed;
            Hintversion1_9.Visibility = Visibility.Collapsed;
            Hintversion1_8.Visibility = Visibility.Collapsed;
            Hintversion1_3.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 根据存档字段特征显示大致的版本范围提示。
    /// </summary>
    private void ShowVersionHint()
    {
        if (_currentSaveInfo!.Difficulty.HasValue)
        {
            Hintversion1_9.Visibility = Visibility.Visible;
            Hintversion1_9.Text = Lang.Text("Instance.Saves.Info.VersionHint.1_9");
        }
        else if (_currentSaveInfo.AllowCommands)
        {
            Hintversion1_8.Visibility = Visibility.Visible;
            Hintversion1_8.Text = Lang.Text("Instance.Saves.Info.VersionHint.1_8");
        }
        else
        {
            Hintversion1_3.Visibility = Visibility.Visible;
            Hintversion1_3.Text = Lang.Text("Instance.Saves.Info.VersionHint.1_3");
        }
    }

    /// <summary>
    /// 构建允许作弊和难度修改 UI 控件，并挂接事件处理。
    /// 事件处理中调用 SaveManager.ApplyChangesAsync 以写入 NBT。
    /// </summary>
    private void BuildSettingsPanel()
    {
        var hasAllowCommands = _currentSaveInfo!.AllowCommands || _currentSaveInfo.VersionId.HasValue
            || _currentSaveInfo.Difficulty.HasValue;

        if (!hasAllowCommands && !_currentSaveInfo.Difficulty.HasValue)
            return;

        // ─── 允许作弊 ───
        if (hasAllowCommands)
        {
            PanSettings.Visibility = Visibility.Visible;

            var combo = new MyComboBox
            {
                Width = 100d,
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = Lang.Text("Instance.Saves.Info.Modify.BeforeSave"),
            };
            combo.Items.Add(new { Value = 0, Display = Lang.Text("Instance.Saves.Info.AllowCommands.NotAllowed") });
            combo.Items.Add(new { Value = 1, Display = Lang.Text("Instance.Saves.Info.AllowCommands.Allowed") });
            combo.SelectedValuePath = "Value";
            combo.DisplayMemberPath = "Display";
            combo.SelectedValue = _currentSaveInfo.AllowCommands ? 1 : 0;

            combo.SelectionChanged += async (_, _) =>
            {
                if (_suppressEvents || combo.SelectedValue is null) return;
                var newVal = (int)combo.SelectedValue == 1;
                await ApplyEditAsync(new SaveChanges { AllowCommands = new Editable<bool>(newVal) },
                    Lang.Text("Instance.Saves.Info.Modify.CheatSuccess"));
            };

            AddControlToSettings(Lang.Text("Instance.Saves.Info.AllowCommands"), combo);
        }

        // ─── 难度设置 ───
        if (_currentSaveInfo.Difficulty.HasValue)
        {
            PanSettings.Visibility = Visibility.Visible;

            var difficultyCombo = new MyComboBox
            {
                Width = 100d,
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = Lang.Text("Instance.Saves.Info.Modify.BeforeSave"),
            };
            difficultyCombo.Items.Add(new { Value = 0, Display = Lang.Text("Instance.Saves.Info.Difficulty.Peaceful") });
            difficultyCombo.Items.Add(new { Value = 1, Display = Lang.Text("Instance.Saves.Info.Difficulty.Easy") });
            difficultyCombo.Items.Add(new { Value = 2, Display = Lang.Text("Instance.Saves.Info.Difficulty.Normal") });
            difficultyCombo.Items.Add(new { Value = 3, Display = Lang.Text("Instance.Saves.Info.Difficulty.Hard") });
            difficultyCombo.SelectedValuePath = "Value";
            difficultyCombo.DisplayMemberPath = "Display";
            difficultyCombo.SelectedValue = (byte)_currentSaveInfo.Difficulty.Value;

            var lockCheckBox = new MyCheckBox
            {
                Text = Lang.Text("Instance.Saves.Info.LockDifficulty"),
                ToolTip = Lang.Text("Instance.Saves.Info.LockDifficulty.ToolTip"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10d, 0d, 0d, 0d),
            };

            if (_currentSaveInfo.IsHardcore)
            {
                // 极限模式下锁定难度不可见（始终锁定）
                lockCheckBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                lockCheckBox.Checked = _currentSaveInfo.IsDifficultyLocked;
            }

            difficultyCombo.SelectionChanged += async (_, _) =>
            {
                if (_suppressEvents || difficultyCombo.SelectedValue is null) return;
                var newDifficulty = (Difficulty)(byte)difficultyCombo.SelectedValue;
                var isHardcore = _currentSaveInfo.IsHardcore;

                var changes = new SaveChanges { Difficulty = new Editable<Difficulty>(newDifficulty) };
                if (!isHardcore)
                    changes.LockDifficulty = new Editable<bool>(lockCheckBox.Checked == true);

                await ApplyEditAsync(changes, Lang.Text("Instance.Saves.Info.Modify.DifficultySuccess"));
            };

            lockCheckBox.Change += async (_, user) =>
            {
                if (_suppressEvents || !user || difficultyCombo.SelectedValue is null) return;
                var newDifficulty = (Difficulty)(byte)difficultyCombo.SelectedValue;
                var isHardcore = _currentSaveInfo.IsHardcore;

                var changes = new SaveChanges { Difficulty = new Editable<Difficulty>(newDifficulty) };
                if (!isHardcore)
                    changes.LockDifficulty = new Editable<bool>(lockCheckBox.Checked == true);

                await ApplyEditAsync(changes, Lang.Text("Instance.Saves.Info.Modify.DifficultySuccess"));
            };

            var difficultyPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            difficultyPanel.Children.Add(difficultyCombo);
            difficultyPanel.Children.Add(lockCheckBox);

            AddControlToSettings(Lang.Text("Instance.Saves.Info.GameDifficultyLabel"), difficultyPanel);
        }
    }

    /// <summary>
    /// 将修改写入 level.dat 并刷新 UI。
    /// </summary>
    private async Task ApplyEditAsync(SaveChanges changes, string successMessage)
    {
        try
        {
            _suppressEvents = true;
            await _saveManager!.ApplyChangesAsync(_currentSavePath!, changes);
            ModMain.Hint(successMessage, ModMain.HintType.Finish);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "存档设置修改失败");
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    /// <summary>
    /// 将控件添加到设置面板指定行。
    /// </summary>
    private void AddControlToSettings(string label, UIElement control)
    {
        var rowIndex = PanSettingsList.RowDefinitions.Count;
        PanSettingsList.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Auto) });

        var headTextBlock = new TextBlock { Text = label, Margin = new Thickness(0d, 3d, 0d, 3d) };
        Grid.SetRow(headTextBlock, rowIndex);
        Grid.SetColumn(headTextBlock, 0);

        Grid.SetRow(control, rowIndex);
        Grid.SetColumn(control, 2);

        PanSettingsList.Children.Add(headTextBlock);
        PanSettingsList.Children.Add(control);

        PanSettingsList.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8d, GridUnitType.Pixel) });
    }

    private static string ResolveGameModeName(SaveInfo info) => info.GameMode switch
    {
        GameMode.Survival => Lang.Text("Instance.Saves.Info.GameMode.Survival"),
        GameMode.Creative => Lang.Text("Instance.Saves.Info.GameMode.Creative"),
        GameMode.Adventure => Lang.Text("Instance.Saves.Info.GameMode.Adventure"),
        GameMode.Spectator => Lang.Text("Instance.Saves.Info.GameMode.Spectator"),
        GameMode.Hardcore => Lang.Text("Instance.Saves.Info.GameMode.Hardcore"),
        _ => Lang.Text("Instance.Saves.Info.GetFailed"),
    };

    private static string ResolveDifficultyName(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Peaceful => Lang.Text("Instance.Saves.Info.Difficulty.Peaceful"),
        Difficulty.Easy => Lang.Text("Instance.Saves.Info.Difficulty.Easy"),
        Difficulty.Normal => Lang.Text("Instance.Saves.Info.Difficulty.Normal"),
        Difficulty.Hard => Lang.Text("Instance.Saves.Info.Difficulty.Hard"),
        _ => Lang.Text("Instance.Saves.Info.GetFailed"),
    };

    #region 表格工具方法

    private void ClearInfoTable()
    {
        PanList.Children.Clear();
        PanList.RowDefinitions.Clear();
    }

    private void AddInfoTable(string head, string content, bool isSeed = false, string? versionName = null,
        bool allowCopy = false)
    {
        var headTextBlock = new TextBlock { Text = head, Margin = new Thickness(0d, 3d, 0d, 3d) };
        var contentStack = new StackPanel { Orientation = Orientation.Horizontal };
        UIElement contentTextBlock;
        if (allowCopy)
        {
            var thisBtn = new MyTextButton { Text = content, Margin = new Thickness(0d, 3d, 0d, 3d) };
            contentTextBlock = thisBtn;
            thisBtn.Click += (_, _) =>
            {
                try
                {
                    ModBase.ClipboardSet(content);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "复制到剪贴板失败", ModBase.LogLevel.Hint);
                }
            };
        }
        else
        {
            contentTextBlock = new TextBlock { Text = content, Margin = new Thickness(0d, 3d, 0d, 3d) };
        }

        contentStack.Children.Add(contentTextBlock);

        if (isSeed && content != Lang.Text("Instance.Saves.Info.GetFailed"))
        {
            var btnChunkbase = new MyIconButton
            {
                Logo = Icon.IconButtonlink,
                ToolTip = Lang.Text("Instance.Saves.Info.Chunkbase.ToolTip"),
                Width = 22d,
                Height = 22d,
            };
            contentStack.Children.Add(btnChunkbase);

            btnChunkbase.Click += (_, _) =>
            {
                try
                {
                    if (versionName is null)
                    {
                        ModBase.Log(Lang.Text("Instance.Saves.Info.Chunkbase.UnknownVersion"),
                            ModBase.LogLevel.Hint);
                        return;
                    }

                    if (versionName.Any(c => char.IsLetter(c)))
                    {
                        ModBase.Log(
                            Lang.Text("Instance.Saves.Info.Chunkbase.PreviewVersion", versionName),
                            ModBase.LogLevel.Hint);
                        return;
                    }

                    var versionParts = versionName.Split('.');
                    string usedVersion;
                    if (versionName.StartsWith("1.21"))
                        usedVersion = versionName.Replace(".", "_");
                    else if (versionName.Contains("."))
                        usedVersion = string.Join("_", versionName.Split('.').Take(2));
                    else
                        usedVersion = versionName.Replace(".", "_");
                    var cbUri =
                        $"https://www.chunkbase.com/apps/seed-map#seed={content}&platform=java_{usedVersion}&dimension=overworld";
                    ModBase.OpenWebsite(cbUri);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "跳转到 Chunkbase 失败", ModBase.LogLevel.Hint);
                }
            };
        }

        PanList.Children.Add(headTextBlock);
        PanList.Children.Add(contentStack);
        var targetRow = new RowDefinition();
        PanList.RowDefinitions.Add(targetRow);
        var rowIndex = PanList.RowDefinitions.IndexOf(targetRow);
        Grid.SetRow(headTextBlock, rowIndex);
        Grid.SetColumn(headTextBlock, 0);
        Grid.SetRow(contentTextBlock, rowIndex);
        Grid.SetColumn(contentTextBlock, 2);
        Grid.SetRow(contentStack, rowIndex);
        Grid.SetColumn(contentStack, 2);
    }

    #endregion
}
