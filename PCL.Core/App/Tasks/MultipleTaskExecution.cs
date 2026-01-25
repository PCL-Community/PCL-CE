/*
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks;

public class MultipleTaskExecution {
    public async Task ExecuteTaskSequentiallyAsync<TInput, TOutput>(
        TaskBase<TOutput> mainLoader,
        List<KeyValuePair<TaskBase<TOutput>, int>> loaderList,
        TInput input,
        bool isForceRestart = false) {
        if (isForceRestart) {
            foreach (var loader in loaderList) {
                loader.Key.State = TaskState.Waiting;
                loader.Key.Result = default;
            }
        } else {
            foreach (var loader in loaderList) {
                if (loader.Key.State == TaskState.Completed && Equals(input, loader.Key.BackgroundTask?.AsyncState)) {
                    mainLoader.Result = loader.Key.Result;
                    return;
                }
            }
        }

        var tcs = new TaskCompletionSource<bool>();
        var currentIndex = 0;

        async void OnStateChanged(object sender, TaskState oldState, TaskState newState) {
            var loader = (TaskBase<TOutput>) sender;
            switch (newState) {
                case TaskState.Completed : {
                    mainLoader.Result = loader.Result;
                    AbortOtherLoaders(loaderList, loader);
                    tcs.SetResult(true);
                    break;
                } 
                case TaskState.Failed or TaskState.Canceled : {
                    if (currentIndex < loaderList.Count - 1) {
                        currentIndex++;
                        var nextLoader = loaderList[currentIndex].Key;
                        nextLoader.State = TaskState.Waiting;
                        nextLoader.RunBackground(input);
                        await Task.Delay(loaderList[currentIndex].Value * 1000).ContinueWith(t => {
                            if (nextLoader.State == TaskState.Running)
                                nextLoader.State = TaskState.Failed;
                        });
                    } else {
                        var error = loaderList
                                        .Select(l => l.Key.BackgroundTask?.Exception?.InnerException)
                                        .FirstOrDefault(e => e != null && e.Message.Contains("不可用"))
                                    ?? new TimeoutException("所有下载源连接超时");
                        AbortOtherLoaders(loaderList, null);
                        tcs.SetException(error);
                    }
                    break;
                }
            }
        }

        // 启动第一个加载器
        var firstLoader = loaderList[0].Key;
        firstLoader.StateChanged += OnStateChanged;
        firstLoader.RunBackground(input);
        await Task.Delay(loaderList[0].Value * 1000).ContinueWith(t => {
            if (firstLoader.State == TaskState.Running)
                firstLoader.State = TaskState.Failed;
        });

        await tcs.Task;
    }

    private void AbortOtherLoaders<TOutput>(List<KeyValuePair<TaskBase<TOutput>, int>> loaderList, TaskBase<TOutput>? except) {
        foreach (var loader in loaderList) {
            if (loader.Key != except && loader.Key.State == TaskState.Running) {
                loader.Key.State = TaskState.Canceled;
            }
        }
    }
}
*/
