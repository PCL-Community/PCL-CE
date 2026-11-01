import Foundation

public enum CommunityResourceKind: String, CaseIterable, Codable, Sendable {
    case mod, modpack, datapack, resourcepack, shader
    public var displayName: String {
        switch self { case .mod: return "Mod"; case .modpack: return "整合包"; case .datapack: return "数据包"; case .resourcepack: return "资源包"; case .shader: return "光影包" }
    }
}

public enum CommunitySource: String, CaseIterable, Codable, Sendable {
    case all, modrinth, curseForge
    public var displayName: String {
        switch self { case .all: return "全部来源"; case .modrinth: return "Modrinth"; case .curseForge: return "CurseForge" }
    }
}

public enum CommunitySort: String, CaseIterable, Codable, Sendable {
    case relevance, downloads, updated
    public var displayName: String {
        switch self { case .relevance: return "综合排序"; case .downloads: return "下载量"; case .updated: return "更新时间" }
    }
}

public struct CommunitySearchQuery: Sendable {
    public var kind: CommunityResourceKind
    public var text: String
    public var minecraftVersion: String?
    public var loader: ModLoaderKind?
    public var category: String?
    public var source: CommunitySource
    public var sort: CommunitySort
    public var offset: Int
    public var limit: Int
    public init(kind: CommunityResourceKind, text: String = "", minecraftVersion: String? = nil,
                loader: ModLoaderKind? = nil, category: String? = nil, source: CommunitySource = .all,
                sort: CommunitySort = .relevance, offset: Int = 0, limit: Int = 40) {
        self.kind = kind; self.text = text; self.minecraftVersion = minecraftVersion; self.loader = loader
        self.category = category; self.source = source; self.sort = sort; self.offset = offset; self.limit = limit
    }
}

public struct CommunityProject: Identifiable, Hashable, Sendable {
    public var id: String { source.rawValue + ":" + projectID }
    public let projectID: String
    public let source: CommunitySource
    public let kind: CommunityResourceKind
    public let name: String
    public let summary: String
    public let author: String
    public let iconURL: URL?
    public let websiteURL: URL
    public let downloads: Int64
    public let updated: Date?
    public let categories: [String]
    public let gameVersions: [String]
    public let loaders: [String]
    public init(projectID: String, source: CommunitySource, kind: CommunityResourceKind,
                name: String, summary: String = "", author: String = "", iconURL: URL? = nil,
                websiteURL: URL, downloads: Int64 = 0, updated: Date? = nil,
                categories: [String] = [], gameVersions: [String] = [], loaders: [String] = []) {
        self.projectID = projectID; self.source = source; self.kind = kind; self.name = name
        self.summary = summary; self.author = author; self.iconURL = iconURL; self.websiteURL = websiteURL
        self.downloads = downloads; self.updated = updated; self.categories = categories
        self.gameVersions = gameVersions; self.loaders = loaders
    }
}

public struct CommunitySearchPage: Sendable {
    public let projects: [CommunityProject]
    public let offset: Int
    public let total: Int
    public let hasMore: Bool
    public let warnings: [String]
    public init(projects: [CommunityProject], offset: Int, total: Int, hasMore: Bool, warnings: [String] = []) {
        self.projects = projects; self.offset = offset; self.total = total; self.hasMore = hasMore; self.warnings = warnings
    }
}

public struct CommunityProjectDetail: Sendable {
    public let project: CommunityProject
    public let body: String
    public let license: String?
    public let gallery: [URL]
    public init(project: CommunityProject, body: String = "", license: String? = nil, gallery: [URL] = []) {
        self.project = project; self.body = body; self.license = license; self.gallery = gallery
    }
}

public struct CommunityVersion: Identifiable, Hashable, Codable, Sendable {
    public let id: String
    public let source: CommunitySource
    public let projectID: String
    public let name: String
    public let versionNumber: String
    public let gameVersions: [String]
    public let loaders: [String]
    public let published: Date?
    public let releaseType: String
    public let files: [CommunityResourceFile]
    public let dependencies: [CommunityDependency]
    public var primaryFile: CommunityResourceFile? { files.first(where: \.primary) ?? files.first }
    public init(id: String, source: CommunitySource, projectID: String, name: String, versionNumber: String,
                gameVersions: [String] = [], loaders: [String] = [], published: Date? = nil,
                releaseType: String = "release", files: [CommunityResourceFile] = [], dependencies: [CommunityDependency] = []) {
        self.id = id; self.source = source; self.projectID = projectID; self.name = name; self.versionNumber = versionNumber
        self.gameVersions = gameVersions; self.loaders = loaders; self.published = published; self.releaseType = releaseType
        self.files = files; self.dependencies = dependencies
    }
}

public struct CommunityResourceFile: Hashable, Codable, Sendable {
    public let filename: String
    public let size: Int64
    public let urls: [URL]
    public let sha1: String?
    public let sha512: String?
    public let md5: String?
    public let primary: Bool
    public init(filename: String, size: Int64, urls: [URL], sha1: String? = nil,
                sha512: String? = nil, md5: String? = nil, primary: Bool = false) {
        self.filename = filename; self.size = size; self.urls = urls; self.sha1 = sha1
        self.sha512 = sha512; self.md5 = md5; self.primary = primary
    }
}

public struct CommunityDependency: Hashable, Codable, Sendable {
    public let projectID: String?
    public let versionID: String?
    public let required: Bool
    public init(projectID: String? = nil, versionID: String? = nil, required: Bool = true) {
        self.projectID = projectID; self.versionID = versionID; self.required = required
    }
}

public enum CommunityResourceError: LocalizedError {
    case invalidResponse(String), unavailable(String), invalidRequest(String)
    public var errorDescription: String? {
        switch self {
        case .invalidResponse(let message): return "社区资源信息无效：\(message)"
        case .unavailable(let message): return "社区资源暂时无法获取：\(message)"
        case .invalidRequest(let message): return message
        }
    }
}
