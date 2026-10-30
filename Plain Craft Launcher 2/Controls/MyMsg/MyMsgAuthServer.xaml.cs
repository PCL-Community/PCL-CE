using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PCL.Core.App.Localization;
using PCL.Core.UI.Controls;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Validate;

namespace PCL;

public partial class MyMsgAuthServer
{
    private readonly ModMain.MyMsgBoxConverter _converter;
    private readonly int _uuid = ModBase.GetUuid();

    public MyMsgAuthServer(ModMain.MyMsgBoxConverter converter)
    {
        InitializeComponent();
        _converter = converter;
        LabTitle.Text = converter.Title;
        Btn1.Text = converter.Button1;
        Btn2.Text = converter.Button2;
        ComboServer.Text = converter.AuthServerDefault;
        foreach (var preset in converter.AuthServerPresets ?? new Dictionary<string, string>())
            ComboServer.Items.Add(new MyComboBoxItem { Content = preset.Key, Tag = preset.Value });
        ComboServer.SelectionChanged += (_, _) =>
        {
            if (ComboServer.SelectedItem is MyComboBoxItem item)
            {
                var value = item.Tag?.ToString() ?? string.Empty;
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    ComboServer.SelectedItem = null;
                    ComboServer.Text = value;
                }));
            }
        };
        ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
        LabTitle.MouseLeftButtonDown += Drag;
        PanBorder.MouseLeftButtonDown += Drag;
        Loaded += Load;
    }

    private void Load(object sender, EventArgs e)
    {
        ModMain.frmMain.PanMsgBackground.Background = new ModBase.MyColor(90d, 0d, 0d, 0d);
        Opacity = 1d;
        TransformPos.Y = 0d;
        TransformRotate.Angle = 0d;
    }

    private void Btn1_Click(object sender, MouseButtonEventArgs e)
    {
        var server = ComboServer.Text?.Trim();
        if (string.IsNullOrWhiteSpace(server) || !server.IsMatch(RegexPatterns.HttpUri))
        {
            HintService.Hint(Lang.Text("Launch.Account.Auth.InvalidServer"), HintType.Error);
            return;
        }
        _converter.IsExited = true;
        _converter.Result = server;
        _Close();
    }

    private void Btn2_Click(object sender, MouseButtonEventArgs e)
    {
        if (_converter.IsExited) return;
        _converter.IsExited = true;
        _converter.Result = null;
        _Close();
    }

    private void _Close()
    {
        _converter.WaitFrame.Continue = false;
        _converter.CompletionObject.TrySetResult(_converter.Result);
        ComponentDispatcher.PopModal();
        // 隐藏
        ModBase.RunInUiLater(() =>
        {
            if (!ModMain.WaitingMyMsgBox.Any())
                ModMain.frmMain.PanMsgBackground.Background = new ModBase.MyColor(0d, 0d, 0d, 0d);
        }, 30);
        if (Parent is Grid parentGrid)
            parentGrid.Children.Remove(this);
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.GetPosition(ShapeLine).Y <= 2d)
                ModMain.frmMain.DragMove();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "拖拽移动失败", ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }
    }
}
