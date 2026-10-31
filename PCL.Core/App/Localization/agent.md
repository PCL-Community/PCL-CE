# App/Localization 目录

本地化子系统，位于命名空间 `PCL.Core.App.Localization`。负责语言切换、本地化字符串解析与字体回退/裁剪，使 UI 能按用户语言呈现。

## 本目录模块

- **Lang.cs**：语言相关常量与语言枚举标识。
- **LocalizationService.cs**：本地化核心服务（`[LifecycleService]`），加载语言、提供取词与变更通知。
- **LocalizationLanguage.cs**：语言定义（名称、代码、资源引用）。
- **WeakLanguageChanged.cs**：弱引用方式的语言变更通知（避免内存泄漏）。
- **LocalizationFormatConverter.cs**：本地化字符串格式化转换。
- **LocalizationFontProfile.cs / LocalizationFontService.cs**：字体本地化档案与字体服务（按语言选择/裁剪字体子集）。

## 子目录导航

- **Languages**（资源目录，无源码：各语言翻译资源文件，如 `*.xaml`/资源字典，未单独文档化）。
