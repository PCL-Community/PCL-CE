# UI/Animation/Core 目录

动画核心框架，位于命名空间 `PCL.Core.UI.Animation.Core`。实现动画的基类、帧、数据、分组、状态机与调度服务，支撑 FromTo/顺序/并行/动作等多种动画形态。

## 本目录模块

- **AnimationBase.cs**：动画基类。
- **IAnimation.cs / IAnimationData.cs / IAnimationFrame.cs**：动画、动画数据、动画帧接口。
- **AnimationData.cs / AnimationFrame.cs**：动画数据与帧实现。
- **AnimationValueType.cs**：动画值类型枚举。
- **AnimationStatus.cs**：动画状态枚举。
- **FromToAnimationBase.cs / IFromToAnimation.cs / FromToAnimationFrame.cs**：FromTo 动画基类、接口与帧。
- **IImplicitAnimation.cs / IKeyFrameAnimation.cs**：隐式动画与关键帧动画接口。
- **ActionAnimation.cs / ActionAnimationFrame.cs / RunAnimation.cs**：动作动画（执行回调）与运行包装。
- **AnimationGroup.cs / SequentialAnimationGroup.cs / ParallelAnimationGroup.cs**：动画组、顺序组与并行组。
- **AnimationExtensions.cs**：动画扩展方法。
- **AnimationService.cs**：动画服务（注册/驱动动画运行）。
