# UI/Theme 目录

主题子系统，位于命名空间 `PCL.Core.UI.Theme`。管理启动器的配色与主题（支持 LAB 颜色运算、系统主题跟随、色调档案），并暴露主题服务。

## 本目录模块

- **ThemeService.cs**：主题服务（`[LifecycleService]`），应用/切换主题、跟随系统。
- **LabColor.cs**：LAB 颜色模型（用于准确的颜色插值/明暗计算）。
- **CatColorResource.cs**：猫色资源（启动器配色资源键/资源字典管理）。
- **ToneProfile.cs / ToneProfileConfig.cs**：色调档案与配置（自定义配色方案）。
- **SystemThemeHelper.cs**：系统主题辅助（检测 Windows 浅色/深色模式）。
