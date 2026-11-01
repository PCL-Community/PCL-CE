import AppKit
import SwiftUI

/// Original PCL geometries and bitmap fallbacks. Provenance is preserved in
/// Resources/SourceIcons/source-map.json; rebuild with make-source-icons.py.
enum PCLSourceIcons {
    static let names: Set<String> = [
        "About",
        "Account",
        "AccountOffline",
        "AccountPremium",
        "Add",
        "AddFolder",
        "ArrowLeft",
        "ArrowRight",
        "Check",
        "ChevronDown",
        "ChevronLeft",
        "ChevronRight",
        "ChevronUp",
        "Circle",
        "CircleFilled",
        "Clear",
        "Close",
        "Datapack",
        "Date",
        "DefaultMod",
        "Delete",
        "Disable",
        "Download",
        "Enable",
        "Export",
        "Favorite",
        "FavoriteFilled",
        "Feedback",
        "Folder",
        "GameVersion",
        "Globe",
        "Help",
        "Homepage",
        "ImportPack",
        "Info",
        "Launch",
        "Log",
        "Mod",
        "ModDisabled",
        "Modpack",
        "Online",
        "OpenFile",
        "PageFirst",
        "PageLast",
        "PageNext",
        "PagePrevious",
        "Personalize",
        "Refresh",
        "Reset",
        "Resourcepack",
        "Restore",
        "Search",
        "SelectionCheck",
        "Settings",
        "SetupLaunch",
        "SetupOther",
        "Shader",
        "Skin",
        "TaskCancel",
        "TaskFailure",
        "TaskManager",
        "TaskSuccess",
        "TaskWaiting",
        "Tools",
        "Update",
        "Vanilla",
        "Vote",
        "Warning",
]
    private static let bitmapNames: Set<String> = ["DefaultMod", "ModDisabled"]
    private static let images: [String: NSImage] = {
        var result: [String: NSImage] = [:]
        for name in names {
            let bitmap = bitmapNames.contains(name)
            guard let url = PCLResources.url(name, extension: bitmap ? "png" : "pdf", subdirectory: "SourceIcons"),
                  let image = NSImage(contentsOf: url) else { continue }
            image.isTemplate = !bitmap
            result[name] = image
        }
        return result
    }()

    static func image(_ name: String) -> NSImage? { images[name] }
}

/// Resizable source artwork; callers retain each original control's icon size.
/// Ideal dimensions keep unframed labels compact without overriding explicit frames.
struct PCLIcon: View {
    let name: String
    init(_ name: String) { self.name = name }
    var body: some View {
        Group {
            if let image = PCLSourceIcons.image(name) ?? PCLSourceIcons.image("DefaultMod") {
                Image(nsImage: image)
                    .renderingMode(image.isTemplate ? .template : .original)
                    .resizable().interpolation(.high).scaledToFit()
            } else {
                Color.clear
            }
        }
        .frame(idealWidth: 14, idealHeight: 14)
        .accessibilityHidden(true)
    }
}

struct PCLIconLabel: View {
    let title: String
    let icon: String
    init(_ title: String, icon: String) { self.title = title; self.icon = icon }
    var body: some View {
        Label { Text(title) } icon: { PCLIcon(icon).frame(width: 14, height: 14) }
    }
}
