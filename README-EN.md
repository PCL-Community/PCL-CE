[简体中文](README.md) | **English** | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="PCL.Desktop/Assets/icon.ico" alt="Logo" width="80" height="80">

# PCL N Edition

[![Stars](https://img.shields.io/github/stars/MuXue1230-owo/PCL-N?style=flat&logo=data:image/svg%2bxml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZlcnNpb249IjEiIHdpZHRoPSIxNiIgaGVpZ2h0PSIxNiI+PHBhdGggZD0iTTggLjI1YS43NS43NSAwIDAgMSAuNjczLjQxOGwxLjg4MiAzLjgxNSA0LjIxLjYxMmEuNzUuNzUgMCAwIDEgLjQxNiAxLjI3OWwtMy4wNDYgMi45Ny43MTkgNC4xOTJhLjc1MS43NTEgMCAwIDEtMS4wODguNzkxTDggMTIuMzQ3bC0zLjc2NiAxLjk4YS43NS43NSAwIDAgMS0xLjA4OC0uNzlsLjcyLTQuMTk0TC44MTggNi4zNzRhLjc1Ljc1IDAgMCAxIC40MTYtMS4yOGw0LjIxLS42MTFMNy4zMjcuNjY4QS43NS43NSAwIDAgMSA4IC4yNVoiIGZpbGw9IiNlYWM1NGYiLz48L3N2Zz4=&logoSize=auto&label=stars&labelColor=444444&color=eac54f)](https://github.com/MuXue1230-owo/PCL-N/)
![GitHub Release](https://img.shields.io/github/v/release/MuXue1230-owo/PCL-N?label=release&logo=github)
[![Issues](https://img.shields.io/github/issues/MuXue1230-owo/PCL-N?style=flat&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/MuXue1230-owo/PCL-N/issues)
[![Pull requests](https://img.shields.io/github/issues-pr/MuXue1230-owo/PCL-N?style=flat&label=pull%20requests&labelColor=444444&color=1F883D&logo=github)](https://github.com/MuXue1230-owo/PCL-N/pulls)
![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/MuXue1230-owo/PCL-N/build-test.yml)
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/MuXue1230-owo/PCL-N/total)

[Download](https://github.com/MuXue1230-owo/PCL-N/releases/latest) |
[Submit issues](https://github.com/MuXue1230-owo/PCL-N/issues/new/choose) |
[Sponsor](https://ifdian.net/a/pclne)

</div>

**PCL N Edition** (Plain Craft Launcher N Edition) is a Minecraft launcher independently developed and maintained by [MUXUE1230](https://github.com/MuXue1230-owo).

The current mainline is a rewrite on **.NET 10 + Avalonia 12**, targeting Windows, Linux, and macOS with single-file publishing and a modular architecture. Version numbers **do not strictly map** to PCL / PCL-CE mainline releases—please do **not** report PCL N issues to other repositories.

Feedback and contributions are welcome!

## ✨ Features

- **Cross-platform desktop shell**: `PCL.Desktop` on Avalonia, with win / linux / osx builds for x64 and arm64
- **Modular core**: portable core, domain model, application services, platform abstractions, and UI abstractions
- **Launch & instances**: version install, Java selection, launch argument planning, instance metadata and export
- **Accounts**: Microsoft, offline, and third-party / Authlib-Injector login flows
- **Downloads & assets**: client / asset / library download planning and task management
- **Plugin host**: built-in HostModule bridge and third-party `.pnp` plugin runtime (signature verification, isolated loading)
- **Release flavors**: SelfContained and NoRuntime artifacts; release packages are GPG-signed

## 🏗 Repository layout

Main solution: [`PCL-N.slnx`](PCL-N.slnx)

| Project | Description |
|---|---|
| `PCL.Core.Portable` | Portable core primitives (IO, utilities, Minecraft-related helpers); Native AOT friendly |
| `PCL.Domain` | Domain model |
| `PCL.Platform.Abstractions` / `PCL.Platform` | Platform capability abstractions and default implementations (paths, processes, system info, Java discovery, etc.) |
| `PCL.Application` | Application services (accounts, downloads, instances, launching, settings, etc.) |
| `PCL.UI.Abstractions` | UI-agnostic commands, navigation, themes, notifications, and related abstractions |
| `PCL.Desktop` | Avalonia desktop shell and feature pages |
| `PCL.Desktop.SourceGenerators` | Source generators for desktop navigation / settings / download / instance page registration |
| `*.Test` / `*.AotSmoke` | Unit tests and AOT smoke tests |

Related repos and directories:

- Public third-party plugin contracts: [PCL-N-Plugin-SDK](https://github.com/MuXue1230-owo/PCL-N-Plugin-SDK)
- Private plugin runtime and built-in HostModule: `PCL.Plugin/` (see its README)
- Online server: `PCL.Server/` (deployed separately; see its README)

## 💻 Supported platforms

| Platform | Architectures | Status |
|---|---|---|
| Windows 10 / 11 | x64, ARM64 | ✅ Fully supported |
| Linux | x64, ARM64 | ✅ Supported (distro differences depend on latest release testing) |
| macOS | x64, ARM64 | ✅ Supported |
| Older Windows / other OS | — | ❌ Not guaranteed |

**Release artifact naming** (Release channel):

- `PCL_N_Release_<rid>_SelfContained`: self-contained; no preinstalled .NET required
- `PCL_N_Release_<rid>_NoRuntime`: smaller download; requires the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

`<rid>` is one of `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.

**Notes**:

- Community support targets the **latest launcher version** only
- A recent OS and GPU driver stack is recommended
- You may still try unsupported environments, but extra issues are expected

## 🛠 Build from source

**Requirements**: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (see `global.json` for SDK policy)

```powershell
# Restore and build the main solution
dotnet restore .\PCL-N.slnx
dotnet build .\PCL-N.slnx -c Release

# Run the desktop launcher (development)
dotnet run --project .\PCL.Desktop\PCL.Desktop.csproj

# Run tests
dotnet test .\PCL-N.slnx -c Release

# Publish a single-file binary (example: Windows x64 self-contained)
dotnet publish .\PCL.Desktop\PCL.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\artifacts\win-x64
```

Full multi-platform releases are produced by GitHub Actions workflows `release-stable_publish.yml` and `release-beta_publish.yml`.

## 🔒 License

Source code in this repository is licensed under the [Apache License 2.0](LICENSE).

Third-party license notices are listed in `PCL.Desktop/metadata.json` and project NOTICE files where applicable.

## 🌟 Statistics

![Alt](https://repobeats.axiom.co/api/embed/803751b94f0b1e8682bf9b5aba0f0dd9f2d156fd.svg "Repobeats analytics image")

[![Star History Chart](https://api.star-history.com/svg?repos=MuXue1230-owo/PCL-N&type=Date)](https://www.star-history.com/#MuXue1230-owo/PCL-N&Date)

**Views** (Total / Today): [![Hits](https://hits.zkitefly.eu.org/?tag=https://github.com/MuXue1230-owo/PCL-N)](https://hits.zkitefly.eu.org/?tag=https://github.com/MuXue1230-owo/PCL-N&web=true)
