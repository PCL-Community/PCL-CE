# App/Database 目录

轻量数据库子系统，位于命名空间 `PCL.Core.App.Database`。为启动器提供本地结构化数据存储能力。

## 本目录模块

- **DatabaseEntry.cs**：数据库条目模型（可持久化的数据记录基类/接口）。
- **DatabaseService.cs**：数据库服务，封装底层存储（基于 LiteDB / SQLite）的读写访问。
