# Validate 测试分组

本目录测试 PCL.Core 中 `PCL.Core.Utils.Validate` 命名空间下的输入校验器集合。各文件均针对单一 `IValidator` 实现，使用 `[DataRow]` 做多用例边界验证，断言 `Validate(input).IsValid` 与错误信息。

## 本目录测试模块

- `IntValidatorTest.cs` — `IntValidator`（带最小/最大值边界）对整数输入的合法性校验。
- `NullOrEmptyValidatorTest.cs` — 空字符串/Null 检测校验。
- `NullOrWhiteSpaceValidatorTest.cs` — 空白字符/Null 检测校验。
- `StringLengthValidatorTest.cs` — 字符串长度上下限校验。
- `FileNameValidatorTest.cs` — 文件名合法性（非法字符等）校验。
- `FolderNameValidatorTest.cs` — 文件夹名称合法性校验。
- `FolderPathValidatorTest.cs` — 文件夹路径合法性校验。
- `HttpValidatorTest.cs` — HTTP URL 合法性校验。
- `HttpAndUncValidatorTest.cs` — 同时接受 HTTP 与 UNC 路径的校验。
- `BlacklistValidatorTest.cs` — 黑名单（禁止值）校验。
