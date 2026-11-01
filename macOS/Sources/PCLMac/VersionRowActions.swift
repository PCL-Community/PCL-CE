import AppKit
import SwiftUI

/// PageSelectRight / MyIconButton: 25-point hit area, 15-point path at 1.1 scale.
struct PCLVersionActionButton: View {
    let icon: String
    let title: String
    let identifier: String
    let action: () -> Void
    @State private var hovering = false

    private static let images: [String: NSImage] = {
        var result: [String: NSImage] = [:]
        for name in ["Favorite", "FavoriteFilled", "Delete", "Settings"] {
            if let url = PCLResources.url(name, extension: "pdf", subdirectory: "VersionActions"),
               let image = NSImage(contentsOf: url) { result[name] = image }
        }
        return result
    }()

    var body: some View {
        Button(action: action) {
            Group {
                if let image = Self.images[icon] {
                    Image(nsImage: image).renderingMode(.template).resizable().scaledToFit()
                } else {
                    PCLIcon(icon)
                }
            }
            .frame(width: 16.5, height: 16.5)
            .foregroundStyle(Color.pclBlue.opacity(hovering ? 1 : 0.65))
            .frame(width: 25, height: 25).contentShape(Rectangle())
        }
        .buttonStyle(PCLVersionIconButtonStyle())
        .onHover { hovering = $0 }
        .animation(.linear(duration: hovering ? 0.12 : 0.15), value: hovering)
        .help(title).accessibilityLabel(title).accessibilityIdentifier(identifier)
    }
}

private struct PCLVersionIconButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.scaleEffect(configuration.isPressed ? 0.8 : 1)
            .animation(.easeOut(duration: 0.25), value: configuration.isPressed)
    }
}

/// Receives secondary clicks only, leaving selection, hover and the icon buttons to SwiftUI.
struct PCLVersionRowSecondaryClick: NSViewRepresentable {
    let enabled: Bool
    let action: () -> Void

    func makeNSView(context: Context) -> SecondaryClickView { SecondaryClickView() }
    func updateNSView(_ view: SecondaryClickView, context: Context) {
        view.enabled = enabled
        view.action = action
    }

    final class SecondaryClickView: NSView {
        var enabled = true
        var action: (() -> Void)?

        override func hitTest(_ point: NSPoint) -> NSView? {
            guard enabled, let event = NSApp.currentEvent,
                  event.type == .rightMouseDown || event.type == .rightMouseUp else { return nil }
            return super.hitTest(point)
        }

        override func rightMouseDown(with event: NSEvent) {}
        override func rightMouseUp(with event: NSEvent) {
            guard enabled, bounds.contains(convert(event.locationInWindow, from: nil)) else { return }
            action?()
        }
    }
}
