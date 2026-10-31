# Utils/Validate 目录

输入校验器，位于命名空间 `PCL.Core.Utils.Validate`。提供一组可组合的输入合法性校验器（用于配置项、用户输入等）。

## 本目录模块

- **NullOrEmptyValidator.cs**：空值/空字符串校验。
- **NullOrWhiteSpaceValidator.cs**：空白字符串校验。
- **StringLengthValidator.cs**：字符串长度校验。
- **IntValidator.cs**：整数范围校验。
- **RegexValidator.cs**：正则校验。
- **FileNameValidator.cs / FolderNameValidator.cs**：文件名/文件夹名校验（非法字符）。
- **FileSystemValidator.cs**：文件系统路径校验。
- **FolderPathValidator.cs**：文件夹路径校验。
- **HttpValidator.cs / HttpAndUncValidator.cs**：HTTP 地址/UNC 路径校验。
- **BlacklistValidator.cs**：黑名单校验（屏蔽违规内容）。
