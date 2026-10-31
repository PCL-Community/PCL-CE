# Pages/PageLaunch

启动页面，命名空间 `PCL`。提供启动游戏入口与账号/档案登录管理。

## 本目录直接文件

- `PageLaunch.xaml(.cs)`：启动页容器。
- `PageLaunchLeft.xaml(.cs)` / `PageLaunchRight.xaml(.cs)`：启动左右栏（左：版本与快速操作；右：启动按钮、版本信息、日志入口）。
- 登录/账号：
  - `PageLoginAuth.xaml(.cs)`：正版（验证）登录。
  - `PageLoginMs.xaml(.cs)`：微软账号登录。
  - `PageLoginOffline.xaml(.cs)`：离线账号登录。
  - `PageLoginProfile.xaml(.cs)`：账号档案管理。
  - `PageLoginProfileSkin.xaml(.cs)`：档案皮肤设置。
- `MySkin.xaml(.cs)`：皮肤展示控件。
- `MyMsgLogin.xaml(.cs)`：登录相关消息框。
