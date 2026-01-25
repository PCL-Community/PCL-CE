namespace PCL.Core.App.Tasks;

public delegate void ProgressChangedHandler(object source, double progress);

/// <summary>
/// 可量化进度模型。<br/>
/// 实现 <see cref="IStateChangedSource{TProperty}"/> 或直接实现
/// <see cref="IObservableProgressSource"/> 以动态响应进度改变。
/// </summary>
public interface IProgressSource
{
    /// <summary>
    /// 当前进度。
    /// </summary>
    double Progress { get; }
}

/// <summary>
/// 可动态响应进度变化的可量化进度模型。
/// </summary>
public interface IObservableProgressSource : IProgressSource, IStateChangedSource<double>;
