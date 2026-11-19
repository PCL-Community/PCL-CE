using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;
using PCL.Core.UI;

namespace PCL.Core.Test.UI;

[TestClass]
[DoNotParallelize]
public sealed class SystemDialogsAsyncTest
{
    private readonly TestFileDialogService _fileDialogs = new();
    private int _uiThreadId;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Initialize()
    {
        // MSTest may construct the test class on a different thread than the test method.
        // Capture the actual test thread here so CheckAccess() has the right expectation.
        _uiThreadId = Environment.CurrentManagedThreadId;
        ApplicationRuntime.ResetForTests();
        var provider = new TestServiceProvider()
            .Add<IFileDialogService>(_fileDialogs)
            .Add<IUiDispatcher>(new TestUiDispatcher(_uiThreadId));
        ApplicationRuntime.Initialize(
            new ApplicationServices
            {
                Platform = new PlatformModule
                {
                    Kind = PlatformKind.Linux,
                    Capabilities = PlatformCapabilities.Linux
                },
                Ui = new UiModule(),
                Services = provider
            });
    }

    [TestCleanup]
    public void Cleanup()
    {
        ApplicationRuntime.Shutdown();
        ApplicationRuntime.ResetForTests();
    }

    [TestMethod]
    public async Task SelectFolderAsyncReturnsServiceResult()
    {
        var result = await SystemDialogs.SelectFolderAsync(
            "folder",
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(TestFileDialogService.FolderPath, result);
        Assert.AreEqual("folder", _fileDialogs.LastTitle);
    }

    [TestMethod]
    public async Task SelectFilesAsyncReturnsServiceResults()
    {
        var result = await SystemDialogs.SelectFilesAsync(
            "*.jar",
            cancellationToken: TestContext.CancellationToken);

        Assert.AreSequenceEqual(TestFileDialogService.Files, result);
    }

    [TestMethod]
    public async Task SelectSaveFileAsyncReturnsServiceResult()
    {
        var result = await SystemDialogs.SelectSaveFileAsync(
            "title",
            "name.txt",
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(TestFileDialogService.SavePath, result);
    }

    [TestMethod]
    public void BlockingEntryRejectsUiThread()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SystemDialogs.SelectFolderBlockingOnWorkerThread());
    }

    [TestMethod]
    public void BlockingEntryWorksOnWorkerThread()
    {
        string? result = null;
        Exception? error = null;

        // Task.Run may reuse the calling thread-pool thread when it is idle, making
        // this test non-deterministic. Use a dedicated thread so the "worker thread"
        // contract is always exercised.
        var worker = new Thread(() =>
        {
            try
            {
                result = SystemDialogs.SelectFolderBlockingOnWorkerThread();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        worker.Start();
        Assert.IsTrue(worker.Join(TimeSpan.FromSeconds(5)), "worker thread did not finish");

        Assert.IsNull(error);
        Assert.AreEqual(TestFileDialogService.FolderPath, result);
    }

    private sealed class TestFileDialogService : IFileDialogService
    {
        public const string FolderPath = "/tmp/pcl-selected/";
        public const string SavePath = "/tmp/pcl-selected.txt";
        public static readonly string[] Files = ["/tmp/a.jar", "/tmp/b.jar"];

        public string? LastTitle { get; private set; }

        public bool IsAvailable => true;

        public Task<string> SelectSaveFileAsync(
            string? title,
            string fileName,
            string? fileFilter = null,
            string? initialDirectory = null,
            CancellationToken cancellationToken = default)
        {
            LastTitle = title;
            return Task.FromResult(SavePath);
        }

        public Task<string> SelectFileAsync(
            string? fileFilter = null,
            string? title = null,
            string? initialDirectory = null,
            CancellationToken cancellationToken = default)
        {
            LastTitle = title;
            return Task.FromResult(Files[0]);
        }

        public Task<string[]> SelectFilesAsync(
            string? fileFilter = null,
            string? title = null,
            string? initialDirectory = null,
            bool allowMultiSelect = true,
            CancellationToken cancellationToken = default)
        {
            LastTitle = title;
            return Task.FromResult(Files);
        }

        public Task<string> SelectFolderAsync(
            string? title = null,
            string? initialDirectory = null,
            CancellationToken cancellationToken = default)
        {
            LastTitle = title;
            return Task.FromResult(FolderPath);
        }
    }

    private sealed class TestUiDispatcher(int uiThreadId) : IUiDispatcher
    {
        private readonly int _uiThreadId = uiThreadId;

        public bool CheckAccess()
        {
            return Environment.CurrentManagedThreadId == _uiThreadId;
        }

        public void Post(Action action)
        {
            action();
        }

        public void Invoke(Action action)
        {
            action();
        }

        public T? Invoke<T>(Func<T> func)
        {
            return func();
        }

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            return Task.FromResult(func());
        }
    }
}