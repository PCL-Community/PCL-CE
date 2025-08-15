## [unreleased]

### 🚀 新功能

- *ShaderPack*: 可单独选择光影加载器 (#1311)
- *page-instance-setup*: 自动将服务器地址替换为半角冒号 (#1299)
- *link*: 更换 QA 页面 (#1325)
- *link*: 允许在有多个 ET 实例的情况下与正确的 ET 实例通讯 (#1328)
- *identify*: 将识别码移入 Core 中
- *link*: 更新 EasyTier 到 2.4.2 (#1342)
- *modBase-hash*: 哈希计算使用 Core 内提供的方法

### 🐛 问题修复

- *Instance*: 修复官方启动器文件夹类型判断错误 (#1307)
- 修复 (Legacy) Fabric API 的选择框下端间隔不正常 (#1315)
- *link*: 连接到陶瓦用户的大厅时部分文本显示异常 (#1308)
- *text*: 忘记改上游版本号了
- *ms-login*: 可能会误认为 AccessToken 为 null 的用户为已登录用户

### 🚜 重构

- 再次重新规划分类与命名空间
- *Setup*: 迁移配置系统 (#1313)
- *setup*: 迁移 SystemSystemUpdateBranch (#1349)

### ⚡ 性能优化

- *SavesInfo*: 加入存档信息缓存 (#1268)

### ⚙️ 其他小改动

- *Webp*: 提供 Decoder 静态实例，减少重复实例化加载负担
- 完善带有 Async 修饰的异步逻辑
- 修改 Mirror 酱介绍文本，减少不熟悉的用户对此的误解的概率
- 更新解决方案的配置引用


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.1 更新一览(2025-08-07)

### 🚀 新功能

- *link*: 提高连接的等待时间
- *mc-server*: 独立的 Minecraft 服务器查询控件 (#1274)
- *select-left*: 添加文件夹列表排序 (#1251)

### 🐛 问题修复

- *link*: 可能因为一些信息未能获取到错误地判定大厅不存在 (#1296)
- *link*: 输出重定向导致的 ET 在部分情况下卡住 (#1304)
- Typo of `mcimirrmr` (#1292)
- *link*: 为 web 服务端初始化添加日志和异常处理 (#1290)
- *launch*: 意外提示登录失败

### 🎨 部分样式调整

- 一个巨大的 typo 修复 (#1293)
- PageOtherFeedback.xaml.vb下部分符号错误问题 (#1302)


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0 更新一览(2025-08-06)

### 🚀 新功能

- *McPing*: 支持检测旧版本 Minecraft 的世界信息

### 🐛 问题修复

- *Profile*: 无法正常添加微软账号
- *FormMain*: 二次修复更新公告显示问题 (#1252)
- *comp*: 从世界下载来源选项中移除 Modrinth (#1259)
- *skin*: 在皮肤站可能没有 SSL 支持的情况下错误使用使用 HTTPS 协议下载皮肤文件
- *Java*: 启用禁用的合法性问题 & typo 修正 & Java 手动添加限定 java.exe
- Windows 的窗口排列机制可能会导致锁定窗口大小失效
- *Download*: 错误地将官方版本的 Json 文件进行读取并且没有读取 UVMC 文件
- *ModSecret*: 修复 ContextMenu 不会正常响应主题刷新 (#1271)
- *ModComp*: 修复识别剪贴板资源功能不起效 (#1269)
- *Link*: 是奇怪的 ET 进程，直接干掉

### 🚜 重构

- 重新规划 Core 分类与命名空间

### ⚡ 性能优化

- *Setup*: 优化字体下拉框的加载速度

### ⚙️ 其他小改动

- *Feedback*: 移除早期添加的备用 api
- *build*: 为 Core 添加 CI 构建配置并将 Release 与 Beta 合并为 Publish
- 调整部分 Core 文件结构
- *Link*: 启动 MC 使用的启动器展示小改动
- *Launch*: 移除 msal 组件

### ◀️ 回退改动

- *Auth*: 回滚 MSAL (#1281)


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.15 更新一览(2025-08-02)

### 🐛 问题修复

- 更新日志以错误的弹窗展示


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.14 更新一览(2025-08-02)

### 🚀 新功能

- *Link*: 用户输入错误的房间号时给予友好提示
- *Net*: 将 CacheCow 缓存应用于部分网络请求
- *Export*: 支持导出光影配置文件
- *LocalComp*: 自动识别资源包图标 (#1204)
- *Theme*: 支持自定义灰阶配置 (#1193)
- *Account*: 账户系统
- *Link*: 支持加入陶瓦的联机房间 (#1182)
- *PageOtherTest*: 加入PCL游戏启动次数显示 (#1224)
- *Comp*: 支持从 CurseForge 下载世界并自动安装 (#1185)
- *PageInstance*: 实例启动次数统计和查看 (#1239)
- *Link*: 更新 EasyTier 到 2.4.0 并支持 ACL (#1225)
- *Comp*: 收藏夹加入世界资源分类 (#1245)
- *MyCheckBox*: 复选框的三态 (#1246)
- *Link*: 更新 EasyTier 至 2.4.1 (#1247)

### 🐛 问题修复

- *Sort*: 错误地执行了刷新整个 UI 导致卡顿
- *CompResource*: 修复文件夹排序和打开逻辑 (#1170)
- *CompMod*: 模组启动和禁用会导致模组列表项目缺失内容
- *McPing*: 遇到不回复的端口会无限制等待的问题
- *smtc*: 无法正确传递媒体信息
- *Link*: Natayark 账户登录可能由于回调服务端没有正确启动而失败
- *Link*: 快速从联机页面点入设置会导致 Natayark 账号登录失效
- *Link*: 输入错误的房间号时不应该进入大厅界面
- *Link*: 可能找不到局域网广播信息
- *Link*: 直接与 Minecraft 通信而不是局域网广播
- *Profile*: 再次尝试解决正版登录可能的弹窗问题
- *PageOtherTest*: 瞅眼服务器的默认图标没有展示
- *CompResource*: 修复启动器全选收藏和分享所选Bug (#1200)
- *Theme*: 修复窗体加载初期的 MyButton 空指针异常 (#1209)
- *SavesInfo*: 修复1.4版本以下存档信息获取出错 (#1210)
- *Crash*: 修复错误报告导出中启动器日志命名不正确的问题 (#1178)
- *CompResource*: 修复模组禁用/启用列表项目缺失 (#1215)
- *ModProfile*: 修复在启动游戏前切换档案会导致档案没切过来 (#1208)
- *Link*: 使用 Process.WaitForExit 而不是 Thread.Sleep 等待输出结果
- *Launch*: 游戏参数没有被正确替换（未同步上游更新）
- *Launch*: Build error
- *uvmc*: 修复错误的下载地址 (#1226)
- *CompResource*: 修复下载页面搜索框有几率不显示 (#1234)
- *PageInstance*: 修复实例选择按钮点击后页面跳转顺序问题 (#1217)
- *CompResource*: 无法进行一些批量操作
- *Launch*: 游戏启动参数传递错误
- *Launch*: 启动计数统计可能会有 null 异常
- *Link*: 登录失败会报错
- 硬件调查弹窗异常弹出
- *Profile*: 档案页面意外的切换回原来选择的档案
- *Profile*: 加载离线皮肤时的 UUID 错误
- *Profile*: 切换档案失败

### 🚜 重构

- *Net*: 换用自实现的 HTTP 缓存存储，同时加入缓存压缩减少空间占用 (#1241)

### ⚡ 性能优化

- *CompResource*: 优化模组排序性能 (#1228)
- *CompResource*: 对模组文件信息缓存的更多应用 (#1237)
- *CompResource*: RawPath是对的，使用HashSet代替List (#1240)

### ⚙️ 其他小改动

- *Profile*: 加入更多的标志以方便错误跟踪
- 等待更新日志的工作流完成后再发起发版更新
- *Java*: 代码样式小改动


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.12 更新一览(2025-07-24)

### 🚀 新功能

- *SnapLite*: 允许查看备份详情
- *Launcher*: 上游更新 (#1168)

### 🐛 问题修复

- 自定义主页无法调用今日人品 (#1164)
- *Link*: 修改信息传递逻辑，换用分隔符分离信息 (#1169)
- *CompFavorites*: 修复收藏夹删除最后一个收藏项后 UI 显示的问题 (#1171)
- *Link*: 意外允许旧版本使用大厅


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.11 更新一览(2025-07-23)

### 🐛 问题修复

- *Profile*: 没有输出错误信息
- *Vote*: 投票页面加载错误
- *Modpack*: 在安装整合包时可能会出现类型装换错误
- *Page*: 减少发现 null 值时候的日志输出量
- *Modpack-Install*: 安装整合包时会报错 (#1150)
- *LocalComp*: 游戏资源管理相关优化 (#1152)

### 🎨 部分样式调整

- *update*: 将 String.Concat 替换为字符串模板

### ⚙️ 其他小改动

- *MsLogin*: 删改一些不必要的代码


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.10 更新一览(2025-07-22)

### 🚀 新功能

- *log*: 添加导出日志页面
- *update*: 使用核心库更新服务代替原文件替换过程
- *CustomDownload*: 为自定义下载添加 User-Agent 支持#1041 (#1058)
- *LifeCycle*: 加入更多的可观察日志
- *CompMod*: 接入 MCIM 文件下载镜像源
- *launch*: 支持切换使用 java.exe 而不是 javaw.exe 启动游戏 (#1115)
- *PageVersionCompResource*: 原理图管理相关优化
- *PageVersion*: 添加.litematic详细信息中原始名称显示
- *savesinfo*: 优化旧版本提示 (#1081)
- Legacy Fabric 支持 (#1118)
- *Download*: 更新未列版本镜像源地址 (#1137)
- *Test*: 添加创建快捷方式按钮 (#1132)
- *PageOtherTest*: 允许下载 UNC 文件 (#1123)

### 🐛 问题修复

- *JLW*: 部分地方没有默认不使用 JLW
- 主页市场滚动条偏左 (#1059)
- *Mod*: 项目结构
- *Link*: 存在重复的刷新
- *log*: 修正工程文件
- *MsLogin*: 尝试性修复正版验证第五步错误
- *log*: 修复导出日志无法匹配 LastPending
- *Profile*: 可能的导出导入失败
- *UI*: 点击导出页面的刷新按钮会跳转到修改页面
- *CompMod*: 存在同名的模组文件时搜索会发生崩溃
- *Profile*: 微软账户登录的一些小问题
- 披风预览的一些问题
- *PageVersionCompResource*: 优化文件夹操作提示和空状态显示
- *modpack*: 可能会因为镜像源获取到的模组信息不够直接爆炸而不重试
- *Profile*: 修改玩家名输入名称界面点取消后仍然弹出警告
- *Link*: 大厅功能优化与修复 (#1068)
- *Link*: 修复局域网广播可能找不到的问题 (#1144)

### 🚜 重构

- *McPing*: 重构 McPing 进 PCL.Core
- 优化代码结构，提高可读性
- *about*: 重新排列关于页面并添加社区版相关信息 (#1119)
- *rpc*: 替换过时的工具调用 (#1146)

### ⚡ 性能优化

- *link*: 去除 --smol-tcp 参数
- *MsLogin*: 优化 MsLoginStep1 阶段的代码
- *Net*: 网络代码小优化
- *UI*: 版本 -> 实例 / version -> instance (#1141)

### ⚙️ 其他小改动

- Upd core
- Upd core
- *log*: 解决冲突
- Add tracking branch to submodule
- *MsLogin*: 修改验证字段的参数使用 JObject 构造
- *submodule*: Update to 9191dd00802be014f7b04a2d3c1a2009281d8fc1
- *submodule*: Update to 7b8392bd46482568ec7c1214d0afcd4eace31034
- *submodule*: Update to 14b5eab84664ea8922b20ca7e53dbc80e7b8091e
- *submodule*: Update to 97bbf5e309fa4f14e6f3b6a8a6471f52bb218e35
- *submodule*: Update to 40bc03587fd744f5525a0e8b920e3d172c95ecea
- *submodule*: Update to b35abf9994089b1c5632212664906b984daccf57
- *submodule*: Update to d1028c825230d5457c12820c912972b173ed9a10
- 向 changelog 添加 imp 和 chore 类型
- 去除 changelog 配置结尾逗号
- *McPing*: 移除旧 McPing
- *Profile*: 默认登录使用设备代码流以减少交互式登录带来的各种问题
- *NuGet*: 升级 msal 到 4.74.0
- *Net*: 指定 SslProtocols


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.9 更新一览(2025-07-09)

### 🐛 问题修复

- *link*: 修复联机设置自定义节点输入字符崩溃的问题  (#1052)

### ⚡ 性能优化

- *Link*: UI 界面


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.8 更新一览(2025-07-08)

### 🐛 问题修复

- *link*: 传入的大厅编号不正确

### ⚙️ 其他小改动

- *Link*: 改动 UI


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.7 更新一览(2025-07-08)

### 🚀 新功能

- *Link*: 改进联机安全性 (#1042)
- *Notification*: 支持简单的 Toast 通知 (#1043)

### 🐛 问题修复

- *MCPing*: 日志写入没有按照预期进行
- *link*: 创建者网络密钥不正确

### ⚡ 性能优化

- *Link*: UI 小改动

### ◀️ 回退改动

- *NetCache*: 由于稳定性因素，暂时移除 CacheCow


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.6 更新一览(2025-07-07)

### 🚀 新功能

- *setup*: 独立的主页预设/市场页面 (#998)

### 🐛 问题修复

- *Link*: 压根没连上啊


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.5 更新一览(2025-07-07)

### 🐛 问题修复

- *link*: 我服了 Action 居然不能处理引号和逗号 (#1028)
- *favorites*: 修复收藏夹修改备注时点击取消将丢失原来的备注信息的问题  (#1027)
- *ci*: 可能的密钥替换错误
- *ci*: 无法替换密钥
- *ci*: 这次密钥真的应用成功了
- *Link*: 检测 Minecraft 可能会被过长时间地卡住
- *authlib*: 去除请求体中包含的错误的参数，并在部分弹窗上加入详细的错误信息 (#1030)
- *Update*: 可能意外地在 UI 线程内获取信息 (#1029)

### ⚡ 性能优化

- *NetCache*: 使用单次创建的实例化避免多次实例化造成的性能问题


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.4 更新一览(2025-07-07)

### 🚀 新功能

- *link*: 联机支持第二服务器与其他优化和修复 (#1020)

### 🐛 问题修复

- *Cape*: 下载披风文件失败导致无法换披风
- *Cape*: 使用了错误的图片
- *Link*: 检测不到多个游戏端口
- *NetCache*: 使用数据库会导致部分情况下数据量过大导致数据库爆炸


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.3 更新一览(2025-07-06)

### 🚀 新功能

- *promote*: 支持运行时提权操作
- *promote*: 添加预定义函数支持
- *SnapLite*: 部分内容改为仅修改而不是重新写入
- *UI*: 改回部分 UI
- 投影原理图管理相关优化，解决加载过慢的问题 (#1000)
- *JLW*: 不默认启用 (#1013)
- 支持锁定窗口大小 (#1014)
- *NetCache*: 网络缓存 (#1015)
- *launch*: 支持分析 mod loader 版本问题导致的崩溃并提示用户修改版本 (#1012)

### 🐛 问题修复

- MMC 整合包 Libraries 应用可能出错
- *NetCache*: 错误的索引查找
- *NetCache*: 错误的索引
- 搜索游戏版本时的 Card 标题不正确 (#1018)
- 大厅相关修复 (#1010)
- *NetCache*: 一些使用问题

### 📚 文档修改

- 将贡献说明指向 wiki 页面 (#1016)

### ⚙️ 其他小改动

- Upd core


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.2 更新一览(2025-07-04)

### 🐛 问题修复

- *link*: 常量少打个 Const
- *Bsdiff*: 修复差分失败问题


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.12.0-beta.1 更新一览(2025-07-04)

### 🚀 新功能

- 可选的各种蓝色 (#821)
- *RP*: 大概思路
- 写了一堆💩，先放着吧
- *link*: 大厅早期支持 (#741)

### 🐛 问题修复

- *Java*: 可能会错误的选择低版本 Java 安装 Forge
- *Profile*: 修改皮肤前不会登录
- *LabyMod*: 可能会错误的检测资源情况
- 规避 WPF 剪贴板卡顿，修复剪切不清除文本的问题 (#997)
- *SnapLite*: 改成先删后增应用备份，以防止部分情况下文件没有写入到位
- *Java*: 剔除过于宽泛的关键词

### 🚜 重构

- *MCPing*: 换用 Core 实现的更加完善的 VarInt

### ⚙️ 其他小改动

- 依赖修改
- Upd core
- Upd core
- *link*: Clean up (#1001)


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.9 更新一览(2025-07-02)

### 🚀 新功能

- *Init*: 删除旧的日志文件
- *SnapLite*: 允许清理无用文件
- *Saves*: 展示"是否允许作弊"和"游戏时长" (#973)
- 原理图管理支持显示子文件夹 (#971)
- *log*: 细化日志输出
- 投影文件管理优化 (#979)
- *ID*: 修改识别码算法
- 投影原理图管理子文件夹的相关优化 (#991)
- *Update*: 支持增量更新
- *Update*: 应用更新之前校验 SHA256
- *Profile*: 尝试性提醒用户创建档案啥的
- *Profile*: 允许显示披风预览
- *Markdown*: 初步实现 Markdown (#982)
- *Markdown*: 更多窗口支持展示 Markdown

### 🐛 问题修复

- *Tip*: 可能会错误的提醒启动路径问题从而造成一些困扰
- *Tip*: 检查 UTF8 而不是是不是 GBK
- *Java*: 尽可能区分 OpenJDK 与 OracleJDK
- *SnapLite*: 这里不能用异步，不会等待……
- *ModLaunch*: 修复检查存档快捷启动失败的问题 (#975)
- *Saves*: 左侧栏选中项目不正确
- *SavesInfo*: 1.8以下的版本没有difficulty导致报错 (#977)
- *Comp*: 原理图在进入新文件夹后切换版本查看原理图会发生展示内容错误
- *Update*: 更新通道选择错误
- *Update*: Mirror 酱更新修复
- *Profile*: 皮肤修改出错的问题
- *Java*: Java 初始化异步进行以防止线程堵塞
- *Version*: 部分版本分类错误
- *Comp*: 错误的文件路径 (#992)

### ⚡ 性能优化

- *UI*: 小调整存档详情页面 UI

### ⚙️ 其他小改动

- *Profile*: 小改 UI
- *Saves*: 优化左栏按钮
- *Hash*: 提供可直接使用的 Instance 属性


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.9-beta.1 更新一览(2025-06-28)

### 🚀 新功能

- *lifecycle*: 适配生命周期管理
- *profile*: 自适应档案选择视图大小 (#947)
- *LocalModItem*: 在 Mod 列表中显示模组自带的方形图标 (#885)
- 添加投影原理图支持并优化组件管理功能 (#959)
- *modpack*: MMC JSON-Patches 支持 (#841)
- *World*: 存档版本识别功能 (#929)
- *tools*: 瞅眼服务器支持展示默认图标
- *SnapLite*: 支持存档备份 (#945)
- *SavesInfo*: 支持简单查看存档信息

### 🐛 问题修复

- *blur*: 意外的高级材质 (#934)
- *runtime*: 部分用户可能运行时正确但是版本号就是不对
- *logger*: 错误信息太少
- *java*: 无法确定版本的 Java 会添加失败
- *comp*: 在没有游戏版本选中时下载资源会报错
- *nuget*: System.Memory 软件包版本不一致
- *Java*: Oracle 可能被判断为 OpenJDK
- *Java*: Oracle 可能被判断为 OpenJDK
- *Profile*: 窗口较小时档案切换区域显示不完整 * 2
- *lifecycle*: 无缓存时构建失败
- *lifecycle*: 结束程序不生效
- *install*: 修改 Fabric 版本失败 (#904)
- 反复切换页面后 issue 列表出现重复项目 (#915)
- 初始化第一阶段日志丢失
- *lifecycle*: 缓冲区日志未清空导致重复写入
- *logger*: 可能存在的资源访问冲突
- *logger*: 高负载下文件名可能重复
- *url*: 替换官方仓库 URL
- *launch*: 无法启动新安装的 LabyMod (#952)
- 在提交反馈时仍然提示是上传PCL/Log-CE1~5.txt
- *SnapLite*: 无法正确备份

### 🚜 重构

- *log*: 适配生命周期

### 📚 文档修改

- *README.md*: Pull Requests 图标指向的链接有误
- *README.md*: 删除更新日志

### ⚡ 性能优化

- *ui*: 修改资源下载中的下载文本的展示宽度逻辑
- *blur*: 加入更多的渲染限制参数
- *logger*: 修改日志默认文件名以方便获取对应日志
- 今日人品优化 (#923)
- *CompUI*: 优化版本管理左侧 UI

### ⚙️ 其他小改动

- *byte*: 加入一个小工具
- *README.md*: 更新官方库链接
- *issue*: 替换掉 Issue 模板中旧的官方组织名 (#940)
- 将 WebServer 移至 Utils 命名空间
- 使用 msbuild 替代 nuget 还原包
- 子模块更新触发 BuildTest
- 移除并行请求登录微软账户
- *dep*: 迁移 Test 项目到 PackageReference 包管理格式


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.8 更新一览(2025-06-21)

### 🚀 新功能

- 设置项
- 响应 BlurChanged
- 高级材质初步完成
- 关于列表添加
- *Id*: 完成用户配置与身份相关的 Core 迁移
- 游戏版本搜索 (#875)
- 把“反馈”页面的issue列表来源改为社区版 (#889)
- *tip*: 增加游戏路径检查提示

### 🐛 问题修复

- *ci*: 不应该运行两次 changlog 制作
- *Logger*: 修复高占用问题
- GetDarkThemeLight
- BlurChanged
- MyCompItem
- *Crash*: 没有导出最新的日志
- 初次打开会默认高级材质 (#901)
- 信息框背景色叠加
- *theme*: 正版登录提示框阴影颜色有误 (#910)

### 🚜 重构

- *db*: 换用 LiteDB 代替 SQLite (#908)
- *rpc*: 主要实现移至 Core (#886)

### ⚡ 性能优化

- *Logger*: 优化 Dispose
- *Logger*: 优化日志写入
- *blur*: 模糊作用地调整与类型纠正
- *Configure*: 优化配置文件的获取和写入

### ⚙️ 其他小改动

- 移动位置
- *blur*: Debug.WriteLine 输出卡死我了
- *issue*: 修改日志文件提交描述


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.7 更新一览(2025-06-20)

### 🚀 新功能

- 添加今日人品
- *ram*: 给游戏分配的内存大于已安装内存的 3/4 时给予提示
- 为修改正版玩家名添加警告 (#882)
- *Logger*: 补上一些日志
- *ci*: 支持自动化 changlog (#891)
- *ci*: 发版文件的结尾内容

### 🐛 问题修复

- *java*: Typo
- *Java*: 排除有非法字符的路径
- *Java*: 排除有非法字符的路径
- *ModPackInstall*: 错误的变量类型转换
- *ModPackInstall*: 可能获取不到正确的下载地址或者获取的太慢
- *Java*: 排除特殊路径下的 Java
- 关闭 Minecraft 无效 (#830)
- *Java*: Java 搜索路径排除逻辑错误 (#871)
- *Http*: 修复重定向问题 (#878)
- *ci*: 修复可能的帮助库获取错误 (#870)
- *auto-install*: 版本修改相关修复 (#842)

### 🚜 重构

- *Log*: 新日志系统 (#887)

### 📚 文档修改

- *README*: Fix Invalid Link

### ⚡ 性能优化

- 为 HTTP 请求扔出来的异常添加 StatusCode 和 ReasonPhrase 属性
- 为 HttpRequestFailedException 添加引发该异常的 Response 属性

### ⚙️ 其他小改动

- Upd core
- *Redirect*: 修改最长超时限制
- Upd core
- *translate*: 为暂时没有译文的情况给予正确提示 (#866)
- Upd core


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.6 更新一览(2025-06-17)

### 🚀 新功能

- *Java*: Java 选择不再直接尝试搜索，避免选择耗时过长
- 档案选择滚动条 (#816)
- 更改瞅眼服务器的背景 (#836)
- *Test*: Add configure test
- *Test*: Modrinth
- *BinaryDiff*: BSDiff Apply 部分

### 🐛 问题修复

- Typo
- *Skin*: 加载头像时可能发生错误
- *ModMain*: 重复的帮助文件
- 设置界面会莫名其妙卡顿一下
- *Java*: 版本选择范围问题
- *LibFix*: 错误的路径传入
- Java 搜索在部分地方未执行
- *ci*: Nuget 包还原失败
- *Test*: Java Test 逻辑有误
- 长于当前收藏夹的选项名显示不完整 (#815)
- *JavaSelect*: 如果用户仍旧想要选择此 Java 那么就依他吧……
- *CompSort*: 排序顺序不正确
- 深色模式对话框阴影颜色有误 (#822)
- *Test*: 修复由于版本过低导致的测试无法执行
- *Java*: 可能会因为路径结尾的 \ 字符导致判断去重失败
- *BsDiff*: 修复 BsDiff 的 Apply 读取不正确的问题
- *Launch*: 启动参数位置有误
- *Net*: 不会处理 Curseforge 的重定向地址

### ⚡ 性能优化

- *CompSaveSelect*: 优化保存自动定位逻辑
- *Net*: 小优化 Http 部分

### 🎨 部分样式调整

- *Java*: 变量名称

### ⚙️ 其他小改动

- 导入 Tasks 命名空间
- Upd core
- 升级依赖
- Upd core
- 降低日志等级 (#825)
- *Test*: 修改测试


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.5 更新一览(2025-06-11)

### 🚀 新功能

- *PipeRPC*: 支持基于管道通信的 RPC
- *PipeRPC*: 返回日志信息
- *PipeRPC*: 处理日志请求
- *realtime-log*: 添加实时日志保留行数设置 (#752)

### 🐛 问题修复

- *JavaSelect*: 没有考虑传值为 null 的情况
- Java 选择写的太💩
- *PipeRPC*: 输出流在客户端读取到实际数据之前被关闭
- Msalruntime 释放错误
- 设置Java环境变量后，Java管理列表显示重复 PCL-Community/PCL2-CE#807
- 可能因为输入地址无效导致登陆失败 (#805)

### ⚡ 性能优化

- *LogUI*: 调整布局

### ⚙️ 其他小改动

- 使用 sln target 代替 vbproj 修复多项目构建问题
- 调整Mirror酱source字段 (#803)


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.3 更新一览(2025-06-10)

### 🐛 问题修复

- Java 选择异常
- *Profile*: 头像刷新失败
- 拖拽安装 Mod 报错
- 拖拽安装 Mod 报错
- 错误的发版名称


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.3-beta.2 更新一览(2025-06-10)

### 🐛 问题修复

- 版本号没改导致 Mirror 酱的 FR 用户会不停地更新(x


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.3-beta.1 更新一览(2025-06-10)

### 🚀 新功能

- 使用 SQLite 替换模组文本数据库 (#768)
- *screenshot*: 点击图像预览区域时使用默认方式打开 (#789)
- *Test*: Java SemVer
- *ci*: 测试构建工作流切换到 Debug 版本以输出更多的报错信息

### 🐛 问题修复

- 解压地址错误
- 判断路径错误
- Http 超时字段优化
- #365
- 1.16.5 离线多人
- 没有与官方版本帮助库隔离导致官方版加载部分帮助库时出错
- Java 8 可能是 1.8 也可能是 8.0
- Java 选择问题
- LabyMod 的一些启动问题
- *ci*: 降级发版脚本的版本

### 🚜 重构

- C# VB.NET 混合开发 & Java 选择页面重构 (#740)
- *profile*: 重构正版玩家名修改逻辑 (#794)

### 📚 文档修改

- 加入开发群群号

### ⚡ 性能优化

- 优化更新体验

### 🎨 部分样式调整

- 简化表达式

### ⚙️ 其他小改动

- *issue_template*: Add some check items
- 压缩错文件了
- Mod 请求过多倾向官方源导致部分时候加载缓慢
- 漏了一处更改
- 换用 http git
- Fix typo
- 尝试清除缓存构建
- 修改翻译接口地址 (#796)
- 修改 Mirror 酱跳转链接
- VersionStandardCode 没改


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.2-beta.3 更新一览(2025-06-03)

### 🐛 问题修复

- 修复瞅眼服务器解析可能产生的异常 (#763)

### ⚡ 性能优化

- 优化代码


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.2-beta.2 更新一览(2025-06-03)

### 🚀 新功能

- SemVer 版本比较
- 导入/导出设置
- 允许 FileChecker 在 Hash 正确下忽略文件大小问题（大概率情况下哈希正确文件没有问题）
- *PageDownloadCompDetail*: 支持为资源包和光影包选择默认下载位置
- 加入新的更新源 (#729)
- 添加瞅眼服务器的随机字符串
- 添加回删除的空行
- 瞅眼服务器延迟测试

### 🐛 问题修复

- 域名:端口 形式的服务器无法解析
- 第三方登录启动失败 (#711)
- 取消下载后 LabyMod 资源文件被删除导致后续无法启动已安装的 LabyMod
- LabyMod 核心文件不再要求 Size
- LabyMod 修改报错
- LabyMod 的各种修复 (#714)
- 可能的 null
- *theme*: 顶栏部分控件颜色不跟随主题色
- 排序比较时间的时候没有考虑时间相同的情况 (#725)
- SMTC 的一些问题 (#718)
- *PageVersionOverall*: 缓存问题导致切换语言失败
- *FileDrag*: 用户无法将文件拖放到可编辑文字控件上
- *PageSetupUI*: 错误显示了内容见 ModSetup 的提示
- *PageSetupUI*: 修正功能隐藏中的选项卡名称
- *ImageTitleLogo*: 保证在重新设置标题栏图片时得到刷新
- 强制使用 HTTPS 避免获取皮肤错误
- *LaunchArg*: 默认窗口大小错误
- Ci
- 滑动选中无法使用
- 可能会在获取到更新信息之前检查启动器最新版本信息 (#735)
- *FormMain*: 即使设置了 PCL_DISABLE_DEBUG_HINT 也不会隐藏提示
- *theme*: 切换深浅主题时 MyCard 背景色不更新
- *theme*: MyLoading 背景非全透明
- *theme*: MyRadioButton 初始化颜色不正确
- 可能的提前 CancelTask
- Java 被强制高性能
- 内存泄露
- 服务器描述随机字符的 DispatcherTimer 存在内存泄漏
- 瞅眼服务器地址解析

### 🚜 重构

- *theme*: 使用基于 HSL 体系的颜色生成重写主题系统
- *PageDownloadCompDetail*: 优化选择默认保存位置部分代码

### ⚡ 性能优化

- 优化哈希计算性能

### ⚙️ 其他小改动

- 微调资源排序处代码
- 提示就没必要了，用户也看不到……
- 更新帮助库
- 更新帮助库
- 更新帮助库
- 修改命名

### ◀️ 回退改动

- D991097


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.2-beta.1 更新一览(2025-05-24)

### ⚙️ 其他小改动

- 发版工作流修改


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.1 更新一览(2025-05-24)

### 🚀 新功能

- 接入 Mirror 酱 (#690)
- *PageVersionCompResource*: 对资源包详情中的 Minecraft 颜色表达式进行处理 (#681)
- *VersionSetup*: 版本设置允许游戏窗口标题为空
- 自动选中已经存在的 Fabric API
- 支持解析 Quilt 模组文件
- 自动选择已存在的 QSL

### 🐛 问题修复

- 由于 ThemeChanged 事件没有解除绑定导致的内存泄露
- 当配置文件出现错误后启动器无法正常使用
- 配置文件保存原子化，防止意外的文件错误
- 第三方登录失败提示超时 (#658)
- 可能无法查询到部分服务器 (#691)
- 系统设置的 UI 问题
- 漏改的检查更新
- 布局错误
- 1.16 及以下 Fabric API 已存在版本匹配错误 (累了 统一用 Hash 吧)

### ⚡ 性能优化

- 资源下载的 UI 变化比例问题……

### ⚙️ 其他小改动

- Add mirrorchyan uploading (#687)
- 发版修复 (#688)
- 删除 Fabric API 的手动确认提示
- 删除修改版本页面的手动确认提示

### ◀️ 回退改动

- UI


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.1b 更新一览(2025-05-17)

### 🐛 问题修复

- Uuid 读取问题


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.1a 更新一览(2025-05-17)

### 🚀 新功能

- 可选遥测
- 加个 BakaXL 检测
- Action Secret
- 支持简单查询 Minecraft 服务器
- 支持 SRV 解析
- 未自定义 Log4j2 参数时不输出 debug.log（需要测试）
- 加入翻译缓存，减少对 api 的频繁调用
- 允许取消强制要求高性能显卡

### 🐛 问题修复

- 修正隐藏功能中的选项卡名称
- *ModProfile*: 从旧版迁移的正版账户获取皮肤时出现 Uuid 为空的错误
- Mcping 等待时间太长
- 错误的 Referer
- Proxy not apply
- 我服了 conflict
- *Profile*: 可能在请求失败时崩溃
- 编译错误
- 标题栏靠左可能崩溃
- 读取可能出错
- *FormMain*: 拖入 Authlib 验证服务器后没有跳转到第三方验证档案创建界面
- 资源界面不显示内容 #603
- 版本设置跳转资源详情报错
- PageType 被注释导致无法获取到要求的资源类型
- 没传 CurrentCompType
- Connection not keep alive
- Cts 潜在的内存泄露
- 使用 md5 可能导致的泄露问题
- 自动进服选项无效
- 档案描述错误
- Quilt 安装失败
- 反馈时日志文件定位异常
- 可能由于数据同步不及时导致的字典数据缺失
- 存档是否可快速启动可能检查失败

### 🚜 重构

- 进一步完成对 WebClient 的替换
- 更进一步替换 NetRequestOnce
- 下载部分完成 HttpClient 替换
- 重构资源下载界面 UI

### ⚡ 性能优化

- 优化环境问题的表述文本

### ⚙️ 其他小改动

- 移除无用配置字段
- 换用 HttpClient
- 为啥不加 TLS 1.3
- 补上一些可能要用到的 HTTP 方法
- 添加更新 Help.zip 的 Action
- 名称忘改了
- 忘写 runs-on
- ¸üÐÂ Help.zip
- 换一种写法试试
- ¸üÐÂ Help.zip
- 解决乱码问题
- 这次应该没问题了
- 更新帮助库
- 更新帮助库
- 漏了一个 HttpClient 没替换
- ALL -> 全部
- 资源下载的 UI 小调整
- 日志描述问题
- 自动挡更新源


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.11.0a 更新一览(2025-05-09)

### 🚀 新功能

- *Proxy*: 同步上游 #5730 为代理添加的新功能
- *PageDownloadLeft*: 增加 Cleanroom 图标
- Install mods to labymod clients
- *wip*: Profile management ui
- *wip*: Profile Management
- 档案管理，但是离线 UUID 没做完
- Duplicate profile judgement
- 正版验证皮肤更改
- 尝试做了个列表头像
- 数据包下载 (WIP)
- 支持加载数据包列表（但是下不了一点）
- *MyMsgLogin*: 增加账号被封禁时的错误提示
- *Mod*: 增加镜像源
- 支持隐藏部分版本设置功能
- Wam

### 🐛 问题修复

- #479
- *FormMain*: 修复标题栏文本导致 CE 图标消失的问题
- 可能的配置项错误读取
- 设置没有保存
- Compile error
- Revert deleted comment
- Str
- 没选档案现在不允许启动游戏了
- 启动画面不会切换
- 没检测有没有文件夹
- 重置个性化设置时空指针异常
- 不能正确读取 Cleanroom 版本信息
- *ModValidate*: 未检查路径是否存在
- *btnDisplayDesc_Click*: 少了个标记
- *MyMsgInput*: Validate 输入
- *FormMain*: 2.9.3 没有更新日志
- *PageVersionExport*: 无法导出某些版本的 Xearo 文件
- *Setup*: 下载速度限制的配置项不会在打开程序时被读取
- 识别问题
- 版本名称写错了
- *ModModPack*: 解压压缩包进度可能超过 100%
- *ModModpack*: 初始值不为 0
- *ModBase*: 加个 Try Catch
- *ShellOnly*: 只为可能出错的部分弹出提示
- *ModModPack*: 还不让用 Finally 了是吧
- *PageSetupUI*: 修改显示文本
- *ModJava*: 排除下载目录的 Java
- 修复了查看数据包工程时崩溃的问题，但可能会炸掉点别的东西，需要测试
- 哪儿来的 FormLoginOAuth
- 编译错误
- *ModLaunch*: 启动脚本乱码
- *ModLaunch*: 同步 #5909 中修改的选择编码的方式
- *ModLoader*: 不应当显示的加载器的进度也被计入到显示的总进度中
- 少了 $
- 通过网络获取到更多可更新模组时未同步至‘可更新’模组列表视图
- *PageDownloadInstall*: 打断安装预览页面进入动画后再次切换至下载页面时显示错误
- *PageDownloadInstall*: 打断安装预览页面进入动画后再次切换至下载页面时显示错误*2
- Build error
- *CompFavourites*: 无法打开页面，操作出现错误
- *Proxy*: 无效的 URI && 日志错误
- 深色模式 #11
- 遇到无法加载的字体不应当崩溃
- 深色模式 #12
- Labymod is collapsed on 1.8.9
- 重置 Forge 可能失败，但其他部分也有潜在 Bug
- 存在版本继承的核心不一定有 Jar 文件
- LabyMod 1# (#560)
- 文件编码错误
- Missing maslruntime
- 没有打包 maslruntime
- 旧版档案类型错误
- Typo
- 修复 msalruntime 资源找不到问题
- 没有更新服务器 & 运行库已存在时仍旧会尝试释放
- 排序可能会出现失败，但是不应该崩溃
- Entry 有可能为 null
- 没有设置系统代理的情况下会获取到错误的系统代理
- 编译错误
- 编译错误
- Labymod 无法启动
- 潜在的路径不正确问题

### 🚜 重构

- 改用 Webhook 处理 Issues (#508)
- 重新构建更新及公告系统
- *Proxy*: 重构代码

### 📚 文档修改

- *CHANGELOG.md*: Update

### 🎨 部分样式调整

- *issue-automation*: 删去 run-name 前多余的 \
- *issue-automation*: 修正 issue-reopened.yml 的 name
- 空格
- 空格
- *modbase*: 无用换行
- *PageVersionOverall*: 移除无用代码
- *MyMsgLogin*: 统一格式

### ⚙️ 其他小改动

- 创建自动更新 CHANGELOG.md 的 GitHub Action
- 考虑上游已有类似反馈的情况
- 删除多余换行
- *MySkin*: 增加渴望披风的翻译
- 筛选器增加版本 1.21.5
- 忽略 system32 的 Java
- *ModJava*: 注释
- *ModJava*: 删掉多余的让龙猫写
- 先丢回去
- 测试代码忘删了
- 加入 Binding 失败的日志提示
- 修改文案
- 检查当前跑的是不是最新版本，是的话不用再下
- 忘记 Exit Sub 了
- 不再公开更新源
- Delete client_id
- Debug 的时候直接读缓存吧……
- 尝试性修改
- 换一种替换标识
- 报错好像有点道理……


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.10.6 更新一览(2025-04-19)

### 🚀 新功能

- *CompExportInfo*: 允许导出资源信息
- *CompExportInfo*: 允许导出资源信息
- *automation*: 关闭 Issue 时删除标签
- *automation*: 暂无计划关闭 Issue 时添加 忽略 标签
- *automation*: 暂无计划关闭 Issue 时添加 忽略 标签
- *automation*: 添加标签后删除冲突标签
- *automation*: 完善添加标签后删除的冲突标签
- *automation*: 使用组织 Bot
- *automation*: 重开 Issue 时一并移除 移交上游 标签
- *automation*: 添加 run-name 以方便区分触发的 Issue
- *automation*: 排除 Bot 操作触发
- 紧凑实时日志 UI
- *wip*: Labymod install
- *wip*: Labymod
- Labymod
- World quick play (#432)
- 使用 AES 加解密
- 配置迁移
- 配置项自动迁移
- 防止使用旧版本导致的覆盖
- 本体更新缓存
- *Comp*: 支持在 Mod 列表中多选收藏 Mod

### 🐛 问题修复

- 整合包导出没有下载正式版 CE 的逻辑
- 可能由于重复计算导致的卡顿
- *automation*: 修复 Issue 重开 部分删除的 Label 缺失与重复
- *automation*: 修复添加标签时操作类型错误
- *ResourcesSort*: Use case-insensitive comparison
- *ResourcesSort*: Fix reversed order issue in item sorting
- *ResourcesSort*: Fix unexcepted negative sign
- *ResourcesSort*: Use case-insensitive comparison
- 按钮颜色在深色模式下不正确
- *RealtimeLog*: 修复大量日志使得界面假死的 Bug
- 正确的http代理提示信息
- 正确的http代理提示信息
- Update & click update btn occur error
- *title*: 修复某些字体无法正确显示 CE 标题的问题
- 不填写注册链接时输入框会变为红色
- 版本修改的加载器选择可能不正确
- *ModDownloadLib*: 1.14 及以上版本无法下载 OptiFine
- *ModLaunch*: 缓存问题导致切换语言失败
- 重选Fabric后FabricAPI不再自动选择
- *ModDownloadLib*: #5920 造成的无法下载 OptiFine
- PCL-Community/PCL2-CE#436
- *title*: 标题栏 SVG 使用实心颜色
- *PageVersionExport*: 修改描述
- *识别码*: 修复潜在的获取失败问题
- 一些错误的逻辑
- 迁移没有成功
- Thumbnail
- Taglib不支持的音乐
- 尝试性修复部分值获取到为 null
- Null reference

### 🚜 重构

- *automation*: 将各个触发情况从 steps 移至 jobs 以支持多个操作

### 📚 文档修改

- *README.md*: 添加徽章并调整布局
- *README.md*: 调整层级关系
- *README.md*: 使用不分段换行
- *README.md*: 自封闭换行标签
- *README.md*: 自封闭标签补充反斜杠前空格
- *README*: 用行内代码块替换引号
- *README*: 添加贡献者列表
- *README*: 添加更多 emoji
- *README*: 添加贡献者列表
- *README*: 添加更多 emoji
- *CONTRIBUTING.md*: 补充提交上游相关条目
- 先把官网暂时删掉吧……
- 跟进新配置项
- 加个 Logo
- *README*: 加入社区版群聊
- *README*: 多余的 splitline

### ⚡ 性能优化

- *Log*: 尝试加入虚拟化

### 🎨 部分样式调整

- 修改文案描述

### ⚙️ 其他小改动

- *changelog*: 补全更新日志
- *Exception*: 再详细点……
- *automation*: Issue 标签处理
- *automation*: Issue 关闭处理
- *automation*: Issue 重开处理
- *automation*: 允许本人触发 issue-labeled
- *automation*: 重命名 issue-labeded.yml 为 issue-labeled.yml
- 适配多次关闭，机器人触发后不予执行
- 换用社区 BOT Token，如果机器人触发则自动跳过
- 加入对 close as completed 时组织成员的判定
- 排除机器人
- *CompInfoExport*: 小改字段
- 适配函数变化
- Fix file conflict
- 尝试发版自动编译
- 修改上传构建产物到 Release 的方式
- 升级依赖包
- 尝试修复 bug
- 小改 Hint
- 提交时只自动构建 Beta 版
- 移除 PR 开启时的自动请求审查功能
- 换用 REST API 实现
- *Update*: 加点日志输出
- 防止小白无脑复制上面的 LittleSkin 地址
- *ModMain*: 修改描述
- *ModMain*: 优化代码
- *ModDownloadLib*: 按意见进行修改
- 更新帮助库
- 使用 GitHub cli 重做
- PR 自动处理增加签出仓库
- Issue 自动处理 优化 (#441)
- 修正缺 token 问题
- 漏了一个（
- 致命失误（
- *favorites*: 不允许分享空收藏夹
- 更改反馈链接
- 加一个异步锁
- SMTC优化
- 撤回关于自定义文件夹的更改，拆分为其他pr
- 许可
- 改用第三方 actions 实现
- [skip ci] edited 似乎没啥用……
- 获取 .NET 版本

### ◀️ 回退改动

- Ci: 排除机器人
- Ci: 加入对 close as completed 时组织成员的判定
- Ci: 换用社区 BOT Token，如果机器人触发则自动跳过
- Ci: 适配多次关闭，机器人触发后不予执行
- SLOOF LIRPA


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.10.5 更新一览(2025-03-23)

### 🚀 新功能

- 支持展示加载失败的收藏
- 多收藏夹
- Allow muilt favs
- Share
- 加载器初筛选
- Muilt download
- Rename
- Sort
- *模组管理*: 添加新排序方式
- 内存优化
- *Exception*: 增加 Java 检查时的输出
- *Comp*: 修改本地资源展现的实现方式（基础修改，能跑的那种）
- *real-time-log*: 完成日志显示
- Sort
- *real-time-log*: 导出实时日志
- *real-time-log*: UI 细节微调
- *CompFavourites*: 允许分享资源管理中选中的项目
- *wip*: Cleanroom 支持
- Cleanroom 重置支持，并给重置加了个小提示
- *about*: 为特别鸣谢添加 Pysio
- *CompFav*: 允许给收藏夹内的项目加入备注
- *CompResource*: 基于 ID 的简单查重
- *wip*: EasyTier 初步支持
- *link*: 支持进行 EasyTier 组网
- *Java*: 允许全局禁用 Java
- *setup*: 允许禁用 jlw
- Output device change
- Mod命名默认值增加“【】xxx”
- *ci*: Forced utf-8
- 支持切换字体

### 🐛 问题修复

- Install function not work
- *log*: 修复部分地方使用原版日志的问题
- Are u sure about that????
- Too long menu & unable to click hint
- Not select first fav
- Reload logic error
- Save format & missing share items
- Error input
- 版本设置下的内存
- Typo
- Changelog lastversion code
- Type error
- Potential repeat
- Not save after remove
- Mistake share webplib with official
- Dir not create
- Sort cost too much time
- Incorrect order
- *收藏夹*: 错误的 Binding
- Check available mod updates for quilt and fabric on quilt instance
- *CompUpdate*: 错误的调用
- *LocalComp*: 错误的窗体指定
- 光影包可能检测不到更新
- 构建错误
- *real-time-log*: 修复已知问题
- Mistake share webplib with official
- Dir not create
- *real-time-log*: 修复结束进程按钮不符预期行为的问题
- 修复移除未选中选项时不会立刻刷新的问题
- Missing End Sub
- 始终检查 Libraries 文件
- Treat cleanroom instances as installable mods
- *PageChange*: 下载页面的跳转不正确
- Not read loader info on modrinth
- 搁这最强大脑呢？
- *Merge*: Build error
- *VersionLeft*: 页面选择错误
- *ShowLog*: 特定版本没有匹配
- *ShowLog*: 判断逻辑错误
- *ShowLog*: RC1 版本日志打开错误
- *ShowLog*: 特定版本没有匹配
- *ShowLog*: 特定版本没有匹配
- *FavDownload*: 没有合适的版本时仍会弹出选择弹窗
- *ScreenshotView*: 修复 #334 的问题
- *CompFavourites*: 切换收藏夹后还显示着选中
- Update server link error
- *Java*: 默认高性能启动 Java 失效
- *PageVersionInstall*: 修改版本时重置加载器的加载状态
- *CompUpdate*: 修复错误的检查更新
- *GameLog*: 修复实时游戏日志在浅色模式下不正常
- *Update*: 判断是否需要更新逻辑有误
- *CompUpdate*: 错误的资源更新数据
- LatestVersion -> LastVersion
- *McInstallLoader*: 始终将 OptiFine 作为 Mod 安装
- *ModLaunch*: 逻辑错误导致版本设置的使用 JLW 选项无效
- 默认音频设备的代号应该是 -1
- *ModMusic*: 千年播放老问题
- *PageSetupLaunch*: 提示错误
- 重命名 Result 枚举为 ProcessReturnValues
- 修复合并错误
- 修改界面加载错误
- 错误提示 OptiFine 与 Cleanroom 兼容
- *JavaSelect*: 鼠标在列表上滑动会使得后面的页面也滑动

### 🚜 重构

- *LocalComp*: UI display
- *LocalComp*: 按照资源类型传入加载器类型
- *LocalComp*: 换用更加合理的加载器实例化方法
- *LocalCompItem*: Swipe Select
- *ModBase*: 重构下 ShellAndGetOutput
- 增加地址判断

### 📚 文档修改

- *changelog.md*: 更新 2.10.3
- *contributing.md*: 更新贡献指南
- *contributing.md*: 优化措辞
- *CHANGELOG.md*: 微调
- *CONTRIBUTING.md*: 增加 `chore` 类型
- *contributing.md*: Optimize typesetting and wording

### ⚡ 性能优化

- *proxy*: 优化判断逻辑 perf(proxy):增加代理状态日志

### 🎨 部分样式调整

- *LocalComp*: Rename class name
- *LocalComp*: Rename file
- *Comp*: Rename CompLoaderType
- *LocalComp*: Rename file name again
- *LocalComp*: Why the file rename fail so many times!!!
- Rename func name
- 修改注释
- Rename folder
- *proxy*: Remove bracket
- 切换逻辑移位
- *PageSetupLaunch*: 语法错误

### ⚙️ 其他小改动

- Comment
- Revise http proxy option description (#288)
- “PCL 已更新至” -> “PCL CE 已更新至”
- 更改侧边栏 Quilt 图标
- 修改文本
- *LocalComp*: 移除 type 字段
- *Comp*: 修改 CompLoaderType 枚举值
- *ui*: 优化 UI
- *UI*: 调整自适应按钮的排布
- *Proxy*: 跳过本地地址的代理
- Use context
- *VersionInstall*: 砍掉版本修改的动画
- *PageVersionLeft*: 修改名称 “自动安装” -> “修改”
- *FavDownload*: 使用 ToNetFile 代替 New NetFile
- *style*: 整理下代码
- 修改社区版提示
- 开源版本/开源内容 ---> 社区版
- *ClickLogo*: 加个……彩蛋？
- *ClickLogo*: 优化一下下
- *ClickLogo*: 改回部分文案&更加还原 PCL1 动画
- *ClickLogo*: 再给这条咸鱼转一下（？
- 删除注释 不会是写给我看的吧（？
- *ui*: 同步光影和资源包下载页面版本预设项
- 你倒是跑啊！
- Fix
- 发版就编译
- 自动发版出了点问题，先默认全编译吧
- *OpenFolder*: 使用新的帮助方法
- 接口
- 修复 arm64 编译问题
- 添加回 arm64 编译选项
- UI 小调整
- 简单修复发版问题
- Build Platform
- Build on windows latest
- 暂时停用 self-hosted


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.10.3 更新一览(2025-02-12)

### 🚀 新功能

- Update
- ARM64 Action 构建

### 🐛 问题修复

- MyHint 浅色下的颜色 (#207)
- MyHint 浅色下的颜色
- 一些模组简介没有被翻译
- #233
- 缺少 ARM64 判断
- Hardcode
- Potential exception throw

### ⚙️ 其他小改动

- Add


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.10.2 更新一览(2025-02-07)

### 🚀 新功能

- Issue template 添加官方版引用 (#213)

### 🐛 问题修复

- Icon 过小
- *ui*: Final 修复
- 版本设置 Java Wrapper 设置不生效
- 不重置 JLW 选项
- Select logic error (#204)

### ⚙️ 其他小改动

- 优化页面提示文本
- *ui*: 文本优化


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.10.1 更新一览(2025-02-05)

### 🐛 问题修复

- Not return array


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.10.0 更新一览(2025-02-04)

### 🚀 新功能

- Change another decouple method
- Decouple
- Decouple
- Mulit
- Log file name
- Change target framework
- 支持对已有核心进行自动安装修改操作，但还有些小问题
- 自动安装修改核心之前先备份文件，失败了可以回滚

### 🐛 问题修复

- Replace file name incorrect
- File name
- Drag file not apply
- Fody warning & unnecessary reference
- Build fail
- #130
- Mod选择和你知道吗
- Logic error
- 不知道为啥漏了几个文件
- 没有核心时点击主页下载按钮会抛出异常
- 回滚逻辑存在问题
- 深色模式 #5
- Button not display well
- Buttons did not display well
- Incorrect value (#178)

### ⚡ 性能优化

- IIf 替换为 If

### ⚙️ 其他小改动

- Change name
- Try catch
- Path update
- Missing one
- 移除 ClientToken & 代码解耦合
- Addition


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.9.5 更新一览(2025-01-24)

### 🚀 新功能

- 初步 SMTC 支持，暂时不支持传歌曲信息
- Allow disable hardware acceleration
- Fps limit
- Adapt Fabrishot
- Allow load webp screenshot
- 允许禁用 Java Wrapper 以进行高级调试
- 识别剪贴板资源并提示跳转
- Java 细致搜索开关
- Resource save auto locate
- Disable theme

### 🐛 问题修复

- Fps limit abnormal
- Text not disappear
- Try fix read error
- Update fail
- Build error
- UI

### 📚 文档修改

- README 添加隐藏提示说明 [skip ci]

### ⚙️ 其他小改动

- NuGet 配置迁移备份文件清理
- Limit method
- Ignore abnormal screenshot files
- 默认开启 SSL 验证
- Misc
- Animate


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.9.4 更新一览(2025-01-22)

### 🚀 新功能

- 识别码
- 清理游戏垃圾
- 使用 SHA256 进行摘要运算
- 添加在标题栏上的社区版标识
- NAT 测试
- 支持检测 IP 版本
- 允许修改注册表设置以隐藏社区版提示
- 完成端口查找
- 完成端口有效性检测
- Link page change
- 网络请求小优化
- Proxy
- 尝试自动检测端口号，但是失败了，所以暂时手动输入端口号
- 联机创建 UPnP 映射测试

### 🐛 问题修复

- 类型错误
- 自己消失的 NuGet 程序包
- 语言标记
- Binding fail
- Ui and set
- 写错一个值导致一直弹第一次检测弹窗
- Config read error
- Config source

### ⚙️ 其他小改动

- 调试模式下展示开发中部分
- 并行网络状态检测
- Tip
- Warn tip
- Ui improve
- Merge clean up


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.9.3 更新一览(2024-12-29)

### 🚀 新功能

- 截图查看管理界面
- 存档查看
- 复制图标
- 存档详细信息查看 & 操作按钮
- 光影 & 资源包 界面
- 打开文件按钮
- 截图图标
- 截图图标
- 调整顺序
- 按钮居中
- 修改建议
- 粘贴按钮
- 截图界面改进
- 资源包展示支持文件夹类型
- 处理文件拖拽
- 框架升级
- 自定义下载文件

### 🐛 问题修复

- 无法启动
- 截图不存在提示 & 查看版本错误
- 复制文件而不是文件内容
- 缓存目录创建与删除问题
- 可能出现的复制错误
- 不合理的剪切板操作
- 文本描述
- UI 错误
- 空值检查对象错误
- 界面元素有时看不到
- 目录不存在时报错
- 不合理的路径获取方式
- 新版本调用问题
- Issue #19
- Libwebp not release
- Issue list display error
- 反馈页面文本不会自动换行
- 投票页面的文本也没允许换行……
- 润色更新日志为 0 时的文本
- 将所有 .NET Framework 4.6.2 改为 .NET Framework 4.8
- 去除没有百宝箱的提示

### ⚙️ 其他小改动

- 收藏夹类型
- 修改版本信息中的 Commit Hash 为前 7 位
- 调整界面
- 资源包使用内部自带描述
- 加入 Try Catch
- That's better
- 序号后退，给导出整合包让位
- Restore nuget
- Fix
- Nuget net48
- Fody version
- 排除资源包调试输出
- 排除资源包调试输出目录的实现方式


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.9.2 更新一览(2024-12-22)

### 🐛 问题修复

- 不知道哪来的 FormLoginOAuth 干爆了项目，但现在它被干爆了


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.9.1 更新一览(2024-12-22)

### 🚀 新功能

- 重新添加了更新功能

### 📚 文档修改

- 添加对版本号的说明 [skip ci]


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.9.0 更新一览(2024-12-21)

### 🚀 新功能

- 创建界面
- 修改界面
- 基本框架
- 界面小调整
- 重构一下获取方法
- 搜索框
- 搜索整合进一张卡片
- 防止全炸
- 减少不必要的多次全部读取构造
- 仅存储 ID 信息
- 先抄个界面
- 修改界面
- 加入删除按钮
- 进入详情页面
- 批量操作（需要优化）
- 完善界面
- MyListItem 修改
- 使用 Property 自动处理……
- 添加设置项

### 🐛 问题修复

- Build failed
- 按钮状态在切换页面时没有切换
- 不会自动刷新
- API 同时请求
- 使用 Setup 而不是 ReadReg
- 可能卡住的任务
- 错误的界面展示
- 不符合直觉的全选
- ListItem click ability
- Abnormal reload
- Listitem layout error
- Build failed

### ⚙️ 其他小改动

- 回滚意外修改
- UI 调整
- 搜索功能先砍了
- 漏了个注释……
- 删除无用字段
- UI 优化
- 优化结构
- UI 错误
- 逻辑小优化
- 多余代码内容
- Apply suggestions
- 使用 CompFavorites 作为存储名称
- Remove MyMiniCompItem
- Decoupling


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
## 2.8.11 更新一览(2024-12-21)

### 🚀 新功能

- *neoforge*: 添加了 NeoForge 的安装选项，并支持获取版本列表
- 正式支持 NeoForge 自动安装
- 适当修改本地 Mod 管理界面与操作逻辑
- 无可更新 Mod 时隐藏对应卡片
- 改成龙猫想要的样子
- 加个分割线
- Mod 管理卡片标题
- 使用 MyRadioButton
- 支持转换 webp 图片
- 微软登录设备代码流，并移除了浏览器验证方式
- 支持复制设备代码和验证网址
- *auth*: 打开的微软验证网页现在会自动填入设备代码
- 加入页面
- 反馈具体化页面
- 加入头像
- 完善反馈页面
- 投票列表
- Use loader
- 复古界面
- 完善投票页面
- MyImage 控件
- 修改实现方式
- UseCache 选项和异步加载图片
- 原版服务端下载功能
- 下载器
- 按F5刷新自定义主页
- 自己下
- 光影包下载
- 支持下载资源包
- Quilt 支持（未完工）
- Quilt 支持
- Quilt 整合包安装
- 本体更新（实验性）

### 🐛 问题修复

- #3461
- NeoForge 版本列表排序，但是抽象实现
- 一处逻辑判断错误，但未解决版本判断错误的问题
- 所有版本都显示为测试版 / 正式版
- 回滚修改，修复版本列表问题
- 1.20.1 NeoForge 安装失败（Json 路径错误）
- 1.20.1 NeoForge 近期版本无法安装（Json 路径错误）
- NeoForge 手动安装版本列表不能正常加载（修完了）
- 官方源总是 Fallback 到 BMCLAPI
- 检测愚人节版本所用时区不正确
- 下载 NeoForge 时，从未使用镜像源
- 意料之外的内容加入
- 禁用或启用模组没有改变其所在卡片
- 错误的全部统计
- 全选逻辑问题、UI问题
- Ctrl+A 实现不合逻辑
- 用枚举替代字符串
- 刷新列表后全部选项不在 Checked 状态
- 鬼知道为什么没有初始化
- 全选逻辑不正确
- 窗口重获焦点后筛选项被重置
- Typo
- 新增的 otc 参数可能导致网页无法打开，解决方案是去掉了这个参数
- 列表重复
- 分类错误
- 暂时使用波浪号检测修复 #4505
- 少了一个字
- 测试忘记删了
- Apply suggestions
- 不使用磁盘缓存
- B站图床
- 不是每个请求都需要的 Header
- 目录问题……
- #5238
- 多打了一个 s 导致有些光影下不了
- 错误的 ComboBox 大小
- 合并了 Curse 和 Modrinth 的部分相似标签（去重）
- 自动安装选择 1.14 会意外崩溃
- 在选择了 Fabric API / QSL 后直接取消选择 Fabric / Quilt 不会重置已选择的 API 选项
- 不知道哪里冒出来的 Exit Sub 和不知道为什么没有同步上游修改导致的重复任务检查爆炸

### 🚜 重构

- 简化搜索 Java 时的关键词检测
- 合并 Forge 和 NeoForge 部分代码
- 使用正则表达式检测 NTFS 8.3 文件名
- 重新整了下 Quilt，但是还没完工

### 🎨 部分样式调整

- 当有 Mod 选中时在下方空开一定空间方便操作

### ⚙️ 其他小改动

- 删除调试用信息
- Clean up
- 代码可读性和相关优化
- 代码清理
- 一些判断和命名修改
- 开源版本提示新增
- Api 参数
- 描述
- Try Catch
- Apply suggestions
- Add summary tip
- Apply suggestions
- 微调参数...
- 删掉莫名其妙的第7行
- 减少屎山
- 删除没用的东西
- 换一个方式
- 漏了一个，问题不大
- 同步 #4360 有关下载任务名称的修改
- 修改标签，消除歧义
- 同步 #4360 有关下载任务名称的修改
- Json -> Json; jar -> Jar
- 整合包安装时部分错误提示可能有误
- 不知道啥时候改了一行测试没改回来


---

[如果你有 Mirror 酱 CDK 可以直接使用此高速下载源](https://mirrorchyan.com/zh/projects?rid=PCL2-CE&source=pcl2ce-gh-release)
你也可以直接在下方的 Assets 中下载
