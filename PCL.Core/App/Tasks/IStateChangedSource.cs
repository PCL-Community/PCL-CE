namespace PCL.Core.App.Tasks;

/// <summary>
/// 状态值改变处理。
/// </summary>
/// <typeparam name="TProperty">状态值的类型</typeparam>
/// <param name="source">来源实例</param>
/// <param name="oldValue">改变前的值</param>
/// <param name="newValue">改变后的值</param>
public delegate void StateChangedHandler<in TProperty>(object source, TProperty oldValue, TProperty newValue);

/// <summary>
/// 可观察的状态值改变模型。
/// </summary>
/// <typeparam name="TProperty">状态值的类型</typeparam>
public interface IStateChangedSource<out TProperty>
{
    /// <summary>
    /// 状态值改变事件。
    /// 当你需要实现这个事件时，请保证只有在当前派生类型中可观察的唯一状态会触发这个事件，而不是像
    /// <see cref="System.ComponentModel.INotifyPropertyChanged"/> 一样将所有属性的更改都传递过去。
    /// </summary>
    event StateChangedHandler<TProperty>? StateChanged;
}