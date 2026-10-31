# Link/EasyTier 目录

EasyTier 组网子系统，位于命名空间 `PCL.Core.Link.EasyTier`。封装 EasyTier（P2P 虚拟组网）的控制器、信息提供、节点信息与中继管理。

## 本目录模块

- **ETController.cs**：EasyTier 控制器（启动/停止组网、下发配置）。
- **ETInfoProvider.cs**：EasyTier 信息提供器（网络状态、节点列表）。
- **ETPeerInfo.cs**：EasyTier 节点（peer）信息模型。
- **ETRelay.cs**：EasyTier 中继管理（中继节点选择/配置）。
