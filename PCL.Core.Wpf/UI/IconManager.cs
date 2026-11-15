using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;

namespace PCL.Core.UI;

// 图标管理器（处理集合和选择逻辑）
public class IconManager : INotifyPropertyChanged
{
    private readonly Dictionary<string, IconModel> _iconIndex = new();

    public IconModel? SelectedIcon
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(SelectedIcon));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool SetSelectedIconByName(string name)
    {
        if (!_iconIndex.TryGetValue(name, out var icon))
            return false;

        SelectedIcon = icon;
        return true;
    }

    public bool AddIconFromXaml(string name, string xamlString)
    {
        if (string.IsNullOrWhiteSpace(name) || _iconIndex.ContainsKey(name)) return false; // 避免重复

        if (!TryLoadIconFromXaml(xamlString, out var content))
            return false;

        var model = new IconModel(name, content);
        _iconIndex[name] = model;
        return true;
    }

    // 可选：添加移除方法
    public void RemoveIconByName(string name)
    {
        _iconIndex.Remove(name);
    }

    // 从 XAML 字符串加载图标
    public static bool TryLoadIconFromXaml(string xamlString, out UIElement? icon)
    {
        icon = null;
        if (string.IsNullOrWhiteSpace(xamlString)) return false;

        // 确保在UI线程执行
        if (!Application.Current.Dispatcher.CheckAccess()) return false;

        try
        {
            icon = (UIElement)XamlReader.Parse(xamlString);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // 从 XAML 字符串加载图标
    public static bool LoadIconFromXaml(string xamlString, out UIElement? icon)
    {
        icon = null;
        if (string.IsNullOrWhiteSpace(xamlString))
            throw new ArgumentNullException(nameof(xamlString), "XAML 字符串不能为空或空白。");

        // 确保在UI线程执行
        if (!Application.Current.Dispatcher.CheckAccess())
            throw new InvalidOperationException("XAML 解析需要在 UI 线程执行。");

        icon = (UIElement)XamlReader.Parse(xamlString);
        return true;
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}