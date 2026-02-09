using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PCL.Core.Utils.Exts;
public static class TaskExtensions
{
    /// <summary>
    /// 返回第一个成功完成的 Task 的结果。
    /// 如果所有 Task 都失败或被取消，则抛出 AggregateException。
    /// </summary>
    /// <typeparam name="T">Task 的返回类型</typeparam>
    /// <param name="tasks">要等待的任务集合</param>
    /// <returns>第一个成功完成的 Task 的结果</returns>
    /// <exception cref="ArgumentException">任务集合为空</exception>
    /// <exception cref="AggregateException">所有任务均未成功完成</exception>
    public static async Task<Task<T>> WhenAnySuccess<T>(this IEnumerable<Task<T>> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks, nameof(tasks));
        var taskList = tasks.ToList();
        if (taskList.Count == 0)
            throw new ArgumentException("Task collection is empty.", nameof(tasks));

        var remaining = new HashSet<Task<T>>(taskList);

        while (remaining.Count > 0)
        {
            var completed = await Task.WhenAny(remaining);
            remaining.Remove(completed);
            if (completed.IsCompletedSuccessfully)
            {
                return completed;
            }
        }

        // 所有任务都失败或被取消
        var faultedExceptions = taskList
            .Where(t => t.IsFaulted)
            .SelectMany(t => t.Exception?.Flatten().InnerExceptions ?? Enumerable.Empty<Exception>())
            .ToList();

        if (faultedExceptions.Count > 0)
        {
            throw new AggregateException("All connection attempts failed.", faultedExceptions);
        }

        // 如果没有失败任务，但还有任务，为 canceled
        if (taskList.Any(t => t.IsCanceled))
        {
            throw new OperationCanceledException("All connection attempts were canceled.");
        }

        // Task 要么成功，要么 Faulted，要么 Canceled，大概率走不到这
        throw new InvalidOperationException("All tasks completed but none succeeded, failed, or canceled.");
    }

    extension(Task task)
    {
        /// <summary>
        /// 不关心 Task 后续的异常，但是需要观察下
        /// </summary>
        public void Forget()
        {
            _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}