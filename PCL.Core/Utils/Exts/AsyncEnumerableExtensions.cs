using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PCL.Core.Utils.Exts;

public static class AsyncEnumerableExtensions
{
    /// <param name="source">源集合</param>
    /// <typeparam name="T">集合元素类型</typeparam>
    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        /// 对集合中的每个元素异步执行指定操作，最多同时运行 maxDegreeOfParallelism 个任务。
        /// </summary>
        /// <param name="action">对每个元素执行的异步操作</param>
        /// <param name="maxDegreeOfParallelism">最大并发数，默认为 10</param>
        /// <returns>所有任务完成后的任务</returns>
        public async Task ForEachAsync(
            Func<T, Task> action,
            int maxDegreeOfParallelism = 10)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(action);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDegreeOfParallelism);

            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

            var tasks = source.Select(async item =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await action(item);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 对集合中的每个元素异步执行指定操作，最多同时运行 maxDegreeOfParallelism 个任务。
        /// </summary>
        /// <param name="action">对每个元素执行的异步操作</param>
        /// <param name="maxDegreeOfParallelism">最大并发数，默认为 10</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>所有任务完成后的任务</returns>
        public async Task ForEachAsync(
            Func<T, CancellationToken, Task> action,
            int maxDegreeOfParallelism = 10,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(action);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDegreeOfParallelism);

            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

            var tasks = source.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await action(item, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 对集合中的每个元素异步执行指定操作，限制并发数，并返回所有操作的结果。
        /// 等同于：source.Select(x => action(x)).WhenAll()，但有并发控制。
        /// </summary>
        /// <typeparam name="TResult">操作返回类型</typeparam>
        /// <param name="selector">异步选择器函数</param>
        /// <param name="maxDegreeOfParallelism">最大并发数，默认为 10</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>包含所有操作结果的集合</returns>
        public async Task<IEnumerable<TResult>> SelectAsync<TResult>(
            Func<T, Task<TResult>> selector,
            int maxDegreeOfParallelism = 10,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(selector);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDegreeOfParallelism);

            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

            var tasks = source.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await selector(item);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            return await Task.WhenAll(tasks);
        }
    }
}