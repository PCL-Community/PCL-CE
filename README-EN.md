[简体中文](README.md) | **English** | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="PCL.Launcher/Assets/Images/icon.svg" alt="Logo" width="80" height="80">

# PCL Community Edition

[![Stars](https://img.shields.io/github/stars/PCL-Community/PCL-CE?style=for-the-badge&logo=data:image/svg%2bxml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZlcnNpb249IjEiIHdpZHRoPSIxNiIgaGVpZ2h0PSIxNiI+PHBhdGggZD0iTTggLjI1YS43NS43NSAwIDAgMSAuNjczLjQxOGwxLjg4MiAzLjgxNSA0LjIxLjYxMmEuNzUuNzUgMCAwIDEgLjQxNiAxLjI3OWwtMy4wNDYgMi45Ny43MTkgNC4xOTJhLjc1MS43NTEgMCAwIDEtMS4wODguNzkxTDggMTIuMzQ3bC0zLjc2NiAxLjk4YS43NS43NSAwIDAgMS0xLjA4OC0uNzlsLjcyLTQuMTk0TC44MTggNi4zNzRhLjc1Ljc1IDAgMCAxIC40MTYtMS4yOGw0LjIxLS42MTFMNy4zMjcuNjY4QS43NS43NSAwIDAgMSA4IC4yNVoiIGZpbGw9IiNlYWM1NGYiLz48L3N2Zz4=&logoSize=auto&label=stars&labelColor=444444&color=eac54f)](https://github.com/PCL-Community/PCL-CE/)
![GitHub Release](https://img.shields.io/github/v/release/PCL-Community/PCL-CE?label=release&logo=github&style=for-the-badge)
![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/PCL-Community/PCL-CE/build-test.yml?style=for-the-badge)

[![Issues](https://img.shields.io/github/issues/PCL-Community/PCL-CE?style=for-the-badge&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/PCL-Community/PCL-CE/issues)
[![Pull requests](https://img.shields.io/github/issues-pr/PCL-Community/PCL-CE?style=for-the-badge&label=pull%20requests&labelColor=444444&color=1F883D&logo=github)](https://github.com/PCL-Community/PCL-CE/pulls)
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/PCL-Community/PCL-CE/total?style=for-the-badge)
[![Bilibili](https://img.shields.io/badge/Threads-bilibili-00A4DB?style=for-the-badge&labelColor=444444&logo=bilibili)](https://space.bilibili.com/3546847192811755/dynamic) <br />

<a href="https://trendshift.io/repositories/21800?utm_source=trendshift-badge&amp;utm_medium=badge&amp;utm_campaign=badge-trendshift-21800" target="_blank" rel="noopener noreferrer"><img src="https://trendshift.io/api/badge/trendshift/repositories/21800/daily?language=C%23" alt="PCL-Community%2FPCL-CE | Trendshift" width="250" height="55"/></a>
<a href="https://trendshift.io/repositories/21800" target="_blank"><img src="https://trendshift.io/api/badge/repositories/21800" alt="PCL-Community%2FPCL-CE | Trendshift" style="width: 250px; height: 55px;" width="250" height="55"/></a>

[Download](https://github.com/PCL-Community/PCL-CE/releases/latest) |
[Upstream repo](https://github.com/Meloong-Git/PCL)

[Submit issues](https://github.com/PCL-Community/PCL-CE/issues/new/choose) |
[Contribution Guidelines](https://github.com/PCL-Community/PCL-CE/wiki/开发指南)

</div>

PCL CE is a community edition developed as a secondary project based on PCL’s open-source code. It includes features and improvements that have not yet been implemented in the main branch.

The version numbers of the community edition do not strictly correspond to those of the main branch, so please do not report community edition issues to the official repository.

Everyone is welcome to try it out!

**Developing group:** [599620549](https://qm.qq.com/q/PNQA4Md8wc) | **User group 1:** [1028074835](https://qm.qq.com/q/qrMGJo8BQQ) | **User group 2:** [1070029541](https://qm.qq.com/q/lBywbDMWhG)

## 💻 Supported Platforms

### Windows

| Version | Architecture | Support | Requirements |
|---|---|---|---|
| Windows 10 1809 (17763) or later, or Windows 11 | x64, ARM64 | ✅ Fully supported | [.NET 10 Runtime](https://get.dot.net/10) |
| Windows 10 1607 (E) | x64 | ⚠️ Expected to run; community support offered at discretion | [.NET 10 Runtime](https://get.dot.net/10) |
| Windows 8.1, 8, 7 or earlier | / | ❌ Not supported | / |

<small>`(E)` means Enterprise or ESU editions. .NET 10 officially supports Windows 10 1607 and 1809 only on these editions.</small>

### Linux

| Distribution | Architecture | Support | Requirements |
|---|---|---|---|
| Common glibc desktop distributions<br>(Ubuntu 22.04 or later, Fedora 43 or later, Arch Linux, etc.) | x64 | ✅ Supported | [.NET 10 Runtime](https://get.dot.net/10), X11, Wayland experimental |
| Same as above | ARM64 | ✅ Supported | [.NET 10 Runtime](https://get.dot.net/10), X11 |
| Other glibc desktop distributions | x64, ARM64 | ⚠️ Expected to run; community support offered at discretion | Requires glibc 2.27 or later and a working X11 desktop |
| Alpine and other musl distributions | x64, ARM64 | ❌ Not supported | Current artifacts have not been tested on musl |

<small>X11 is used by default. Set `PCL_USE_WAYLAND=1` to try native Wayland. If initialization fails, it falls back to X11 automatically. Wayland also requires `libwayland-client` and `libxkbcommon`.</small>

### macOS

| Version | Architecture | Support | Requirements |
|---|---|---|---|
| macOS 12 or later | Apple Silicon (ARM64) | ✅ Supported | [.NET 10 Runtime](https://get.dot.net/10) |
| Intel-based macOS | x64 | ❌ Not supported | / |

<small>Intel (x64) macOS builds are no longer provided.</small>

**✅ Fully supported**: We will provide as much support as possible. Please make sure you are using the latest version.

**⚠️ Expected to run; community support offered at discretion**: These platforms usually work, but we cannot guarantee that every feature will work correctly. The community will help when possible. Upgrading to a supported OS version gives you more complete support.

**❌ Not supported**: These platforms generally cannot run PCL CE, and may not even start. Please upgrade your system before using PCL CE.

**Note**:
- The community only supports the latest version of the launcher.
- Some issues depend on the system environment. For example, if system components are incomplete, you may still need to upgrade your system to get further support.
- We recommend a newer OS for a better experience.
- You can still try running PCL CE on unsupported systems, but you may run into more problems.

## 🔒 License

- `PCL.Launcher/` and `PCL.Launcher.Core/` use [Custom License](https://github.com/PCL-Community/PCL-CE/blob/dev/PCL.Launcher.Core/LICENCE)
- `All other directories` use [Apache License 2.0](https://github.com/PCL-Community/PCL-CE/blob/dev/LICENSE)

## 🌟 Statistic
![Alt](https://repobeats.axiom.co/api/embed/3e46296e6e3a134991a783480fd2f62723bb0353.svg "Repobeats analytics image")

[![Star History Chart](https://api.star-history.com/svg?repos=PCL-Community/PCL-CE&type=Date)](https://www.star-history.com/#PCL-Community/PCL-CE&Date)

**Views** (Total / Today): [![Hits](https://hits.zkitefly.eu.org/?tag=https://github.com/PCL-Community/PCL-CE)](https://hits.zkitefly.eu.org/?tag=https://github.com/PCL-Community/PCL-CE&web=true)
## ❤️ Contributors

[![](https://contrib.rocks/image?repo=PCL-Community/PCL-CE)](https://github.com/PCL-Community/PCL-CE/graphs/contributors)
