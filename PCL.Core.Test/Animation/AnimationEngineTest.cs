using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.UI.Animation;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Core;
using PCL.Core.UI.Animation.Easings;

namespace PCL.Core.Test.Animation;

[TestClass]
public sealed class AnimationEngineTest
{
    [TestInitialize]
    public void Initialize()
    {
        AnimationService.RegisterValueProcessorsForTest();
        AnimationService.Speed = 1d;
    }

    [TestCleanup]
    public void Cleanup()
    {
        AnimationService.ResetSpeed();
    }

    [TestMethod]
    public void AbsoluteAnimationEndsAtTarget()
    {
        var owner = new NumberOwner
        {
            Value = 4d
        };
        var target = CreateTarget(owner);
        var animation = new DoubleFromToAnimation
        {
            From = 0d,
            HasFrom = true,
            To = 10d,
            Duration = TimeSpan.FromSeconds(1),
            Easing = new LinearEasing()
        };

        RunFrames(animation, target);

        Assert.AreEqual(10d, owner.Value, 1e-6d);
    }

    [TestMethod]
    public void RelativeAnimationAddsDeltaToCurrentValue()
    {
        var owner = new NumberOwner
        {
            Value = 5d
        };
        var target = CreateTarget(owner);
        var animation = new DoubleFromToAnimation
        {
            To = 7d,
            ValueType = AnimationValueType.Relative,
            Duration = TimeSpan.FromSeconds(1),
            Easing = new LinearEasing()
        };

        RunFrames(animation, target);

        Assert.AreEqual(12d, owner.Value, 1e-6d);
    }

    [TestMethod]
    public void ExplicitFromIsNotOverwritten()
    {
        var owner = new NumberOwner
        {
            Value = 5d
        };
        var target = CreateTarget(owner);
        var animation = new DoubleFromToAnimation
        {
            From = 0d,
            HasFrom = true,
            To = 10d,
            Duration = TimeSpan.FromSeconds(1),
            Easing = new LinearEasing()
        };

        animation.PrepareForTest(target);
        animation.CurrentFrame = 0;
        var frame = animation.ComputeNextFrame(target);

        Assert.IsNotNull(frame);
        frame.GetAction()();
        Assert.AreEqual(0d, owner.Value, 1e-6d);
    }

    [TestMethod]
    public void SpeedZeroAppliesFinalValueImmediately()
    {
        AnimationService.Speed = 0d;
        var owner = new NumberOwner
        {
            Value = 5d
        };
        var target = CreateTarget(owner);
        var animation = new DoubleFromToAnimation
        {
            To = 7d,
            ValueType = AnimationValueType.Relative,
            Duration = TimeSpan.FromSeconds(1),
            Easing = new LinearEasing()
        };

        var result = animation.RunFireAndForget(target);

        Assert.AreEqual(12d, owner.Value, 1e-6d);
        Assert.AreEqual(AnimationStatus.Completed, result.Status);
    }

    [TestMethod]
    public void SpeedMappingMatchesLegacyCurve()
    {
        Assert.AreEqual(0d, AnimationService.MapSpeedValue(0));
        Assert.AreEqual(1d, AnimationService.MapSpeedValue(9), 1e-6d);
        Assert.AreEqual(3d, AnimationService.MapSpeedValue(29), 1e-6d);
        Assert.AreEqual(200d, AnimationService.MapSpeedValue(30), 1e-6d);
    }

    private static ClrAnimatable<NumberOwner, double> CreateTarget(NumberOwner owner)
    {
        return new ClrAnimatable<NumberOwner, double>(
            owner,
            static value => value.Value,
            static (value, next) => value.Value = next);
    }

    private static void RunFrames(
        FromToAnimationBase<double> animation,
        IAnimatable target)
    {
        animation.PrepareForTest(target);
        for (var frameIndex = 0; frameIndex < animation.TotalFrames; frameIndex++)
        {
            animation.CurrentFrame = frameIndex;
            var frame = animation.ComputeNextFrame(target);
            Assert.IsNotNull(frame);
            frame.GetAction()();
        }
    }

    [TestMethod]
    public void SuspensionScopeIsReentrant()
    {
        Assert.IsFalse(AnimationService.IsSuspended);
        using (AnimationService.SuspendControl())
        {
            Assert.IsTrue(AnimationService.IsSuspended);
            using (AnimationService.SuspendControl())
            {
                Assert.IsTrue(AnimationService.IsSuspended);
            }

            Assert.IsTrue(AnimationService.IsSuspended);
        }

        Assert.IsFalse(AnimationService.IsSuspended);
    }

    [TestMethod]
    public void ActionAnimationRunsImmediatelyWhenDisabled()
    {
        AnimationService.Speed = 0d;
        var called = 0;
        var animation = new ActionAnimation(() => called++);

        var result = animation.RunFireAndForget(EmptyAnimatable.Instance);

        Assert.AreEqual(1, called);
        Assert.AreEqual(AnimationStatus.Completed, result.Status);
    }

    [TestMethod]
    public void LegacyFluentEasingKeepsEndpointAndPowerCurve()
    {
        var ease = new FluentEaseOut(EasePower.Weak);

        Assert.AreEqual(0d, ease.Ease(0d), 1e-6d);
        Assert.AreEqual(1d, ease.Ease(1d), 1e-6d);
        Assert.AreEqual(0.75d, ease.Ease(0.5d), 1e-6d);
    }

    [TestMethod]
    public void AnimationGroupPreservesAfterOrderingWhenDisabled()
    {
        AnimationService.Speed = 0d;
        var calls = new List<int>();

        AnimationService.Run(
            new List<IAnimation>
            {
                new ActionAnimation(() => calls.Add(1)),
                new ActionAnimation(() => calls.Add(2)),
                new ActionAnimation(() => calls.Add(3), true)
            },
            "test-order");

        Assert.AreSequenceEqual(
            new[]
            {
                1,
                2,
                3
            },
            calls);
    }

    [TestMethod]
    public void ElasticEaseOutMatchesLegacyCurve()
    {
        var ease = new ElasticEaseOut(EasePower.Weak);

        Assert.AreEqual(0d, ease.Ease(0d), 1e-6d);
        Assert.AreEqual(0.6122377311846043d, ease.Ease(0.25d), 1e-6d);
        Assert.AreEqual(1.3927820463649314d, ease.Ease(0.5d), 1e-6d);
        Assert.AreEqual(0.9329678868688169d, ease.Ease(0.75d), 1e-6d);
        Assert.AreEqual(1d, ease.Ease(1d), 1e-6d);
    }

    [TestMethod]
    public void ScaleTransformAnimationResetsOriginToCenter()
    {
        var border = new Border
        {
            RenderTransformOrigin = RelativePoint.TopLeft,
            RenderTransform = new ScaleTransform(1d, 1d)
        };

        _ = new ScaleTransformAnimation(border, -0.1d, 1000);

        Assert.AreEqual(RelativePoint.Center, border.RenderTransformOrigin);
    }

    [TestMethod]
    public void CallbackAnimationReceivesFrameDeltas()
    {
        var deltas = new List<double>();
        var animation = new DoubleAnimation(delta => deltas.Add(delta), -10d, 1000);
        var target = animation.Target!;

        animation.PrepareForTest(target);
        for (var frameIndex = 0; frameIndex < animation.TotalFrames; frameIndex++)
        {
            animation.CurrentFrame = frameIndex;
            var frame = animation.ComputeNextFrame(target);
            Assert.IsNotNull(frame);
            frame.GetAction()();
        }

        Assert.AreEqual(-10d, deltas.Sum(), 1e-6d);
        Assert.IsTrue(deltas.All(delta => delta <= 1e-9d));
    }

    [TestMethod]
    public void ParallelRelativeAnimationsCompose()
    {
        var owner = new NumberOwner();
        var target = CreateTarget(owner);
        var first = new DoubleFromToAnimation
        {
            To = 5d,
            ValueType = AnimationValueType.Relative,
            Duration = TimeSpan.FromSeconds(1d),
            Easing = new LinearEasing()
        };
        var second = new DoubleFromToAnimation
        {
            To = 11d,
            ValueType = AnimationValueType.Relative,
            Duration = TimeSpan.FromSeconds(1d),
            Easing = new LinearEasing()
        };

        first.PrepareForTest(target);
        second.PrepareForTest(target);

        for (var frameIndex = 0; frameIndex < first.TotalFrames; frameIndex++)
        {
            first.CurrentFrame = frameIndex;
            first.ComputeNextFrame(target)!.GetAction()();

            second.CurrentFrame = frameIndex;
            second.ComputeNextFrame(target)!.GetAction()();
        }

        Assert.AreEqual(16d, owner.Value, 1e-6d);
    }

    [TestMethod]
    public void TranslateYAnimationsCompose()
    {
        var border = new Border
        {
            RenderTransform = new TranslateTransform(0d, -16d)
        };
        var first = new TranslateYAnimation(border, 5d, 1000);
        var second = new TranslateYAnimation(border, 11d, 1000);

        first.PrepareForTest(first.Target!);
        second.PrepareForTest(second.Target!);

        for (var frameIndex = 0; frameIndex < first.TotalFrames; frameIndex++)
        {
            first.CurrentFrame = frameIndex;
            first.ComputeNextFrame(first.Target!)!.GetAction()();

            second.CurrentFrame = frameIndex;
            second.ComputeNextFrame(second.Target!)!.GetAction()();
        }

        var transform = (TranslateTransform)border.RenderTransform!;
        Assert.AreEqual(0d, transform.Y, 1e-6d);
    }

    [TestMethod]
    public void ClockSampleUsesElapsedTimeInsteadOfFrameSteps()
    {
        var fresh = new NumberOwner
        {
            Value = 5d
        };
        var freshTarget = CreateTarget(fresh);
        var started = new DoubleFromToAnimation
        {
            To = 7d,
            ValueType = AnimationValueType.Relative,
            Duration = TimeSpan.FromMilliseconds(200),
            Easing = new LinearEasing()
        };
        started.PrepareForTest(freshTarget);
        started.ArmClock(Stopwatch.GetTimestamp());
        started.ComputeNextFrame(freshTarget)!.GetAction()();
        Assert.IsFalse(started.IsClockFinished);
        Assert.AreEqual(5d, fresh.Value, 0.05d);

        var finishedOwner = new NumberOwner
        {
            Value = 5d
        };
        var finishedTarget = CreateTarget(finishedOwner);
        var finished = new DoubleFromToAnimation
        {
            To = 7d,
            ValueType = AnimationValueType.Relative,
            Duration = TimeSpan.FromMilliseconds(200),
            Easing = new LinearEasing()
        };
        finished.PrepareForTest(finishedTarget);
        finished.ArmClock(Stopwatch.GetTimestamp() - Stopwatch.Frequency / 4);
        finished.ComputeNextFrame(finishedTarget)!.GetAction()();
        Assert.IsTrue(finished.IsClockFinished);
        Assert.AreEqual(12d, finishedOwner.Value, 1e-6d);
    }

    private sealed class NumberOwner
    {
        public double Value { get; set; }
    }
}