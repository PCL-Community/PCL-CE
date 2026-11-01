import AppKit
import SwiftUI

struct PCLInstanceNavigationRow: View {
    let title: String
    let icon: String
    let selected: Bool
    let accessory: String?
    let accessoryTitle: String?
    let action: () -> Void
    let accessoryAction: () -> Void
    @State private var hovering = false

    var body: some View {
        ZStack(alignment: .trailing) {
            Button(action: action) {
                HStack(spacing: 12) {
                    PCLInstanceIcon(name: icon).frame(width: 18, height: 18)
                    Text(title).font(.custom("PCL English", size: 14)).lineLimit(1).fixedSize(horizontal: true, vertical: false)
                    Spacer(minLength: 35)
                }
                .foregroundStyle(selected ? ClassicTheme.blue : ClassicTheme.text)
                .padding(.leading, 20).padding(.trailing, 12).frame(height: 46).contentShape(Rectangle())
                .background(selected ? ClassicTheme.blue.opacity(0.08) : hovering ? ClassicTheme.blue.opacity(0.045) : .clear)
                .overlay(alignment: .leading) { if selected { Rectangle().fill(ClassicTheme.blue).frame(width: 3) } }
            }.buttonStyle(.plain)
                .accessibilityActions {
                    if let accessoryTitle { Button(accessoryTitle, action: accessoryAction) }
                }
            if let accessory, let accessoryTitle {
                Button(action: accessoryAction) {
                    PCLInstanceIcon(name: accessory).frame(width: 14, height: 14)
                        .foregroundStyle(ClassicTheme.blue).frame(width: 25, height: 25).contentShape(Rectangle())
                }
                .buttonStyle(.plain).padding(.trailing, 12)
                .help(accessoryTitle).accessibilityLabel(accessoryTitle)
                .opacity(hovering ? 1 : 0).allowsHitTesting(hovering).accessibilityHidden(!hovering)
            }
        }
        .onHover { hovering = $0 }
        .animation(.linear(duration: 0.12), value: hovering)
    }
}

struct PCLInstanceIcon: View {
    let name: String
    private static var images: [String: NSImage] = {
        var result: [String: NSImage] = [:]
        for name in ["Overview", "LaunchSettings", "Modify", "Mods", "Export", "Reset", "Refresh"] {
            if let url = PCLResources.url(name, extension: "pdf", subdirectory: "InstanceNavigation"), let image = NSImage(contentsOf: url) { result[name] = image }
        }
        return result
    }()
    var body: some View {
        Group {
            if let image = Self.images[name] { Image(nsImage: image).renderingMode(.template).resizable().scaledToFit() }
            else { PCLIcon("Vanilla") }
        }.accessibilityHidden(true)
    }
}

@MainActor extension LauncherStore {
    func selectVersionSettingsTab(_ tab: VersionSettingsTab) {
        guard !isBusy, versionWindowInputError == nil else { return }
        saveVersionSettings()
        guard profileSaveError == nil else { return }
        versionSettingsTab = tab
        if tab == .modify { prepareVersionModification() }
    }
}
