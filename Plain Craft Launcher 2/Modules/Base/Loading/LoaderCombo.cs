using PCL.Network;

namespace PCL;

public static partial class Loading
{
    /// <summary>
    ///     支持多个加载器连续运作的复合加载器。
    /// </summary>
    public class LoaderCombo : LoaderBase
    {
        public object? input;
        public List<LoaderBase> loaders = [];

        public LoaderCombo(string name, IEnumerable<LoaderBase> loaders)
        {
            foreach (var loader in loaders)
            {
                if (loader is null)
                    continue;

                this.loaders.Add(loader);
                loader.OnStateChangedThread += SubTaskStateChanged;
                loader.hasOnStateChangedThread = true;
            }

            InitParent(null!);
            this.name = name;
        }

        public override double Progress
        {
            get =>
                State switch
                {
                    Common.LoadState.Waiting => 0d,
                    Common.LoadState.Loading => GetWeightedProgress(),
                    _ => 1d
                };
            set => throw new Exception("多重加载器不支持设置进度");
        }

        /// <summary>
        ///     计算所有子加载器的加权进度。
        /// </summary>
        private double GetWeightedProgress()
        {
            var totalWeight = 0d;
            var finishedWeight = 0d;

            foreach (var loader in loaders)
            {
                totalWeight += loader.ProgressWeight;
                finishedWeight += loader.ProgressWeight * loader.Progress;
            }

            return totalWeight == 0d ? 0d : finishedWeight / totalWeight;
        }

        public override void InitParent(LoaderBase parent)
        {
            this.parent = parent;

            foreach (var loader in loaders)
                loader.InitParent(this);
        }

        public override void Start(object? input = null, bool isForceRestart = false)
        {
            isForceRestarting = isForceRestart;

            lock (lockState)
            {
                if (State == Common.LoadState.Loading)
                    return;

                State = Common.LoadState.Loading;
            }

            this.input = input;

            if (isForceRestart)
                foreach (var loader in loaders)
                    loader.State = Common.LoadState.Waiting;

            Common.RunInThread(Update);
        }

        public override void Abort()
        {
            lock (lockState)
            {
                if (State is not (Common.LoadState.Loading or Common.LoadState.Waiting))
                    return;
            }

            foreach (var loader in loaders)
                loader.Abort();

            lock (lockState)
            {
                if (State is Common.LoadState.Loading or Common.LoadState.Waiting)
                    State = Common.LoadState.Aborted;
            }
        }

        /// <summary>
        ///     处理子任务状态变更。
        /// </summary>
        private void SubTaskStateChanged(
            LoaderBase loader,
            Common.LoadState newState,
            Common.LoadState oldState)
        {
            switch (newState)
            {
                case Common.LoadState.Loading or Common.LoadState.Waiting:
                    return;

                case Common.LoadState.Finished:
                    Update();
                    return;

                case Common.LoadState.Aborted:
                    Abort();
                    return;

                default:
                    HandleSubTaskFailure(loader);
                    return;
            }
        }

        /// <summary>
        ///     处理子任务失败。
        /// </summary>
        private void HandleSubTaskFailure(LoaderBase failedLoader)
        {
            lock (lockState)
            {
                if (State >= Common.LoadState.Finished)
                    return;

                Error = new Exception(
                    $"{failedLoader.name}失败",
                    failedLoader.Error);

                State = failedLoader.State;
            }

            foreach (var loader in loaders)
                loader.Abort();

            Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
        }

        /// <summary>
        ///     触发一次更新，启动后续加载器或完成整个任务。
        /// </summary>
        private void Update()
        {
            if (State is Common.LoadState.Finished
                or Common.LoadState.Failed
                or Common.LoadState.Aborted)
                return;

            var allFinished = true;
            var blocked = false;
            var currentInput = input;

            foreach (var loader in loaders)
            {
                var state = loader.State;

                var needsRestart =
                    state is Common.LoadState.Finished or Common.LoadState.Loading
                    && loader is LoaderTask task
                    && NeedsRestart(task, currentInput);

                switch (state)
                {
                    case Common.LoadState.Finished when !needsRestart:
                    {
                        if (loader is LoaderTask)
                            currentInput = ((dynamic)loader).output;
                        if (loader.block && !allFinished)
                            blocked = true;
                        continue;
                    }
                    case Common.LoadState.Loading when !needsRestart:
                        allFinished = false;
                        blocked = true;
                        continue;
                    case Common.LoadState.Waiting:
                    case Common.LoadState.Failed:
                    case Common.LoadState.Aborted:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (needsRestart)
                {
                    if (state == Common.LoadState.Finished)
                        Common.Log(
                            $"[Loader] 由于输入条件变更，重启已完成的加载器 {loader.name}");
                    else
                        Common.Log(
                            $"[Loader] 由于输入条件变更，重启进行中的加载器 {loader.name}",
                            Common.LogLevel.Developer);
                }

                allFinished = false;

                if (blocked)
                    continue;

                StartSubLoader(loader, currentInput);

                if (loader.block)
                    blocked = true;
            }

            if (!allFinished)
                return;

            RaisePreviewFinish();
            State = Common.LoadState.Finished;
            Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
        }

        /// <summary>
        ///     判断任务是否因输入变化而需要重启。
        /// </summary>
        private static bool NeedsRestart(LoaderTask task, object? input)
        {
            var taskInput = GetCompatibleInput(task, input);
            return task.ShouldStart(ref taskInput, false, true);
        }

        /// <summary>
        ///     获取与加载器输入类型匹配的输入，不匹配时返回 null。
        /// </summary>
        private static object? GetCompatibleInput(LoaderBase loader, object? input)
        {
            if (input is null)
                return null;

            var inputType = loader.GetType().GenericTypeArguments.FirstOrDefault();
            return inputType == input.GetType() ? input : null;
        }

        /// <summary>
        ///     启动子加载器，并根据加载器类型传递输入。
        /// </summary>
        private void StartSubLoader(LoaderBase loader, object? input)
        {
            if (input is null)
            {
                loader.Start(isForceRestart: isForceRestarting);
                return;
            }

            var loaderInput = loader switch
            {
                LoaderTask or LoaderCombo => GetCompatibleInput(loader, input),
                _ when input is List<DownloadFile> => input,
                _ => null
            };

            loader.Start(loaderInput, isForceRestarting);
        }

        /// <summary>
        ///     收集符合显示条件的子加载器及其嵌套加载器。
        /// </summary>
        public static void GetLoaderList(
            LoaderCombo loader,
            ref List<LoaderBase> list,
            bool requireShow = true)
        {
            foreach (var subLoader in loader.loaders)
            {
                if (!requireShow || subLoader.show)
                    list.Add(subLoader);

                if (subLoader is LoaderCombo combo)
                    GetLoaderList(combo, ref list, requireShow);
            }
        }

        /// <summary>
        ///     将符合显示条件的加载器追加到指定列表。
        /// </summary>
        public void GetLoaderList(ref List<LoaderBase> list, bool requireShow = true)
        {
            GetLoaderList(this, ref list, requireShow);
        }

        /// <summary>
        ///     获取符合显示条件的加载器列表。
        /// </summary>
        public List<LoaderBase> GetLoaderList(bool requireShow = true)
        {
            var result = new List<LoaderBase>();
            GetLoaderList(ref result, requireShow);
            return result;
        }
    }
}
