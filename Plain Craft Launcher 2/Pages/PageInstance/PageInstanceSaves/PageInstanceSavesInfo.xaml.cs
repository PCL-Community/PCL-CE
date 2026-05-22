using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using fNbt;
using PCL.Core.UI;

namespace PCL;

public partial class PageInstanceSavesInfo : IRefreshable
{
    private bool _loaded;

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

        RefreshInfo();

        _loaded = true;
        if (_loaded)
            return;
    }

    private void RefreshInfo()
    {
        try
        {
            var saveDatPath = Path.Combine(PageInstanceSavesLeft.CurrentSave, "level.dat");
            using (var fs = new FileStream(saveDatPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var saveInfo = new NbtFile();
                saveInfo.LoadFromStream(fs, NbtCompression.AutoDetect);
                ClearInfoTable();
                PanSettingsList.Children.Clear();
                PanSettingsList.RowDefinitions.Clear();

                Hintversion1_9.Visibility = Visibility.Collapsed;
                Hintversion1_8.Visibility = Visibility.Collapsed;
                Hintversion1_3.Visibility = Visibility.Collapsed;
                PanSettings.Visibility = Visibility.Collapsed;

                var gameLevel = saveInfo.RootTag.Get<NbtCompound>("Data");
                AddInfoTable("存档名称", gameLevel.Get<NbtString>("LevelName").Value);
                NbtString versionName = null;
                NbtInt versionId = null;
                var gameVersion = gameLevel.Get<NbtCompound>("Version");
                if (gameVersion is not null)
                {
                    gameVersion.TryGet("Name", out versionName);
                    gameVersion.TryGet("Id", out versionId);
                }

                var CurrentVersionId = versionId?.Value ?? default(int?);
                ModMain.FrmInstanceSavesLeft.ItemDatapack.Visibility =
                    !CurrentVersionId.HasValue || CurrentVersionId < 1444 ? Visibility.Collapsed : Visibility.Visible;

                var hasDifficulty = gameLevel.Contains("Difficulty") || gameLevel.Contains("difficulty_settings");
                var hasAllowCommands = gameLevel.Contains("allowCommands");

                if (versionName is null)
                {
                    if (hasDifficulty)
                    {
                        Hintversion1_9.Visibility = Visibility.Visible;
                        Hintversion1_9.Text = "1.9 以下的版本无法获取存档版本";
                    }
                    else if (hasAllowCommands)
                    {
                        Hintversion1_8.Visibility = Visibility.Visible;
                        Hintversion1_8.Text = "1.8 以下的版本无法获取存档版本和游戏难度";
                    }
                    else
                    {
                        Hintversion1_3.Visibility = Visibility.Visible;
                        Hintversion1_3.Text = "1.3 以下的版本无法获取存档版本、游戏难度和是否允许作弊";
                    }
                }
                else
                {
                    AddInfoTable("存档版本", $"{versionName.Value} ({versionId.Value})");
                }

                NbtLong seedNbt = null;
                string seed;
                if (gameLevel.TryGet("RandomSeed", out seedNbt))
                    seed = seedNbt.Value.ToString();
                else
                {
                    if (gameLevel.Contains("WorldGenSettings"))
                    {
                        seed = gameLevel.Get<NbtCompound>("WorldGenSettings").Get<NbtLong>("seed").Value.ToString();
                    }
                    else
                    {
                        string worldGenSettingsDatPath = System.IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "data", "minecraft", "world_gen_settings.dat");
                        NbtFile worldGenSettingsNbt = new NbtFile(worldGenSettingsDatPath);
                        var worldGenSettings = worldGenSettingsNbt.RootTag.Get<NbtCompound>("data");
                        seed = worldGenSettings.Get<NbtLong>("seed").Value.ToString();
                    }
                }

                AddInfoTable("种子", seed, true, versionName?.Value, true);

                if (hasAllowCommands)
                {
                    PanSettings.Visibility = Visibility.Visible;
                    var allowCommandValue = int.Parse(gameLevel.Get<NbtByte>("allowCommands").Value.ToString());
                    var combo = new MyComboBox
                    {
                        Width = 100d, HorizontalAlignment = HorizontalAlignment.Left,
                        ToolTip = "修改设置前请确保该存档未在游戏中打开，否则会导致设置无效"
                    };
                    combo.Items.Add(new { Value = 0, Display = "不允许" });
                    combo.Items.Add(new { Value = 1, Display = "允许" });
                    combo.SelectedValuePath = "Value";
                    combo.DisplayMemberPath = "Display";
                    combo.SelectedValue = allowCommandValue;

                    combo.SelectionChanged += (s, e) =>
                    {
                        try
                        {
                            var newVal = (byte)combo.SelectedValue;
                            gameLevel.Get<NbtByte>("allowCommands").Value = (byte)newVal;
                            using (var fileStream = new FileStream(saveDatPath, FileMode.Create, FileAccess.Write,
                                       FileShare.None))
                            {
                                saveInfo.SaveToStream(fileStream, NbtCompression.GZip);
                            }

                            ModMain.Hint("作弊设置修改成功", ModMain.HintType.Finish);
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "作弊设置修改失败", ModBase.LogLevel.Hint);
                        }
                    };
                    var rowIndex = PanSettingsList.RowDefinitions.Count;
                    PanSettingsList.RowDefinitions.Add(new RowDefinition
                        { Height = new GridLength(1d, GridUnitType.Auto) });

                    var headTextBlock = new TextBlock { Text = "是否允许作弊", Margin = new Thickness(0d, 3d, 0d, 3d) };
                    Grid.SetRow(headTextBlock, rowIndex);
                    Grid.SetColumn(headTextBlock, 0);

                    Grid.SetRow(combo, rowIndex);
                    Grid.SetColumn(combo, 2);

                    PanSettingsList.Children.Add(headTextBlock);
                    PanSettingsList.Children.Add(combo);
                    PanSettingsList.RowDefinitions.Add(new RowDefinition
                        { Height = new GridLength(8d, GridUnitType.Pixel) });
                }

                if (hasDifficulty)
                {
                    PanSettings.Visibility = Visibility.Visible;
                    NbtByte difficultyElement;

                    if (gameLevel.Contains("difficulty_settings"))
                    {
                        var difficultyElementString = gameLevel.Get<NbtCompound>("difficulty_settings").Get<NbtString>("difficulty").Value;
                        byte value = difficultyElementString switch
                        {
                            "peaceful" => 0,
                            "easy" => 1,
                            "normal" => 2,
                            "hard" => 3,
                            _ => 0
                        };
                        difficultyElement = new NbtByte("Difficulty", value);
                    }
                    else
                    {
                        difficultyElement = gameLevel.Get<NbtByte>("Difficulty");
                    }

                    var difficultyValue = difficultyElement.Value;

                    var difficultyCombo = new MyComboBox
                    {
                        Width = 100d, HorizontalAlignment = HorizontalAlignment.Left,
                        ToolTip = "修改设置前请确保该存档未在游戏中打开，否则会导致设置无效"
                    };
                    difficultyCombo.Items.Add(new { Value = 0, Display = "和平" });
                    difficultyCombo.Items.Add(new { Value = 1, Display = "简单" });
                    difficultyCombo.Items.Add(new { Value = 2, Display = "普通" });
                    difficultyCombo.Items.Add(new { Value = 3, Display = "困难" });
                    difficultyCombo.SelectedValuePath = "Value";
                    difficultyCombo.DisplayMemberPath = "Display";
                    difficultyCombo.SelectedValue = difficultyValue;

                    NbtByte isHardcoreCheck = null;

                    if (gameLevel.Contains("difficulty_settings"))
                        isHardcoreCheck = gameLevel.Get<NbtCompound>("difficulty_settings").Get<NbtByte>("hardcore");
                    else
                        isHardcoreCheck = gameLevel.Get<NbtByte>("hardcore");
                        
                    var isHardcoreMode = isHardcoreCheck.Value == 1;

                    var lockCheckBox = new MyCheckBox
                    {
                        Text = "锁定难度", ToolTip = "锁定当前难度设置，锁定后无法在游戏中更改游戏难度",
                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10d, 0d, 0d, 0d)
                    };

                    if (isHardcoreMode)
                    {
                        lockCheckBox.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        NbtByte lockedElement;

                        if (gameLevel.Contains("difficulty_settings"))
                            lockedElement = gameLevel.Get<NbtCompound>("difficulty_settings").Get<NbtByte>("locked");
                        else
                            lockedElement = gameLevel.Get<NbtByte>("DifficultyLocked");
                        
                        var isLocked = lockedElement is not null && lockedElement.Value == 1;
                        lockCheckBox.Checked = isLocked;
                    }

                    var difficultyPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    difficultyPanel.Children.Add(difficultyCombo);
                    difficultyPanel.Children.Add(lockCheckBox);


                    difficultyCombo.SelectionChanged += (s, e) =>
                    {
                        try
                        {
                            if (difficultyCombo.SelectedValue is null) return;
                            var newDifficulty = (byte)difficultyCombo.SelectedValue;
                            if (gameLevel.Contains("difficulty_settings"))
                            {
                                var newDifficultyString = GetDifficultyName(newDifficulty);
                                gameLevel.Get<NbtCompound>("difficulty_settings").Get<NbtString>("difficulty").Value = newDifficultyString;
                            }
                            else
                            {
                                gameLevel.Get<NbtByte>("Difficulty").Value = (byte)newDifficulty;
                            }
                            
                            if (!isHardcoreMode)
                            {
                                var newLocked = lockCheckBox.Checked == true ? 1 : 0;
                                if (gameLevel.Contains("DifficultyLocked"))
                                    gameLevel.Get<NbtByte>("DifficultyLocked").Value = (byte)newLocked;
                                else if (newLocked == 1)
                                    gameLevel.Add(new NbtByte("DifficultyLocked", (byte)newLocked));
                            }

                            using (var fileStream = new FileStream(saveDatPath, FileMode.Create, FileAccess.Write,
                                       FileShare.None))
                            {
                                saveInfo.SaveToStream(fileStream, NbtCompression.GZip);
                            }

                            ModMain.Hint("难度设置修改成功", ModMain.HintType.Finish);
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "难度设置修改失败", ModBase.LogLevel.Hint);
                        }
                    };


                    lockCheckBox.Change += (sender, user) =>
                    {
                        try
                        {
                            if (difficultyCombo.SelectedValue is null) return;
                            var newDifficulty = (byte)difficultyCombo.SelectedValue;
                            if (gameLevel.Contains("difficulty_settings"))
                            {
                                var newDifficultyString = GetDifficultyName(newDifficulty);
                                gameLevel.Get<NbtCompound>("difficulty_settings").Get<NbtString>("difficulty").Value = newDifficultyString;
                            }
                            else
                            {
                                gameLevel.Get<NbtByte>("Difficulty").Value = (byte)newDifficulty;
                            }
                            
                            if (!isHardcoreMode)
                            {
                                var newLocked = lockCheckBox.Checked == true ? 1 : 0;
                                if (gameLevel.Contains("difficulty_settings"))
                                    gameLevel.Get<NbtCompound>("difficulty_settings").Get<NbtByte>("locked").Value = (byte)newLocked;
                                else if (gameLevel.Contains("DifficultyLocked"))
                                    gameLevel.Get<NbtByte>("DifficultyLocked").Value = (byte)newLocked;
                                else if (newLocked == 1)
                                    if (gameLevel.Contains("difficulty_settings"))
                                        gameLevel.Get<NbtCompound>("difficulty_settings").Add(new NbtByte("locked", (byte)newLocked));
                                    else
                                        gameLevel.Add(new NbtByte("DifficultyLocked", (byte)newLocked));
                            }

                            using (var fileStream = new FileStream(saveDatPath, FileMode.Create, FileAccess.Write,
                                       FileShare.None))
                            {
                                saveInfo.SaveToStream(fileStream, NbtCompression.GZip);
                            }

                            ModMain.Hint("难度设置修改成功", ModMain.HintType.Finish);
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "难度设置修改失败", ModBase.LogLevel.Hint);
                        }
                    };

                    var rowIndex = PanSettingsList.RowDefinitions.Count;
                    PanSettingsList.RowDefinitions.Add(new RowDefinition
                        { Height = new GridLength(1d, GridUnitType.Auto) });

                    var headTextBlock = new TextBlock { Text = "游戏难度", Margin = new Thickness(0d, 3d, 0d, 3d) };
                    Grid.SetRow(headTextBlock, rowIndex);
                    Grid.SetColumn(headTextBlock, 0);

                    Grid.SetRow(difficultyPanel, rowIndex);
                    Grid.SetColumn(difficultyPanel, 2);

                    PanSettingsList.Children.Add(headTextBlock);
                    PanSettingsList.Children.Add(difficultyPanel);
                }

                AddInfoTable("最后一次游玩",
                    new DateTime(1970, 1, 1, 0, 0, 0)
                        .AddMilliseconds(long.Parse(gameLevel.Get<NbtLong>("LastPlayed").Value.ToString()))
                        .ToLocalTime().ToString());

                NbtInt spawnX = null;
                if (gameLevel.TryGet("SpawnX", out spawnX))
                {
                    var spawnY = gameLevel.Get<NbtInt>("SpawnY");
                    var spawnZ = gameLevel.Get<NbtInt>("SpawnZ");
                    AddInfoTable("出生点 (X/Y/Z)", $"{spawnX.Value} / {spawnY.Value} / {spawnZ.Value}");
                }
                else
                {
                    var spawnPos = gameLevel.Get<NbtCompound>("spawn").Get<NbtIntArray>("pos");
                    var spawnXPos = spawnPos[0];
                    var spawnYPos = spawnPos[1];
                    var spawnZPos = spawnPos[2];
                    AddInfoTable("出生点 (X/Y/Z)", $"{spawnXPos} / {spawnYPos} / {spawnZPos}");
                }

                var gameTypeName = "获取失败";

                NbtByte isHardcore = null;

                if (gameLevel.Contains("difficulty_settings"))
                {
                    isHardcore = gameLevel.Get<NbtCompound>("difficulty_settings").Get<NbtByte>("hardcore");
                }
                else
                {
                    isHardcore = gameLevel.Get<NbtByte>("hardcore");
                }

                if (isHardcore.Value == 1)
                {
                    gameTypeName = "极限模式";
                }
                else
                {
                    var gameType = gameLevel.Get<NbtInt>("GameType");
                    switch (gameType.Value)
                    {
                        case 0:
                        {
                            gameTypeName = "生存模式";
                            break;
                        }
                        case 1:
                        {
                            gameTypeName = "创造模式";
                            break;
                        }
                        case 2:
                        {
                            gameTypeName = "冒险模式";
                            break;
                        }
                        case 3:
                        {
                            gameTypeName = "旁观模式";
                            break;
                        }

                        default:
                        {
                            gameTypeName = "生存模式";
                            break;
                        }
                    }
                }

                AddInfoTable("游戏模式", gameTypeName);

                if (hasDifficulty)
                {
                    string difficultyRaw = gameLevel.Contains("difficulty_settings")
                        ? gameLevel.Get<NbtCompound>("difficulty_settings").Get<NbtString>("difficulty").Value
                        : gameLevel.Get<NbtByte>("Difficulty").Value.ToString();

                    string difficultyName = difficultyRaw switch
                    {
                        "0" or "peaceful" => "和平",
                        "1" or "easy" => "简单",
                        "2" or "normal" => "普通",
                        "3" or "hard" => "困难",
                        _ => "获取失败"
                    };

                    NbtByte lockedElement = gameLevel.Contains("difficulty_settings")
                        ? gameLevel.Get<NbtCompound>("difficulty_settings").Get<NbtByte>("locked")
                        : gameLevel.Get<NbtByte>("DifficultyLocked");
                    var isDifficultyLocked =
                        (lockedElement is not null && lockedElement.Value == 1) ||
                        isHardcore.Value == 1 ? "是" :
                        lockedElement is not null ? "否" : "获取失败";
                    if (Hintversion1_8.Visibility != Visibility.Visible)
                        AddInfoTable("困难度", $"{difficultyName} (是否已锁定难度：{isDifficultyLocked})");
                }

                var totalTicks = long.Parse(gameLevel.Get<NbtLong>("Time").Value.ToString());
                var totalSeconds = totalTicks / 20.0d;
                var playTime = TimeSpan.FromSeconds(totalSeconds);
                var formattedPlayTime = $"{playTime.Days} 天 {playTime.Hours} 小时 {playTime.Minutes} 分钟";
                AddInfoTable("游戏时长", formattedPlayTime);
                PanContent.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取存档信息失败", ModBase.LogLevel.Msgbox);
            PanContent.Visibility = Visibility.Collapsed;
            PanSettings.Visibility = Visibility.Collapsed;
            Hintversion1_9.Visibility = Visibility.Collapsed;
            Hintversion1_8.Visibility = Visibility.Collapsed;
            Hintversion1_3.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearInfoTable()
    {
        PanList.Children.Clear();
        PanList.RowDefinitions.Clear();
    }

    private void AddInfoTable(string head, string content, bool isSeed = false, string versionName = null,
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

        if (isSeed && content != "获取失败")
        {
            var BtnChunkbase = new MyIconButton
            {
                Logo = Icon.IconButtonlink,
                ToolTip = "跳转到 Chunkbase",
                Width = 22d,
                Height = 22d
            };
            contentStack.Children.Add(BtnChunkbase);


            BtnChunkbase.Click += (_, _) =>
            {
                try
                {
                    if (versionName is null)
                    {
                        ModBase.Log("当前存档版本无法确定，因此无法跳转到 Chunkbase", ModBase.LogLevel.Hint);
                        return;
                    }

                    if (versionName.Any(c => char.IsLetter(c)))
                    {
                        ModBase.Log($"当前存档版本 '{versionName}' 可能是预览版，不受支持，无法跳转到 Chunkbase", ModBase.LogLevel.Hint);
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
    
    public string GetDifficultyName(int newDifficulty)
    {
        return newDifficulty switch
        {
            0 => "peaceful",
            1 => "easy",
            2 => "normal",
            3 => "hard",
            _ => throw new ArgumentOutOfRangeException(nameof(newDifficulty), "Invalid difficulty value")
        };
    }
}