[简体中文](README.md) | [English](README-EN.md) | **繁體中文**

<div align="center">

<img src="PCL.Launcher/Assets/Images/icon.svg" alt="Logo" width="80" height="80">

# PCL Community Edition

[![Stars](https://img.shields.io/github/stars/PCL-Community/PCL-CE?style=for-the-badge&logo=data:image/svg%2bxml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZlcnNpb249IjEiIHdpZHRoPSIxNiIgaGVpZ2h0PSIxNiI+PHBhdGggZD0iTTggLjI1YS43NS43NSAwIDAgMSAuNjczLjQxOGwxLjg4MiAzLjgxNSA0LjIxLjYxMmEuNzUuNzUgMCAwIDEgLjQxNiAxLjI3OWwtMy4wNDYgMi45Ny43MTkgNC4xOTJhLjc1MS43NTEgMCAwIDEtMS4wODguNzkxTDggMTIuMzQ3bC0zLjc2NiAxLjk4YS43NS43NSAwIDAgMS0xLjA4OC0uNzlsLjcyLTQuMTk0TC44MTggNi4zNzRhLjc1Ljc1IDAgMCAxIC40MTYtMS4yOGw0LjIxLS42MTFMNy4zMjcuNjY4QS43NS43NSAwIDAgMSA4IC4yNVoiIGZpbGw9IiNlYWM1NGYiLz48L3N2Zz4=&logoSize=auto&label=stars&labelColor=444444&color=eac54f)](https://github.com/PCL-Community/PCL-CE/)
![GitHub Release](https://img.shields.io/github/v/release/PCL-Community/PCL-CE?label=release&logo=github&style=for-the-badge)
![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/PCL-Community/PCL-CE/build-test.yml?style=for-the-badge)

[![Issues](https://img.shields.io/github/issues/PCL-Community/PCL-CE?style=for-the-badge&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/PCL-Community/PCL-CE/issues)
[![Pull requests](https://img.shields.io/github/issues-pr/PCL-Community/PCL-CE?style=for-the-badge&label=pull%20requests&labelColor=444444&color=1F883D&logo=github)](https://github.com/PCL-Community/PCL-CE/pulls)
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/PCL-Community/PCL-CE/total?style=for-the-badge)
[![嗶哩嗶哩](https://img.shields.io/badge/貼文-bilibili-00A4DB?style=for-the-badge&labelColor=444444&logo=bilibili)](https://space.bilibili.com/3546847192811755/dynamic) <br />

<a href="https://trendshift.io/repositories/21800?utm_source=trendshift-badge&amp;utm_medium=badge&amp;utm_campaign=badge-trendshift-21800" target="_blank" rel="noopener noreferrer"><img src="https://trendshift.io/api/badge/trendshift/repositories/21800/daily?language=C%23" alt="PCL-Community%2FPCL-CE | Trendshift" width="250" height="55"/></a>
<a href="https://trendshift.io/repositories/21800" target="_blank"><img src="https://trendshift.io/api/badge/repositories/21800" alt="PCL-Community%2FPCL-CE | Trendshift" style="width: 250px; height: 55px;" width="250" height="55"/></a>

[社群版下載](https://github.com/PCL-Community/PCL-CE/releases/latest) |
[上游儲存庫](https://github.com/Meloong-Git/PCL)

[提交問題](https://github.com/PCL-Community/PCL-CE/issues/new/choose) |
[貢獻指南](https://github.com/PCL-Community/PCL-CE/wiki/开发指南)

</div>

PCL CE 是基於 PCL 開放原始碼二次開發的社群版本，包括了主線暫未製作的功能和改進！

社群版的版本號與主線並非嚴格對應關係，也請不要向官方儲存庫回饋社群版問題。

歡迎大家來用用看！

**開發群:** [599620549](https://qm.qq.com/q/PNQA4Md8wc) | **使用者 1 群:** [1028074835](https://qm.qq.com/q/qrMGJo8BQQ) | **使用者 2 群:** [1070029541](https://qm.qq.com/q/lBywbDMWhG)

## 💻 支援平台

### Windows

| 版本 | 架構 | 支援情況 | 環境要求 |
|---|---|---|---|
| Windows 11 24H2 (E) / 25H2 或以上 | x64、ARM64 | ✅ 完整支援 | [.NET 10 Runtime](https://get.dot.net/10) |
| Windows 10 21H2 / 22H2 | x64、ARM64 | ✅ 完整支援 | [.NET 10 Runtime](https://get.dot.net/10) |
| Windows Server 2022 (20348) 或以上 | x64、ARM64 | ✅ 完整支援 | [.NET 10 Runtime](https://get.dot.net/10) |
| Windows 10 1607 (E) / 1809 (E) | x64 | ⚠️ 有限支援 | [.NET 10 Runtime](https://get.dot.net/10) |
| 其他 Windows 11 / 10 / Windows Server、Windows 8.x、7 或更低版本 | / | ❌ 不支援 | / |

`(E)` 指 Enterprise LTSC / ESU / Server 版本。

位於官方支援週期內的 Windows Insider Preview 版本通常一併受到完整支援，但部分功能有時可能無法正常運作。遇到問題可以嘗試提交 Issue。

### Linux

| 發行版 | 架構 | 支援情況 | 環境要求 |
|---|---|---|---|
| 常見 glibc 桌面發行版<br>（Ubuntu 22.04 及以上、Fedora 43 及以上、Arch Linux 等） | x64 | ✅ 支援 | [.NET 10 Runtime](https://get.dot.net/10)、X11、Wayland 實驗 |
| 同上 | ARM64 | ✅ 支援 | [.NET 10 Runtime](https://get.dot.net/10)、X11 |
| 其他 glibc 桌面發行版 | x64、ARM64 | ⚠️ 有限支援 | 需要 glibc 2.27 及以上，並能使用 X11 桌面 |
| Alpine 等 musl 發行版 | x64、ARM64 | ❌ 不支援 | 目前產物尚未在 musl 環境驗證 |

預設使用 X11。設定 `PCL_USE_WAYLAND=1` 可以嘗試原生 Wayland。如果初始化失敗，會自動回退到 X11。使用 Wayland 還需要安裝 `libwayland-client` 和 `libxkbcommon`。

### macOS

| 版本 | 架構 | 支援情況 | 環境要求 |
|---|---|---|---|
| macOS 15 Sequoia 或以上 | Apple Silicon (ARM64)、Intel (x64) | ✅ 支援 | [.NET 10 Runtime](https://get.dot.net/10) |
| macOS 12 Monterey ~ 14 Sonoma | Apple Silicon (ARM64)、Intel (x64) | ⚠️ 有限支援 | [.NET 10 Runtime](https://get.dot.net/10) |
| macOS 11 Big Sur 或更低版本 | / | ❌ 不支援 | / |

最新的 macOS Beta 版通常一併受到完整支援，但部分功能有時可能無法正常運作。遇到問題可以嘗試提交 Issue。

**✅ 完整支援**：會盡量提供支援，但請先確保使用最新版本。

**⚠️ 有限支援**：這些平台通常可以執行，但無法保證所有功能正常。遇到問題時，社群會視情況協助。可能需要先升級到完整支援的平台才可以獲得進一步支援。

**❌ 不支援**：這些平台基本無法正常使用，甚至可能無法啟動。請升級系統後再執行 PCL CE。

**注**：
- 社群只為最新版本的啟動器提供支援。
- 有些問題與系統環境有關。例如系統元件不完整時，仍需升級系統才能繼續獲得支援。
- 建議使用較新的作業系統，以獲得更好的體驗。
- 在不受支援的系統上仍可嘗試執行，但可能遇到更多問題。

## 🔒 授權條款

- `PCL.Launcher/` 和 `PCL.Launcher.Core/` 使用 [自訂授權條款](https://github.com/PCL-Community/PCL-CE/blob/dev/PCL.Launcher.Core/LICENCE)
- `其餘所有目錄` 使用 [Apache License 2.0](https://github.com/PCL-Community/PCL-CE/blob/dev/LICENSE)

## 🌟 統計資料
![Alt](https://repobeats.axiom.co/api/embed/3e46296e6e3a134991a783480fd2f62723bb0353.svg "Repobeats analytics image")

[![Star History Chart](https://api.star-history.com/svg?repos=PCL-Community/PCL-CE&type=Date)](https://www.star-history.com/#PCL-Community/PCL-CE&Date)

**此頁瀏覽量**（總計 / 今日）：[![Hits](https://hits.zkitefly.eu.org/?tag=https://github.com/PCL-Community/PCL-CE)](https://hits.zkitefly.eu.org/?tag=https://github.com/PCL-Community/PCL-CE&web=true)
## ❤️ 貢獻者

[![](https://contrib.rocks/image?repo=PCL-Community/PCL-CE)](https://github.com/PCL-Community/PCL-CE/graphs/contributors)
