namespace PCL.Core.App.Configuration.NTraffic;

// ReSharper disable once InconsistentNaming
public interface TrafficEventArgs
{
    /// <summary>
    /// 转换到特定类型的事件参数。
    /// </summary>
    /// <exception cref="System.InvalidCastException">类型不匹配</exception>
    public TrafficEventArgs<TInput, TOutput> Cast<TInput, TOutput>() => (TrafficEventArgs<TInput, TOutput>)this;

    /// <summary>
    /// 尝试转换到特定类型的事件参数，若类型不匹配则返回 <c>null</c>。
    /// </summary>
    public TrafficEventArgs<TInput, TOutput>? TryCast<TInput, TOutput>() => this as TrafficEventArgs<TInput, TOutput>;
}

// ReSharper disable once InconsistentNaming
public interface PreviewTrafficEventArgs
{
    /// <summary>
    /// 转换到特定类型的事件参数。
    /// </summary>
    /// <exception cref="System.InvalidCastException">类型不匹配</exception>
    public TrafficEventArgs<TInput, TOutput>.Preview Cast<TInput, TOutput>() => (TrafficEventArgs<TInput, TOutput>.Preview)this;

    /// <summary>
    /// 尝试转换到特定类型的事件参数，若类型不匹配则返回 <c>null</c>。
    /// </summary>
    public TrafficEventArgs<TInput, TOutput>.Preview? TryCast<TInput, TOutput>() => this as TrafficEventArgs<TInput, TOutput>.Preview;
}

public class TrafficEventArgs<TInput, TOutput> : TrafficEventArgs
{
    /// <summary>
    /// 执行类型。
    /// </summary>
    public TrafficAccess Access { get; init; }

    /// <summary>
    /// 上下文。
    /// </summary>
    public object? Context { get; init; }

    /// <summary>
    /// 指示是否具有输入值，该值为 <c>false</c> 代表应忽略 <see cref="Input"/>，无论其为何值。
    /// </summary>
    public bool HasInput { get; private init; }

    /// <summary>
    /// 输入值。
    /// </summary>
    public TInput? Input
    {
        get;
        init
        {
            field = value;
            HasInput = true;
        }
    }

    /// <summary>
    /// 指示是否具有输出值，该值为 <c>false</c> 代表应忽略 <see cref="Output"/>，无论其为何值。
    /// </summary>
    public bool HasOutput { get; private set; }

    /// <summary>
    /// 在 <see cref="HasOutput"/> 为 <c>true</c> 时，指示当前 <see cref="Output"/> 是否是初始值。
    /// </summary>
    public bool IsInitialOutput { get; private set; }

    /// <summary>
    /// 当 <see cref="HasOutput"/> 和 <see cref="IsInitialOutput"/> 同时为 <c>true</c> 时，该值为 <c>true</c>。
    /// </summary>
    public bool HasInitialOutput => HasOutput && IsInitialOutput;

    /// <summary>
    /// 输出值。
    /// </summary>
    public TOutput? Output { get; private set; }

    public TrafficEventArgs() { HasInput = false; }
    public TrafficEventArgs(TInput input) { Input = input; HasInput = true; }

    public class Preview : TrafficEventArgs<TInput, TOutput>, PreviewTrafficEventArgs
    {
        /// <summary>
        /// 设置输出值。
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="isInitial">指示该值是否应作为初始值处理</param>
        public void SetOutput<T>(T? value, bool isInitial = false)
        {
            Output = (TOutput?)(object?)value;
            HasOutput = true;
            IsInitialOutput = isInitial;
        }

        protected void SetInitialOutput<T>(T? value)
        {
            SetOutput(value, true);
        }
    }
}

public class PreviewTrafficEventArgs<TInput, TOutput> : TrafficEventArgs<TInput, TOutput>.Preview
{
    public PreviewTrafficEventArgs() { }
    public PreviewTrafficEventArgs(TInput input) { Input = input; }
    public PreviewTrafficEventArgs(TInput input, TOutput initialOutput) { Input = input; SetInitialOutput(initialOutput); }
}
