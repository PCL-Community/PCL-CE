using System.IO;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Utils.OS;

namespace PCL
{
    public class CustomEvent
    {
        public EventType Type { get; set; } = EventType.None;
        public string Data { get; set; }

        public CustomEvent() { }

        public CustomEvent(EventType type, string data)
        {
            Type = type;
            Data = data;
        }

        public void Raise() => Raise(Type, Data);

        public static void Raise(EventType type, string arg)
        {
            if (type == EventType.None) return;
            ModBase.Log($"[Control] Executing custom event: {type}, {arg}");

            try
            {
                if (ActionMap.TryGetValue(type, out var action))
                    action.Execute(arg, type);
                else
                    ModMain.MyMsgBox(
                        Lang.Text("Event.Error.UnknownType", type.ToString()),
                        Lang.Text("Event.Error.Title"));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, Lang.Text("Event.Error.ExecutionFailed", type, arg), ModBase.LogLevel.Msgbox);
            }
        }

        public static string GetCustomVariable(string name, string defaultValue = "") =>
            States.CustomVariables.TryGetValue(name, out var value) ? value : defaultValue;

        #region Action dispatch

        private interface IEventAction
        {
            void Execute(string arg, EventType type);
        }

        private static readonly Dictionary<EventType, IEventAction> ActionMap = new()
        {
            [EventType.OpenUrl] = new OpenUrlAction(),
            [EventType.OpenFile] = new OpenFileAction(),
            [EventType.ExecuteCommand] = new OpenFileAction(),
            [EventType.LaunchGame] = new LaunchGameAction(),
            [EventType.CopyText] = new CopyTextAction(),
            [EventType.RefreshHomepage] = new RefreshAction(),
            [EventType.RefreshPage] = new RefreshAction(),
            [EventType.DailyFortune] = new DailyFortuneAction(),
            [EventType.ClearTrash] = new ClearTrashAction(),
            [EventType.ShowDialog] = new ShowDialogAction(),
            [EventType.ShowHint] = new ShowHintAction(),
            [EventType.SwitchPage] = new SwitchPageAction(),
            [EventType.ImportModpack] = new ModpackInstallAction(),
            [EventType.InstallModpack] = new ModpackInstallAction(),
            [EventType.DownloadFile] = new DownloadFileAction(),
            [EventType.ModifySetting] = new SettingAction(),
            [EventType.WriteSetting] = new SettingAction(),
            [EventType.ModifyVariable] = new VariableAction(),
            [EventType.WriteVariable] = new VariableAction(),
        };

        private sealed class OpenUrlAction : IEventAction
        {
            public void Execute(string arg, EventType type)
            {
                arg = arg.Replace('\\', '/');
                if (!arg.Contains("://") || arg.StartsWithF("file", true))
                {
                    ModMain.MyMsgBox(Lang.Text("Event.Error.UrlRequired"), Lang.Text("Event.Error.Title"));
                    return;
                }
                ModMain.Hint(Lang.Text("Event.OpenUrl.Opening", arg));
                ModBase.RunInThread(() => ModBase.OpenWebsite(arg));
            }
        }

        private sealed class OpenFileAction : IEventAction
        {
            public void Execute(string arg, EventType type)
            {
                var args = arg?.Split('|') ?? [""];
                ModBase.RunInThread(() =>
                {
                    try
                    {
                        var actualPaths = GetAbsoluteUrls(args[0], type);
                        string location = actualPaths[0], workingDir = actualPaths[1];
                        ModBase.Log($"[Control] Open-event actual path: {location}, working directory: {workingDir}");

                        if (!EventSafetyConfirm($"即将执行：{location}{(args.Length >= 2 ? " " + args[1] : "")}"))
                            return;
                        ProcessInterop.Start(location, args.Length >= 2 ? args[1] : "");
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, $"事件执行失败（{type}, {arg}）", ModBase.LogLevel.Msgbox);
                    }
                });
            }
        }

        private sealed class LaunchGameAction : IEventAction
        {
            public void Execute(string arg, EventType type)
            {
                var args = arg?.Split('|') ?? [""];
                if (args[0] == "\\current")
                {
                    if (ModInstanceList.McMcInstanceSelected is null)
                    {
                        ModMain.Hint(Lang.Text("Event.LaunchGame.SelectVersion"), ModMain.HintType.Critical);
                        return;
                    }
                    args[0] = ModInstanceList.McMcInstanceSelected.Name;
                }
                ModBase.RunInUi(() =>
                {
                    var options = new ModLaunch.McLaunchOptions
                    {
                        ServerIp = args.Length >= 2 ? args[1] : null,
                        instance = new McInstance(args[0])
                    };
                    if (ModLaunch.McLaunchStart(options))
                        ModMain.Hint(Lang.Text("Event.LaunchGame.Starting", args[0]));
                });
            }
        }

        private sealed class CopyTextAction : IEventAction
        {
            public void Execute(string arg, EventType type) => ModBase.ClipboardSet(arg);
        }

        private sealed class RefreshAction : IEventAction
        {
            public void Execute(string arg, EventType type)
            {
                if (ModMain.frmMain?.pageRight is IRefreshable refreshable)
                {
                    ModBase.RunInUiWait(() => refreshable.Refresh());
                    if (string.IsNullOrEmpty(arg))
                        ModMain.Hint(Lang.Text("Event.Refresh.Success"), ModMain.HintType.Finish);
                }
                else
                {
                    ModMain.Hint(Lang.Text("Event.Refresh.NotSupported"), ModMain.HintType.Critical);
                }
            }
        }

        private sealed class DailyFortuneAction : IEventAction
        {
            public void Execute(string arg, EventType type) => PageToolsTest.Jrrp();
        }

        private sealed class ClearTrashAction : IEventAction
        {
            public void Execute(string arg, EventType type) =>
                ModBase.RunInThread(PageToolsTest.RubbishClear);
        }

        private sealed class ShowDialogAction : IEventAction
        {
            public void Execute(string arg, EventType type)
            {
                var args = arg?.Split('|') ?? [""];
                if (args.Length == 1)
                    throw new Exception(Lang.Text("Event.Error.MissingArgs", type.ToString(), "Title|Content"));
                ModMain.MyMsgBox(
                    args[1].Replace("\\n", "\r\n"),
                    args[0].Replace("\\n", "\r\n"),
                    args.Length > 2 ? args[2] : Lang.Text("Common.Action.Confirm"));
            }
        }

        private sealed class ShowHintAction : IEventAction
        {
            public void Execute(string arg, EventType type)
            {
                var args = arg?.Split('|') ?? [""];
                var hintType = args.Length == 1
                    ? ModMain.HintType.Info
                    : (ModMain.HintType)Enum.Parse(typeof(ModMain.HintType), args[1], true);
                ModMain.Hint(args[0].Replace("\\n", "\r\n"), hintType);
            }
        }

        private sealed class SwitchPageAction : IEventAction
        {
            public void Execute(string arg, EventType type)
            {
                var args = arg?.Split('|') ?? [""];
                ModBase.RunInUi(() =>
                {
                    var pageType = (FormMain.PageType)Enum.Parse(typeof(FormMain.PageType), args[0], true);
                    var subType = args.Length == 1
                        ? FormMain.PageSubType.Default
                        : (FormMain.PageSubType)Enum.Parse(typeof(FormMain.PageSubType), args[1], true);
                    ModMain.frmMain?.PageChange(pageType, subType);
                });
            }
        }

        private sealed class ModpackInstallAction : IEventAction
        {
            public void Execute(string arg, EventType type) =>
                ModBase.RunInUi(ModModpack.ModpackInstall);
        }

        private sealed class DownloadFileAction : IEventAction
        {
            public void Execute(string arg, EventType type)
            {
                var args = arg?.Split('|') ?? [""];
                args[0] = args[0].Replace('\\', '/');
                if (!args[0].StartsWithF("http://", true) && !args[0].StartsWithF("https://", true))
                {
                    ModMain.MyMsgBox(Lang.Text("Event.Error.DownloadUrlRequired"), Lang.Text("Event.Error.Title"));
                    return;
                }
                if (!EventSafetyConfirm(Lang.Text("Event.Download.Confirm", args[0])))
                    return;

                try
                {
                    switch (args.Length)
                    {
                        case 1:
                            PageToolsTest.StartCustomDownload(args[0], ModBase.GetFileNameFromPath(args[0]));
                            break;
                        case 2:
                            PageToolsTest.StartCustomDownload(args[0], args[1]);
                            break;
                        default:
                            PageToolsTest.StartCustomDownload(args[0], args[1], args[2]);
                            break;
                    }
                }
                catch
                {
                    PageToolsTest.StartCustomDownload(args[0], Lang.Text("Common.State.Unknown"));
                }
            }
        }

        private sealed class SettingAction : IEventAction
        {
            public void Execute(string arg, EventType type)
            {
                var args = arg?.Split('|') ?? [""];
                if (args.Length == 1)
                    throw new Exception(Lang.Text("Event.Error.MissingArgs", type.ToString(), "SettingName|Value"));
                if (ConfigService.TryGetConfigItemNoType(args[0], out var item) && item.Source != ConfigSource.SharedEncrypt)
                    item.SetValueNoType(args[1], ModInstanceList.McMcInstanceSelected?.PathInstance);
                if (args.Length == 2)
                    ModMain.Hint(Lang.Text("Event.Setting.Written", args[0], args[1]), ModMain.HintType.Finish);
            }
        }

        private sealed class VariableAction : IEventAction
        {
            public void Execute(string arg, EventType type)
            {
                var args = arg?.Split('|') ?? [""];
                if (args.Length == 1)
                    throw new Exception(Lang.Text("Event.Error.MissingArgs", type.ToString(), "VariableName|Value"));
                States.CustomVariables[args[0]] = args[1];
                States.CustomVariables = States.CustomVariables;
                if (args.Length == 2)
                    ModMain.Hint(Lang.Text("Event.Variable.Written", args[0], args[1]), ModMain.HintType.Finish);
            }
        }

        #endregion

        #region Shared helpers

        public static string[] GetAbsoluteUrls(string relativeUrl, EventType type)
        {
            relativeUrl = relativeUrl.Replace('/', '\\').ToLower().TrimStart('\\');

            string location, workingDir = Path.Combine(Basics.ExecutableDirectory, "PCL");

            if (relativeUrl.Contains(":\\"))
            {
                location = relativeUrl;
                ModBase.Log($"[Control] Custom event absolute path {type}: {location}");
            }
            else if (File.Exists(Path.Combine(Basics.ExecutableDirectory, "PCL", relativeUrl)))
            {
                location = Path.Combine(Basics.ExecutableDirectory, "PCL", relativeUrl);
                ModBase.Log($"[Control] Custom event relative-to-PCL path {type}: {location}");
            }
            else if (type is EventType.OpenFile or EventType.ExecuteCommand)
            {
                location = relativeUrl;
                ModBase.Log($"[Control] Custom event direct {type}: {location}");
            }
            else
            {
                throw new FileNotFoundException(Lang.Text("Event.Error.FileNotFound", relativeUrl), relativeUrl);
            }

            return [location, workingDir];
        }

        private static bool EventSafetyConfirm(string message)
        {
            if (States.Hint.HomepageCommand)
                return true;

            return ModMain.MyMsgBox(
                message + "\r\n请在确认没有安全隐患后再继续。",
                Lang.Text("Event.Safety.Title"),
                Lang.Text("Event.Safety.Continue"),
                Lang.Text("Event.Safety.ContinueAlways"),
                Lang.Text("Common.Action.Cancel")) switch
            {
                1 => true,
                2 => (States.Hint.HomepageCommand = true) is true,
                _ => false,
            };
        }

        #endregion
    }
}
