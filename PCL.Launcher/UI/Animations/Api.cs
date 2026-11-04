using System.Collections;
using System.Diagnostics;
using PCL.Core.Logging;

namespace PCL;

public static partial class Animations
{
    private const int InstantDeltaTick = 1_000_000_000;

    /// <summary>
    ///     Start an animation group. A group already running under the same name is stopped first, and the
    ///     new one is appended after the existing groups, so it wins whenever both animate the same
    ///     property. When <see cref="AniSpeed" /> is 0 the group is applied to its final state
    ///     immediately instead of being animated.
    /// </summary>
    /// <param name="aniGroup">Animations built by the <c>Aa*</c> helpers; nested lists are flattened.</param>
    /// <param name="name">
    ///     Name used by <c>AniStop</c> and <c>AniIsRun</c>; empty means a generated unique
    ///     name.
    /// </param>
    /// <param name="refreshTime">
    ///     Reset the frame clock before starting, so a stalled UI thread does not
    ///     eat the animation's beginning.
    /// </param>
    public static void AniStart(IList aniGroup, string name = "", bool refreshTime = false)
    {
        if (AniSpeed <= 0d)
        {
            var instantData = GetFullList<AniData>(aniGroup);
            RunOnUi(() => _AniRunInstant(instantData));
            return;
        }

        AniStart();

        if (refreshTime)
            _aniLastTick = Stopwatch.GetTimestamp();

        var newEntry = new AniGroupEntry
        {
            data = GetFullList<AniData>(aniGroup),
            startTick = Stopwatch.GetTimestamp()
        };
        if (string.IsNullOrEmpty(name))
            name = newEntry.Uuid.ToString();
        newEntry.Key = name;

        lock (AniGroupsLock)
        {
            if (AniByName.TryGetValue(name, out var old))
            {
                old.Dead = true;
                _aniOrderDirty = true;
            }

            AniByName[name] = newEntry;
            AniPendingAdds.Add(newEntry); // runs from the next frame on, after older groups
        }
    }

    /// <summary>
    ///     Start an animation group that contains a single animation.
    /// </summary>
    /// <param name="aniGroup">The animation to run.</param>
    /// <param name="name">
    ///     Name used by <c>AniStop</c> and <c>AniIsRun</c>; empty means a generated unique
    ///     name.
    /// </param>
    /// <param name="refreshTime">Reset the frame clock before starting.</param>
    public static void AniStart(AniData aniGroup, string name = "", bool refreshTime = false)
    {
        AniStart(
            new List<AniData>
            {
                aniGroup
            },
            name,
            refreshTime);
    }

    /// <summary>
    ///     Stop the group registered under the given name. The group stops on the current frame;
    ///     property values it already applied are not rolled back.
    /// </summary>
    /// <param name="name">Name passed to <c>AniStart</c>. Unknown names are ignored.</param>
    public static void AniStop(string name)
    {
        lock (AniGroupsLock)
        {
            if (!AniByName.Remove(name, out var entry))
                return;
            entry.Dead = true;
            _aniOrderDirty = true;
        }
    }

    /// <summary>
    ///     Check whether a group is currently registered under the given name.
    /// </summary>
    /// <param name="name">Name passed to <c>AniStart</c>.</param>
    /// <returns>
    ///     <see langword="true" /> while the group is registered, including frames where it is only
    ///     waiting on a delay.
    /// </returns>
    public static bool AniIsRun(string name)
    {
        lock (AniGroupsLock)
        {
            return AniByName.ContainsKey(name);
        }
    }

    private static void _AniRunInstant(List<AniData> data)
    {
        if (data.Count == 0) return;

        var entry = new AniGroupEntry
        {
            data = data,
            startTick = Stopwatch.GetTimestamp()
        };

        try
        {
            for (var pass = 0; pass <= data.Count + 1 && data.Count > 0; pass++)
                _AniRunGroup(entry, InstantDeltaTick);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Animation", "瞬时完成动画组失败");
        }
        finally
        {
            entry.Dead = true;
        }
    }

    private static void _AniFinishAllGroupsInstantly()
    {
        var groups = AniOrder;
        foreach (var entry in groups.Where(entry => !entry.Dead))
            try
            {
                for (var pass = 0; pass <= entry.data.Count + 1 && entry.data.Count > 0; pass++)
                    _AniRunGroup(entry, InstantDeltaTick);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Animation", "瞬时完成动画组失败");
                entry.Dead = true;
            }
    }
}