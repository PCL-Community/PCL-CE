## v1.0.72-beta

### 新功能
- **自定义下载 API**：实现了 `HttpDlConnection` + `FileDlWriter` + `PclDlService` 下载框架，接入下载界面（任务栏进度、下载去重、错误提示）
- **ModId 去重**：下载前置时按项目 ID 去重，同名 Mod 不同文件名不再重复下载
- **文件名一致性**：前置文件和主文件统一使用相同的命名格式（中文前缀配置一致）

### Bug 修复
- **Issue 模板不显示**：修复 config.yml 自引用链接和 type 字段导致 GitHub 拒绝所有模板
- **下载按钮无响应**：修复从"下载 Minecraft"跳转到实例安装页后按钮无法点击
- **模组浏览器**：修复搜索结果为空时无限加载循环、搜索失败后无法重新搜索、加载动画不正确；结果列表添加淡入动画
- **正版验证重复弹窗**：删除 CE 原版"正版购买提示"弹窗，避免离线/第三方登录时弹出两次
- **Yggdrasil 验证端点错误**：修复 ValidateAsync 把 `/validate` 写成了 `/invalidate`
- **下载界面接入**：`PageInstanceModDetail` 和 `MyCompItem` 中的下载按钮现已接入任务栏进度和下载管理

### 构建
- GitHub Actions 升级到 .NET 10，独立打包（无需运行时环境）
- Publish workflow 支持手动触发
