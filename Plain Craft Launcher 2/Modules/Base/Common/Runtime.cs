using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Threading;
using PCL.Core.Utils.OS;

namespace PCL;

public static partial class Common
{
    public delegate bool ComparisonBoolean<in T>(T left, T right);

    private static int _uuid = 1;
    private static object? _uuidLock;

    public static bool IsUtf8CodePage()
    {
        return Encoding.Default.CodePage == 65001;
    }

    /// <summary>
    ///     可用于临时存放文件的，不含任何特殊字符的文件夹路径，以“\”结尾。
    /// </summary>
    private static string GetPureAsciiDir()
    {
        if (ExePath.IsAscii()) return $@"{ExePath}PCL\";
        if (pathAppdata.IsAscii()) return pathAppdata;
        if (pathTemp.IsAscii()) return pathTemp;
        return Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL");
    }

    extension(Type genericType)
    {
        /// <summary>
        ///     判断对象是否为某个泛型类型的实例。
        /// </summary>
        public bool IsInstanceOfGenericType(object? obj)
        {
            if (obj is null)
                return false;

            var t = obj.GetType();
            while (t is not null)
            {
                if (t.IsGenericType
                    && ReferenceEquals(t.GetGenericTypeDefinition(), genericType))
                    return true;
                t = t.BaseType;
            }

            return false;
        }
    }

    /// <summary>
    ///     获取一个全程序内不会重复的数字（伪 Uuid）。
    /// </summary>
    public static int GetUuid()
    {
        _uuidLock ??= new object();
        lock (_uuidLock)
        {
            _uuid += 1;
            return _uuid;
        }
    }

    /// <summary>
    ///     将元素与 List 的混合体拆分为元素组。
    /// </summary>
    public static List<T> GetFullList<T>(IList data)
    {
        var getFullListRet = new List<T>();
        for (int i = 0, loopTo = data.Count - 1; i <= loopTo; i++)
            if (data[i] is ICollection collection)
                getFullListRet.AddRange((IEnumerable<T>)collection);
            else
                getFullListRet.Add((T)data[i]!);
        return getFullListRet;
    }

    extension<T>(ICollection<T> arr)
    {
        /// <summary>
        ///     数组去重。
        /// </summary>
        public List<T> Distinct(ComparisonBoolean<T> isEqual)
        {
            var resultArray = new List<T>();
            for (int i = 0, loopTo = arr.Count - 1; i <= loopTo; i++)
            {
                var isDuplicate = false;
                for (int ii = i + 1, loopTo1 = arr.Count - 1; ii <= loopTo1; ii++)
                {
                    if (!isEqual(arr.ElementAt(i), arr.ElementAt(ii))) continue;
                    isDuplicate = true;
                    break;
                }

                if (!isDuplicate)
                    resultArray.Add(arr.ElementAt(i));
            }

            return resultArray;
        }
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
            Log($"[System] 执行外部命令：{fileName} {arguments}");
            program.Start();
        }
        catch (Exception ex)
        {
            Log(
                ex,
                $"打开文件或程序失败：{fileName}",
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
    public static ProcessReturnValues ShellAndGetExitCode(
        string fileName,
        string arguments = "",
        int timeout = 1000000)
    {
        try
        {
            using var program = new Process();
            program.StartInfo.Arguments = arguments;
            program.StartInfo.FileName = fileName;
            Log($"[System] 执行外部命令并等待返回码：{fileName} {arguments}");
            program.Start();
            return program.WaitForExit(timeout)
                ? (ProcessReturnValues)program.ExitCode
                : ProcessReturnValues.Timeout;
        }
        catch (Exception ex)
        {
            Log(ex, $"执行命令失败：{fileName}", LogLevel.Msgbox);
            return ProcessReturnValues.Fail;
        }
    }

    /// <summary>
    ///     静默运行文件并返回输出流字符串。执行失败会抛出异常。
    /// </summary>
    /// <param name="fileName">文件名。可以为“notepad”等缩写。</param>
    /// <param name="arguments">运行参数。</param>
    /// <param name="timeout">等待该程序结束的最长时间（毫秒）。超时会抛出错误。</param>
    public static string ShellAndGetOutput(
        string fileName,
        string arguments = "",
        int timeout = 1000000,
        string? workingDirectory = null)
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

        if (!string.IsNullOrEmpty(workingDirectory))
            info.WorkingDirectory = workingDirectory.TrimEnd('\\');

        Log($"[System] 执行外部命令并等待返回结果：{fileName} {arguments}");

        using var program = new Process();
        program.StartInfo = info;
        program.Start();

        var outputTask = program.StandardOutput.ReadToEndAsync();
        var errorTask = program.StandardError.ReadToEndAsync();

        if (program.WaitForExit(timeout))
        {
            Task.WaitAll(outputTask, errorTask);
        }
        else
        {
            program.Kill();
            Task.WaitAll(outputTask, errorTask);
        }

        return outputTask.Result + errorTask.Result;
    }

    /// <summary>
    ///     在新的工作线程中执行代码。
    /// </summary>
    public static Thread RunInNewThread(
        Action action,
        string? name = null,
        ThreadPriority priority = ThreadPriority.Normal)
    {
        var th = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (ThreadInterruptedException)
            {
                Log($"{name}：线程已中止");
            }
            catch (Exception ex)
            {
                Log(ex, $"{name}：线程执行失败", LogLevel.Feedback);
            }
        })
        {
            Name = name ?? $"Runtime New Invoke {GetUuid()}#",
            Priority = priority
        };
        th.IsBackground = true;
        th.Start();
        return th;
    }

    /// <summary>
    ///     确保在 UI 线程中执行代码。
    ///     如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    ///     为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
    /// </summary>
    public static Output RunInUiWait<Output>(Func<Output> action)
    {
        return RunInUi() ? action() : UiDispatcher.Invoke(action)!;
    }

    /// <summary>
    ///     确保在 UI 线程中执行代码。
    ///     如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    ///     为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
    /// </summary>
    public static void RunInUiWait(Action action)
    {
        if (RunInUi())
            action();
        else
            UiDispatcher.Invoke(action);
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
            System.Windows.Application.Current.Dispatcher.InvokeAsync(
                action,
                forceWaitUntilLoaded
                    ? DispatcherPriority.Loaded
                    : DispatcherPriority.Normal);
    }

    /// <summary>
    ///     确保在工作线程中执行代码。
    /// </summary>
    public static void RunInThread(Action action)
    {
        if (RunInUi())
            RunInNewThread(action, $"Runtime Invoke {GetUuid()}#");
        else
            action();
    }

    extension<T>(IList<T> list)
    {
        /// <summary>
        ///     使用优化的归并排序算法进行稳定排序。
        /// </summary>
        /// <param name="sortRule">传入两个对象，若第一个对象应该排在前面，则返回 True。</param>
        public List<T> Sort(ComparisonBoolean<T> sortRule)
        {
            var tempList = new List<T>(list);
            if (tempList.Count <= 1)
                return tempList;

            MergeSort_Sort(ref tempList, 0, tempList.Count - 1, sortRule);
            return tempList;
        }
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

        while (leftPtr < leftArray.Count
               && rightPtr < rightArray.Count)
        {
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

    /// <summary>
    ///     返回列表的浅表副本。
    /// </summary>
    public static IList<T> Clone<T>(this IList<T> list)
    {
        return new List<T>(list);
    }

    extension<TKey, TValue>(Dictionary<TKey, TValue> dict) where TKey : notnull
    {
        /// <summary>
        ///     尝试从字典中获取某项，如果该项不存在，则返回默认值。
        /// </summary>
        public TValue GetOrDefault(
            TKey key,
            TValue defaultValue = default!)
        {
            return dict.GetValueOrDefault(key, defaultValue);
        }
    }

    extension<TKey, TValue>(Dictionary<TKey, List<TValue>> dict) where TKey : notnull
    {
        /// <summary>
        ///     将某项添加到以列表作为值的字典中。
        /// </summary>
        public void AddToList(
            TKey key,
            TValue value)
        {
            if (dict.TryGetValue(key, out var value1))
                value1.Add(value);
            else
                dict.Add(key, [value]);
        }
    }

    /// <summary>
    ///     获取程序启动参数。
    /// </summary>
    /// <param name="name">参数名。</param>
    /// <param name="defaultValue">默认值。</param>
    public static object? GetProgramArgument(string name, object? defaultValue = null)
    {
        var allArguments = Interaction.Command().Split(" ");
        for (int i = 0, loopTo = allArguments.Length - 1; i <= loopTo; i++)
            if ((allArguments[i] ?? "") == ($"-{name}" ?? ""))
                return allArguments.Length == i + 1 || allArguments[i + 1].StartsWithF("-")
                    ? true
                    : allArguments[i + 1];
        return defaultValue;
    }

    /// <summary>
    ///     打开网页。
    /// </summary>
    public static void OpenWebsite(string url)
    {
        try
        {
            if (!url.StartsWithF("http", true)
                && !url.StartsWithF("minecraft://", true))
                throw new Exception($"{url} 不是一个有效的网址，它必须以 http 开头！");
            Log($"[System] 正在打开网页：{url}");
            var psi = new ProcessStartInfo(url)
            {
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log(ex, $"无法打开网页（{url}）");
            ClipboardSet(url, false);
            var message = ExceptionDetails.Compose(
                Lang.Text("SystemDialog.Browser.OpenFailed.Message", url),
                ex);
            Launcher.MyMsgBox(
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
            Log($"[System] 正在打开资源管理器：{location}");
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
                catch (Exception) when (attempt < 5)
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
        Log($"[System] 从剪贴板粘贴文件到：{dest}");
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
                if (copyFile && File.Exists(i))
                    try
                    {
                        var thisDest = dest + GetFileNameFromPath(i);
                        if (File.Exists(thisDest))
                        {
                            Log($"[System] 已存在同名文件：{thisDest}");
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

                if (copyDir && Directory.Exists(i))
                    try
                    {
                        var thisDest = dest + GetFolderNameFromPath(i);
                        if (Directory.Exists(thisDest))
                        {
                            Log($"[System] 已存在同名文件夹：{thisDest}");
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
    public static Stream? GetResourceStream(string path)
    {
        var resourceInfo =
            System.Windows.Application.GetResourceStream(
                new Uri(
                    $"pack://application:,,,/{path}",
                    UriKind.Absolute));
        return resourceInfo?.Stream;
    }

    /// <summary>
    ///     检查是否拥有某一文件夹的 I/O 权限。如果文件夹不存在，会返回 False。
    /// </summary>
    public static bool CheckPermission(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
                return false;
            if (!path.EndsWithF(@"\"))
                path += @"\";
            if (path.EndsWithF(@":\System Volume Information\")
                || path.EndsWithF(@":\$RECYCLE.BIN\"))
                return false;
            if (!Directory.Exists(path))
                return false;
            var fileName = $"CheckPermission{GetUuid()}";
            if (File.Exists(path + fileName))
                File.Delete(path + fileName);
            File.Create(path + fileName).Dispose();
            File.Delete(path + fileName);
            return true;
        }
        catch (Exception ex)
        {
            Log(ex, $"没有对文件夹 {path} 的权限，请尝试以管理员权限运行 PCL");
            return false;
        }
    }

    /// <summary>
    ///     检查是否拥有某一文件夹的 I/O 权限。如果出错，则抛出异常。
    /// </summary>
    public static void CheckPermissionWithException(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path), "文件夹名不能为空！");
        if (!path.EndsWithF(@"\"))
            path += @"\";
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException("文件夹不存在！");
        if (File.Exists($"{path}CheckPermission"))
            File.Delete($"{path}CheckPermission");
        File.Create($"{path}CheckPermission").Dispose();
        File.Delete($"{path}CheckPermission");
    }

    /// <summary>
    ///     线程安全的 List。
    ///     通过在 For Each 循环中使用一个浅表副本规避多线程操作或移除自身导致的异常。
    /// </summary>
    public class SafeList<T> : IDisposable, ICollection<T>
    {
        private readonly List<T> _internalList;
        private readonly ReaderWriterLockSlim _lock = new();

        public SafeList()
        {
            _internalList = [];
        }

        public SafeList(IEnumerable<T> data)
        {
            _internalList = [.. data];
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

        public IEnumerator<T> GetEnumerator()
        {
            return ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            _lock.Dispose();
        }

        public List<T> ToList()
        {
            _lock.EnterReadLock();
            try
            {
                return [.. _internalList];
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
    ///     指示用户手动取消了操作，或用户已知晓操作被取消的原因。
    /// </summary>
    public class CancelledException : Exception;

    /// <summary>
    ///     用于储存 RaiseByMouse 的 EventArgs。
    /// </summary>
    public sealed class RouteEventArgs(bool raiseByMouse = false) : EventArgs
    {
        public bool handled = false;
        public bool raiseByMouse = raiseByMouse;
    }
}