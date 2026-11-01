import PCLCore
import SwiftUI

struct VersionModifyView: View {
    @EnvironmentObject private var store: LauncherStore
    var body: some View {
        let state = store.versionModifyState
        Group {
            if state.loading && state.originalName.isEmpty {
                PCLCard(title: "") { HStack { ProgressView().controlSize(.small); Text("正在读取已安装组件") }.padding(.vertical, 20) }
            } else {
                if let error = state.error { PCLHint(text: error, warning: true) }
                PCLCard(title: "") {
                    HStack(spacing: 12) {
                        PCLResourceImage(name: "Grass", fallback: "GameVersion").frame(width: 32, height: 32)
                        TextField("版本名称", text: Binding(get: { state.newName }, set: { state.newName = $0 })).textFieldStyle(.roundedBorder)
                        if state.newName != state.originalName {
                            Text("原名：\(state.originalName)").font(.custom("PCL English", size: 11)).foregroundStyle(.secondary)
                            Button("恢复") { state.newName = state.originalName }.buttonStyle(PCLActionStyle())
                        }
                    }
                }
                PCLCard(title: "Minecraft") {
                    HStack(spacing: 12) {
                        Text(state.originalMinecraft).foregroundStyle(.secondary)
                        PCLIcon("ArrowRight").frame(width: 14, height: 14).foregroundStyle(Color.pclBlue)
                        PCLComboBox(title: "Minecraft 版本", selection: Binding(get: { state.minecraft }, set: { store.chooseModificationMinecraft($0) }),
                                    options: minecraftOptions)
                            .frame(maxWidth: .infinity).frame(height: 28)
                        if state.minecraft != state.originalMinecraft { Button("恢复") { store.chooseModificationMinecraft(state.originalMinecraft) }.buttonStyle(PCLActionStyle()) }
                    }
                }
                ForEach(ModLoaderKind.allCases, id: \.self) { kind in
                    loader(kind)
                }
                if state.loaderKind == .fabric || (state.originalLoader?.kind == .fabric && state.originalFabricAPI?.files.isEmpty == false) {
                    fabricAPI
                }
                if state.loaderKind == .fabric, state.fabricAPIDisplay == "无", !state.loadingFabricAPI {
                    PCLHint(text: "尚未选择 Fabric API，许多 Fabric Mod 需要它才能正常运行。", warning: true)
                }
                ForEach(ExtraComponentKind.allCases, id: \.self) { kind in extraComponent(kind) }
                if let error = store.versionModificationError, state.changed { PCLHint(text: error, warning: true) }
                HStack {
                    Button("恢复原始选择") { store.restoreModificationComponents() }.buttonStyle(PCLActionStyle())
                    Spacer()
                    Button("开始修改") { store.startVersionModification() }.buttonStyle(PCLActionStyle(highlight: true))
                        .disabled(!state.changed || store.versionModificationError != nil || store.isBusy || store.isGameRunning)
                }.padding(.top, 4)
            }
        }.font(.custom("PCL English", size: 13))
            .onAppear { store.prepareVersionModification() }
    }

    private var minecraftOptions: [PCLComboBoxOption<String>] {
        let state = store.versionModifyState
        let entries = state.minecraftVersions.map { PCLComboBoxOption(title: $0.id, value: $0.id) }
        return state.minecraftVersions.contains(where: { $0.id == state.minecraft })
            ? entries : [PCLComboBoxOption(title: state.minecraft, value: state.minecraft)] + entries
    }

    private func loader(_ kind: ModLoaderKind) -> some View {
        let state = store.versionModifyState
        return PCLCard(title: kind.displayName) {
            VStack(alignment: .leading, spacing: 8) {
                Button { store.toggleModificationLoader(kind) } label: {
                    HStack(spacing: 12) {
                        PCLResourceImage(name: kind == .forge ? "Anvil" : kind == .neoForge ? "NeoForge" : "Fabric", fallback: "Mod").frame(width: 28, height: 28)
                        Text(state.originalLoader?.kind == kind ? state.originalLoader!.version : "无").foregroundStyle(.secondary)
                        PCLIcon("ArrowRight").frame(width: 14, height: 14).foregroundStyle(Color.pclBlue)
                        Text(store.modificationLoaderConflict(kind) ?? (state.loaderKind == kind ? state.loaderVersion.isEmpty ? "请选择版本" : state.loaderVersion : "未选择"))
                        Spacer(); PCLIcon(state.expandedLoader == kind ? "ChevronUp" : "ChevronDown").frame(width: 11, height: 11).foregroundStyle(Color.pclBlue)
                    }.contentShape(Rectangle())
                }.buttonStyle(.plain).disabled(store.modificationLoaderConflict(kind) != nil || store.isBusy || store.isGameRunning)
                if (state.loaderKind == kind || state.originalLoader?.kind == kind),
                   state.loaderKind != state.originalLoader?.kind || state.loaderVersion != (state.originalLoader?.version ?? "") {
                    Button("恢复") { store.restoreModificationLoader(kind) }.buttonStyle(PCLActionStyle())
                        .disabled(state.originalLoader?.kind == kind && state.minecraft != state.originalMinecraft)
                }
                if state.expandedLoader == kind {
                    if state.loadingLoader { HStack { ProgressView().controlSize(.small); Text("正在获取版本列表") } }
                    else {
                        Button("不安装 \(kind.displayName)") { store.chooseModificationLoader(nil) }.buttonStyle(PCLActionStyle())
                        if !store.modificationLoaderVersions(kind).isEmpty {
                            let entries = store.modificationLoaderVersions(kind)
                            ScrollView {
                                LazyVStack(alignment: .leading, spacing: 2) {
                                    ForEach(entries) { entry in
                                        Button { store.chooseModificationLoader(entry) } label: {
                                            HStack { Text(entry.version); Spacer(); if state.loaderKind == kind && state.loaderVersion == entry.version { PCLIcon("Check").frame(width: 12, height: 12) } }
                                                .padding(.vertical, 6).contentShape(Rectangle())
                                        }.buttonStyle(.plain).foregroundStyle(Color.pclBlue)
                                    }
                                }
                            }.frame(maxHeight: 220)
                        } else { Text("没有适用于此 Minecraft 版本的可安装版本。").foregroundStyle(.secondary) }
                    }
                }
            }
        }
    }

    private var fabricAPI: some View {
        let state = store.versionModifyState
        return PCLCard(title: "Fabric API") {
            VStack(alignment: .leading, spacing: 8) {
                HStack(spacing: 12) {
                    Button { store.toggleModificationFabricAPI() } label: {
                        HStack(spacing: 12) {
                            PCLResourceImage(name: "Fabric", fallback: "Mod").frame(width: 28, height: 28)
                            Text(state.originalFabricAPI?.displayVersion ?? "无").foregroundStyle(.secondary)
                            PCLIcon("ArrowRight").frame(width: 14, height: 14).foregroundStyle(Color.pclBlue)
                            Text(state.originalFabricAPI == nil ? "正在读取已安装组件" : state.fabricAPIDisplay)
                            Spacer()
                            PCLIcon(state.expandedFabricAPI ? "ChevronUp" : "ChevronDown").frame(width: 11, height: 11).foregroundStyle(Color.pclBlue)
                        }.contentShape(Rectangle())
                    }.buttonStyle(.plain).disabled(state.loaderKind != .fabric || state.originalFabricAPI == nil)
                    if state.fabricAPIChanged {
                        Button("恢复") { store.restoreModificationFabricAPI() }.buttonStyle(PCLActionStyle())
                            .disabled(state.minecraft != state.originalMinecraft || state.loaderKind != state.originalLoader?.kind)
                    }
                    if state.fabricAPIDisplay != "无" {
                        Button("清除") { store.chooseModificationFabricAPI(nil) }.buttonStyle(PCLActionStyle())
                    }
                }
                if let error = state.fabricAPIError {
                    PCLHint(text: error, warning: true)
                    Button("重试") { store.loadModificationFabricAPI() }.buttonStyle(PCLActionStyle())
                }
                if state.expandedFabricAPI {
                    if state.loadingFabricAPI { HStack { ProgressView().controlSize(.small); Text("正在获取 Fabric API 版本") } }
                    else {
                        Button("不安装 Fabric API") { store.chooseModificationFabricAPI(nil) }.buttonStyle(PCLActionStyle())
                        if state.fabricAPIVersions.isEmpty {
                            Text("没有适用于此 Minecraft 版本的 Fabric API。").foregroundStyle(.secondary)
                        } else {
                            ScrollView {
                                LazyVStack(alignment: .leading, spacing: 2) {
                                    ForEach(state.fabricAPIVersions) { entry in
                                        Button { store.chooseModificationFabricAPI(entry) } label: {
                                            HStack {
                                                Text(entry.versionNumber)
                                                Spacer()
                                                if state.keepOriginalFabricAPI ? state.originalFabricAPI?.matches(entry) == true : state.selectedFabricAPI?.id == entry.id {
                                                    PCLIcon("Check").frame(width: 12, height: 12)
                                                }
                                            }.padding(.vertical, 6).contentShape(Rectangle())
                                        }.buttonStyle(.plain).foregroundStyle(Color.pclBlue)
                                    }
                                }
                            }.frame(maxHeight: 220)
                        }
                    }
                }
            }
        }
    }

    private func extraComponent(_ kind: ExtraComponentKind) -> some View {
        let state = store.versionModifyState
        let value = store.modificationExtraDisplay(kind)
        let oldValue = store.modificationExtraDisplay(kind, original: true)
        let unavailable = store.modificationExtraUnavailableReason(kind)
        return PCLCard(title: kind.title) {
            VStack(alignment: .leading, spacing: 8) {
                HStack(spacing: 12) {
                    Button { store.toggleModificationExtraCatalogue(kind) } label: {
                        HStack(spacing: 12) {
                            PCLResourceImage(name: kind.icon, fallback: "Mod").frame(width: 28, height: 28)
                            Text(oldValue).foregroundStyle(.secondary)
                            PCLIcon("ArrowRight").frame(width: 14, height: 14).foregroundStyle(Color.pclBlue)
                            Text(state.originalExtras == nil ? "正在读取已安装组件" : (value == "无" ? unavailable ?? value : value))
                            Spacer()
                            PCLIcon(state.extraCatalogue.expanded == kind ? "ChevronUp" : "ChevronDown").frame(width: 11, height: 11).foregroundStyle(Color.pclBlue)
                        }.contentShape(Rectangle())
                    }.buttonStyle(.plain).disabled(unavailable != nil || state.originalExtras == nil)
                        .accessibilityLabel("选择 \(kind.title)，\(value)")
                    if store.modificationExtraChanged(kind) {
                        Button("恢复") { store.restoreModificationExtra(kind) }.buttonStyle(PCLActionStyle())
                            .disabled(state.minecraft != state.originalMinecraft || (unavailable != nil && oldValue != "无"))
                            .accessibilityLabel("恢复 \(kind.title)")
                    }
                    if value != "无" {
                        Button("清除") { store.chooseModificationExtra(nil, kind: kind) }.buttonStyle(PCLActionStyle())
                            .accessibilityLabel("清除 \(kind.title)")
                    }
                }.disabled(store.isBusy || store.isGameRunning)
                if state.extraCatalogue.expanded == kind {
                    if state.extraCatalogue.loading.contains(kind) {
                        HStack { ProgressView().controlSize(.small); Text("正在获取 \(kind.title) 版本列表") }
                    } else if let error = state.extraCatalogue.errors[kind] {
                        PCLHint(text: error, warning: true)
                        Button("重新获取") { store.loadModificationExtraCatalogue(kind) }.buttonStyle(PCLActionStyle())
                    } else {
                        Button("不安装 \(kind.title)") { store.chooseModificationExtra(nil, kind: kind) }.buttonStyle(PCLActionStyle())
                        if store.modificationExtraVersions(kind).isEmpty {
                            Text("没有适用于此 Minecraft 和组件组合的版本。").foregroundStyle(.secondary)
                        } else {
                            let entries = store.modificationExtraVersions(kind)
                            ScrollView {
                                LazyVStack(alignment: .leading, spacing: 2) {
                                    ForEach(entries) { entry in
                                        Button { store.chooseModificationExtra(entry, kind: kind) } label: {
                                            HStack {
                                                Text(entry.title); Spacer()
                                                if store.modificationExtraIsSelected(entry) { PCLIcon("Check").frame(width: 12, height: 12) }
                                            }.padding(.vertical, 6).contentShape(Rectangle())
                                        }.buttonStyle(.plain).foregroundStyle(Color.pclBlue)
                                    }
                                }
                            }.frame(maxHeight: 220)
                        }
                    }
                } else if let error = state.extraCatalogue.errors[kind] {
                    PCLHint(text: error, warning: true)
                    Button("重新获取") { store.loadModificationExtraCatalogue(kind) }.buttonStyle(PCLActionStyle())
                }
                if kind == .optiFabric && state.loaderKind == .fabric && state.selectedOptiFine != nil && value == "无" && !state.extraCatalogue.loading.contains(kind) {
                    PCLHint(text: "Fabric 加载 OptiFine 需要安装兼容的 OptiFabric。", warning: true)
                }
            }
        }.disabled(store.isBusy || store.isGameRunning)
    }
}
