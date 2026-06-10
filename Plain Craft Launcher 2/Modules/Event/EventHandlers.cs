using System.Reflection;
using System.Text.RegularExpressions;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     事件处理函数。每个 <see cref="EventType"/> 对应一个 private static 方法。
/// </summary>
public static partial class EventHandlers
{
    public static void Raise(EventType type, string? data)
    {
        if (type == EventType.None) return;
        ModBase.Log($"[Event] 执行事件：{type}, {data}");

        try
        {
            switch (type)
            {
                case EventType.OpenUrl:        OpenUrl(data); break;
                case EventType.LaunchGame:     LaunchGame(data); break;
                case EventType.CopyText:       CopyText(data); break;
                case EventType.RefreshHome:    RefreshHome(data); break;
                case EventType.ShowDialog:     ShowDialog(data); break;
                case EventType.ShowHint:       ShowHint(data); break;
                case EventType.InvokeFunction: InvokeFunction(data); break;
                default:
                    ModMain.MyMsgBox(Lang.Text("Event.Error.UnknownType", type), Lang.Text("Event.Error.Title"));
                    break;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"事件执行失败（{type}, {data}）", ModBase.LogLevel.Msgbox);
        }
    }

    private static void OpenUrl(string? data)
    {
        var url = (data ?? "").Replace('\\', '/');
        if (!url.Contains("://") || url.StartsWith("file", StringComparison.OrdinalIgnoreCase))
        {
            ModMain.MyMsgBox(Lang.Text("Event.Error.InvalidUrl"), Lang.Text("Event.Error.Title"));
            return;
        }
        ModMain.Hint(Lang.Text("Event.Hint.OpeningUrl", url));
        ModBase.RunInThread(() => ModBase.OpenWebsite(url));
    }

    private static void LaunchGame(string? data)
    {
        var a = (data ?? "").Split('|');
        var name = a.ElementAtOrDefault(0) ?? "";

        if (name == "\\current")
        {
            if (ModInstanceList.McMcInstanceSelected is null)
            {
                ModMain.Hint(Lang.Text("Event.Error.NoInstanceSelected"), ModMain.HintType.Critical);
                return;
            }
            name = ModInstanceList.McMcInstanceSelected.Name;
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            ModMain.Hint(Lang.Text("Event.Error.NoInstanceSpecified"), ModMain.HintType.Critical);
            return;
        }
        ModBase.RunInUi(() =>
        {
            if (ModLaunch.McLaunchStart(new()
            {
                ServerIp = a.Length >= 2 ? a[1] : null,
                instance = new McInstance(name),
            }))
                ModMain.Hint(Lang.Text("Event.Hint.Launching", name));
        });
    }

    private static void CopyText(string? data) => ModBase.ClipboardSet(data ?? "");

    private static void RefreshHome(string? data)
    {
        if (ModMain.frmMain?.pageRight is IRefreshable r)
        {
            ModBase.RunInUiWait(() => r.Refresh());
            if (string.IsNullOrEmpty(data)) ModMain.Hint(Lang.Text("Event.Hint.Refreshed"), ModMain.HintType.Finish);
        }
        else ModMain.Hint(Lang.Text("Event.Error.RefreshNotSupported"), ModMain.HintType.Critical);
    }

    private static void ShowDialog(string? data)
    {
        var a = (data ?? "").Split('|');
        if (a.Length < 2) throw new Exception("ShowDialog 至少需要 2 个用 | 分隔的参数：标题|内容");
        ModMain.MyMsgBox(a[1].Replace("\\n", "\r\n"), a[0].Replace("\\n", "\r\n"),
            a.Length > 2 ? a[2] : Lang.Text("Common.Action.Confirm"));
    }

    private static void ShowHint(string? data)
    {
        var a = (data ?? "").Split('|');
        var t = a.Length >= 2 && Enum.TryParse<ModMain.HintType>(a[1], true, out var h) ? h : ModMain.HintType.Info;
        ModMain.Hint(a.ElementAtOrDefault(0)?.Replace("\\n", "\r\n") ?? "", t);
    }

    private static void InvokeFunction(string? data)
    {
        var expr = (data ?? "").Trim();
        if (string.IsNullOrEmpty(expr)) { ModMain.Hint(Lang.Text("Event.Error.InvokeFunctionEmpty"), ModMain.HintType.Critical); return; }
        var m = InvokeRegex().Match(expr);
        if (!m.Success) { ModMain.Hint(Lang.Text("Event.Error.InvokeFunctionSyntax", expr), ModMain.HintType.Critical); return; }

        var type = Type.GetType($"PCL.{m.Groups[1].Value}, Plain Craft Launcher 2", false, true)
                ?? Type.GetType($"PCL.{m.Groups[1].Value}, PCL.Core", false, true)
                ?? Type.GetType(m.Groups[1].Value, false, true);
        if (type is null) { ModMain.Hint(Lang.Text("Event.Error.TypeNotFound", m.Groups[1].Value), ModMain.HintType.Critical); return; }

        var args = ParseArgs(m.Groups[3].Value);
        var method = type.GetMethod(m.Groups[2].Value, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                        args.Select(a => a?.GetType() ?? typeof(object)).ToArray())
                  ?? type.GetMethod(m.Groups[2].Value, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                        [typeof(string)]);
        if (method is null) { ModMain.Hint(Lang.Text("Event.Error.MethodNotFound", $"{m.Groups[1].Value}.{m.Groups[2].Value}"), ModMain.HintType.Critical); return; }
        if (method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(string)) args = [data];

        try { method.Invoke(method.IsStatic ? null : Activator.CreateInstance(type), args); }
        catch (TargetInvocationException ex) { ModBase.Log(ex.InnerException ?? ex, $"InvokeFunction 失败：{expr}", ModBase.LogLevel.Msgbox); }
        catch (Exception ex) { ModBase.Log(ex, $"InvokeFunction 失败：{expr}", ModBase.LogLevel.Msgbox); }
    }

    private static object?[] ParseArgs(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return [];
        var l = new List<object?>(); var r = s.Trim();
        while (r.Length > 0)
        {
            r = r.TrimStart();
            if (r.StartsWith('"'))
            {
                var e = r.IndexOf('"', 1);
                if (e < 0) { l.Add(r); break; }
                l.Add(r[1..e]); r = r[(e + 1)..].TrimStart();
                if (r.StartsWith(',')) r = r[1..];
            }
            else
            {
                var e = r.IndexOf(',');
                if (e < 0) { l.Add(Parse(r.Trim())); break; }
                l.Add(Parse(r[..e].Trim())); r = r[(e + 1)..];
            }
        }
        return l.ToArray();
    }

    private static object? Parse(string t) => t switch
    {
        "null" => null, "true" => true, "false" => false,
        _ when int.TryParse(t, out var i) => i,
        _ when double.TryParse(t, out var d) => d,
        _ => t,
    };

    [GeneratedRegex(@"^(\w+(?:\.\w+)*)\.(\w+)\((.*)\)$")]
    private static partial Regex InvokeRegex();
}
