# Utils/WinRT/Interface/Windows/Data/Xml/Dom 目录

WinRT XML DOM 接口，位于命名空间 `PCL.Core.Utils.WinRT.Interface.Windows.Data.Xml.Dom`。声明 WinRT `Windows.Data.Xml.Dom.XmlDocument` 的 `IXmlDocumentIO` COM 接口（C# 不安全结构体 + 虚函数表 `Vtbl` 的 `delegate* unmanaged` 函数指针）。主要用于加载/保存 Toast 通知的 XML 模板内容，供上层 `UI.ToastNotification`（配合 `Windows.UI.Notifications`）构建与写回 XML 负载。本目录**只定义接口布局，不含调用逻辑**。

## 本目录模块

- **IXmlDocumentIO.cs**：WinRT `IXmlDocumentIO` 接口声明（XML 文档的加载/保存互操作）。

## 运行流程（典型调用链，供上层参考）

1. 通过 `RoActivateInstance` 以 `ActivatableClassId = "Windows.Data.Xml.Dom.XmlDocument"` 激活 XML 文档对象，再用 `Iid` 取得 `IXmlDocumentIO` 指针（`lpVtbl`）。
2. 填充内容：
   - `LoadXml(string xml)`：把一段 XML 字符串直接载入文档（最常用于把 `ToastNotificationManager.GetTemplateContent` 得到的模板 XML 进一步改写）；
   - `LoadXmlWithSettings(string xml, XmlLoadSettings settings)`：带 `XmlLoadSettings`（如是否禁止 DTD/外部实体）载入，用于更严格/宽松的解析。
3. 保存：
   - `SaveToFileAsync(IStorageFile file)`：把文档异步写入指定 `IStorageFile`，返回 `IAsyncAction`（WinRT 异步操作），需通过 WinRT 异步模式等待完成，而非直接 `await` 原生 `void**`。
4. 该文档对象随后可作为 `IToastNotificationFactory.CreateToastNotification(XmlDocument)` 的入参（见 `Windows.UI.Notifications`）。

接口标识：
- `IXmlDocumentIOInfo`：IID `6cd0e74e-ee65-4489-9ebf-ca43e87ba637`，ActivatableClassId `Windows.Data.Xml.Dom.XmlDocument`。

## 输入输出规范

### 结构布局（IXmlDocumentIO.cs）
- `public static class IXmlDocumentIOInfo`：存 `ActivatableClassId` 与 `Iid`。
- `public unsafe struct IXmlDocumentIO`：仅含 `public IXmlDocumentIOVtbl* lpVtbl;`。
- `public unsafe struct IXmlDocumentIOVtbl`：
  - **IUnknown**：`QueryInterface(void*, Guid*, void**, int)`、`AddRef(void*, uint)`、`Release(void*, uint)`。
  - **IInspectable**：`GetIids(void*, uint*, Guid**, int)`、`GetRuntimeClassName(void*, IntPtr*, int)`、`GetTrustLevel(void*, int*, int)`。
  - **IXmlDocumentIO**：
    - `LoadXml(void*, IntPtr, int)`：入参 `xml` 为 `IntPtr`（WinRT HSTRING），同步载入。
    - `LoadXmlWithSettings(void*, IntPtr, void*, int)`：入参 `xml`（HSTRING）+ `settings`（XmlLoadSettings 接口指针）。
    - `SaveToFileAsync(void*, void*, void**, int)`：入参 `file`（IStorageFile 接口指针），输出 `IAsyncAction`（`void**`）。
- 约定：首参 `void*` 为 COM 对象 this 指针；返回 `int` 为 HRESULT（0=S_OK）。

## 使用约束与注意事项

- **不安全 COM 互操作**：`unsafe` + 裸函数指针，指针误用（空 `lpVtbl`、错误签名）会直接 `AccessViolationException`/`SEHException`。
- **调用约定/ABI**：WinRT 方法默认 `__stdcall`；当前 `delegate* unmanaged<...>` 未显式标注调用约定，需确认平台默认与 WinRT ABI 一致（建议显式 `unmanaged["stdcall"]`，待确认）。
- **字符串/HSTRING**：`LoadXml` 的 `xml` 按 WinRT HSTRING 封送，调用方须用 `WindowsCreateString` 构造并释放，不能直接传托管 `string` 指针。
- **异步处理**：`SaveToFileAsync` 返回的是 `IAsyncAction`（经 `void**` 输出），**不是**可直接 `await` 的 .NET `Task`；必须由上层用 WinRT 异步等待机制（如 `IAsyncInfo`/`AsyncActionCompletedHandler` 或语言投影）等待完成，否则容易在文件写入完成前就释放资源。
- **COM 线程模型**：调用线程需经 `RoInitialize` 初始化为合适套间（通常 STA/MTA 视宿主而定），否则返回 `RPC_E_WRONG_THREAD`。
- **引用计数**：持有 `IXmlDocumentIO`/`IStorageFile` 指针时要正确 `AddRef`/`Release`，注意跨方法生命周期，避免悬空或泄漏。
- **安全设置**：`LoadXmlWithSettings` 的 `XmlLoadSettings` 可禁用 DTD/外部实体，加载不可信 XML 时应启用以防止 XXE/外部实体攻击；`LoadXml` 默认设置待确认（建议优先用带 Settings 的重载处理外部来源）。
- **平台限制**：仅 Windows + 含 `Windows.winmd` 运行时可用，非 Windows 平台需条件编译隔离。
- **GUID 准确性**：`Iid` 必须与系统 `IXmlDocumentIO` 契约 GUID 完全一致，否则 `QueryInterface` 返回 `E_NOINTERFACE`。
