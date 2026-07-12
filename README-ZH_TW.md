[简体中文](README.md) | [English](README-EN.md) | **繁體中文**

<div align="center">

<img src="PCL.Desktop/Assets/icon.ico" alt="Logo" width="80" height="80">

# PCL N Edition

[![Stars](https://img.shields.io/github/stars/MuXue1230-owo/PCL-N?style=flat&logo=data:image/svg%2bxml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZlcnNpb249IjEiIHdpZHRoPSIxNiIgaGVpZ2h0PSIxNiI+PHBhdGggZD0iTTggLjI1YS43NS43NSAwIDAgMSAuNjczLjQxOGwxLjg4MiAzLjgxNSA0LjIxLjYxMmEuNzUuNzUgMCAwIDEgLjQxNiAxLjI3OWwtMy4wNDYgMi45Ny43MTkgNC4xOTJhLjc1MS43NTEgMCAwIDEtMS4wODguNzkxTDggMTIuMzQ3bC0zLjc2NiAxLjk4YS43NS43NSAwIDAgMS0xLjA4OC0uNzlsLjcyLTQuMTk0TC44MTggNi4zNzRhLjc1Ljc1IDAgMCAxIC40MTYtMS4yOGw0LjIxLS42MTFMNy4zMjcuNjY4QS43NS43NSAwIDAgMSA4IC4yNVoiIGZpbGw9IiNlYWM1NGYiLz48L3N2Zz4=&logoSize=auto&label=stars&labelColor=444444&color=eac54f)](https://github.com/MuXue1230-owo/PCL-N/)
![GitHub Release](https://img.shields.io/github/v/release/MuXue1230-owo/PCL-N?label=release&logo=github)
[![Issues](https://img.shields.io/github/issues/MuXue1230-owo/PCL-N?style=flat&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/MuXue1230-owo/PCL-N/issues)
[![Pull requests](https://img.shields.io/github/issues-pr/MuXue1230-owo/PCL-N?style=flat&label=pull%20requests&labelColor=444444&color=1F883D&logo=github)](https://github.com/MuXue1230-owo/PCL-N/pulls)
![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/MuXue1230-owo/PCL-N/build-test.yml)
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/MuXue1230-owo/PCL-N/total)

[下載最新版本](https://github.com/MuXue1230-owo/PCL-N/releases/latest) |
[提交問題](https://github.com/MuXue1230-owo/PCL-N/issues/new/choose) |
[贊助](https://ifdian.net/a/pclne)

</div>

**PCL N Edition**（Plain Craft Launcher N Edition）是由 [MUXUE1230](https://github.com/MuXue1230-owo) 獨立開發和維護的 Minecraft 啟動器。

目前主線基於 **.NET 10 + Avalonia 12** 重寫，面向 Windows / Linux / macOS，提供單檔案發佈與模組化架構。版本號與 PCL / PCL-CE 主線**並非嚴格對應**，請不要向其他儲存庫回饋 PCL N 的問題。

歡迎試用與回饋！

## ✨ 主要特性

- **跨平台桌面殼**：`PCL.Desktop` 基於 Avalonia，支援 win / linux / osx 的 x64 與 arm64
- **模組化核心**：可攜式核心、領域模型、應用服務、平台抽象與 UI 抽象分層
- **啟動與實例管理**：版本安裝、Java 選擇、啟動參數規劃、實例中繼資料與匯出
- **帳號體系**：微軟正版、離線、第三方 / Authlib-Injector 等登入流程
- **下載與資源**：Minecraft 用戶端 / 資源 / 函式庫下載規劃與任務管理
- **外掛宿主**：內建 HostModule 橋接與第三方 `.pnp` 外掛執行階段（簽章驗證、隔離載入）
- **發佈形態**：自包含（SelfContained）與依賴執行階段（NoRuntime）兩種產物，發佈包附帶 GPG 簽章

## 🏗 儲存庫結構

主解決方案：[`PCL-N.slnx`](PCL-N.slnx)

| 專案 | 說明 |
|---|---|
| `PCL.Core.Portable` | 可攜式核心原語（IO、工具、Minecraft 協定相關等），Native AOT 友善 |
| `PCL.Domain` | 領域模型 |
| `PCL.Platform.Abstractions` / `PCL.Platform` | 平台能力抽象與預設實作（路徑、程序、系統資訊、Java 定位等） |
| `PCL.Application` | 應用服務層（帳號、下載、實例、啟動、設定等） |
| `PCL.UI.Abstractions` | 與 UI 無關的命令、導覽、主題、通知等抽象 |
| `PCL.Desktop` | Avalonia 桌面殼與功能頁面 |
| `PCL.Desktop.SourceGenerators` | 桌面導覽 / 設定 / 下載 / 實例頁面註冊原始碼產生器 |
| `*.Test` / `*.AotSmoke` | 單元測試與 AOT 冒煙測試 |

相關儲存庫與目錄：

- 第三方外掛公開契約：[PCL-N-Plugin-SDK](https://github.com/MuXue1230-owo/PCL-N-Plugin-SDK)
- 私有外掛執行階段與內建 HostModule：`PCL.Plugin/`（見其 README）
- 線上服務端：`PCL.Server/`（獨立部署，見其 README）

## 💻 支援平台

| 平台 | 架構 | 支援情況 |
|---|---|---|
| Windows 10 / 11 | x64、ARM64 | ✅ 完整支援 |
| Linux | x64、ARM64 | ✅ 支援（發行版差異請以最新版本實測為準） |
| macOS | x64、ARM64 | ✅ 支援 |
| 更舊的 Windows / 其他系統 | — | ❌ 不保證可用 |

**發佈產物命名慣例**（Release）：

- `PCL_N_Release_<rid>_SelfContained`：自包含，無需預先安裝 .NET
- `PCL_N_Release_<rid>_NoRuntime`：體積更小，需要本機安裝 [.NET 10 執行階段](https://dotnet.microsoft.com/download/dotnet/10.0)

其中 `<rid>` 為 `win-x64`、`win-arm64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64`。

**說明**：

- 社群僅對**最新版本**提供支援
- 建議使用較新的作業系統與顯示卡驅動以獲得最佳體驗
- 在不受支援的環境上仍可嘗試執行，但可能遇到額外問題

## 🛠 從原始碼建置

**環境需求**：[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（倉庫 `global.json` 固定 SDK 策略）

```powershell
# 還原與建置主解決方案
dotnet restore .\PCL-N.slnx
dotnet build .\PCL-N.slnx -c Release

# 執行桌面啟動器（開發）
dotnet run --project .\PCL.Desktop\PCL.Desktop.csproj

# 執行測試
dotnet test .\PCL-N.slnx -c Release

# 發佈單檔案（範例：Windows x64 自包含）
dotnet publish .\PCL.Desktop\PCL.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\artifacts\win-x64
```

完整多平台發佈由 GitHub Actions 的 `release-stable_publish.yml` / `release-beta_publish.yml` 完成。

## 🔒 授權條款

本儲存庫程式碼預設使用 [Apache License 2.0](LICENSE)。

第三方相依的授權資訊見 `PCL.Desktop/metadata.json` 與各專案 NOTICE（如有）。

## 🌟 統計資料

![Alt](https://repobeats.axiom.co/api/embed/803751b94f0b1e8682bf9b5aba0f0dd9f2d156fd.svg "Repobeats analytics image")

[![Star History Chart](https://api.star-history.com/svg?repos=MuXue1230-owo/PCL-N&type=Date)](https://www.star-history.com/#MuXue1230-owo/PCL-N&Date)

**此頁瀏覽量**（總計 / 今日）：[![Hits](https://hits.zkitefly.eu.org/?tag=https://github.com/MuXue1230-owo/PCL-N)](https://hits.zkitefly.eu.org/?tag=https://github.com/MuXue1230-owo/PCL-N&web=true)
