# Modules/Updates

更新与公告模块，命名空间 `PCL`。负责检查启动器新版本、拉取公告，并聚合多个更新源。

## 本目录直接文件

- `UpdateManager.cs`：更新编排器。维护 `remoteServer`（`UpdatesWrapperModel`，聚合多个 `IUpdateSource`），负责版本比对、下载更新与"等待重启"状态（`isUpdateWaitingRestart`）。
- `IUpdateSource.cs`：更新源接口（`SourceName`、`IsAvailable()`、确保最新版本等），各镜像源实现该接口。
- `UpdatesWrapperModel.cs`：更新源包装模型，聚合多源并提供统一查询。
- `UpdatesMirrorChyanModel.cs` / `UpdatesMinioModel.cs` / `UpdatesRandomModel.cs`：具体更新源实现（MirrorChyan、MinIO、随机源等）。
- `AnnouncementService.cs`：公告加载（`Load()`），按已展示记录去重并展示新公告。
- `VersionDataModel.cs` / `VersionAnnouncementDataModel.cs`：版本与公告的数据模型。
- `EnumUpdateChannel.cs`：`UpdateChannel`（stable/beta）、`UpdateArch`（x64/arm64）枚举。
- `UpdateEnums.cs`：更新相关其他枚举。
