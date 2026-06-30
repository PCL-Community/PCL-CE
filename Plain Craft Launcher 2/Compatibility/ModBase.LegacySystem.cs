using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualBasic;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL;

public static partial class ModBase
{
    #region 系统

    public static bool IsUtf8CodePage()
    {
        return EncodingUtils.IsDefaultEncodingUtf8();
    }

    /// <summary>
    ///     线程安全的 List。
    ///     通过在 For Each 循环中使用一个浅表副本规避多线程操作或移除自身导致的异常。
    /// </summary>
    public class SafeList<T> : IEnumerable<T>, IDisposable, ICollection<T>
    {
        private readonly List<T> _internalList;
        private readonly ReaderWriterLockSlim _lock = new();

        public SafeList()
        {
            _internalList = [];
        }

        public SafeList(IEnumerable<T> data)
        {
            _internalList = new List<T>(data);
        }

        public T this[int index]
        {
            get => _internalList[index];
            set => _internalList[index] = value;
        }

        public void Add(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                _internalList.Add(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool Remove(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                return _internalList.Remove(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _internalList.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _internalList.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public bool IsReadOnly => ((ICollection<T>)_internalList).IsReadOnly;

        public bool Contains(T item)
        {
            return ((ICollection<T>)_internalList).Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            ((ICollection<T>)_internalList).CopyTo(array, arrayIndex);
        }

        public void Dispose()
        {
            _lock.Dispose();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public List<T> ToList()
        {
            _lock.EnterReadLock();
            try
            {
                return _internalList.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void RemoveAt(int index)
        {
            _lock.EnterWriteLock();
            try
            {
                _internalList.RemoveAt(index);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }

    /// <summary>
    ///     可用于临时存放文件的，不含任何特殊字符的文件夹路径，以“\”结尾。
    /// </summary>
    public static string pathPure = GetPureASCIIDir();

    private static string GetPureASCIIDir()
    {
        if (exePath.IsASCII()) return exePath + @"PCL\";

        if (pathAppdata.IsASCII()) return pathAppdata;

        if (pathTemp.IsASCII()) return pathTemp;

        return Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL");
    }

    /// <summary>
    ///     指示接取到这个异常的函数进行重试。
    /// </summary>
    public class RestartException : Exception
    {
    }

    /// <summary>
    ///     指示用户手动取消了操作，或用户已知晓操作被取消的原因。
    /// </summary>
    public class CancelledException : Exception
    {
    }

    /// <summary>
    ///     判断对象是否为某个泛型类型的实例。
    /// </summary>
    public static bool IsInstanceOfGenericType(this Type genericType, object obj)
    {
        if (obj is null)
            return false;
        var t = obj.GetType();
        while (t is not null)
        {
            if (t.IsGenericType && ReferenceEquals(t.GetGenericTypeDefinition(), genericType))
                return true;
            t = t.BaseType;
        }

        return false;
    }

    private static int uuid = 1;
    private static object uuidLock;

    /// <summary>
    ///     获取一个全程序内不会重复的数字（伪 Uuid）。
    /// </summary>
    public static int GetUuid()
    {
        uuidLock ??= new object();
        lock (uuidLock)
        {
            uuid += 1;
            return uuid;
        }
    }

    /// <summary>
    ///     将元素与 List 的混合体拆分为元素组。
    /// </summary>
    public static List<T> GetFullList<T>(IList data)
    {
        List<T> getFullListRet = default;
        getFullListRet = [];
        for (int i = 0, loopTo = data.Count - 1; i <= loopTo; i++)
            if (data[i] is ICollection)
                getFullListRet.AddRange((IEnumerable<T>)data[i]);
            else
                getFullListRet.Add((T)data[i]);

        return getFullListRet;
    }

    /// <summary>
    ///     数组去重。
    /// </summary>
    public static List<T> Distinct<T>(this ICollection<T> arr, ComparisonBoolean<T> isEqual)
    {
        var resultArray = new List<T>();
        for (int i = 0, loopTo = arr.Count - 1; i <= loopTo; i++)
        {
            for (int ii = i + 1, loopTo1 = arr.Count - 1; ii <= loopTo1; ii++)
                if (isEqual(arr.ElementAtOrDefault(i), arr.ElementAtOrDefault(ii)))
                    goto NextElement;
            resultArray.Add(arr.ElementAtOrDefault(i));
            NextElement: ;
        }

        return resultArray;
    }

    /// <summary>
    ///     对集合的每个元素执行指定操作。
    /// </summary>
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> collection, Action<T> action)
    {
        foreach (var item in collection)
            action(item);
        return collection;
    }

    /// <summary>
    ///     用于储存 RaiseByMouse 的 EventArgs。
    /// </summary>
    public sealed class RouteEventArgs(bool raiseByMouse = false) : EventArgs
    {
        public bool handled = false;
        public bool raiseByMouse = raiseByMouse;
    }

    /// <summary>
    ///     前台运行文件。
    /// </summary>
    /// <param name="fileName">文件名。可以为“notepad”等缩写。</param>
    /// <param name="arguments">运行参数。</param>
    public static void ShellOnly(string fileName, string arguments = "")
    {
        try
        {
            fileName = ShortenPath(fileName);
            using var program = new Process();
            program.StartInfo.Arguments = arguments;
            program.StartInfo.FileName = fileName;
            program.StartInfo.UseShellExecute = true;
            Log("[System] 执行外部命令：" + fileName + " " + arguments);
            program.Start();
        }
        catch (Exception ex)
        {
            Log(
                ex,
                "打开文件或程序失败：" + fileName,
                LogLevel.Msgbox,
                userSummary: Lang.Text("SystemDialog.File.OpenFailed.Message", fileName));
        }
    }

    /// <summary>
    ///     前台运行文件并返回返回值。
    /// </summary>
    /// <param name="fileName">文件名。可以为“notepad”等缩写。</param>
    /// <param name="arguments">运行参数。</param>
    /// <param name="timeout">等待该程序结束的最长时间（毫秒）。超时会返回 Result.Timeout。</param>
    public static ProcessReturnValues ShellAndGetExitCode(string fileName, string arguments = "", int timeout = 1000000)
    {
        try
        {
            using var program = new Process();
            program.StartInfo.Arguments = arguments;
            program.StartInfo.FileName = fileName;
            Log("[System] 执行外部命令并等待返回码：" + fileName + " " + arguments);
            program.Start();
            if (program.WaitForExit(timeout)) return (ProcessReturnValues)program.ExitCode;

            return ProcessReturnValues.Timeout;
        }
        catch (Exception ex)
        {
            Log(ex, "执行命令失败：" + fileName, LogLevel.Msgbox);
            return ProcessReturnValues.Fail;
        }
    }

    /// <summary>
    ///     静默运行文件并返回输出流字符串。执行失败会抛出异常。
    /// </summary>
    /// <param name="fileName">文件名。可以为“notepad”等缩写。</param>
    /// <param name="arguments">运行参数。</param>
    /// <param name="timeout">等待该程序结束的最长时间（毫秒）。超时会抛出错误。</param>
    public static string ShellAndGetOutput(string fileName, string arguments = "", int timeout = 1000000,
        string workingDirectory = null)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // 设置工作目录（如果提供）
        if (!string.IsNullOrEmpty(workingDirectory)) info.WorkingDirectory = workingDirectory.TrimEnd('\\');

        Log("[System] 执行外部命令并等待返回结果：" + fileName + " " + arguments);

        using var program = new Process();
        program.StartInfo = info;
        program.Start();

        // 异步读取输出和错误流
        var outputTask = program.StandardOutput.ReadToEndAsync();
        var errorTask = program.StandardError.ReadToEndAsync();

        // 等待进程退出或超时
        if (program.WaitForExit(timeout))
        {
            // 确保异步读取完成
            Task.WaitAll(outputTask, errorTask);
        }
        else
        {
            // 超时后终止进程
            program.Kill();
            // 仍然尝试获取已输出的内容
            Task.WaitAll(outputTask, errorTask);
        }

        // 合并结果并返回
        return outputTask.Result + errorTask.Result;
    }

    /// <summary>
    ///     在新的工作线程中执行代码。
    /// </summary>
    public static Thread RunInNewThread(
        Action action,
        string name = null,
        ThreadPriority priority = ThreadPriority.Normal)
    {
        return Basics.RunInNewThread(action, name ?? "Runtime New Invoke " + GetUuid() + "#", priority);
    }

    /// <summary>
    ///     确保在 UI 线程中执行代码。
    ///     如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    ///     为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
    /// </summary>
    public static Output RunInUiWait<Output>(Func<Output> action)
    {
        return RunInUi()
            ? action()
            : System.Windows.Application.Current.Dispatcher.Invoke(action);
    }

    /// <summary>
    ///     确保在 UI 线程中执行代码。
    ///     如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    ///     为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
    /// </summary>
    public static void RunInUiWait(Action action)
    {
        if (System.Windows.Application.Current is null)
            return;
        if (RunInUi())
            action();
        else
            System.Windows.Application.Current.Dispatcher.Invoke(action);
    }

    /// <summary>
    ///     确保在 UI 线程中执行代码，代码按触发顺序执行。
    ///     如果当前并非 UI 线程，也不阻断当前线程的执行。
    /// </summary>
    public static void RunInUi(Action action, bool forceWaitUntilLoaded = false)
    {
        if (System.Windows.Application.Current is null)
            return;
        if (RunInUi())
            action();
        else
            System.Windows.Application.Current.Dispatcher.InvokeAsync(action,
                forceWaitUntilLoaded ? DispatcherPriority.Loaded : DispatcherPriority.Normal);
    }

    /// <summary>
    ///     确保在工作线程中执行代码。
    /// </summary>
    public static void RunInThread(Action action)
    {
        if (RunInUi())
            RunInNewThread(action, "Runtime Invoke " + GetUuid() + "#");
        else
            action();
    }

    /// <summary>
    ///     使用优化的归并排序算法进行稳定排序。
    /// </summary>
    /// <param name="sortRule">传入两个对象，若第一个对象应该排在前面，则返回 True。</param>
    public static List<T> Sort<T>(this IList<T> list, ComparisonBoolean<T> sortRule)
    {
        return SortUtils.Sort(list, sortRule);
    }

    private static void MergeSort_Sort<T>(
        ref List<T> array,
        int left,
        int right,
        ComparisonBoolean<T> comparator)
    {
        if (left >= right)
            return;

        var mid = (left + right) / 2;
        MergeSort_Sort(ref array, left, mid, comparator);
        MergeSort_Sort(ref array, mid + 1, right, comparator);
        MergeSort_Merge(ref array, left, mid, right, comparator);
    }

    private static void MergeSort_Merge<T>(
        ref List<T> array,
        int left,
        int mid,
        int right,
        ComparisonBoolean<T> comparator)
    {
        var leftArray = new List<T>();
        var rightArray = new List<T>();

        for (var i = left; i <= mid; i++)
            leftArray.Add(array[i]);

        for (var j = mid + 1; j <= right; j++)
            rightArray.Add(array[j]);

        var leftPtr = 0;
        var rightPtr = 0;
        var current = left;

        while (leftPtr < leftArray.Count && rightPtr < rightArray.Count)
        {
            // 保持稳定性的关键比较逻辑：当相等时优先取左数组元素
            if (comparator(leftArray[leftPtr], rightArray[rightPtr]))
            {
                array[current] = leftArray[leftPtr];
                leftPtr += 1;
            }
            else
            {
                array[current] = rightArray[rightPtr];
                rightPtr += 1;
            }

            current += 1;
        }

        while (leftPtr < leftArray.Count)
        {
            array[current] = leftArray[leftPtr];
            leftPtr += 1;
            current += 1;
        }

        while (rightPtr < rightArray.Count)
        {
            array[current] = rightArray[rightPtr];
            rightPtr += 1;
            current += 1;
        }
    }

    public delegate bool ComparisonBoolean<in T>(T left, T right);

    /// <summary>
    ///     返回列表的浅表副本。
    /// </summary>
    public static IList<T> Clone<T>(this IList<T> list)
    {
        return new List<T>(list);
    }

    /// <summary>
    ///     尝试从字典中获取某项，如果该项不存在，则返回默认值。
    /// </summary>
    public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key,
        TValue defaultValue = default)
    {
        return dict.GetValueOrDefault(key, defaultValue);
    }

    /// <summary>
    ///     将某项添加到以列表作为值的字典中。
    /// </summary>
    public static void AddToList<TKey, TValue>(
        this Dictionary<TKey, List<TValue>> dict,
        TKey key,
        TValue value)
    {
        if (dict.TryGetValue(key, out var value1))
            value1.Add(value);
        else
            dict.Add(key, [value]);
    }

    /// <summary>
    ///     获取程序启动参数。
    /// </summary>
    /// <param name="name">参数名。</param>
    /// <param name="defaultValue">默认值。</param>
    public static object GetProgramArgument(string name, object defaultValue = null)
    {
        var allArguments = Interaction.Command().Split(" ");
        for (int i = 0, loopTo = allArguments.Length - 1; i <= loopTo; i++)
            if ((allArguments[i] ?? "") == ("-" + name ?? ""))
            {
                if (allArguments.Length == i + 1 || allArguments[i + 1].StartsWithF("-"))
                    return true;
                return allArguments[i + 1];
            }

        return defaultValue;
    }

    /// <summary>
    ///     打开网页。
    /// </summary>
    public static void OpenWebsite(string url)
    {
        try
        {
            if (!url.StartsWithF("http", true) && !url.StartsWithF("minecraft://", true))
                throw new Exception(url + " 不是一个有效的网址，它必须以 http 开头！");
            Log("[System] 正在打开网页：" + url);
            var psi = new ProcessStartInfo(url)
            {
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log(ex, "无法打开网页（" + url + "）");
            ClipboardSet(url, false);
            var message = ExceptionDetails.Compose(
                Lang.Text("SystemDialog.Browser.OpenFailed.Message", url),
                ex);
            ModMain.MyMsgBox(
                message,
                Lang.Text("SystemDialog.Browser.OpenFailed.Title"));
        }
    }

    /// <summary>
    ///     打开 explorer。
    ///     若不以 \ 结尾，则将视作文件路径，打开并选中此文件。
    /// </summary>
    public static void OpenExplorer(string location)
    {
        try
        {
            location = ShortenPath(location.Replace("/", @"\").Trim(' ', '"'));
            Log("[System] 正在打开资源管理器：" + location);
            if (location.EndsWithF(@"\"))
                ShellOnly(location);
            else
                ShellOnly("explorer", $"/select,\"{location}\"");
        }
        catch (Exception ex)
        {
            Log(
                ex,
                "打开资源管理器失败，请尝试关闭安全软件（如 360 安全卫士）",
                LogLevel.Msgbox,
                userSummary: Lang.Text("SystemDialog.Folder.OpenFailed.Message", location));
        }
    }

    /// <summary>
    ///     设置剪贴板。将在另一线程运行，且不会抛出异常。
    /// </summary>
    public static void ClipboardSet(string text, bool showSuccessHint = true)
    {
        RunInThread(() =>
        {
            var success = false;

            for (var attempt = 0; attempt <= 5; attempt++)
                try
                {
                    RunInUi(() => Clipboard.SetText(text));
                    success = true;
                    break;
                }
                catch (Exception ex) when (attempt < 5)
                {
                    Thread.Sleep(20);
                }
                catch (Exception finalEx)
                {
                    Log(
                        finalEx,
                        "剪贴板被占用，文本复制失败",
                        LogLevel.Hint,
                        userSummary: Lang.Text("Common.Hint.CopyFailed"));
                }

            if (success && showSuccessHint)
                RunInUi(() => HintService.Hint(Lang.Text("Common.Hint.Copied"), HintType.Success));
        });
    }

    /// <summary>
    ///     从剪切板粘贴文件或文件夹
    /// </summary>
    /// <param name="dest">目标文件夹</param>
    /// <param name="copyFile">是否粘贴文件</param>
    /// <param name="copyDir">是否粘贴文件夹</param>
    /// <returns>总共粘贴的数量</returns>
    public static int PasteFileFromClipboard(string dest, bool copyFile = true, bool copyDir = true)
    {
        Log("[System] 从剪贴板粘贴文件到：" + dest);
        try
        {
            var files = Clipboard.GetFileDropList();
            if (files.Count.Equals(0))
            {
                Log("[System] 剪贴板内无文件可粘贴");
                return 0;
            }

            var copiedFiles = 0;
            var copiedFolders = 0;
            foreach (var i in files)
            {
                if (copyFile && File.Exists(i)) // 文件
                    try
                    {
                        var thisDest = dest + GetFileNameFromPath(i);
                        if (File.Exists(thisDest))
                        {
                            Log("[System] 已存在同名文件：" + thisDest);
                        }
                        else
                        {
                            File.Copy(i, thisDest);
                            copiedFiles += 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex, "[System] 复制文件时出错");
                        continue;
                    }

                if (copyDir && Directory.Exists(i)) // 文件夹
                    try
                    {
                        var thisDest = dest + GetFolderNameFromPath(i);
                        if (Directory.Exists(thisDest))
                        {
                            Log("[System] 已存在同名文件夹：" + thisDest);
                        }
                        else
                        {
                            CopyDirectory(i, thisDest);
                            copiedFolders += 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex, "[System] 复制文件时出错");
                    }
            }

            HintService.Hint(Lang.Text("Common.Hint.FilesPasted", copiedFiles, copiedFolders));
        }
        catch (Exception ex)
        {
            Log(ex, "[System] 从剪切板粘贴文件失败", LogLevel.Hint);
        }

        return 0;
    }

    /// <summary>
    ///     获取程序打包资源的输入流。该资源必须声明为 <c>Resource</c> 类型，否则将会报错，<c>Images</c>
    ///     和 <c>Resources</c> 目录已默认声明该类型。
    /// </summary>
    public static Stream GetResourceStream(string path)
    {
        return Basics.GetResourceStream(path);
    }

    #endregion
}