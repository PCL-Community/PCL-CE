import CryptoKit
import Darwin
import Foundation

public enum VersionComponentManager {
    public static func fingerprint(version: InstalledVersion, gameDirectory: URL) throws -> String {
        let file = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(version.id)/\(version.id).json", under: gameDirectory)
        return digest(try Data(contentsOf: file))
    }

    /// Commit a fully installed temporary profile to an existing version. Runtime
    /// files and macOS settings stay in place; only the client and metadata change.
    @discardableResult public static func applyPreparedProfile(
        version: InstalledVersion, preparedID: String, gameDirectory: URL,
        newName: String? = nil, expectedFingerprint: String
    ) throws -> String {
        try Task.checkCancellation()
        let fm = FileManager.default
        let finalName = newName ?? version.id
        try VersionManager.validateName(finalName)
        try MinecraftVersionMetadata.validateVersionID(preparedID)
        guard preparedID != version.id else { throw error("临时组件版本不能与原版本相同") }
        let folder = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(version.id)", under: gameDirectory)
        guard folder.resolvingSymlinksInPath() == version.directory.resolvingSymlinksInPath(),
              (try? fm.destinationOfSymbolicLink(atPath: version.directory.path)) == nil else { throw error("版本文件夹已改变") }
        let target = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(finalName)", under: gameDirectory)
        if finalName.caseInsensitiveCompare(version.id) != .orderedSame, fm.fileExists(atPath: target.path) {
            throw VersionManagementError.destinationExists
        }
        for name in ["\(version.id).json", "\(version.id).jar"] {
            guard (try? fm.destinationOfSymbolicLink(atPath: folder.appendingPathComponent(name).path)) == nil else {
                throw error("版本主文件不能是符号链接")
            }
        }
        let jsonFile = try MinecraftVersionMetadata.safeFile(relativePath: "\(version.id).json", under: folder)
        let jarFile = try MinecraftVersionMetadata.safeFile(relativePath: "\(version.id).jar", under: folder)
        let oldData = try Data(contentsOf: jsonFile)
        guard digest(oldData) == expectedFingerprint else { throw error("原版本配置已改变，请刷新修改页面后重试") }
        let previous = try MinecraftVersionMetadata.load(versionID: version.id, gameDirectory: gameDirectory)
        let versionsFolder = try MinecraftVersionMetadata.safeFile(relativePath: "versions", under: gameDirectory)
        for sibling in try fm.contentsOfDirectory(at: versionsFolder, includingPropertiesForKeys: nil, options: [.skipsHiddenFiles])
            where sibling.lastPathComponent != version.id && sibling.lastPathComponent != preparedID {
            guard let other = try? MinecraftVersionMetadata.load(versionID: sibling.lastPathComponent, gameDirectory: gameDirectory) else { continue }
            if [other["inheritsFrom"] as? String, other["jar"] as? String].compactMap({ $0 })
                .contains(where: { $0.caseInsensitiveCompare(version.id) == .orderedSame }) {
                throw VersionManagementError.dependentVersion(sibling.lastPathComponent)
            }
        }
        var replacement = try MinecraftVersionMetadata.resolve(versionID: preparedID, gameDirectory: gameDirectory)
        guard replacement["mainClass"] as? String != nil,
              let minecraft = replacement["pclOriginalVersion"] as? String, !minecraft.isEmpty,
              let jarID = replacement["jar"] as? String else { throw error("新组件版本尚未安装完整") }
        try MinecraftVersionMetadata.validateVersionID(jarID)
        let client = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(jarID)/\(jarID).jar", under: gameDirectory)
        let clientValues = try client.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
        guard clientValues.isRegularFile == true, clientValues.isSymbolicLink != true else { throw error("新客户端文件无效") }
        if let downloads = replacement["downloads"] as? [String: Any],
           let info = downloads["client"] as? [String: Any], let hash = info["sha1"] as? String {
            guard MinecraftInstaller.validSHA1(hash), try MinecraftInstaller.matches(client, sha1: hash, size: (info["size"] as? NSNumber)?.int64Value) else {
                throw error("新客户端文件校验失败")
            }
        }
        let replacedKeys: Set<String> = ["pclOriginalVersion", "pclLoader", "pclLoaderKind", "pclLoaderVersion",
                                         "pclOptiFine", "pclLiteLoader", "pclOptiFabric", "pclExtraComponents"]
        for (key, value) in previous where key.hasPrefix("pcl") && !replacedKeys.contains(key) { replacement[key] = value }
        replacement["id"] = version.id
        replacement["jar"] = version.id
        replacement.removeValue(forKey: "inheritsFrom")
        let bytes = try JSONSerialization.data(withJSONObject: replacement, options: [.prettyPrinted, .sortedKeys])
        let staging = try MinecraftVersionMetadata.safeFile(relativePath: ".pcl-component-commit-\(UUID().uuidString)", under: gameDirectory)
        try fm.createDirectory(at: staging, withIntermediateDirectories: false)
        var preserveBackup = false
        defer { if !preserveBackup { try? fm.removeItem(at: staging) } }
        let backupJSON = staging.appendingPathComponent("original.json")
        let backupJar = staging.appendingPathComponent("original.jar")
        let nextJSON = staging.appendingPathComponent("next.json")
        let nextJar = staging.appendingPathComponent("next.jar")
        try oldData.write(to: backupJSON)
        let hadOwnJar = fm.fileExists(atPath: jarFile.path)
        if hadOwnJar { try ModpackImporter.cloneOrCopy(jarFile, to: backupJar) }
        try ModpackImporter.cloneOrCopy(client, to: nextJar)
        try bytes.write(to: nextJSON)
        try Task.checkCancellation()
        guard try fingerprint(version: version, gameDirectory: gameDirectory) == expectedFingerprint else { throw error("原版本配置已改变，请重试") }
        var committedJar = false
        do {
            try replace(nextJar, jarFile); committedJar = true
            try replace(nextJSON, jsonFile)
            if finalName != version.id {
                return try VersionManager.rename(version: version, to: finalName, gameDirectory: gameDirectory)
            }
            return version.id
        } catch {
            let failure = error
            do {
                guard fm.fileExists(atPath: folder.path) else { throw VersionManagementError.rollbackFailed(staging.path) }
                if committedJar {
                    if hadOwnJar { try replace(backupJar, jarFile) }
                    else if fm.fileExists(atPath: jarFile.path) { try fm.removeItem(at: jarFile) }
                }
                try replace(backupJSON, jsonFile)
            } catch {
                preserveBackup = true
                throw VersionManagementError.rollbackFailed(staging.path)
            }
            throw failure
        }
    }

    private static func replace(_ source: URL, _ destination: URL) throws {
        let result = source.path.withCString { from in destination.path.withCString { to in Darwin.rename(from, to) } }
        if result != 0 { throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno)) }
    }
    private static func digest(_ data: Data) -> String { SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined() }
    private static func error(_ message: String) -> NSError { NSError(domain: "PCL.Components", code: 1, userInfo: [NSLocalizedDescriptionKey: message]) }
}
