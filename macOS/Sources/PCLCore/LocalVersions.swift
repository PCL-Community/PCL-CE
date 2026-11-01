import Foundation

public enum LocalVersionScanner {
    public static func scan(gameDirectory: URL) throws -> [InstalledVersion] {
        let root = gameDirectory.appendingPathComponent("versions", isDirectory: true)
        guard FileManager.default.fileExists(atPath: root.path) else { return [] }
        let children = try FileManager.default.contentsOfDirectory(at: root, includingPropertiesForKeys: [.isDirectoryKey], options: [.skipsHiddenFiles])
        return children.compactMap { directory -> InstalledVersion? in
            guard (try? directory.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) == true,
                  let metadata = try? MinecraftVersionMetadata.load(versionID: directory.lastPathComponent, gameDirectory: gameDirectory) else { return nil }
            let resolved = (try? MinecraftVersionMetadata.resolve(versionID: directory.lastPathComponent, gameDirectory: gameDirectory)) ?? metadata
            let libraries = (resolved["libraries"] as? [[String: Any]] ?? []).compactMap { $0["name"] as? String }
            let intelNatives = libraries.contains { $0.hasPrefix("org.lwjgl.lwjgl:lwjgl:") }
            let lite = (try? LiteLoaderInstaller.detect(metadata: resolved))?.version
            let opti = (try? OptiFineInstaller.detect(metadata: resolved))?.version
            return InstalledVersion(id: directory.lastPathComponent, type: metadata["type"] as? String ?? "custom",
                                    requiredJava: MinecraftVersionMetadata.requiredJava(resolved), directory: directory,
                                    inheritsFrom: metadata["inheritsFrom"] as? String,
                                    modLoader: MinecraftVersionMetadata.modLoader(in: resolved), liteLoaderVersion: lite,
                                    optiFineVersion: opti, requiredJavaArchitecture: intelNatives ? "x86_64" : nil)
        }.sorted { $0.id.localizedStandardCompare($1.id) == .orderedDescending }
    }
}
