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
        var taskList = tasks?.ToList() ?? throw new ArgumentNullException(nameof(tasks));
        if (taskList.Count == 0)
            throw new ArgumentException("Task collection is empty.", nameof(tasks));

        var remaining = new HashSet<Task<T>>(taskList);

        while (remaining.Count > 0)
        {
            var completed = await Task.WhenAny(remaining);
            remaining.Remove(completed);
            if (completed.IsFaulted || completed.IsCanceled) continue;

            try
            {
                _ = await completed;
                return completed;
            }
            catch
            {
                // 忽略异常，继续尝试其他任务
            }
        }

        // 所有任务都失败或被取消
        var exceptions = taskList
            .Where(t => t.IsFaulted)
            .SelectMany(t => t.Exception?.InnerExceptions ?? Enumerable.Empty<Exception>())
            .ToList();

        if (exceptions.Count == 0)
        {
            // 所有任务都被取消
            exceptions.Add(new TaskCanceledException("All tasks were canceled."));
        }

        throw new AggregateException("All tasks failed or were canceled.", exceptions);
    }
}