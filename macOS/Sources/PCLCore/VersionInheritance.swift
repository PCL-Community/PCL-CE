import Foundation

public extension MinecraftVersionMetadata {
    /// Resolve launcher profiles without altering their on-disk inheritance links.
    static func resolve(versionID: String, gameDirectory: URL) throws -> [String: Any] {
        try resolve(versionID: versionID, gameDirectory: gameDirectory, ancestors: [])
    }

    static func modLoader(in metadata: [String: Any]) -> ModLoaderVersion? {
        let base = metadata["pclOriginalVersion"] as? String ?? metadata["inheritsFrom"] as? String ?? metadata["id"] as? String ?? ""
        let marker = metadata["pclLoader"] as? [String: String] ?? [:]
        if let kindName = metadata["pclLoaderKind"] as? String ?? marker["kind"],
           let version = metadata["pclLoaderVersion"] as? String ?? marker["version"], !version.isEmpty,
           let kind = ModLoaderKind.allCases.first(where: { $0.rawValue.lowercased() == kindName.lowercased() }) {
            return ModLoaderVersion(kind: kind, minecraftVersion: base, version: version)
        }
        for library in metadata["libraries"] as? [[String: Any]] ?? [] {
            guard let name = library["name"] as? String else { continue }
            let fields = name.split(separator: ":").map(String.init)
            guard fields.count >= 3 else { continue }
            let kind: ModLoaderKind
            switch "\(fields[0]):\(fields[1])" {
            case "net.fabricmc:fabric-loader": kind = .fabric
            case "org.quiltmc:quilt-loader": kind = .quilt
            case "net.minecraftforge:forge", "net.minecraftforge:minecraftforge": kind = .forge
            case "net.neoforged:neoforge", "net.neoforged:forge": kind = .neoForge
            default: continue
            }
            var version = fields[2]
            if version.hasPrefix(base + "-") { version = String(version.dropFirst(base.count + 1)) }
            return ModLoaderVersion(kind: kind, minecraftVersion: base, version: version)
        }
        return nil
    }

    private static func resolve(versionID: String, gameDirectory: URL, ancestors: Set<String>) throws -> [String: Any] {
        let key = versionID.precomposedStringWithCanonicalMapping.lowercased()
        guard ancestors.count < 32, !ancestors.contains(key) else {
            throw LauncherError.invalidVersion("版本继承循环或层数过多：\(versionID)")
        }
        let child = try load(versionID: versionID, gameDirectory: gameDirectory)
        if let value = child["inheritsFrom"], !(value is String) {
            throw LauncherError.invalidVersion("inheritsFrom 必须是版本名称")
        }
        if let value = child["jar"], !(value is String) {
            throw LauncherError.invalidVersion("jar 必须是客户端版本名称")
        }
        guard let parentID = child["inheritsFrom"] as? String else {
            var root = child
            root["jar"] = child["jar"] ?? versionID
            root["pclOriginalVersion"] = child["pclOriginalVersion"] ?? child["id"] ?? versionID
            return root
        }
        try validateVersionID(parentID)
        var visited = ancestors
        visited.insert(key)
        let parent = try resolve(versionID: parentID, gameDirectory: gameDirectory, ancestors: visited)
        var result = parent.merging(child) { _, value in value }
        result["id"] = child["id"] ?? versionID
        result["jar"] = child["jar"] ?? parent["jar"] ?? parentID
        result["pclOriginalVersion"] = child["pclOriginalVersion"] ?? parent["pclOriginalVersion"] ?? parentID

        let childLibraries = child["libraries"] as? [[String: Any]] ?? []
        let parentLibraries = parent["libraries"] as? [[String: Any]] ?? []
        let overrides = Set(childLibraries.filter { InstallPlatform.libraryAllowed($0) }.compactMap(libraryIdentity))
        result["libraries"] = childLibraries + parentLibraries.filter {
            guard let key = libraryIdentity($0) else { return true }
            return !overrides.contains(key)
        }

        var arguments = parent["arguments"] as? [String: Any] ?? [:]
        let childArguments = child["arguments"] as? [String: Any] ?? [:]
        for (name, value) in childArguments {
            if let inherited = arguments[name] as? [Any], let additions = value as? [Any] {
                arguments[name] = inherited + additions
            } else { arguments[name] = value }
        }
        // Legacy Forge profiles supply their complete game argument string.
        if child["minecraftArguments"] is String, childArguments["game"] == nil {
            arguments.removeValue(forKey: "game")
        }
        if !arguments.isEmpty { result["arguments"] = arguments }
        return result
    }

    /// A child can upgrade a module while retaining separate native classifiers.
    private static func libraryIdentity(_ library: [String: Any]) -> String? {
        guard let coordinate = library["name"] as? String, !coordinate.isEmpty else { return nil }
        let parts = coordinate.split(separator: "@", maxSplits: 1, omittingEmptySubsequences: false).map(String.init)
        let fields = parts[0].split(separator: ":", omittingEmptySubsequences: false)
        guard fields.count == 3 || fields.count == 4 else { return nil }
        let classifier = fields.count == 4 ? String(fields[3]) : ""
        return "\(fields[0]):\(fields[1]):\(classifier)@\(parts.count > 1 ? parts[1] : "jar")"
    }
}
