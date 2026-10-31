# Utils/WinRT/Interface/Windows/UI/Notifications 目录

WinRT Toast 通知接口，位于命名空间 `PCL.Core.Utils.WinRT.Interface.Windows.UI.Notifications`。声明 Windows Toast 通知所需的 COM 接口（C# 不安全结构体 + 虚函数表 `Vtbl` 的 `delegate* unmanaged` 函数指针），供上层 `UI.ToastNotification` 通过 COM 互操作调用系统 Toast。本目录**只定义接口布局，不含激活/调用逻辑**。

## 本目录模块

- **IToastNotificationManagerStatics.cs**：Toast 通知管理器静态接口（创建 Toast 通知器、获取模板内容）。
- **IToastNotifier.cs**：Toast 通知器接口（显示/隐藏通知、管理计划通知）。
- **IToastNotificationFactory.cs**：Toast 通知工厂接口（由 XML 内容构造通知对象）。

## 运行流程（典型调用链，供上层参考）

1. 通过 `RoActivateInstance` / `RoGetActivationFactory` 以 `ActivatableClassId` 激活对应 WinRT 类，得到 COM 对象指针，再用 `Iid` 对其 `QueryInterface` 取得对应接口结构体（`lpVtbl`）。
2. `IToastNotificationManagerStatics`：
   - `CreateToastNotifier()` 或 `CreateToastNotifier(string applicationId)` → 取得 `IToastNotifier` 指针；
   - `GetTemplateContent(ToastTemplateType)` → 取得 `XmlDocument` 指针（其底层 `IXmlDocumentIO` 见 `Windows.Data.Xml.Dom`）。
3. 用 `IToastNotificationFactory.CreateToastNotification(XmlDocument)` 把 XML 内容包装成 `ToastNotification` 对象。
4. 用 `IToastNotifier.Show(ToastNotification)` 弹出；`Hide` 移除；`get_Setting` 读取 `NotificationSetting`；`AddToSchedule`/`RemoveFromSchedule`/`GetScheduledToastNotifications` 管理计划通知。

三个接口各自的接口标识（使用时需 `QueryInterface`）：
- `IToastNotificationManagerStatics`：IID `50ac103f-d235-4598-bbef-98fe4d1a3ad4`，ActivatableClassId `Windows.UI.Notifications.ToastNotificationManager`。
- `IToastNotifier`：IID `75927b93-03f3-41ec-91d3-6e5bac1b38e7`，ActivatableClassId `Windows.UI.Notifications.ToastNotifier`。
- `IToastNotificationFactory`：IID `04124b20-82c6-4229-b109-fd9ed4662b53`，ActivatableClassId `Windows.UI.Notifications.ToastNotification`。

## 输入输出规范

三个文件结构一致：`*Info` 静态类（存 `ActivatableClassId` 与 `Iid`）+ `unsafe struct`（仅含 `Vtbl*`）+ `*Vtbl` 结构体（IUnknown + IInspectable + 接口方法，全部为 `delegate* unmanaged<...>`）。

### 通用 Vtbl 头部（每个接口都有）
- **IUnknown**：`QueryInterface(void*, Guid*, void**, int)`、`AddRef(void*, uint)`、`Release(void*, uint)`。
- **IInspectable**：`GetIids(void*, uint*, Guid**, int)`、`GetRuntimeClassName(void*, IntPtr*, int)`、`GetTrustLevel(void*, int*, int)`。
- 约定：第一个 `void*` 参数是 COM 对象自身（this 指针）；返回 `int` 为 HRESULT（0=S_OK）。

### IToastNotificationManagerStaticsVtbl（IToastNotificationManagerStatics.cs）
- `CreateToastNotifier(void*, void**, int)`：无参创建 `ToastNotifier`，结果经 `void**` 输出。
- `CreateToastNotifierWithId(void*, IntPtr, void**, int)`：带 `applicationId`（`IntPtr`=HSTRING）创建。
- `GetTemplateContent(void*, int, void**, int)`：`ToastTemplateType`（int 枚举）入参，输出 `XmlDocument`。

### IToastNotifierVtbl（IToastNotifier.cs）
- `Show(void*, void*, int)`：显示给定 `ToastNotification`。
- `Hide(void*, void*, int)`：隐藏给定 `ToastNotification`。
- `get_Setting(void*, int, int)`：获取 `NotificationSetting`（out int）。
- `AddToSchedule(void*, void*, int)` / `RemoveFromSchedule(void*, void*, int)`：增删 `ScheduledToastNotification`。
- `GetScheduledToastNotifications(void*, void**, int)`：输出 `IVectorView<ScheduledToastNotification>`。

### IToastNotificationFactoryVtbl（IToastNotificationFactory.cs）
- `CreateToastNotifier`? 实际为 `CreateToastNotification(void*, void*, void**, int)`：以 `XmlDocument`（void*）入参，输出 `ToastNotification`。

## 使用约束与注意事项

- **不安全 COM 互操作**：全部 `unsafe` + 裸函数指针。任何指针误用（空 `lpVtbl`、错误签名、错误 ABI）会直接导致 `AccessViolationException` 或 `SEHException`，无托管保护。
- **调用约定/ABI**：WinRT 方法默认 `__stdcall`；声明为 `delegate* unmanaged<...>` 时需确保项目/平台的实际调用约定与之匹配（通常显式指定 `unmanaged["stdcall"]`，当前代码未标注则采用平台默认，待确认是否需要显式 stdcall 修饰）。
- **COM 线程模型**：WinRT API 通常要求调用线程已通过 `RoInitialize` 初始化（桌面应用多为单线程套间 STA 或 MTA，取决于宿主）。未在正确初始化的线程调用会返回 `RPC_E_WRONG_THREAD` 等 HRESULT。
- **引用计数**：持有接口指针期间需 `AddRef`，不再使用时 `Release`；跨方法传递指针要小心生命周期，避免悬空指针或泄漏。
- **字符串/HSTRING**：带 `IntPtr` 的字符串参数按 WinRT HSTRING 约定封送（非普通 `char*`），调用方须用 `WindowsCreateString` 等构造，并负责释放。
- **异步方法**：本目录接口无异步方法；若上层需要 `Show` 之后的完成通知，需结合 `IToastNotification` 的事件接口（不在本目录）。
- **平台限制**：这些接口仅 Windows + 含 `Windows.winmd` 运行时可用，非 Windows 平台编译/运行需条件编译隔离。
- **GUID 准确性**：`Iid` 必须与系统实际契约 GUID 完全一致，否则 `QueryInterface` 返回 `E_NOINTERFACE`；当前值以代码为准，如需验证可对照 Windows SDK 元数据。
