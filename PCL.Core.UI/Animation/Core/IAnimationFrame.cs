namespace PCL.Core.UI.Animation.Core;

public interface IAnimationFrame
{
    void Apply();

    Action GetAction();
}