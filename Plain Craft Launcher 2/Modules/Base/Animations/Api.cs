using System.Collections;
using System.Diagnostics;

namespace PCL;

public static partial class Animations
{
    /// <summary>
    ///     Start an animation group. A group already running under the same name is stopped first, and the
    ///     new one is appended after the existing groups, so it wins whenever both animate the same property.
    /// </summary>
    /// <param name="aniGroup">Animations built by the <c>Aa*</c> helpers; nested lists are flattened.</param>
    /// <param name="name">Name used by <c>AniStop</c> and <c>AniIsRun</c>; empty means a generated unique name.</param>
    /// <param name="refreshTime">Reset the frame clock before starting, so a stalled UI thread does not eat the animation's beginning.</param>
    public static void AniStart(IList aniGroup, string name = "", bool refreshTime = false)
    {
        if (refreshTime)
            _aniLastTick = Stopwatch.GetTimestamp();

        var newEntry = new AniGroupEntry
        {
            data = Common.GetFullList<AniData>(aniGroup),
            startTick = Stopwatch.GetTimestamp()
        };
        if (string.IsNullOrEmpty(name))
            name = newEntry.Uuid.ToString();
        newEntry.Key = name;

        lock (_aniGroupsLock)
        {
            if (_aniByName.TryGetValue(name, out var old))
            {
                old.Dead = true;
                _aniOrderDirty = true;
            }

            _aniByName[name] = newEntry;
            _aniPendingAdds.Add(newEntry); // runs from the next frame on, after older groups
        }
    }

    /// <summary>
    ///     Start an animation group that contains a single animation.
    /// </summary>
    /// <param name="aniGroup">The animation to run.</param>
    /// <param name="name">Name used by <c>AniStop</c> and <c>AniIsRun</c>; empty means a generated unique name.</param>
    /// <param name="refreshTime">Reset the frame clock before starting.</param>
    public static void AniStart(AniData aniGroup, string name = "", bool refreshTime = false)
    {
        AniStart(new List<AniData> { aniGroup }, name, refreshTime);
    }

    /// <summary>
    ///     Stop the group registered under the given name. The group stops on the current frame;
    ///     property values it already applied are not rolled back.
    /// </summary>
    /// <param name="name">Name passed to <c>AniStart</c>. Unknown names are ignored.</param>
    public static void AniStop(string name)
    {
        lock (_aniGroupsLock)
        {
            if (!_aniByName.Remove(name, out var entry))
                return;
            entry.Dead = true;
            _aniOrderDirty = true;
        }
    }

    /// <summary>
    ///     Check whether a group is currently registered under the given name.
    /// </summary>
    /// <param name="name">Name passed to <c>AniStart</c>.</param>
    /// <returns><see langword="true" /> while the group is registered, including frames where it is only waiting on a delay.</returns>
    public static bool AniIsRun(string name)
    {
        lock (_aniGroupsLock)
            return _aniByName.ContainsKey(name);
    }
}
