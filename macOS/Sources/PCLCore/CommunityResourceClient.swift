import Foundation
import CoreFoundation

/// The same two community catalogs used by PCL. CurseForge metadata is read
/// through the original MCIM mirror; application/user credentials never enter
/// either public metadata session.
public actor CommunityResourceClient {
    private let modrinth: URLSession
    private let curseForge: URLSession
    private static let modrinthHost = "api.modrinth.com"
    private static let loaderNames = ["forge", "neoforge", "fabric", "quilt", "liteloader", "rift"]

    public init(session: URLSession = .shared) {
        modrinth = Self.publicSession(copying: session, host: Self.modrinthHost)
        curseForge = Self.publicSession(copying: session, host: CurseForgeResolver.mirrorHost)
    }
    deinit { modrinth.finishTasksAndInvalidate(); curseForge.finishTasksAndInvalidate() }

    public func search(_ query: CommunitySearchQuery) async throws -> CommunitySearchPage {
        guard (1...50).contains(query.limit), query.offset >= 0, query.offset < 20_000,
              query.text.count <= 1000 else { throw CommunityResourceError.invalidRequest("搜索条件或页码超出范围") }
        if let value = query.minecraftVersion, !value.isEmpty { try MinecraftInstaller.validateComponent(value) }
        let category = Self.categoryParts(query.category)
        let useMR = query.source != .curseForge && category.modrinth != nil
        let useCF = query.source != .modrinth && category.curseForge != nil
        var warnings: [String] = []
        if query.source != .modrinth && !useCF { warnings.append("此分类仅在 Modrinth 提供，已显示可用来源。") }
        if query.source != .curseForge && !useMR { warnings.append("此分类仅在 CurseForge 提供，已显示可用来源。") }
        if !useMR && !useCF { throw CommunityResourceError.invalidRequest("所选来源不支持此分类") }
        let mixed = useMR && useCF
        let mrCount = mixed ? (query.limit + (query.offset % 2 == 0 ? 1 : 0)) / 2 : query.limit
        let cfCount = mixed ? query.limit - mrCount : query.limit
        let mrOffset = mixed ? (query.offset + 1) / 2 : query.offset
        let cfOffset = mixed ? query.offset / 2 : query.offset
        var mrPage: ProviderPage?, cfPage: ProviderPage?, errors: [String] = []
        // Independent providers load concurrently. A failing catalog must not
        // discard useful results from the other catalog.
        await withTaskGroup(of: ProviderResult.self) { group in
            if useMR && mrCount > 0 {
                let transport = modrinth
                group.addTask {
                    do { return .success(.modrinth, try await Self.searchModrinth(query, offset: mrOffset, limit: mrCount, category: category.modrinth ?? "", session: transport)) }
                    catch { return .failure(.modrinth, error) }
                }
            }
            if useCF && cfCount > 0 {
                let transport = curseForge
                group.addTask {
                    do { return .success(.curseForge, try await Self.searchCurseForge(query, offset: cfOffset, limit: cfCount, category: category.curseForge ?? "", session: transport)) }
                    catch { return .failure(.curseForge, error) }
                }
            }
            for await result in group {
                switch result {
                case .success(let source, let page): if source == .modrinth { mrPage = page } else { cfPage = page }
                case .failure(let source, let error): errors.append("\(source.displayName)：\(error.localizedDescription)")
                }
            }
        }
        try Task.checkCancellation()
        guard mrPage != nil || cfPage != nil else { throw CommunityResourceError.unavailable(errors.joined(separator: "\n")) }
        warnings += errors
        var projects: [CommunityProject] = []
        if mixed {
            let mr = mrPage?.projects ?? [], cf = cfPage?.projects ?? []
            for index in 0..<max(mr.count, cf.count) {
                if query.offset % 2 == 0 {
                    if index < mr.count { projects.append(mr[index]) }; if index < cf.count { projects.append(cf[index]) }
                } else {
                    if index < cf.count { projects.append(cf[index]) }; if index < mr.count { projects.append(mr[index]) }
                }
            }
        } else { projects = mrPage?.projects ?? cfPage?.projects ?? [] }
        return CommunitySearchPage(projects: projects, offset: query.offset,
            total: (mrPage?.total ?? 0) + (cfPage?.total ?? 0),
            hasMore: mrPage?.hasMore == true || cfPage?.hasMore == true, warnings: warnings)
    }

    public func detail(_ project: CommunityProject) async throws -> CommunityProjectDetail {
        switch project.source {
        case .modrinth:
            let data = try await Self.fetchMR(path: "/project/" + Self.identifier(project.projectID), session: modrinth)
            let value = try Self.object(data)
            let actual = try Self.parseMRProject(value, kind: project.kind, fallback: project)
            guard actual.projectID == project.projectID else { throw CommunityResourceError.invalidResponse("项目身份不匹配") }
            let license = value["license"] as? [String: Any]
            let gallery = (value["gallery"] as? [[String: Any]] ?? []).compactMap { Self.webURL($0["url"]) }
            return CommunityProjectDetail(project: actual, body: value["body"] as? String ?? "", license: license?["name"] as? String ?? license?["id"] as? String, gallery: gallery)
        case .curseForge:
            let id = try Self.numericID(project.projectID)
            async let mod = Self.fetchCF(path: "/mods/\(id)", session: curseForge)
            async let description = Self.curseForgeDescription(id: id, session: curseForge)
            let value = try Self.envelopeObject(try await mod)
            let body = try await description
            guard Self.number(value["id"]) == id else { throw CommunityResourceError.invalidResponse("项目身份不匹配") }
            return CommunityProjectDetail(project: try Self.parseCFProject(value, kind: project.kind), body: body ?? value["summary"] as? String ?? "",
                gallery: (value["screenshots"] as? [[String: Any]] ?? []).compactMap { Self.webURL($0["url"]) })
        case .all: throw CommunityResourceError.invalidRequest("请选择具体资源来源")
        }
    }

    public func project(id: String, source: CommunitySource, kind: CommunityResourceKind = .mod) async throws -> CommunityProject {
        switch source {
        case .modrinth:
            return try Self.parseMRProject(Self.object(try await Self.fetchMR(path: "/project/" + Self.identifier(id), session: modrinth)), kind: kind, useReportedKind: true)
        case .curseForge:
            let value = try Self.envelopeObject(try await Self.fetchCF(path: "/mods/\(Self.numericID(id))", session: curseForge))
            guard Self.number(value["id"]) == Int64(id) else { throw CommunityResourceError.invalidResponse("项目身份不匹配") }
            return try Self.parseCFProject(value, kind: kind)
        case .all: throw CommunityResourceError.invalidRequest("请选择具体资源来源")
        }
    }

    public func versions(for project: CommunityProject, minecraftVersion: String? = nil, loader: ModLoaderKind? = nil) async throws -> [CommunityVersion] {
        if let minecraftVersion, !minecraftVersion.isEmpty { try MinecraftInstaller.validateComponent(minecraftVersion) }
        switch project.source {
        case .modrinth:
            var query = [URLQueryItem(name: "include_changelog", value: "false")]
            if let minecraftVersion, !minecraftVersion.isEmpty { query.append(.init(name: "game_versions", value: Self.jsonString([minecraftVersion]))) }
            if project.kind == .datapack { query.append(.init(name: "loaders", value: Self.jsonString(["datapack"]))) }
            else if let loader { query.append(.init(name: "loaders", value: Self.jsonString([Self.loaderName(loader)]))) }
            let data = try await Self.fetchMR(path: "/project/" + Self.identifier(project.projectID) + "/version", query: query, session: modrinth)
            guard let values = try JSONSerialization.jsonObject(with: data) as? [[String: Any]] else { throw CommunityResourceError.invalidResponse("版本列表格式错误") }
            var result: [CommunityVersion] = []
            for value in values {
                // A removed/empty historical upload must not hide the usable
                // releases in the same project. Identity and unsafe metadata
                // still fail rather than being accepted as a different file.
                do {
                    let parsed = try Self.parseMRVersion(value)
                    guard parsed.projectID == project.projectID else { throw CommunityResourceError.invalidResponse("版本不属于当前项目") }
                    result.append(parsed)
                } catch CommunityResourceError.unavailable { continue }
            }
            return result.filter {
                guard Self.compatible($0, minecraft: minecraftVersion, loader: project.kind == .datapack ? nil : loader) else { return false }
                if project.kind == .datapack { return $0.loaders.contains("datapack") }
                if project.kind == .mod {
                    return $0.loaders.isEmpty || $0.loaders.contains { Self.loaderNames.contains($0.lowercased()) }
                }
                return true
            }
                .sorted { ($0.published ?? .distantPast) > ($1.published ?? .distantPast) }
        case .curseForge:
            let id = try Self.numericID(project.projectID)
            var query = [URLQueryItem(name: "pageSize", value: "50")]
            if let minecraftVersion, !minecraftVersion.isEmpty { query.append(.init(name: "gameVersion", value: minecraftVersion)) }
            if let loader { query.append(.init(name: "modLoaderType", value: String(Self.curseForgeLoader(loader)))) }
            var result: [CommunityVersion] = [], seen = Set<String>(), offset = 0
            repeat {
                try Task.checkCancellation()
                let envelope = try Self.object(try await Self.fetchCF(path: "/mods/\(id)/files", query: query + [.init(name: "index", value: String(offset))], session: curseForge))
                guard let values = envelope["data"] as? [[String: Any]] else { throw CommunityResourceError.invalidResponse("版本列表格式错误") }
                for value in values {
                    if project.kind == .modpack && value["isServerPack"] as? Bool == true { continue }
                    do {
                        let parsed = try Self.parseCFVersion(value)
                        guard parsed.projectID == project.projectID else { throw CommunityResourceError.invalidResponse("版本不属于当前项目") }
                        if seen.insert(parsed.id).inserted { result.append(parsed) }
                    } catch CommunityResourceError.unavailable { continue }
                }
                let total = Self.number((envelope["pagination"] as? [String: Any])?["totalCount"]) ?? Int64(offset + values.count)
                offset += values.count
                if values.count < 50 || offset >= total || offset >= 1000 { break }
            } while true
            return result.filter { Self.compatible($0, minecraft: minecraftVersion, loader: loader) }.sorted { ($0.published ?? .distantPast) > ($1.published ?? .distantPast) }
        case .all: throw CommunityResourceError.invalidRequest("请选择具体资源来源")
        }
    }

    public func version(id: String, source: CommunitySource) async throws -> CommunityVersion {
        switch source {
        case .modrinth:
            let parsed = try Self.parseMRVersion(Self.object(try await Self.fetchMR(path: "/version/" + Self.identifier(id), session: modrinth)))
            guard parsed.id == id else { throw CommunityResourceError.invalidResponse("文件身份不匹配") }
            return parsed
        case .curseForge:
            let fileID = try Self.numericID(id)
            let data = try await Self.fetchCF(path: "/mods/files", body: ["fileIds": [fileID]], session: curseForge)
            let object = try Self.object(data)
            guard let values = object["data"] as? [[String: Any]], values.count == 1 else { throw CommunityResourceError.unavailable("没有找到指定文件 \(id)") }
            let parsed = try Self.parseCFVersion(values[0])
            guard parsed.id == id else { throw CommunityResourceError.invalidResponse("文件身份不匹配") }
            return parsed
        case .all: throw CommunityResourceError.invalidRequest("请选择具体资源来源")
        }
    }

    /// PCL ModDownload.DlSourceModGet maps the official Modrinth CDN to
    /// MCIM with exactly the same encoded path and query. Keep both candidates;
    /// every downloaded byte is still checked against the official full hashes.
    public nonisolated static func modrinthDownloadURLs(_ original: URL) throws -> [URL] {
        guard let components = URLComponents(url: original, resolvingAgainstBaseURL: false),
              components.scheme?.lowercased() == "https", components.host?.lowercased() == "cdn.modrinth.com",
              components.user == nil, components.password == nil, components.port == nil || components.port == 443 else {
            throw CommunityResourceError.invalidResponse("Modrinth CDN 地址无效")
        }
        var mirror = components
        mirror.host = CurseForgeResolver.mirrorHost
        guard let candidate = mirror.url else { throw CommunityResourceError.invalidResponse("Modrinth 镜像地址无效") }
        return [original, candidate]
    }

    private struct ProviderPage: Sendable { let projects: [CommunityProject]; let total: Int; let hasMore: Bool }
    private enum ProviderResult: @unchecked Sendable { case success(CommunitySource, ProviderPage), failure(CommunitySource, Error) }

    private static func searchModrinth(_ query: CommunitySearchQuery, offset: Int, limit: Int, category: String, session: URLSession) async throws -> ProviderPage {
        var facets = [["project_type:" + query.kind.rawValue]]
        if !category.isEmpty { facets.append(["categories:" + category]) }
        if let loader = query.loader { facets.append(["categories:" + loaderName(loader)]) }
        if let version = query.minecraftVersion, !version.isEmpty { facets.append(["versions:" + version]) }
        let items = [URLQueryItem(name: "query", value: query.text), .init(name: "limit", value: String(limit)), .init(name: "offset", value: String(offset)),
            .init(name: "index", value: query.sort.rawValue), .init(name: "facets", value: jsonString(facets))]
        let object = try Self.object(try await fetchMR(path: "/search", query: items, session: session))
        guard let values = object["hits"] as? [[String: Any]], let total = number(object["total_hits"]), total >= 0 else { throw CommunityResourceError.invalidResponse("Modrinth 搜索格式错误") }
        let projects = try values.map { try parseMRProject($0, kind: query.kind) }
        return ProviderPage(projects: projects, total: Int(total), hasMore: offset + projects.count < total)
    }

    private static func searchCurseForge(_ query: CommunitySearchQuery, offset: Int, limit: Int, category: String, session: URLSession) async throws -> ProviderPage {
        if offset >= 10_000 { return ProviderPage(projects: [], total: 10_000, hasMore: false) }
        var items = [URLQueryItem(name: "gameId", value: "432"), .init(name: "classId", value: String(classID(query.kind))),
            .init(name: "searchFilter", value: query.text), .init(name: "pageSize", value: String(min(limit, 10_000 - offset))), .init(name: "index", value: String(offset)),
            .init(name: "sortField", value: query.sort == .relevance ? "2" : query.sort == .downloads ? "6" : "3"), .init(name: "sortOrder", value: "desc")]
        if !category.isEmpty { items.append(.init(name: "categoryId", value: category)) }
        if let version = query.minecraftVersion, !version.isEmpty { items.append(.init(name: "gameVersion", value: version)) }
        if let loader = query.loader { items.append(.init(name: "modLoaderType", value: String(curseForgeLoader(loader)))) }
        let object = try Self.object(try await fetchCF(path: "/mods/search", query: items, session: session))
        guard let values = object["data"] as? [[String: Any]] else { throw CommunityResourceError.invalidResponse("CurseForge 搜索格式错误") }
        let total = min(10_000, Int(number((object["pagination"] as? [String: Any])?["totalCount"]) ?? Int64(offset + values.count)))
        let projects = try values.map { try parseCFProject($0, kind: query.kind) }
        return ProviderPage(projects: projects, total: total, hasMore: offset + values.count < total)
    }

    private static func categoryParts(_ raw: String?) -> (curseForge: String?, modrinth: String?) {
        guard let raw, !raw.isEmpty else { return ("", "") }
        if raw.contains("/") {
            let parts = raw.split(separator: "/", maxSplits: 1, omittingEmptySubsequences: false).map(String.init)
            let cf = Int(parts[0]).flatMap { $0 > 0 ? String($0) : nil }
            return (cf, parts[1].isEmpty ? nil : parts[1])
        }
        if let cf = Int(raw), cf > 0 { return (String(cf), nil) }
        return (nil, raw)
    }

    private static func parseMRProject(_ value: [String: Any], kind: CommunityResourceKind, fallback: CommunityProject? = nil, useReportedKind: Bool = false) throws -> CommunityProject {
        guard let id = value["project_id"] as? String ?? value["id"] as? String,
              let name = value["title"] as? String, !name.isEmpty else { throw CommunityResourceError.invalidResponse("Modrinth 项目缺少名称或 ID") }
        _ = try identifier(id)
        let categories = value["categories"] as? [String] ?? []
        let types = value["project_type"] as? String
        let actualKind = useReportedKind && kind == .mod ? (types.flatMap(CommunityResourceKind.init(rawValue:)) ?? kind) : kind
        let slug = (value["slug"] as? String).flatMap { (try? identifier($0)) == nil ? nil : $0 } ?? id
        let website = URL(string: "https://modrinth.com/\(actualKind.rawValue)/\(slug)")!
        return CommunityProject(projectID: id, source: .modrinth, kind: actualKind, name: name,
            summary: value["description"] as? String ?? "", author: value["author"] as? String ?? fallback?.author ?? "",
            iconURL: webURL(value["icon_url"]), websiteURL: website, downloads: number(value["downloads"]) ?? 0,
            updated: date(value["date_modified"] ?? value["updated"]), categories: categories,
            gameVersions: value["versions"] as? [String] ?? value["game_versions"] as? [String] ?? [],
            loaders: value["loaders"] as? [String] ?? categories.filter { loaderNames.contains($0.lowercased()) })
    }

    private static func parseCFProject(_ value: [String: Any], kind: CommunityResourceKind) throws -> CommunityProject {
        guard let id = number(value["id"]), id > 0, let name = value["name"] as? String, !name.isEmpty else { throw CommunityResourceError.invalidResponse("CurseForge 项目缺少名称或 ID") }
        let actualKind = number(value["classId"]).flatMap { kindForClass(Int($0)) } ?? kind
        let links = value["links"] as? [String: Any]
        let path: String
        switch actualKind { case .mod: path = "mc-mods"; case .modpack: path = "modpacks"; case .datapack: path = "data-packs"; case .resourcepack: path = "texture-packs"; case .shader: path = "shaders" }
        let slug = (value["slug"] as? String).flatMap { (try? identifier($0)) == nil ? nil : $0 } ?? String(id)
        let categories = (value["categories"] as? [[String: Any]] ?? []).compactMap { $0["name"] as? String }
        let indices = value["latestFilesIndexes"] as? [[String: Any]] ?? []
        let gameVersions = Array(Set(indices.compactMap { $0["gameVersion"] as? String })).sorted()
        let loaders = Array(Set(indices.compactMap { number($0["modLoader"]).flatMap { loaderForCurseForge(Int($0)) } })).sorted()
        return CommunityProject(projectID: String(id), source: .curseForge, kind: actualKind, name: name,
            summary: value["summary"] as? String ?? "", author: (value["authors"] as? [[String: Any]] ?? []).compactMap { $0["name"] as? String }.joined(separator: ", "),
            iconURL: webURL((value["logo"] as? [String: Any])?["thumbnailUrl"] ?? (value["logo"] as? [String: Any])?["url"]),
            websiteURL: webURL(links?["websiteUrl"]) ?? URL(string: "https://www.curseforge.com/minecraft/\(path)/\(slug)")!,
            downloads: number(value["downloadCount"]) ?? 0, updated: date(value["dateModified"]), categories: categories, gameVersions: gameVersions, loaders: loaders)
    }

    private static func parseMRVersion(_ value: [String: Any]) throws -> CommunityVersion {
        guard let id = value["id"] as? String, let project = value["project_id"] as? String,
              let name = value["name"] as? String, let version = value["version_number"] as? String,
              let files = value["files"] as? [[String: Any]] else { throw CommunityResourceError.invalidResponse("Modrinth 版本缺少文件信息") }
        _ = try identifier(id); _ = try identifier(project)
        guard !files.isEmpty else { throw CommunityResourceError.unavailable("此版本暂无可下载文件") }
        let parsedFiles = try files.map { file -> CommunityResourceFile in
            guard let name = file["filename"] as? String, let size = number(file["size"]), size >= 0,
                  let url = webURL(file["url"]), url.host?.lowercased() == "cdn.modrinth.com" else { throw CommunityResourceError.invalidResponse("Modrinth 文件缺少可校验的 HTTPS 下载信息") }
            guard let hashes = file["hashes"] as? [String: String], hashes["sha1"] != nil || hashes["sha512"] != nil else {
                throw CommunityResourceError.unavailable("此版本文件暂无内容哈希")
            }
            try validFilename(name)
            let sha1 = try digest(hashes["sha1"], length: 40), sha512 = try digest(hashes["sha512"], length: 128)
            guard sha1 != nil || sha512 != nil else { throw CommunityResourceError.unavailable("此版本文件暂无内容哈希") }
            return CommunityResourceFile(filename: name, size: size, urls: try modrinthDownloadURLs(url), sha1: sha1, sha512: sha512, primary: file["primary"] as? Bool ?? false)
        }
        let dependencies = try (value["dependencies"] as? [[String: Any]] ?? []).map { entry -> CommunityDependency in
            let project = entry["project_id"] as? String, version = entry["version_id"] as? String
            if let project { _ = try identifier(project) }; if let version { _ = try identifier(version) }
            return CommunityDependency(projectID: project, versionID: version, required: entry["dependency_type"] as? String == "required")
        }
        return CommunityVersion(id: id, source: .modrinth, projectID: project, name: name, versionNumber: version,
            gameVersions: value["game_versions"] as? [String] ?? [], loaders: value["loaders"] as? [String] ?? [],
            published: date(value["date_published"]), releaseType: value["version_type"] as? String ?? "release", files: parsedFiles, dependencies: dependencies)
    }

    private static func parseCFVersion(_ value: [String: Any]) throws -> CommunityVersion {
        guard let id = number(value["id"]), let project = number(value["modId"]), id > 0, project > 0 else { throw CommunityResourceError.invalidResponse("CurseForge 文件 ID 无效") }
        guard value["isAvailable"] as? Bool != false else { throw CommunityResourceError.unavailable("此 CurseForge 文件已不可用") }
        let hashes = value["hashes"] as? [[String: Any]] ?? []
        guard hashes.contains(where: { number($0["algo"]) == 1 || number($0["algo"]) == 2 }) else {
            throw CommunityResourceError.unavailable("此 CurseForge 文件暂无可校验的内容哈希")
        }
        let resolved = try CurseForgeResolver.parse(value, reference: .init(projectID: Int(project), fileID: Int(id)))
        let filename = String(resolved.path.split(separator: "/").last ?? "")
        try validFilename(filename)
        let strings = value["gameVersions"] as? [String] ?? []
        let loaders = strings.map { $0.lowercased() }.filter { loaderNames.contains($0) }
        let gameVersions = strings.filter { $0.first?.isNumber == true }
        let dependencies = try (value["dependencies"] as? [[String: Any]] ?? []).map { entry -> CommunityDependency in
            guard let dependency = number(entry["modId"]), dependency > 0 else { throw CommunityResourceError.invalidResponse("CurseForge 依赖项目 ID 无效") }
            return CommunityDependency(projectID: String(dependency), required: number(entry["relationType"]) == 3)
        }
        let release: String
        switch number(value["releaseType"]) { case 2: release = "beta"; case 3: release = "alpha"; default: release = "release" }
        return CommunityVersion(id: String(id), source: .curseForge, projectID: String(project),
            name: value["displayName"] as? String ?? filename, versionNumber: filename,
            gameVersions: gameVersions, loaders: loaders, published: date(value["fileDate"]), releaseType: release,
            files: [CommunityResourceFile(filename: filename, size: resolved.size, urls: resolved.urls, sha1: resolved.sha1, md5: resolved.md5, primary: true)], dependencies: dependencies)
    }

    private static func compatible(_ version: CommunityVersion, minecraft: String?, loader: ModLoaderKind?) -> Bool {
        if let minecraft, !minecraft.isEmpty, !version.gameVersions.contains(minecraft) { return false }
        if let loader, !version.loaders.map({ $0.lowercased() }).contains(loaderName(loader)) { return false }
        return true
    }
    private static func loaderName(_ value: ModLoaderKind) -> String { value == .neoForge ? "neoforge" : value.rawValue }
    private static func curseForgeLoader(_ value: ModLoaderKind) -> Int { switch value { case .forge: return 1; case .fabric: return 4; case .quilt: return 5; case .neoForge: return 6 } }
    private static func loaderForCurseForge(_ value: Int) -> String? { switch value { case 1: return "forge"; case 4: return "fabric"; case 5: return "quilt"; case 6: return "neoforge"; default: return nil } }
    private static func classID(_ value: CommunityResourceKind) -> Int { switch value { case .mod: return 6; case .modpack: return 4471; case .datapack: return 6945; case .resourcepack: return 12; case .shader: return 6552 } }
    private static func kindForClass(_ value: Int) -> CommunityResourceKind? { CommunityResourceKind.allCases.first { classID($0) == value } }
    private static func jsonString(_ object: Any) -> String { String(decoding: (try? JSONSerialization.data(withJSONObject: object)) ?? Data("[]".utf8), as: UTF8.self) }
    private static func identifier(_ value: String) throws -> String {
        guard !value.isEmpty, value.count <= 160, value.range(of: "^[A-Za-z0-9_-]+$", options: .regularExpression) != nil else { throw CommunityResourceError.invalidRequest("资源标识无效") }
        return value
    }
    private static func numericID(_ value: String) throws -> Int64 {
        guard let id = Int64(value), id > 0, String(id) == value else { throw CommunityResourceError.invalidRequest("CurseForge 标识无效") }; return id
    }
    private static func validFilename(_ value: String) throws {
        guard !value.isEmpty, value == (value as NSString).lastPathComponent, !value.contains("\\"), !value.contains(":"), !value.contains("\0"), value != ".", value != ".." else { throw CommunityResourceError.invalidResponse("下载文件名无效") }
    }
    private static func digest(_ value: String?, length: Int) throws -> String? {
        guard let value else { return nil }
        guard value.count == length, value.range(of: "^[0-9a-fA-F]+$", options: .regularExpression) != nil else { throw CommunityResourceError.invalidResponse("文件校验哈希无效") }; return value.lowercased()
    }
    private static func webURL(_ value: Any?) -> URL? {
        guard let raw = value as? String, let url = URL(string: raw), url.scheme?.lowercased() == "https", url.user == nil, url.password == nil,
              url.host != nil, url.port == nil || url.port == 443 else { return nil }; return url
    }
    private static func date(_ value: Any?) -> Date? {
        guard let value = value as? String else { return nil }
        let formatter = ISO8601DateFormatter(); formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter.date(from: value) ?? ISO8601DateFormatter().date(from: value)
    }
    private static func number(_ value: Any?) -> Int64? {
        guard let value = value as? NSNumber, CFGetTypeID(value) != CFBooleanGetTypeID(), value.doubleValue.isFinite,
              value.doubleValue.rounded(.towardZero) == value.doubleValue, value.doubleValue >= 0, value.doubleValue < Double(Int64.max) else { return nil }; return value.int64Value
    }
    private static func object(_ data: Data) throws -> [String: Any] {
        guard let object = try JSONSerialization.jsonObject(with: data) as? [String: Any] else { throw CommunityResourceError.invalidResponse("返回内容不是 JSON 对象") }; return object
    }
    private static func envelopeObject(_ data: Data) throws -> [String: Any] {
        guard let object = try object(data)["data"] as? [String: Any] else { throw CommunityResourceError.invalidResponse("响应缺少项目数据") }; return object
    }
    private static func publicSession(copying original: URLSession, host: String) -> URLSession {
        let configuration = original.configuration
        configuration.httpCookieStorage = nil; configuration.urlCredentialStorage = nil; configuration.urlCache = nil
        configuration.httpAdditionalHeaders = configuration.httpAdditionalHeaders?.filter { !["authorization", "cookie", "x-api-key"].contains(String(describing: $0.key).lowercased()) }
        return URLSession(configuration: configuration, delegate: CurseForgeMetadataRedirectPolicy(host: host), delegateQueue: nil)
    }
    private static func curseForgeDescription(id: Int64, session: URLSession) async throws -> String? {
        do {
            let object = try object(try await fetchCF(path: "/mods/\(id)/description", session: session))
            return object["data"] as? String
        } catch {
            try Task.checkCancellation()
            if error is CancellationError || (error as? URLError)?.code == .cancelled { throw CancellationError() }
            // MCIM deployments may not expose descriptions. The real project
            // summary and author website remain usable; version access is intact.
            return nil
        }
    }

    private static func fetchMR(path: String, query: [URLQueryItem] = [], session: URLSession) async throws -> Data {
        try await request(host: modrinthHost, prefix: "/v2", path: path, query: query, body: nil, session: session)
    }
    private static func fetchCF(path: String, query: [URLQueryItem] = [], body: [String: Any]? = nil, session: URLSession) async throws -> Data {
        try await request(host: CurseForgeResolver.mirrorHost, prefix: "/curseforge/v1", path: path, query: query, body: body, session: session)
    }
    private static func request(host: String, prefix: String, path: String, query: [URLQueryItem], body: [String: Any]?, session: URLSession) async throws -> Data {
        var components = URLComponents(); components.scheme = "https"; components.host = host; components.path = prefix + path
        if !query.isEmpty { components.queryItems = query }
        guard let url = components.url else { throw CommunityResourceError.invalidRequest("资源地址无效") }
        var request = URLRequest(url: url, cachePolicy: .reloadIgnoringLocalCacheData, timeoutInterval: 35)
        request.setValue(CurseForgeResolver.userAgent, forHTTPHeaderField: "User-Agent"); request.setValue("application/json", forHTTPHeaderField: "Accept")
        if let body { request.httpMethod = "POST"; request.httpBody = try JSONSerialization.data(withJSONObject: body); request.setValue("application/json", forHTTPHeaderField: "Content-Type") }
        for attempt in 0..<2 {
            try Task.checkCancellation()
            do {
                let (data, response) = try await PCLHTTPS.data(for: request, session: session)
                guard let http = response as? HTTPURLResponse, response.url?.scheme == "https", response.url?.host == host else { throw URLError(.badServerResponse) }
                if [429, 500, 502, 503, 504].contains(http.statusCode), attempt == 0 { try await Task.sleep(nanoseconds: 500_000_000); continue }
                guard (200..<300).contains(http.statusCode) else { throw MinecraftInstallError.http(http.statusCode, host == modrinthHost ? "Modrinth" : "CurseForge") }
                guard data.count <= 24 * 1024 * 1024 else { throw CommunityResourceError.invalidResponse("响应内容过大") }
                return data
            } catch {
                try Task.checkCancellation()
                if (error as? URLError)?.code == .cancelled || error is CancellationError { throw CancellationError() }
                if let error = error as? URLError, [.timedOut, .networkConnectionLost, .secureConnectionFailed].contains(error.code), attempt == 0 { continue }
                throw error
            }
        }
        throw URLError(.badServerResponse)
    }
}
