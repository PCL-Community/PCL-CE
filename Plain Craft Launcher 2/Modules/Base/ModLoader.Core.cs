using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.App;
using PCL.Core.Utils;
using System.Collections;
using System.IO;
using System.Windows.Shell;
using PCL.Network;

namespace PCL;

public static partial class ModLoader
{
    public enum LoaderFolderRunType
    {
        RunOnUpdated,
        ForceRun,
        UpdateOnly
    }

    // 文件夹刷新类委托
    private static readonly Dictionary<LoaderBase, LoaderFolderDictionaryEntry> LoaderFolderDictionary = new();

    /// <summary>
    ///     执行以文件夹检测作为输入的加载器。加载器需以文件夹路径为输入值。
    ///     返回是否执行了加载器。
    /// </summary>
    /// <param name="ExtraPath">用于检查文件夹修改的额外路径。该路径不会传入加载器。</param>
    /// <param name="LoaderInput">如果不想要文件夹路径为输入值，则传入期望数据</param>
    public static bool LoaderFolderRun(LoaderBase Loader, string FolderPath, LoaderFolderRunType Type, int MaxDepth = 0,
        string ExtraPath = "", bool WaitForExit = false, object LoaderInput = null)
    {
        DirectoryInfo FolderInfo;
        var Value = new LoaderFolderDictionaryEntry { FolderPath = FolderPath + ExtraPath, LastCheckTime = default };
        try
        {
            // 获取数据
            FolderInfo = new DirectoryInfo(FolderPath + ExtraPath);
            Value.LastCheckTime = FolderInfo.Exists ? GetActualLastWriteTimeUtc(FolderInfo, MaxDepth) : null;
            // 如果已经检查过，则跳过
            if (Type == LoaderFolderRunType.RunOnUpdated && LoaderFolderDictionary.ContainsKey(Loader))
            {
                if (FolderInfo.Exists)
                {
                    if (LoaderFolderDictionary[Loader].LastCheckTime is not null &&
                        Value.Equals(LoaderFolderDictionary[Loader]))
                        return false;
                }
                else if (LoaderFolderDictionary[Loader].LastCheckTime is null)
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "文件夹加载器启动检测出错");
        }

        // 写入检查数据
        LoaderFolderDictionary[Loader] = Value;
        // 开始检查
        if (Type == LoaderFolderRunType.UpdateOnly)
            return false;
        if (WaitForExit)
            Loader.WaitForExit(LoaderInput ?? FolderPath, IsForceRestart: true);
        else
            Loader.Start(LoaderInput ?? FolderPath, true);
        return true;
    }

    private static DateTime GetActualLastWriteTimeUtc(DirectoryInfo FolderInfo, int MaxDepth)
    {
        var Time = FolderInfo.LastWriteTimeUtc;
        if (MaxDepth > 0)
            foreach (var Folder in FolderInfo.EnumerateDirectories())
            {
                var FolderTime = GetActualLastWriteTimeUtc(Folder, MaxDepth - 1);
                if (FolderTime > Time)
                    Time = FolderTime;
            }

        return Time;
    }

    // 各类加载器
    /// <summary>
    ///     加载器的统一基类。
    /// </summary>
    public abstract partial class LoaderBase : ILoadingTrigger
    {
        public delegate void OnStateChangedThreadEventHandler(LoaderBase Loader, LoadState NewState,
            LoadState OldState);

        public delegate void OnStateChangedUiEventHandler(LoaderBase Loader, LoadState NewState,
            LoadState OldState);

        public delegate void PreviewFinishEventHandler(LoaderBase Loader);

        /// <summary>
        ///     父加载器。
        /// </summary>
        public LoaderBase Parent;

        public LoaderBase()
        {
            Name = "未命名任务 " + Uuid + "#";
        }

        /// <summary>
        ///     最上级的加载器。
        /// </summary>
        public LoaderBase RealParent
        {
            get
            {
                LoaderBase RealParentRet = default;
                try
                {
                    RealParentRet = Parent;
                    while (RealParentRet is not null && RealParentRet.Parent is not null)
                        RealParentRet = RealParentRet.Parent;
                }
                catch (Exception ex)
                {
                    LauncherLogger.Log(ex, "获取父加载器失败（" + Name + "）", LauncherLogger.LogLevel.Feedback);
                    return null;
                }

                return RealParentRet;
            }
        }

        /// <summary>
        ///     简易的在 UI 线程添加触发事件的方式。主要用于在新建 Loader 时直接使用 With 绑定事件，以及进行老代码兼容。
        /// </summary>
        public Action<LoaderBase> OnStateChanged
        {
            set { OnStateChangedUi += (Loader, NewState, OldState) => value(Loader); }
        }

        public bool IsLoader { get; } = true;

        public virtual void InitParent(LoaderBase Parent)
        {
            this.Parent = Parent;
        }
    }

    // 说实话，我真的觉得 C# 应该学学 VB 的那种近乎 Java 泛型擦除的兼容性，省掉一堆麻烦
    public abstract partial class LoaderTask : LoaderBase
    {
    }

    /// <summary>
    ///     用于异步执行并监控单一函数的加载器。
    /// </summary>
    public partial class LoaderTask<InputType, OutputType> : LoaderTask
    {
        public LoaderTask(string Name, Action<LoaderTask<InputType, OutputType>> LoadDelegate,
            Func<InputType?>? InputDelegate = null, ThreadPriority Priority = ThreadPriority.Normal)
        {
            this.Name = Name;
            this.LoadDelegate = LoadDelegate;
            this.InputDelegate = InputDelegate;
        }
    }

    /// <summary>
    ///     支持多个加载器连续运作的复合加载器。
    /// </summary>
    public partial class LoaderCombo : LoaderBase
    {
        public List<LoaderBase> Loaders = new();

        public LoaderCombo(string Name, IEnumerable<LoaderBase> Loaders)
        {
            this.Loaders.Clear();
            foreach (var Loader in Loaders)
                if (Loader is not null)
                {
                    this.Loaders.Add(Loader);
                    Loader.OnStateChangedThread += SubTaskStateChanged;
                    Loader.HasOnStateChangedThread = true;
                }

            InitParent(null);
            this.Name = Name;
        }

        public override void InitParent(LoaderBase Parent)
        {
            this.Parent = Parent;
            foreach (var Loader in Loaders)
                Loader.InitParent(this);
        }

        /// <summary>
        ///     获得最底层的，应被显示给用户的加载器列表，并追加于 List。
        /// </summary>
        public static void GetLoaderList(LoaderCombo Loader, ref List<LoaderBase> List, bool RequireShow = true)
        {
            foreach (var SubLoader in Loader.Loaders)
            {
                if (SubLoader.Show || !RequireShow)
                    List.Add(SubLoader);
                if (SubLoader is LoaderCombo combo)
                    GetLoaderList(combo, ref List);
            }
        }

        /// <summary>
        ///     获得最底层的，应被显示给用户的加载器列表，并追加于 List。
        /// </summary>
        public void GetLoaderList(ref List<LoaderBase> List, bool RequireShow = true)
        {
            GetLoaderList(this, ref List, RequireShow);
        }

        /// <summary>
        ///     获得最底层的，应被显示给用户的加载器列表。
        /// </summary>
        public List<LoaderBase> GetLoaderList(bool RequireShow = true)
        {
            var List = new List<LoaderBase>();
            GetLoaderList(ref List, RequireShow);
            return List;
        }
    }

    /// <summary>
    ///     支持多个加载器连续运作的复合加载器（泛型版本）。
    /// </summary>
    public partial class LoaderCombo<InputType> : LoaderCombo
    {
        public LoaderCombo(string Name, IEnumerable<LoaderBase> Loaders) : base(Name, Loaders) { }
    }

    private partial struct LoaderFolderDictionaryEntry
    {
        public string FolderPath;
    }
}
