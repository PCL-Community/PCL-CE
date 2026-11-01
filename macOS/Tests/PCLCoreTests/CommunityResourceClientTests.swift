import Foundation
import XCTest
import CryptoKit
@testable import PCLCore

final class CommunityResourceClientTests: XCTestCase {
    private func client(_ mode: String = "normal") -> (CommunityResourceClient, String) {
        let id = mode + ":" + UUID().uuidString
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [CommunityFixtureProtocol.self]
        configuration.httpAdditionalHeaders = ["X-Community-Fixture": id, "Authorization": "fixture-secret", "x-api-key": "fixture-key", "Cookie": "fixture-cookie"]
        let session = URLSession(configuration: configuration)
        addTeardownBlock { session.invalidateAndCancel(); CommunityFixtureProtocol.remove(id) }
        return (CommunityResourceClient(session: session), id)
    }

    func testAllFiveKindsUseOriginalCatalogClassesAndCurrentModrinthTypes() async throws {
        let (client, id) = client()
        for kind in CommunityResourceKind.allCases {
            let page = try await client.search(.init(kind: kind, limit: 4))
            XCTAssertEqual(page.projects.count, 2)
            XCTAssertTrue(page.projects.allSatisfy { $0.kind == kind })
            XCTAssertEqual(Set(page.projects.map(\.source)), [.modrinth, .curseForge])
            let requests = CommunityFixtureProtocol.requests(id)
            let mr = try XCTUnwrap(requests.last { $0.url?.host == "api.modrinth.com" })
            let facets = try XCTUnwrap(Self.query(mr)["facets"])
            XCTAssertTrue(facets.contains("project_type:" + kind.rawValue))
            let cf = try XCTUnwrap(requests.last { $0.url?.host == "mod.mcimirror.top" })
            XCTAssertEqual(Self.query(cf)["classId"], [CommunityResourceKind.mod: "6", .modpack: "4471", .datapack: "6945", .resourcepack: "12", .shader: "6552"][kind])
        }
    }

    func testFiltersAreEncodedAndMirrorNeverReceivesInheritedCredentials() async throws {
        let (client, id) = client()
        let page = try await client.search(.init(kind: .mod, text: "中文 + &= test", minecraftVersion: "1.20.1", loader: .neoForge, category: "406/worldgen", sort: .downloads, offset: 40))
        XCTAssertTrue(page.warnings.isEmpty)
        let requests = CommunityFixtureProtocol.requests(id)
        XCTAssertEqual(requests.count, 2)
        for request in requests {
            XCTAssertNil(request.value(forHTTPHeaderField: "Authorization"))
            XCTAssertNil(request.value(forHTTPHeaderField: "x-api-key"))
            XCTAssertNil(request.value(forHTTPHeaderField: "Cookie"))
            XCTAssertEqual(request.value(forHTTPHeaderField: "User-Agent"), CurseForgeResolver.userAgent)
        }
        let mr = try XCTUnwrap(requests.first { $0.url?.host == "api.modrinth.com" })
        XCTAssertEqual(Self.query(mr)["query"], "中文 + &= test")
        XCTAssertEqual(Self.query(mr)["offset"], "20")
        XCTAssertEqual(Self.query(mr)["index"], "downloads")
        let facets = try XCTUnwrap(Self.query(mr)["facets"])
        XCTAssertTrue(facets.contains("categories:worldgen")); XCTAssertTrue(facets.contains("categories:neoforge")); XCTAssertTrue(facets.contains("versions:1.20.1"))
        let cf = try XCTUnwrap(requests.first { $0.url?.host == "mod.mcimirror.top" })
        XCTAssertEqual(Self.query(cf)["searchFilter"], "中文 + &= test")
        XCTAssertEqual(Self.query(cf)["index"], "20")
        XCTAssertEqual(Self.query(cf)["categoryId"], "406")
        XCTAssertEqual(Self.query(cf)["modLoaderType"], "6")
        XCTAssertEqual(Self.query(cf)["sortField"], "6")
    }

    func testOneCatalogFailureKeepsOtherResultsAndVisibleWarning() async throws {
        let (client, _) = client("cf-fails")
        let page = try await client.search(.init(kind: .mod))
        XCTAssertEqual(page.projects.map(\.source), [.modrinth])
        XCTAssertEqual(page.warnings.count, 1)
        XCTAssertTrue(page.warnings[0].contains("CurseForge"))
    }

    func testSourceSpecificCategoriesAreNotSilentlyIgnored() async throws {
        let (client, id) = client()
        let onlyMR = try await client.search(.init(kind: .mod, category: "/optimization"))
        XCTAssertEqual(onlyMR.projects.map(\.source), [.modrinth])
        XCTAssertEqual(CommunityFixtureProtocol.requests(id).count, 1)
        XCTAssertFalse(onlyMR.warnings.isEmpty)
        let onlyCF = try await client.search(.init(kind: .mod, category: "421/"))
        XCTAssertEqual(onlyCF.projects.map(\.source), [.curseForge])
        XCTAssertFalse(onlyCF.warnings.isEmpty)
        do { _ = try await client.search(.init(kind: .mod, category: "421/", source: .modrinth)); XCTFail("Explicit source cannot silently skip filter") }
        catch CommunityResourceError.invalidRequest { }
    }

    func testModrinthFileHashesDependenciesAndDataPackVersions() async throws {
        let (client, id) = client()
        let project = CommunityProject(projectID: "MRProject", source: .modrinth, kind: .datapack, name: "Fixture", websiteURL: URL(string: "https://modrinth.com/project/MRProject")!)
        let versions = try await client.versions(for: project, minecraftVersion: "1.20.1")
        XCTAssertEqual(versions.count, 1)
        XCTAssertEqual(versions[0].primaryFile?.sha1, String(repeating: "a", count: 40))
        XCTAssertEqual(versions[0].primaryFile?.sha512, String(repeating: "b", count: 128))
        XCTAssertEqual(versions[0].dependencies.filter(\.required).map(\.projectID), ["RequiredDependency"])
        let request = try XCTUnwrap(CommunityFixtureProtocol.requests(id).last)
        XCTAssertEqual(Self.query(request)["loaders"], "[\"datapack\"]")
        XCTAssertEqual(Self.query(request)["game_versions"], "[\"1.20.1\"]")
    }

    func testCurseForgeExactFileUsesNoKeyBatchAndRequiredRelationOnly() async throws {
        let (client, id) = client()
        let version = try await client.version(id: "456", source: .curseForge)
        XCTAssertEqual(version.projectID, "123")
        XCTAssertEqual(version.primaryFile?.filename, "fixture.jar")
        XCTAssertEqual(version.primaryFile?.md5, String(repeating: "c", count: 32))
        XCTAssertEqual(version.dependencies.filter(\.required).map(\.projectID), ["789"])
        XCTAssertEqual(version.loaders, ["fabric"])
        let request = try XCTUnwrap(CommunityFixtureProtocol.requests(id).first)
        XCTAssertEqual(request.httpMethod, "POST")
        XCTAssertEqual(request.url?.path, "/curseforge/v1/mods/files")
        XCTAssertNil(request.value(forHTTPHeaderField: "x-api-key"))
        let body = try XCTUnwrap(request.httpBody)
        XCTAssertEqual((try JSONSerialization.jsonObject(with: body) as? [String: [Int]])?["fileIds"], [456])
    }

    func testUnsafeFileNamesHashesHostsAndForeignProjectVersionsFail() async throws {
        for mode in ["unsafe-name", "unsafe-hash", "unsafe-url", "foreign-project"] {
            let (client, _) = client(mode)
            let project = CommunityProject(projectID: "MRProject", source: .modrinth, kind: .mod, name: "Fixture", websiteURL: URL(string: "https://modrinth.com/project/MRProject")!)
            do { _ = try await client.versions(for: project); XCTFail("Must reject \(mode)") }
            catch { XCTAssertFalse(error is CancellationError) }
        }
    }

    func testDetailBodyAndIdentityArePreserved() async throws {
        let (client, _) = client()
        let mr = try await client.project(id: "MRProject", source: .modrinth)
        let detail = try await client.detail(mr)
        XCTAssertEqual(detail.body, "# Project body")
        XCTAssertEqual(detail.license, "MIT")
        XCTAssertEqual(detail.project.projectID, "MRProject")
        let cf = try await client.project(id: "123", source: .curseForge)
        let cfDetail = try await client.detail(cf)
        XCTAssertEqual(cfDetail.body, "<p>Project body</p>")
        XCTAssertEqual(cfDetail.project.projectID, "123")
    }

    func testUnavailableDescriptionKeepsRealProjectAndVersionAccess() async throws {
        let (client, _) = client("cf-no-description")
        let project = try await client.project(id: "123", source: .curseForge)
        let detail = try await client.detail(project)
        XCTAssertEqual(detail.body, "summary")
        XCTAssertEqual(detail.project.projectID, project.projectID)
        let versions = try await client.versions(for: project)
        XCTAssertFalse(versions.isEmpty)
    }

    func testOddMixedPageOffsetsDoNotRepeatOrSkipProviderPositions() async throws {
        let (client, id) = client()
        let page = try await client.search(.init(kind: .mod, offset: 41, limit: 5))
        XCTAssertEqual(page.projects.first?.source, .curseForge)
        let requests = CommunityFixtureProtocol.requests(id)
        let mr = try XCTUnwrap(requests.first { $0.url?.host == "api.modrinth.com" })
        let cf = try XCTUnwrap(requests.first { $0.url?.host == "mod.mcimirror.top" })
        XCTAssertEqual(Self.query(mr)["offset"], "21")
        XCTAssertEqual(Self.query(mr)["limit"], "2")
        XCTAssertEqual(Self.query(cf)["index"], "20")
        XCTAssertEqual(Self.query(cf)["pageSize"], "3")
        XCTAssertEqual(page.offset, 41)
    }

    func testCurseForgeClientModpacksExcludeServerArchives() async throws {
        let (client, _) = client("server-pack")
        let project = CommunityProject(projectID: "123", source: .curseForge, kind: .modpack, name: "Fixture", websiteURL: URL(string: "https://www.curseforge.com/minecraft/modpacks/fixture")!)
        let values = try await client.versions(for: project)
        XCTAssertEqual(values.map(\.id), ["456"])
    }

    func testInvalidIdentifiersCannotBecomeRequestPaths() async throws {
        let (client, id) = client()
        for value in ["../outside", "id?token=x", "a/b", "", "id#fragment"] {
            do { _ = try await client.project(id: value, source: .modrinth); XCTFail("Invalid ID") } catch { }
        }
        do { _ = try await client.version(id: "0", source: .curseForge); XCTFail("Invalid CF ID") } catch { }
        XCTAssertTrue(CommunityFixtureProtocol.requests(id).isEmpty)
    }

    func testHistoricalUploadsWithoutFilesOrHashesDoNotHideUsableVersions() async throws {
        let (client, _) = client("unavailable-history")
        for source in [CommunitySource.modrinth, .curseForge] {
            let project = try await client.project(id: source == .modrinth ? "MRProject" : "123", source: source)
            let versions = try await client.versions(for: project)
            XCTAssertEqual(versions.count, 1)
            XCTAssertEqual(versions[0].id, source == .modrinth ? "MRVersion" : "456")
        }
    }

    func testMultiTypeModProjectDoesNotListDataPackOrServerPluginVersions() async throws {
        let (client, _) = client("mixed-project")
        let project = try await client.project(id: "MRProject", source: .modrinth, kind: .mod)
        let versions = try await client.versions(for: project)
        XCTAssertEqual(versions.map(\.id), ["MRVersion"])
        let datapack = CommunityProject(projectID: project.projectID, source: project.source, kind: .datapack, name: project.name, websiteURL: project.websiteURL)
        let datapackVersions = try await client.versions(for: datapack)
        XCTAssertEqual(datapackVersions.map(\.id), ["DataPackVersion"])
    }

    func testExactUnavailableFileStillFailsExplicitly() async throws {
        let (client, _) = client("exact-unavailable")
        for (source, id) in [(CommunitySource.modrinth, "MRVersion"), (.curseForge, "456")] {
            do { _ = try await client.version(id: id, source: source); XCTFail("An exact removed file cannot silently change version") }
            catch CommunityResourceError.unavailable { }
        }
    }

    func testOriginalModrinthMirrorMappingKeepsEncodedPathAndQuery() throws {
        let original = URL(string: "https://cdn.modrinth.com/data/Project/versions/Version/中文%20name%2Btest.jar?name=a%2Bb")!
        let candidates = try CommunityResourceClient.modrinthDownloadURLs(original)
        XCTAssertEqual(candidates.count, 2)
        XCTAssertEqual(candidates.first, original)
        XCTAssertEqual(candidates.last?.host, "mod.mcimirror.top")
        XCTAssertEqual(URLComponents(url: candidates[0], resolvingAgainstBaseURL: false)?.percentEncodedPath,
                       URLComponents(url: candidates[1], resolvingAgainstBaseURL: false)?.percentEncodedPath)
        XCTAssertEqual(URLComponents(url: candidates[0], resolvingAgainstBaseURL: false)?.percentEncodedQuery,
                       URLComponents(url: candidates[1], resolvingAgainstBaseURL: false)?.percentEncodedQuery)
        for raw in ["http://cdn.modrinth.com/data/a.jar", "https://cdn.modrinth.com.evil.invalid/data/a.jar",
                    "https://user:password@cdn.modrinth.com/data/a.jar", "https://cdn.modrinth.com:8443/data/a.jar", "https://other.invalid/data/a.jar"] {
            XCTAssertThrowsError(try CommunityResourceClient.modrinthDownloadURLs(URL(string: raw)!))
        }
    }

    func testCommunityModrinthVersionsIncludeBothVerifiedDownloadCandidates() async throws {
        let (client, _) = client()
        let version = try await client.version(id: "MRVersion", source: .modrinth)
        XCTAssertEqual(version.primaryFile?.urls.map(\.host), ["cdn.modrinth.com", "mod.mcimirror.top"])
        XCTAssertEqual(version.primaryFile?.urls.first?.path, version.primaryFile?.urls.last?.path)
    }

    func testOfficialTLSFailureFallsBackToHashVerifiedModrinthMirror() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [ModrinthMirrorFallbackProtocol.self]
        let session = URLSession(configuration: configuration)
        defer { session.invalidateAndCancel() }
        ModrinthMirrorFallbackProtocol.reset()
        let bytes = ModrinthMirrorFallbackProtocol.bytes
        let sha1 = Insecure.SHA1.hash(data: bytes).map { String(format: "%02x", $0) }.joined()
        let sha512 = SHA512.hash(data: bytes).map { String(format: "%02x", $0) }.joined()
        let urls = try CommunityResourceClient.modrinthDownloadURLs(URL(string: "https://cdn.modrinth.com/data/Fixture/versions/Fixture/fixture-fallback.jar")!)
        let file = CommunityResourceFile(filename: "fixture-fallback.jar", size: Int64(bytes.count), urls: urls, sha1: sha1, sha512: sha512, primary: true)
        let version = CommunityVersion(id: "FixtureVersion", source: .modrinth, projectID: "Fixture", name: "Fixture", versionNumber: "1", files: [file])
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent("PCLFallback-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let installer = CommunityResourceInstaller(session: session)
        let destination = try await installer.download(version: version, to: directory.appendingPathComponent(file.filename), progress: { _ in })
        XCTAssertEqual(try Data(contentsOf: destination), bytes)
        let hosts = ModrinthMirrorFallbackProtocol.hosts
        XCTAssertEqual(hosts.first, "cdn.modrinth.com")
        XCTAssertTrue(hosts.filter { $0 == "cdn.modrinth.com" }.count >= 2, "Keep the existing TLS1.2 compatibility attempt before switching source")
        XCTAssertEqual(hosts.last, "mod.mcimirror.top")
        XCTAssertEqual(hosts.filter { $0 == "mod.mcimirror.top" }.count, 1)
        XCTAssertTrue(try CommunityResourceInstaller.fileMatches(at: destination, file: file))
    }

    func testDefaultProductionSessionCanDownloadVerifiedModrinthMirror() async throws {
        guard ProcessInfo.processInfo.environment["PCL_COMMUNITY_MIRROR_SMOKE"] == "1" else { throw XCTSkip("Opt into a real default-session Modrinth mirror download with PCL_COMMUNITY_MIRROR_SMOKE=1") }
        let client = CommunityResourceClient()
        let project = try await client.project(id: "AANobbMI", source: .modrinth)
        let values = try await client.versions(for: project, minecraftVersion: "1.20.1", loader: .fabric)
        let version = try XCTUnwrap(values.first { $0.versionNumber.contains("0.5.8") })
        let file = try XCTUnwrap(version.primaryFile)
        let mirrors = file.urls.filter { $0.host == "mod.mcimirror.top" }
        XCTAssertEqual(mirrors.count, 1)
        let mirrorFile = CommunityResourceFile(filename: file.filename, size: file.size, urls: mirrors, sha1: file.sha1, sha512: file.sha512, md5: file.md5, primary: true)
        let mirrorVersion = CommunityVersion(id: version.id, source: version.source, projectID: version.projectID,
            name: version.name, versionNumber: version.versionNumber, gameVersions: version.gameVersions, loaders: version.loaders,
            published: version.published, releaseType: version.releaseType, files: [mirrorFile], dependencies: version.dependencies)
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent("PCLMirrorSmoke-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let destination = directory.appendingPathComponent(file.filename)
        let installer = CommunityResourceInstaller(session: .shared)
        let saved = try await installer.download(version: mirrorVersion, to: destination, progress: { _ in })
        XCTAssertTrue(try CommunityResourceInstaller.fileMatches(at: saved, file: file))
        let bytes = try Data(contentsOf: saved)
        let sha1 = Insecure.SHA1.hash(data: bytes).map { String(format: "%02x", $0) }.joined()
        let sha512 = SHA512.hash(data: bytes).map { String(format: "%02x", $0) }.joined()
        XCTAssertEqual(sha1, file.sha1)
        XCTAssertEqual(sha512, file.sha512)
        let report: [String: Any] = ["date": "2026-11-01", "defaultSharedSession": true, "proxyConfigurationModified": false,
            "trustEvaluationBypassed": false, "version": version.id, "filename": file.filename, "bytes": bytes.count,
            "mirrorURL": mirrors.first!.absoluteString, "sha1": sha1, "sha512": sha512, "allDeclaredHashesVerified": true]
        let output = URL(fileURLWithPath: ProcessInfo.processInfo.environment["PCL_COMMUNITY_MIRROR_REPORT"] ?? "/tmp/pcl-community-mirror.json")
        try JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys]).write(to: output)
        print("COMMUNITY_MIRROR_REPORT \(output.path)")
    }

    func testReadOnlyLiveCatalogs() async throws {
        guard ProcessInfo.processInfo.environment["PCL_COMMUNITY_API_SMOKE"] == "1" else { throw XCTSkip("Opt into read-only live community catalog validation with PCL_COMMUNITY_API_SMOKE=1") }
        let configuration = URLSessionConfiguration.ephemeral; configuration.connectionProxyDictionary = [:]
        let session = URLSession(configuration: configuration); defer { session.invalidateAndCancel() }
        let client = CommunityResourceClient(session: session)
        var rows: [[String: Any]] = []
        for kind in CommunityResourceKind.allCases {
            let page = try await client.search(.init(kind: kind, limit: 4))
            XCTAssertFalse(page.projects.isEmpty)
            XCTAssertEqual(Set(page.projects.map(\.source)), [.modrinth, .curseForge], "Both public catalogs should answer \(kind)")
            rows.append(["kind": kind.rawValue, "count": page.projects.count, "total": page.total, "sources": page.projects.map { $0.source.rawValue }, "names": page.projects.map(\.name), "warnings": page.warnings])
            if kind == .datapack {
                for source in [CommunitySource.modrinth, .curseForge] {
                    let project = try XCTUnwrap(page.projects.first { $0.source == source })
                    let detail = try await client.detail(project)
                    XCTAssertFalse(detail.body.isEmpty)
                    let versions = try await client.versions(for: project)
                    XCTAssertFalse(versions.isEmpty)
                    XCTAssertNotNil(versions.first?.primaryFile?.sha1)
                    let exact = try await client.version(id: try XCTUnwrap(versions.first?.id), source: source)
                    XCTAssertEqual(exact.projectID, project.projectID)
                    if source == .modrinth { XCTAssertTrue(versions.allSatisfy { $0.loaders.contains("datapack") }) }
                    rows.append(["source": source.rawValue, "project": project.projectID, "bodyCharacters": detail.body.count, "versions": versions.count, "primaryFile": versions.first?.primaryFile?.filename ?? "", "exactFileIdentityVerified": exact.id == versions.first?.id])
                }
            }
        }
        let output = URL(fileURLWithPath: ProcessInfo.processInfo.environment["PCL_COMMUNITY_API_SMOKE_REPORT"] ?? "/tmp/pcl-community-live-catalogs.json")
        try JSONSerialization.data(withJSONObject: ["date": "2026-11-01", "readOnly": true, "userAPIKey": false, "rows": rows], options: [.prettyPrinted, .sortedKeys]).write(to: output)
        print("COMMUNITY_LIVE_REPORT \(output.path)")
    }

    private static func query(_ request: URLRequest) -> [String: String] { Dictionary(uniqueKeysWithValues: (URLComponents(url: request.url!, resolvingAgainstBaseURL: false)?.queryItems ?? []).map { ($0.name, $0.value ?? "") }) }
}

private final class CommunityFixtureProtocol: URLProtocol, @unchecked Sendable {
    private static let lock = NSLock()
    private static var captured: [String: [URLRequest]] = [:]
    static func requests(_ id: String) -> [URLRequest] { lock.lock(); defer { lock.unlock() }; return captured[id] ?? [] }
    static func remove(_ id: String) { lock.lock(); captured[id] = nil; lock.unlock() }
    override class func canInit(with request: URLRequest) -> Bool { request.value(forHTTPHeaderField: "X-Community-Fixture") != nil }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        let id = request.value(forHTTPHeaderField: "X-Community-Fixture") ?? ""
        var recorded = request
        if recorded.httpBody == nil, let stream = request.httpBodyStream {
            stream.open(); defer { stream.close() }
            var body = Data(), bytes = [UInt8](repeating: 0, count: 16_384)
            while stream.hasBytesAvailable {
                let count = stream.read(&bytes, maxLength: bytes.count)
                if count <= 0 { break }
                body.append(contentsOf: bytes.prefix(count))
            }
            recorded.httpBody = body
        }
        Self.lock.lock(); Self.captured[id, default: []].append(recorded); Self.lock.unlock()
        let mode = id.split(separator: ":").first.map(String.init) ?? "normal"
        let url = request.url!
        let status = mode == "cf-fails" && url.host == "mod.mcimirror.top" ? 403 : (mode == "cf-no-description" && url.path.hasSuffix("/description") ? 404 : 200)
        do {
            let body = try JSONSerialization.data(withJSONObject: Self.body(url, mode: mode))
            client?.urlProtocol(self, didReceive: HTTPURLResponse(url: url, statusCode: status, httpVersion: "HTTP/1.1", headerFields: ["Content-Type": "application/json"])!, cacheStoragePolicy: .notAllowed)
            client?.urlProtocol(self, didLoad: body); client?.urlProtocolDidFinishLoading(self)
        } catch { client?.urlProtocol(self, didFailWithError: error) }
    }
    override func stopLoading() { }
    private static let mrProject: [String: Any] = ["id": "MRProject", "project_id": "MRProject", "title": "MR fixture", "slug": "mr-fixture", "description": "summary", "author": "author", "downloads": 10, "project_type": "mod", "categories": ["fabric"], "body": "# Project body", "license": ["name": "MIT"]]
    private static let cfProject: [String: Any] = ["id": 123, "name": "CF fixture", "summary": "summary", "slug": "cf-fixture", "downloadCount": 11, "classId": 6, "links": ["websiteUrl": "https://www.curseforge.com/minecraft/mc-mods/cf-fixture"], "authors": [["name": "author"]]]
    private static var cfFile: [String: Any] { ["id": 456, "modId": 123, "fileName": "fixture.jar", "displayName": "Fixture 1.0", "fileLength": 10, "downloadUrl": NSNull(), "hashes": [["algo": 1, "value": String(repeating: "a", count: 40)], ["algo": 2, "value": String(repeating: "c", count: 32)]], "gameVersions": ["1.20.1", "Fabric"], "fileDate": "2026-10-31T00:00:00Z", "releaseType": 1, "dependencies": [["modId": 789, "relationType": 3], ["modId": 790, "relationType": 2]]] }
    private static func mrVersion(_ mode: String) -> [String: Any] {
        ["id": "MRVersion", "project_id": mode == "foreign-project" ? "Foreign" : "MRProject", "name": "Fixture 1.0", "version_number": "1.0", "game_versions": ["1.20.1"], "loaders": ["fabric", "datapack"], "date_published": "2026-10-31T00:00:00Z", "files": [["filename": mode == "unsafe-name" ? "../outside.jar" : "fixture.jar", "size": 10, "primary": true, "url": mode == "unsafe-url" ? "https://evil.invalid/payload.jar" : "https://cdn.modrinth.com/data/MRProject/versions/MRVersion/fixture.jar", "hashes": ["sha1": mode == "unsafe-hash" ? "invalid" : String(repeating: "a", count: 40), "sha512": String(repeating: "b", count: 128)]]], "dependencies": [["project_id": "RequiredDependency", "version_id": NSNull(), "dependency_type": "required"], ["project_id": "OptionalDependency", "dependency_type": "optional"]]]
    }
    private static func body(_ url: URL, mode: String) -> Any {
        if url.host == "api.modrinth.com" {
            if url.path == "/v2/search" { return ["hits": [mrProject], "total_hits": 42, "offset": 0] }
            if url.path.hasSuffix("/version") {
                if mode == "mixed-project" {
                    var main = mrVersion(mode); main["loaders"] = ["fabric"]
                    var data = mrVersion(mode); data["id"] = "DataPackVersion"; data["loaders"] = ["datapack"]
                    var plugin = mrVersion(mode); plugin["id"] = "PluginVersion"; plugin["loaders"] = ["paper"]
                    return [main, data, plugin]
                }
                if mode == "unavailable-history" {
                    var empty = mrVersion(mode); empty["id"] = "OldEmpty"; empty["files"] = []
                    var unhashed = mrVersion(mode); unhashed["id"] = "OldUnhashed"
                    var files = unhashed["files"] as! [[String: Any]]; files[0]["hashes"] = [String: String](); unhashed["files"] = files
                    return [empty, mrVersion(mode), unhashed]
                }
                return [mrVersion(mode)]
            }
            if url.path.hasPrefix("/v2/version/") {
                var version = mrVersion(mode)
                if mode == "exact-unavailable" { version["files"] = [] }
                return version
            }
            return mrProject
        }
        if url.path.hasSuffix("/description") { return ["data": "<p>Project body</p>"] }
        if url.path.hasSuffix("/mods/search") {
            var project = cfProject
            project["classId"] = Int(URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems?.first(where: { $0.name == "classId" })?.value ?? "6") ?? 6
            return ["data": [project], "pagination": ["totalCount": 42]]
        }
        if url.path.hasSuffix("/files") {
            var files = [cfFile]
            if mode == "unavailable-history" {
                var removed = cfFile; removed["id"] = 457; removed["isAvailable"] = false
                var unhashed = cfFile; unhashed["id"] = 458; unhashed["hashes"] = []
                files = [removed, cfFile, unhashed]
            }
            if mode == "exact-unavailable" { var removed = cfFile; removed["isAvailable"] = false; files = [removed] }
            if mode == "server-pack" { var server = cfFile; server["id"] = 457; server["isServerPack"] = true; files.append(server) }
            return ["data": files, "pagination": ["totalCount": files.count]]
        }
        return ["data": cfProject]
    }
}

private final class ModrinthMirrorFallbackProtocol: URLProtocol, @unchecked Sendable {
    static let bytes = Data("verified full mirror file payload".utf8)
    private static let lock = NSLock()
    private static var values: [String] = []
    static var hosts: [String] { lock.lock(); defer { lock.unlock() }; return values }
    static func reset() { lock.lock(); values = []; lock.unlock() }
    override class func canInit(with request: URLRequest) -> Bool {
        ["cdn.modrinth.com", "mod.mcimirror.top"].contains(request.url?.host ?? "") && request.url?.lastPathComponent == "fixture-fallback.jar"
    }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        let host = request.url!.host!
        Self.lock.lock(); Self.values.append(host); Self.lock.unlock()
        if host == "cdn.modrinth.com" { client?.urlProtocol(self, didFailWithError: URLError(.secureConnectionFailed)); return }
        client?.urlProtocol(self, didReceive: HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: "HTTP/1.1", headerFields: ["Content-Type": "application/octet-stream", "Content-Length": String(Self.bytes.count)])!, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Self.bytes)
        client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() { }
}
