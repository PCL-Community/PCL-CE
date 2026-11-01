import Foundation
import CoreFoundation

struct CurseForgeFileReference: Hashable, Sendable {
    let projectID: Int
    let fileID: Int
}

struct ResolvedCurseForgeFile: Sendable {
    let projectID: Int
    let fileID: Int
    let path: String
    let urls: [URL]
    let sha1: String?
    let md5: String?
    let size: Int64
    let fingerprint: UInt32?
}

/// Resolve exact exported file IDs, as PCL's DlModRequest does, through MCIM.
/// An optional application-owned key enables an official fallback. Credentials
/// never accompany mirror requests or redirects to another host.
actor CurseForgeResolver {
    static let mirrorHost = "mod.mcimirror.top"
    static let officialHost = "api.curseforge.com"
    static let userAgent = "PCL-SwiftUI-Community/0.1.0"
    private let session: URLSession

    init(session: URLSession = .shared) { self.session = session }

    func resolve(_ references: [CurseForgeFileReference], apiKey: String? = nil,
                 progress: @escaping @Sendable (Int, Int) -> Void = { _, _ in }) async throws -> [ResolvedCurseForgeFile] {
        var ordered: [CurseForgeFileReference] = [], expected: [Int: CurseForgeFileReference] = [:]
        for reference in references {
            guard reference.projectID > 0, reference.fileID > 0 else {
                throw ModpackImportError.invalidManifest("CurseForge 项目或文件 ID 无效")
            }
            if let old = expected[reference.fileID] {
                guard old == reference else { throw ModpackImportError.invalidManifest("同一 CurseForge 文件 ID 对应多个项目") }
                continue
            }
            expected[reference.fileID] = reference
            ordered.append(reference)
        }
        guard !ordered.isEmpty else { return [] }
        let key = apiKey?.trimmingCharacters(in: .whitespacesAndNewlines)
        if let key, !key.isEmpty {
            guard key.count <= 8192, key.rangeOfCharacter(from: .controlCharacters) == nil else {
                throw ModpackImportError.invalidManifest("应用的 CurseForge API 配置无效")
            }
        }
        let mirror = Self.metadataSession(copying: session, host: Self.mirrorHost)
        let official = key?.isEmpty == false ? Self.metadataSession(copying: session, host: Self.officialHost) : nil
        defer { mirror.finishTasksAndInvalidate(); official?.finishTasksAndInvalidate() }
        var resolved: [Int: ResolvedCurseForgeFile] = [:]
        progress(0, ordered.count)
        for start in stride(from: 0, to: ordered.count, by: 500) {
            try Task.checkCancellation()
            let chunk = Array(ordered[start..<min(start + 500, ordered.count)])
            let chunkExpected = Dictionary(uniqueKeysWithValues: chunk.map { ($0.fileID, $0) })
            var batch = URLRequest(url: URL(string: "https://\(Self.mirrorHost)/curseforge/v1/mods/files")!)
            batch.httpMethod = "POST"
            batch.httpBody = try JSONSerialization.data(withJSONObject: ["fileIds": chunk.map(\.fileID)])
            batch.setValue("application/json", forHTTPHeaderField: "Content-Type")
            let data: Data?
            do { data = try await Self.request(batch, session: mirror) }
            catch {
                try Self.checkCancellation(error)
                data = nil // Older mirror deployments may only implement GET.
            }
            if let data {
                let envelope = try Self.object(data)
                guard let files = envelope["data"] as? [[String: Any]] else {
                    throw ModpackImportError.invalidManifest("CurseForge 批量文件信息格式无效")
                }
                for file in files {
                    guard let id = Self.integer(file["id"]), let reference = chunkExpected[id], resolved[id] == nil else {
                        throw ModpackImportError.invalidManifest("CurseForge 批量响应含有未知或重复文件 ID")
                    }
                    resolved[id] = try Self.parse(file, reference: reference)
                }
                progress(resolved.count, ordered.count)
            }
            let missing = chunk.filter { resolved[$0.fileID] == nil }
            // A missing batch member can still be available at its individual endpoint.
            try await withThrowingTaskGroup(of: ResolvedCurseForgeFile.self) { group in
                var iterator = missing.makeIterator()
                func add(_ reference: CurseForgeFileReference) {
                    group.addTask {
                        try await Self.resolveOne(reference, mirror: mirror, official: official, apiKey: key)
                    }
                }
                for _ in 0..<min(4, missing.count) { if let reference = iterator.next() { add(reference) } }
                while let file = try await group.next() {
                    try Task.checkCancellation()
                    resolved[file.fileID] = file
                    progress(resolved.count, ordered.count)
                    if let reference = iterator.next() { add(reference) }
                }
            }
        }
        var paths = Set<String>()
        return try ordered.map { reference in
            guard let file = resolved[reference.fileID] else { throw Self.unavailable(reference) }
            guard paths.insert(ModpackZIPArchive.pathKey(file.path)).inserted else {
                throw ModpackImportError.invalidManifest("CurseForge 文件名冲突：\(file.path)")
            }
            return file
        }
    }

    private static func resolveOne(_ reference: CurseForgeFileReference, mirror: URLSession,
                                   official: URLSession?, apiKey: String?) async throws -> ResolvedCurseForgeFile {
        let path = "/v1/mods/\(reference.projectID)/files/\(reference.fileID)"
        var request = URLRequest(url: URL(string: "https://\(mirrorHost)/curseforge" + path)!)
        do {
            let envelope = try object(try await self.request(request, session: mirror))
            guard let file = envelope["data"] as? [String: Any] else { throw unavailable(reference) }
            return try parse(file, reference: reference)
        } catch {
            try checkCancellation(error)
            let mirrorMissing = isNotFound(error)
            guard let official, let apiKey, !apiKey.isEmpty else {
                if mirrorMissing, let exact = try legacyResourceFile(reference) { return exact }
                throw unavailable(reference)
            }
            request.url = URL(string: "https://\(officialHost)" + path)!
            request.setValue(apiKey, forHTTPHeaderField: "x-api-key")
            do {
                let envelope = try object(try await self.request(request, session: official))
                guard let file = envelope["data"] as? [String: Any] else { throw unavailable(reference) }
                return try parse(file, reference: reference)
            } catch {
                try checkCancellation(error)
                if isNotFound(error), let exact = try legacyResourceFile(reference) { return exact }
                throw error
            }
        }
    }

    /// These two historical attachments disappeared from the file API, while the
    /// exact author-published CDN objects remain available. Verified 2026-11-01:
    /// ZIP CRCs, SHA-1, MD5 and CDN ETag were checked against the downloaded bytes.
    /// Sources: https://www.curseforge.com/minecraft/texture-packs/xekr-square-pattern/files/3811549
    ///          https://www.curseforge.com/minecraft/texture-packs/xekr-square-pattern/files/3811554
    /// Only a confirmed 404/410 for the exact project/file pair uses these pins.
    private static func legacyResourceFile(_ reference: CurseForgeFileReference) throws -> ResolvedCurseForgeFile? {
        guard reference.projectID == 338_808 else { return nil }
        let name: String, sha1: String, md5: String, size: Int64
        switch reference.fileID {
        case 3_811_549:
            name = "XKSP 花团锦簇 lush flowers.zip"
            sha1 = "4a3e8512007d224535adc94640ba0a43ab01a2b6"
            md5 = "914585b7cc200cd1e3332cc3c49c132d"
            size = 213_889
        case 3_811_554:
            name = "XKSP 披花戴叶 flower armor.zip"
            sha1 = "c3e8c41abed1631f9efc486a5c48ba8fd2f8231a"
            md5 = "95ddbb4e8a25d16ad2dd8111ea13bb1e"
            size = 131_951
        default: return nil
        }
        return ResolvedCurseForgeFile(projectID: reference.projectID, fileID: reference.fileID,
            path: "resourcepacks/" + name, urls: try downloadURLs(rawURL: nil, fileID: reference.fileID, fileName: name),
            sha1: sha1, md5: md5, size: size, fingerprint: nil)
    }

    private static func isNotFound(_ error: Error) -> Bool {
        guard case MinecraftInstallError.http(let status, _) = error else { return false }
        return status == 404 || status == 410
    }

    static func parse(_ file: [String: Any], reference: CurseForgeFileReference) throws -> ResolvedCurseForgeFile {
        guard integer(file["id"]) == reference.fileID, integer(file["modId"]) == reference.projectID,
              let name = file["fileName"] as? String, !name.contains("/"), !name.contains("\\"),
              let length = number(file["fileLength"]), length >= 0,
              let hashes = file["hashes"] as? [[String: Any]] else {
            throw ModpackImportError.invalidManifest("CurseForge 文件身份、名称、大小或校验信息无效：\(reference.projectID)/\(reference.fileID)")
        }
        var sha1: String?, md5: String?
        for hash in hashes {
            guard let algorithm = integer(hash["algo"]), [1, 2].contains(algorithm) else { continue }
            guard let rawValue = hash["value"] as? String, rawValue.count == (algorithm == 1 ? 40 : 32),
                  rawValue.range(of: "^[0-9A-Fa-f]+$", options: .regularExpression) != nil else {
                throw ModpackImportError.invalidManifest("CurseForge 文件哈希格式无效：\(name)")
            }
            let value = rawValue.lowercased()
            if algorithm == 1 {
                guard sha1 == nil || sha1 == value else { throw ModpackImportError.invalidManifest("CurseForge SHA-1 信息冲突") }
                sha1 = value
            } else {
                guard md5 == nil || md5 == value else { throw ModpackImportError.invalidManifest("CurseForge MD5 信息冲突") }
                md5 = value
            }
        }
        guard sha1 != nil || md5 != nil else { throw ModpackImportError.invalidManifest("CurseForge 文件没有可校验的哈希：\(name)") }
        let modules = (file["modules"] as? [[String: Any]] ?? []).compactMap { $0["name"] as? String }
        let folder: String
        if name.lowercased().hasSuffix(".jar") || modules.contains(where: { ["META-INF", "mcmod.info", "fabric.mod.json", "quilt.mod.json"].contains($0) }) {
            folder = "mods"
        } else if modules.contains("pack.mcmeta") { folder = "resourcepacks" }
        else if modules.contains("shaders") { folder = "shaderpacks" }
        else { folder = modules.isEmpty ? "mods" : "saves" }
        let path = try ModpackZIPArchive.validatedPath(folder + "/" + name)
        let rawURL = file["downloadUrl"] as? String
        let urls = try downloadURLs(rawURL: rawURL, fileID: reference.fileID, fileName: name)
        let fingerprint = number(file["fileFingerprint"]).flatMap { UInt32(exactly: $0) }
        return ResolvedCurseForgeFile(projectID: reference.projectID, fileID: reference.fileID, path: path,
                                     urls: urls, sha1: sha1, md5: md5, size: length, fingerprint: fingerprint)
    }

    static func downloadURLs(rawURL: String?, fileID: Int, fileName: String) throws -> [URL] {
        guard fileID > 0, !fileName.contains("/"), !fileName.contains("\\") else { throw ModpackImportError.unsafePath(fileName) }
        _ = try ModpackZIPArchive.validatedPath("mods/" + fileName)
        var result: [URL] = []
        if let rawURL, !rawURL.isEmpty {
            guard var source = URLComponents(string: rawURL), source.scheme == "https", source.user == nil, source.password == nil,
                  source.port == nil || source.port == 443, source.fragment == nil, let rawHost = source.host?.lowercased() else {
                throw ModpackImportError.invalidManifest("CurseForge 下载地址无效")
            }
            let aliases = ["edge-service.overwolf.wtf": "edge.forgecdn.net", "media-service.overwolf.wtf": "media.forgecdn.net",
                           "mediafilez-service.overwolf.wtf": "mediafilez.forgecdn.net"]
            let host = aliases[rawHost] ?? rawHost
            guard ["edge.forgecdn.net", "mediafilez.forgecdn.net", "media.forgecdn.net", mirrorHost].contains(host) else {
                throw ModpackImportError.invalidManifest("CurseForge 下载域名不受支持")
            }
            source.host = host
            if host != mirrorHost {
                var preferred = source; preferred.host = "mediafilez.forgecdn.net"
                if let url = preferred.url { result.append(url) }
            }
            if let url = source.url { result.append(url) }
        }
        // File IDs group by thousands; preserve Unicode and reserved filename
        // characters through URLComponents instead of assembling escaped strings.
        for host in ["mediafilez.forgecdn.net", "edge.forgecdn.net", "media.forgecdn.net", mirrorHost] {
            var url = URLComponents()
            url.scheme = "https"; url.host = host
            url.path = "/files/\(fileID / 1000)/\(fileID % 1000)/\(fileName)"
            if let value = url.url { result.append(value) }
        }
        var seen = Set<String>()
        return result.filter { seen.insert($0.absoluteString).inserted }
    }

    private static func metadataSession(copying source: URLSession, host: String) -> URLSession {
        let configuration = source.configuration
        configuration.urlCache = nil
        configuration.httpCookieStorage = nil
        configuration.urlCredentialStorage = nil
        configuration.requestCachePolicy = .reloadIgnoringLocalCacheData
        configuration.httpAdditionalHeaders = configuration.httpAdditionalHeaders?.filter { key, _ in
            !["x-api-key", "authorization", "cookie"].contains(String(describing: key).lowercased())
        }
        return URLSession(configuration: configuration, delegate: CurseForgeMetadataRedirectPolicy(host: host), delegateQueue: nil)
    }

    private static func request(_ original: URLRequest, session: URLSession) async throws -> Data {
        var request = original
        request.timeoutInterval = 45
        request.setValue(userAgent, forHTTPHeaderField: "User-Agent")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        for attempt in 0..<3 {
            try Task.checkCancellation()
            do {
                let (data, response) = try await PCLHTTPS.data(for: request, session: session)
                guard let http = response as? HTTPURLResponse, response.url?.scheme == "https",
                      response.url?.host == request.url?.host else { throw URLError(.badServerResponse) }
                guard (200..<300).contains(http.statusCode) else {
                    if [429, 500, 502, 503, 504].contains(http.statusCode), attempt < 2 {
                        try await Task.sleep(nanoseconds: UInt64(attempt + 1) * 700_000_000)
                        continue
                    }
                    throw MinecraftInstallError.http(http.statusCode, "CurseForge 文件信息")
                }
                guard data.count <= 16 * 1024 * 1024 else { throw ModpackImportError.invalidManifest("CurseForge 文件信息过大") }
                return data
            } catch {
                try checkCancellation(error)
                guard let network = error as? URLError,
                      [.timedOut, .networkConnectionLost, .secureConnectionFailed, .cannotConnectToHost].contains(network.code), attempt < 2 else { throw error }
                try await Task.sleep(nanoseconds: UInt64(attempt + 1) * 400_000_000)
            }
        }
        throw URLError(.badServerResponse)
    }

    private static func checkCancellation(_ error: Error) throws {
        try Task.checkCancellation()
        if error is CancellationError || (error as? URLError)?.code == .cancelled { throw CancellationError() }
    }
    private static func object(_ data: Data) throws -> [String: Any] {
        guard let value = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw ModpackImportError.invalidManifest("CurseForge 文件响应不是 JSON 对象")
        }
        return value
    }
    private static func number(_ value: Any?) -> Int64? {
        guard let number = value as? NSNumber, CFGetTypeID(number) != CFBooleanGetTypeID(),
              number.doubleValue.isFinite, number.doubleValue.rounded(.towardZero) == number.doubleValue,
              number.doubleValue >= Double(Int64.min), number.doubleValue < Double(Int64.max) else { return nil }
        return number.int64Value
    }
    private static func integer(_ value: Any?) -> Int? { number(value).flatMap(Int.init(exactly:)) }
    private static func unavailable(_ reference: CurseForgeFileReference) -> ModpackImportError {
        .unavailableFile("CurseForge 项目 \(reference.projectID)，文件 \(reference.fileID)")
    }
}

final class CurseForgeMetadataRedirectPolicy: NSObject, URLSessionTaskDelegate, @unchecked Sendable {
    let host: String
    init(host: String) { self.host = host }
    static func allows(_ request: URLRequest, host: String) -> Bool {
        guard let url = request.url else { return false }
        return url.scheme == "https" && url.host == host && url.user == nil && url.password == nil && (url.port == nil || url.port == 443)
    }
    func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse,
                    newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void) {
        completionHandler(Self.allows(request, host: host) ? request : nil)
    }
}
