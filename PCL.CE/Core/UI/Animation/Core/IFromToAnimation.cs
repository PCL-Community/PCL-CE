namespace PCL.CE.Core.UI.Animation.Core;

public interface IFromToAnimation : IAnimation
{
    object? CurrentValue { get; internal set; }
    int TotalFrames { get; }
}