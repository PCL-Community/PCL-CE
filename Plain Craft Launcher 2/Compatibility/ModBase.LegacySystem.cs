using System.Collections;
using System.IO;
using PCL.Core.App;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Exts;

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
    public static string pathPure
    {
        get => LauncherPaths.PureAsciiDirectory;
        set => LauncherPaths.PureAsciiDirectory = value;
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
        return ReflectionUtils.IsInstanceOfGenericType(genericType, obj);
    }

    /// <summary>
    ///     获取一个全程序内不会重复的数字（伪 Uuid）。
    /// </summary>
    public static int GetUuid()
    {
        return LauncherRuntime.GetUuid();
    }

    /// <summary>
    ///     将元素与 List 的混合体拆分为元素组。
    /// </summary>
    public static List<T> GetFullList<T>(IList data)
    {
        return CollectionUtils.FlattenMixedList<T>(data);
    }

    /// <summary>
    ///     数组去重。
    /// </summary>
    public static List<T> Distinct<T>(this ICollection<T> arr, ComparisonBoolean<T> isEqual)
    {
        return CollectionUtils.DistinctByComparison(arr, (left, right) => isEqual(left, right), true);
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
    public static void ShellOnly(string fileName, string arguments = "")
    {
        LauncherProcess.ShellOnly(fileName, arguments);
    }

    /// <summary>
    ///     前台运行文件并返回返回值。
    /// </summary>
    public static ProcessReturnValues ShellAndGetExitCode(string fileName, string arguments = "", int timeout = 1000000)
    {
        return (ProcessReturnValues)LauncherProcess.ShellAndGetExitCode(fileName, arguments, timeout);
    }

    /// <summary>
    ///     静默运行文件并返回输出流字符串。执行失败会抛出异常。
    /// </summary>
    public static string ShellAndGetOutput(string fileName, string arguments = "", int timeout = 1000000,
        string workingDirectory = null)
    {
        return LauncherProcess.ShellAndGetOutput(fileName, arguments, timeout, workingDirectory);
    }

    /// <summary>
    ///     在新的工作线程中执行代码。
    /// </summary>
    public static Thread RunInNewThread(Action action, string name = null,
        ThreadPriority priority = ThreadPriority.Normal)
    {
        return Basics.RunInNewThread(action, name ?? "Runtime New Invoke " + GetUuid() + "#", priority);
    }

    /// <summary>
    ///     确保在 UI 线程中执行代码。
    ///     如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    /// </summary>
    public static Output RunInUiWait<Output>(Func<Output> action)
    {
        return UiThread.Invoke(action);
    }

    /// <summary>
    ///     确保在 UI 线程中执行代码。
    ///     如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    /// </summary>
    public static void RunInUiWait(Action action)
    {
        UiThread.Invoke(action);
    }

    /// <summary>
    ///     确保在 UI 线程中执行代码，代码按触发顺序执行。
    ///     如果当前并非 UI 线程，也不阻断当前线程的执行。
    /// </summary>
    public static void RunInUi(Action action, bool forceWaitUntilLoaded = false)
    {
        UiThread.Post(action, forceWaitUntilLoaded);
    }

    /// <summary>
    ///     确保在工作线程中执行代码。
    /// </summary>
    public static void RunInThread(Action action)
    {
        UiThread.RunInThread(action);
    }

    /// <summary>
    ///     使用优化的归并排序算法进行稳定排序。
    /// </summary>
    public static List<T> Sort<T>(this IList<T> list, ComparisonBoolean<T> sortRule)
    {
        return SortUtils.Sort(list, (left, right) => sortRule(left, right));
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
    public static TValue GetOrDefault<TKey, TValue>(
        this Dictionary<TKey, TValue> dict,
        TKey key,
        TValue defaultValue = default)
    {
        return DictionaryExtensions.GetOrDefault(dict, key, defaultValue);
    }

    /// <summary>
    ///     将某项添加到以列表作为值的字典中。
    /// </summary>
    public static void AddToList<TKey, TValue>(
        this Dictionary<TKey, List<TValue>> dict,
        TKey key,
        TValue value)
    {
        DictionaryExtensions.AddToList(dict, key, value);
    }

    /// <summary>
    ///     获取程序启动参数。
    /// </summary>
    public static object GetProgramArgument(string name, object? defaultValue = null)
    {
        return LauncherArguments.Get(name, defaultValue);
    }

    /// <summary>
    ///     打开网页。
    /// </summary>
    public static void OpenWebsite(string url)
    {
        LauncherProcess.OpenWebsite(url);
    }

    /// <summary>
    ///     打开 explorer。
    ///     若不以 \ 结尾，则将视作文件路径，打开并选中此文件。
    /// </summary>
    public static void OpenExplorer(string location)
    {
        LauncherProcess.OpenExplorer(location);
    }

    /// <summary>
    ///     设置剪贴板。将在另一线程运行，且不会抛出异常。
    /// </summary>
    public static void ClipboardSet(string text, bool showSuccessHint = true)
    {
        LauncherProcess.ClipboardSet(text, showSuccessHint);
    }

    /// <summary>
    ///     从剪切板粘贴文件或文件夹
    /// </summary>
    public static int PasteFileFromClipboard(string dest, bool copyFile = true, bool copyDir = true)
    {
        return LauncherProcess.PasteFileFromClipboard(dest, copyFile, copyDir);
    }

    /// <summary>
    ///     获取程序打包资源的输入流。
    /// </summary>
    public static Stream GetResourceStream(string path)
    {
        return Basics.GetResourceStream(path);
    }

    #endregion
}