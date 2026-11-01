import AppKit
import SwiftUI

extension Color {
    static let pclBlue = ClassicTheme.blue
}

struct PCLCard<Content: View>: View {
    let title: String
    let collapsible: Bool
    let content: Content
    @State private var expanded: Bool

    init(title: String, collapsible: Bool = false, expanded: Bool = true, @ViewBuilder content: () -> Content) {
        self.title = title; self.collapsible = collapsible; self.content = content()
        _expanded = State(initialValue: expanded)
    }

    var body: some View {
        VStack(spacing: 0) {
            if collapsible {
                Button {
                    withAnimation(.easeInOut(duration: 0.15)) { expanded.toggle() }
                } label: { header }.buttonStyle(.plain)
            } else { header }
            if expanded || !collapsible {
                VStack(alignment: .leading, spacing: 10) { content }
                    .frame(maxWidth: .infinity, alignment: .leading).padding(.horizontal, 20).padding(.bottom, 15)
            }
        }.pclCardBox()
    }

    private var header: some View {
        HStack {
            Text(title).font(.custom("PCL English", size: 13).weight(.bold)).foregroundStyle(Color(red: 0.20, green: 0.24, blue: 0.29))
            Spacer()
            if collapsible { PCLIcon(expanded ? "ChevronUp" : "ChevronDown").frame(width: 10, height: 10).foregroundStyle(Color.pclBlue) }
        }.padding(.horizontal, 15).frame(height: 40).contentShape(Rectangle())
    }
}

struct PCLActionStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    var highlight = false
    var destructive = false
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.font(.custom("PCL English", size: 13))
            .foregroundStyle(destructive ? Color.red.opacity(0.8) : (highlight ? Color.pclBlue : Color(red: 0.35, green: 0.37, blue: 0.39)))
            .padding(.horizontal, 14).frame(minHeight: 35)
            .background(configuration.isPressed ? Color.pclBlue.opacity(0.10) : Color.white.opacity(0.75), in: RoundedRectangle(cornerRadius: 3))
            .overlay { RoundedRectangle(cornerRadius: 3).strokeBorder(destructive ? Color.red.opacity(0.7) : (highlight ? Color.pclBlue : Color.gray.opacity(0.65)), lineWidth: 1) }
            .contentShape(Rectangle())
            .opacity(isEnabled ? 1 : 0.45)
    }
}

struct PCLListStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.padding(.horizontal, 5)
            .background(configuration.isPressed ? Color.pclBlue.opacity(0.09) : .clear, in: RoundedRectangle(cornerRadius: 3))
    }
}

struct PCLResourceImage: View {
    let name: String
    var extensionName = "png"
    let fallback: String
    var body: some View {
        Group {
            if let url = PCLResources.url(name, extension: extensionName), let image = NSImage(contentsOf: url) {
                Image(nsImage: image).resizable().interpolation(.high).scaledToFit()
            } else {
                PCLIcon(fallback).foregroundStyle(Color.pclBlue.opacity(0.85))
            }
        }.accessibilityHidden(true)
    }
}

extension View {
    func pclCardBox() -> some View {
        frame(maxWidth: .infinity, alignment: .leading)
            .background(Color.white.opacity(0.96), in: RoundedRectangle(cornerRadius: 5))
            .shadow(color: Color.pclBlue.opacity(0.12), radius: 3, y: 1)
    }
}
