import CryptoKit
import Foundation
import XCTest
import zlib
@testable import PCLCore

final class OptiFineInstallerTests: XCTestCase {
    private func root() throws -> URL {
        let value = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-OptiFine-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: value, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: value) }
        return value.resolvingSymlinksInPath()
    }
    private var version: OptiFineVersion { .init(minecraftVersion: "1.20.1", version: "HD_U_I6", requiredForgeVersion: "47.2.18") }
    private func jar(_ version: OptiFineVersion, destination: URL) throws {
        let bytes = optiFineZIP([("optifine/OptiFineTweaker.class", Data([0xca,0xfe,0xba,0xbe])),
                                 ("notch/net/optifine/Config.class", Data(("OptiFine_" + version.minecraftVersion + "_" + version.version).utf8))])
        try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
        try bytes.write(to: destination)
    }
    private func seed(_ version: OptiFineVersion, root: URL) throws -> URL {
        let folder = root.appendingPathComponent("PCL/installers/optifine/\(version.minecraftVersion)/\(version.version)")
        let file = folder.appendingPathComponent(version.filename)
        try jar(version, destination: file)
        let digest = SHA256.hash(data: try Data(contentsOf: file)).map { String(format: "%02x", $0) }.joined()
        try digest.write(to: folder.appendingPathComponent(version.filename + ".sha256"), atomically: true, encoding: .utf8)
        return file
    }
    private func profile(_ root: URL, id: String = "Prepared") throws -> URL {
        let file = root.appendingPathComponent("versions/\(id)/\(id).json")
        try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
        let metadata: [String: Any] = ["id": id,"jar": id,"pclOriginalVersion":"1.20.1","mainClass":"net.fabricmc.loader.impl.launch.knot.KnotClient",
            "pclLoader":["kind":"fabric","version":"0.18.4","minecraftVersion":"1.20.1"],"libraries":[],"arguments":["game":["--username","${auth_player_name}"]]]
        try JSONSerialization.data(withJSONObject: metadata, options: [.sortedKeys]).write(to: file)
        return file
    }

    func testMirrorCatalogPreservesForgeBoundsSortsAndNormalizesMinecraftIdentity() throws {
        let data = Data(#"[{"mcversion":"1.20.1","type":"HD_U","patch":"I5","filename":"OptiFine_1.20.1_HD_U_I5.jar","forge":"Forge #47.0.35"},{"mcversion":"1.20.1","type":"HD_U_I6","patch":"pre1","filename":"preview_OptiFine_1.20.1_HD_U_I6_pre1.jar","forge":"Forge N/A"},{"mcversion":"1.20.1","type":"HD_U","patch":"I6","filename":"OptiFine_1.20.1_HD_U_I6.jar","forge":"Forge 47.2.18"},{"mcversion":"1.19.4","type":"HD_U","patch":"I4","filename":"OptiFine_1.19.4_HD_U_I4.jar"}]"#.utf8)
        let values = try OptiFineInstaller.parseMirror(data, minecraftVersion: "1.20.1")
        XCTAssertEqual(values.map(\.version), ["HD_U_I6","HD_U_I5","HD_U_I6_pre1"])
        XCTAssertEqual(values.first?.requiredForgeVersion,"47.2.18")
        XCTAssertNil(values.last?.requiredForgeVersion)
        XCTAssertTrue(values.last!.preview)
        XCTAssertThrowsError(try OptiFineInstaller.parseMirror(Data(#"[{"mcversion":"1.20.1","type":"HD_U","patch":"I6","filename":"other.jar"}]"#.utf8), minecraftVersion:"1.20.1"))
    }
    func testOfficialFallbackRetainsPreviewAndForgeIncompatibility() throws {
        let html = "<table><tr><td><a href=\"adloadx?f=OptiFine_1.20.1_HD_U_I6.jar\">mirror</a></td><td class='colForge'>Forge 47.2.18</td></tr><tr><td><a href=\"adloadx?f=preview_OptiFine_1.20.1_HD_U_I6_pre1.jar\">mirror</a></td><td class='colForge'>Forge N/A</td></tr></table>"
        let values = try OptiFineInstaller.parseOfficial(Data(html.utf8), minecraftVersion:"1.20.1")
        XCTAssertEqual(values.count,2);XCTAssertEqual(values[0].requiredForgeVersion,"47.2.18");XCTAssertNil(values[1].requiredForgeVersion)
    }
    func testDetectionUsesMarkersLibrariesAndRenamedModIdentity() throws {
        let marker = try JSONSerialization.jsonObject(with: JSONEncoder().encode(version))
        XCTAssertEqual(try OptiFineInstaller.detect(metadata:["pclOptiFine":marker]),version)
        XCTAssertEqual(try OptiFineInstaller.detect(metadata:["libraries":[["name":"optifine:OptiFine:1.20.1_HD_U_I6"]]])?.version,"HD_U_I6")
        let mods = try root().appendingPathComponent("mods")
        try jar(version,destination:mods.appendingPathComponent("renamed.jar"))
        XCTAssertEqual(try OptiFineInstaller.detect(metadata:[:],modsDirectory:mods)?.version,"HD_U_I6")
        XCTAssertEqual(try OptiFineInstaller.detect(metadata:["pclOptiFine":marker],modsDirectory:mods)?.requiredForgeVersion,"47.2.18")
        XCTAssertNil(try OptiFineInstaller.detect(metadata:["pclOptiFine":marker],modsDirectory:mods.appendingPathComponent("missing")))
        try jar(version,destination:mods.appendingPathComponent("second.jar"))
        XCTAssertThrowsError(try OptiFineInstaller.detect(metadata:[:],modsDirectory:mods))
    }
    func testPublicCatalogSessionDropsCredentialsWhileKeepingInjectedTransport() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.httpAdditionalHeaders = ["Authorization":"test-only-auth", "Cookie":"test-only-cookie", "X-API-Key":"test-only-key"]
        configuration.protocolClasses = [OptiFineMetadataFixtureProtocol.self]
        let session = URLSession(configuration:configuration);defer{session.invalidateAndCancel()}
        let versions = try await OptiFineInstaller(session:session).fetchVersions(minecraftVersion:"1.20.1")
        XCTAssertEqual(versions.map(\.version),["HD_U_I6"])
    }
    func testWorkingMirrorDoesNotFetchOfficialPageBeforeOrAfterDownload() async throws {
        let root=try root(), fixture=OptiFineDownloadFixture(mode:.mirrorSuccess,version:version)
        let configuration=URLSessionConfiguration.ephemeral;configuration.protocolClasses=[OptiFineDownloadProtocol.self]
        let session=URLSession(configuration:configuration);defer{session.invalidateAndCancel()}
        OptiFineDownloadRegistry.shared.set(fixture);defer{OptiFineDownloadRegistry.shared.set(nil)}
        let file=try await OptiFineInstaller(session:session).prepare(version:version,gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/usr/bin/false"),progress:{_ in})
        XCTAssertEqual(try Data(contentsOf:file),fixture.jar)
        XCTAssertEqual(fixture.requests,["mirror"])
    }
    func testOfficialFallbackIsResolvedOnlyAfterMirrorHttpOrArchiveFailure() async throws {
        for mode in [OptiFineDownloadFixture.Mode.mirrorHTTPFailure,.mirrorInvalidArchive] {
            let root=try root(),fixture=OptiFineDownloadFixture(mode:mode,version:version)
            let configuration=URLSessionConfiguration.ephemeral;configuration.protocolClasses=[OptiFineDownloadProtocol.self]
            let session=URLSession(configuration:configuration);defer{session.invalidateAndCancel()}
            OptiFineDownloadRegistry.shared.set(fixture);defer{OptiFineDownloadRegistry.shared.set(nil)}
            let file=try await OptiFineInstaller(session:session).prepare(version:version,gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/usr/bin/false"),progress:{_ in})
            XCTAssertEqual(try Data(contentsOf:file),fixture.jar)
            XCTAssertEqual(fixture.requests,["mirror","official-page","official-file"])
        }
    }
    func testBothDownloadSourcesFailWithoutPublishingUnverifiedCache() async throws {
        let root=try root(),fixture=OptiFineDownloadFixture(mode:.bothFail,version:version)
        let configuration=URLSessionConfiguration.ephemeral;configuration.protocolClasses=[OptiFineDownloadProtocol.self]
        let session=URLSession(configuration:configuration);defer{session.invalidateAndCancel()}
        OptiFineDownloadRegistry.shared.set(fixture);defer{OptiFineDownloadRegistry.shared.set(nil)}
        do {
            _ = try await OptiFineInstaller(session:session).prepare(version:version,gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/usr/bin/false"),progress:{_ in})
            XCTFail("Both failed sources must report failure")
        } catch {XCTAssertTrue(error.localizedDescription.contains("502"));XCTAssertTrue(error.localizedDescription.contains("503"))}
        XCTAssertEqual(fixture.requests,["mirror","official-page"])
        XCTAssertFalse(FileManager.default.fileExists(atPath:root.appendingPathComponent("PCL/installers/optifine/1.20.1/HD_U_I6/"+version.filename).path))
    }
    func testCachedInstallerAppliedAsModPreservesFabricAndCopiesExactBytes() async throws {
        let root = try root(), file = try profile(root), installer = OptiFineInstaller()
        let cached = try seed(version,root:root)
        let java = URL(fileURLWithPath:"/usr/bin/false")
        let prepared = try await installer.prepare(version:version,gameDirectory:root,javaExecutable:java,progress:{_ in})
        XCTAssertEqual(try Data(contentsOf:prepared),try Data(contentsOf:cached))
        try await installer.apply(version:version,toProfile:"Prepared",gameDirectory:root,javaExecutable:java,asMod:true,progress:{_ in})
        let metadata = try JSONSerialization.jsonObject(with:Data(contentsOf:file)) as! [String:Any]
        XCTAssertEqual(metadata["mainClass"] as? String,"net.fabricmc.loader.impl.launch.knot.KnotClient")
        XCTAssertEqual((metadata["pclLoader"] as? [String:String])?["kind"],"fabric")
        XCTAssertEqual(try Data(contentsOf:root.appendingPathComponent("versions/Prepared/mods/"+version.filename)),try Data(contentsOf:cached))
        XCTAssertEqual(try OptiFineInstaller.detect(metadata:metadata),version)
    }
    func testWrongMinecraftAndUnsafeProfileAreRejectedBeforeChanges() async throws {
        let root = try root(), file = try profile(root), before = try Data(contentsOf:file)
        let installer = OptiFineInstaller()
        do {try await installer.apply(version:.init(minecraftVersion:"1.19.4",version:"HD_U_I4"),toProfile:"Prepared",gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/usr/bin/false"),asMod:true,progress:{_ in});XCTFail("Wrong MC accepted")}catch{}
        XCTAssertEqual(try Data(contentsOf:file),before)
        XCTAssertThrowsError(try OptiFineInstaller.validate(.init(minecraftVersion:"1.20.1",version:"HD_U_I6",filename:"../bad.jar")))
    }
    func testModernInheritedLayerKeepsVanillaLibrariesJvmGameArgumentsAndOtherTweakers() throws {
        let base: [String: Any] = ["id":"Prepared", "jar":"Prepared", "mainClass":"net.minecraft.client.main.Main",
            "libraries":[["name":"net.sf.jopt-simple:jopt-simple:5.0.4"], ["name":"org.ow2.asm:asm:4.0"], ["name":"com.mumfrey:liteloader:1.12.2"]],
            "arguments":["game":["--username","${auth_player_name}","--tweakClass","com.mumfrey.liteloader.launch.LiteLoaderTweaker"],
                         "jvm":["-Djava.library.path=${natives_directory}","-cp","${classpath}"]]]
        let layer: [String: Any] = ["id":"1.20.1-OptiFine_HD_U_I6", "inheritsFrom":"1.20.1", "mainClass":"net.minecraft.launchwrapper.Launch",
            "libraries":[["name":"optifine:OptiFine:1.20.1_HD_U_I6"], ["name":"optifine:launchwrapper-of:2.3"], ["name":"org.ow2.asm:asm:9.0"]],
            "arguments":["game":["--tweakClass","optifine.OptiFineTweaker"],"jvm":["-Doptifine.fixture=true"]]]
        let merged = try OptiFineInstaller.mergeGeneratedProfile(base:base,layer:layer)
        let names = (merged["libraries"] as! [[String:Any]]).compactMap{$0["name"] as? String}
        XCTAssertTrue(names.contains("net.sf.jopt-simple:jopt-simple:5.0.4"));XCTAssertTrue(names.contains("com.mumfrey:liteloader:1.12.2"))
        XCTAssertTrue(names.contains("org.ow2.asm:asm:9.0"));XCTAssertFalse(names.contains("org.ow2.asm:asm:4.0"))
        let arguments = merged["arguments"] as! [String:Any]
        XCTAssertEqual(arguments["game"] as? [String],["--username","${auth_player_name}","--tweakClass","com.mumfrey.liteloader.launch.LiteLoaderTweaker","--tweakClass","optifine.OptiFineTweaker"])
        XCTAssertEqual(arguments["jvm"] as? [String],["-Djava.library.path=${natives_directory}","-cp","${classpath}","-Doptifine.fixture=true"])
        XCTAssertEqual(merged["id"] as? String,"Prepared");XCTAssertEqual(merged["jar"] as? String,"Prepared");XCTAssertNil(merged["inheritsFrom"])
        XCTAssertEqual(merged["mainClass"] as? String,"net.minecraft.launchwrapper.Launch")
    }
    func testLegacyLiteCombinationUsesForgeTweakerLastAndPreservesArguments() throws {
        var profile: [String:Any] = ["libraries":[["name":"com.mumfrey:liteloader:1.12.2-SNAPSHOT"]],
            "minecraftArguments":"--username ${auth_player_name} --tweakClass optifine.OptiFineTweaker --gameDir \"a folder\" --tweakClass com.mumfrey.liteloader.launch.LiteLoaderTweaker --tweakClass optifine.OptiFineForgeTweaker"]
        try OptiFineInstaller.appendLegacyTweaker(to:&profile)
        let game = try MinecraftArguments.tokenize(profile["minecraftArguments"] as! String)
        XCTAssertEqual(Array(game.suffix(2)),["--tweakClass","optifine.OptiFineForgeTweaker"])
        XCTAssertFalse(game.contains("optifine.OptiFineTweaker"))
        XCTAssertEqual(game.filter{$0 == "optifine.OptiFineForgeTweaker"}.count,1)
        XCTAssertTrue(game.contains("a folder"));XCTAssertTrue(game.contains("com.mumfrey.liteloader.launch.LiteLoaderTweaker"));XCTAssertTrue(game.contains("--username"))
    }
    func testLegacyTweakerSelectionRecognizesArrayMarkersAndKeepsStandalonePlain() throws {
        for mode in ["marker","library","fml"] {
            var profile: [String:Any] = ["arguments":["game":["--version","1.12.2","--tweakClass","optifine.OptiFineTweaker"]]]
            if mode == "marker" {profile["pclLiteLoader"] = ["version":"1.12.2-SNAPSHOT"]}
            if mode == "library" {profile["libraries"] = [["name":"net.minecraftforge:forge:1.12.2-14.23.5.2860"]]}
            if mode == "fml" {profile["arguments"] = ["game":["--version","1.12.2","--tweakClass","net.minecraftforge.fml.common.launcher.FMLTweaker"]]}
            try OptiFineInstaller.appendLegacyTweaker(to:&profile)
            let game = (profile["arguments"] as! [String:Any])["game"] as! [String]
            XCTAssertEqual(Array(game.suffix(2)),["--tweakClass","optifine.OptiFineForgeTweaker"])
            XCTAssertEqual(game.filter{$0 == "--version"}.count,1)
        }
        var standalone: [String:Any] = ["minecraftArguments":"--version 1.12.2"]
        try OptiFineInstaller.appendLegacyTweaker(to:&standalone)
        XCTAssertEqual(Array(try MinecraftArguments.tokenize(standalone["minecraftArguments"] as! String).suffix(2)),["--tweakClass","optifine.OptiFineTweaker"])
    }
    func testRealModernOptiFineInstallerPatchesVanillaClient() async throws {
        let environment = ProcessInfo.processInfo.environment
        guard let path=environment["PCL_OPTIFINE_REAL_ROOT"],let source=environment["PCL_OPTIFINE_REAL_CLIENT"] else {throw XCTSkip("Opt in with an isolated root and read-only vanilla 1.20.1 client")}
        let mc = environment["PCL_OPTIFINE_REAL_MC"] ?? "1.20.1"
        let edition = mc == "1.12.2" ? "HD_U_G5" : "HD_U_I6"
        let root=URL(fileURLWithPath:path).resolvingSymlinksInPath();guard root.path.contains(".build/legacy-components-validation/") else {throw XCTSkip("Only isolated validation roots accepted")}
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        let vanilla=MinecraftInstaller();let manifest=try await vanilla.fetchManifest();let remote=try XCTUnwrap(manifest.versions.first{$0.id==mc})
        let bytes=try await MinecraftInstaller.fetchData(url:remote.url)
        var metadata=try JSONSerialization.jsonObject(with:bytes) as! [String:Any]
        let expected=((metadata["downloads"] as! [String:Any])["client"] as! [String:Any])["sha1"] as! String
        XCTAssertEqual(MinecraftInstaller.hash(try Data(contentsOf:URL(fileURLWithPath:source))),expected)
        let id=mc == "1.12.2" ? "OptiFine-Legacy-Smoke" : "OptiFine-Modern-Smoke", folder=root.appendingPathComponent("versions/"+id);try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        metadata["id"]=id;metadata["jar"]=id;metadata["pclOriginalVersion"]=mc
        try JSONSerialization.data(withJSONObject:metadata,options:[.prettyPrinted,.sortedKeys]).write(to:folder.appendingPathComponent(id+".json"))
        if !FileManager.default.fileExists(atPath:folder.appendingPathComponent(id+".jar").path){try ModpackImporter.cloneOrCopy(URL(fileURLWithPath:source),to:folder.appendingPathComponent(id+".jar"))}
        let java=try XCTUnwrap(JavaDiscovery.discover().first{$0.majorVersion==17}).executable
        let component=OptiFineInstaller();let entries=try await component.fetchVersions(minecraftVersion:mc);let selected=try XCTUnwrap(entries.first{$0.version==edition})
        try await component.apply(version:selected,toProfile:id,gameDirectory:root,javaExecutable:java,asMod:false,progress:{print("OPTIFINE_REAL \($0.message)")})
        let installed=try MinecraftVersionMetadata.resolve(versionID:id,gameDirectory:root)
        XCTAssertEqual(installed["mainClass"] as? String,"net.minecraft.launchwrapper.Launch")
        let libs=installed["libraries"] as? [[String:Any]] ?? []
        let baseLibraries = metadata["libraries"] as? [[String:Any]] ?? []
        XCTAssertGreaterThanOrEqual(libs.count,baseLibraries.count)
        XCTAssertTrue(libs.contains{($0["name"] as? String)?.hasPrefix("net.sf.jopt-simple:jopt-simple:")==true})
        XCTAssertTrue(((installed["arguments"] as? [String:Any])?["game"] as? [Any])?.contains(where: { $0 as? String == "--username" }) == true || (installed["minecraftArguments"] as? String)?.contains("--username") == true)
        for library in libs where (library["name"] as? String)?.hasPrefix("optifine:")==true {
            XCTAssertTrue(FileManager.default.fileExists(atPath:root.appendingPathComponent("libraries/"+MinecraftVersionMetadata.libraryPath(library)!).path))
        }
        XCTAssertEqual(try OptiFineInstaller.detect(metadata:installed)?.version,edition)
        if let cachedPath = environment["PCL_OPTIFINE_REAL_LIBRARY_ROOT"] {
            let cache = URL(fileURLWithPath:cachedPath)
            var artifacts: [(String,[String:Any]?)] = []
            for library in libs where InstallPlatform.libraryAllowed(library) && !(library["name"] as? String ?? "").hasPrefix("optifine:") {
                let downloads=library["downloads"] as? [String:Any]
                if downloads == nil || downloads?["artifact"] != nil, let path=MinecraftVersionMetadata.libraryPath(library) { artifacts.append((path,downloads?["artifact"] as? [String:Any])) }
                if let classifier=InstallPlatform.nativeClassifier(for:library),let native=(downloads?["classifiers"] as? [String:Any])?[classifier] as? [String:Any],let path=native["path"] as? String { artifacts.append((path,native)) }
            }
            for (path,information) in artifacts {
                let source=try MinecraftVersionMetadata.safeFile(relativePath:"libraries/"+path,under:cache)
                if let hash=information?["sha1"] as? String { XCTAssertTrue(try MinecraftInstaller.matches(source,sha1:hash,size:(information?["size"] as? NSNumber)?.int64Value)) }
                let destination=try MinecraftVersionMetadata.safeFile(relativePath:"libraries/"+path,under:root)
                if !FileManager.default.fileExists(atPath:destination.path) { try FileManager.default.createDirectory(at:destination.deletingLastPathComponent(),withIntermediateDirectories:true);try ModpackImporter.cloneOrCopy(source,to:destination) }
            }
            if let logging=((installed["logging"] as? [String:Any])?["client"] as? [String:Any])?["file"] as? [String:Any],let name=logging["id"] as? String {
                let path="assets/log_configs/"+name,source=try MinecraftVersionMetadata.safeFile(relativePath:path,under:cache),destination=try MinecraftVersionMetadata.safeFile(relativePath:path,under:root)
                if !FileManager.default.fileExists(atPath:destination.path) {try FileManager.default.createDirectory(at:destination.deletingLastPathComponent(),withIntermediateDirectories:true);try ModpackImporter.cloneOrCopy(source,to:destination)}
            }
            let plan=try MinecraftLauncher.makeLaunchPlan(configuration:.init(gameDirectory:root,versionID:id,playerName:"PCLSmoke",javaExecutable:java,memoryMB:2048,runtimeGameDirectory:folder))
            XCTAssertTrue(plan.arguments.contains{$0.contains("jopt-simple")})
            XCTAssertTrue(plan.arguments.contains("--username"));XCTAssertTrue(plan.arguments.contains("optifine.OptiFineTweaker"));XCTAssertTrue(plan.arguments.contains("net.minecraft.launchwrapper.Launch"))
            XCTAssertFalse(plan.arguments.contains{$0.contains("${")})
            print("OPTIFINE_REAL_FULL_LAUNCH_PLAN_READY libraries=\(libs.count) basecacheArtifacts=\(artifacts.count)")
        }
        print("OPTIFINE_REAL_INSTALLER_READY \(root.path)")
    }
}

private final class OptiFineMetadataFixtureProtocol: URLProtocol {
    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        XCTAssertNil(request.value(forHTTPHeaderField:"Authorization"));XCTAssertNil(request.value(forHTTPHeaderField:"Cookie"));XCTAssertNil(request.value(forHTTPHeaderField:"X-API-Key"))
        XCTAssertEqual(request.url?.host,"bmclapi2.bangbang93.com")
        let data=Data(#"[{"mcversion":"1.20.1","type":"HD_U","patch":"I6","filename":"OptiFine_1.20.1_HD_U_I6.jar","forge":"Forge 47.2.18"}]"#.utf8)
        client?.urlProtocol(self,didReceive:HTTPURLResponse(url:request.url!,statusCode:200,httpVersion:"HTTP/1.1",headerFields:["Content-Type":"application/json"])!,cacheStoragePolicy:.notAllowed)
        client?.urlProtocol(self,didLoad:data);client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() {}
}

private final class OptiFineDownloadFixture: @unchecked Sendable {
    enum Mode {case mirrorSuccess,mirrorHTTPFailure,mirrorInvalidArchive,bothFail}
    let mode:Mode
    let version:OptiFineVersion
    let jar:Data
    private let lock=NSLock()
    private var recorded:[String]=[]
    var requests:[String] {lock.lock();defer{lock.unlock()};return recorded}
    init(mode:Mode,version:OptiFineVersion) {
        self.mode=mode;self.version=version
        jar=optiFineZIP([("optifine/OptiFineTweaker.class",Data([0xca,0xfe,0xba,0xbe])),("notch/net/optifine/Config.class",Data("OptiFine_1.20.1_HD_U_I6".utf8)),("padding.bin",Data(repeating:0,count:310_000))])
    }
    func record(_ value:String) {lock.lock();recorded.append(value);lock.unlock()}
}
private final class OptiFineDownloadRegistry: @unchecked Sendable {
    static let shared=OptiFineDownloadRegistry()
    private let lock=NSLock()
    private var fixture:OptiFineDownloadFixture?
    func set(_ value:OptiFineDownloadFixture?) {lock.lock();fixture=value;lock.unlock()}
    func get()->OptiFineDownloadFixture? {lock.lock();defer{lock.unlock()};return fixture}
}
private final class OptiFineDownloadProtocol:URLProtocol,@unchecked Sendable {
    override class func canInit(with request:URLRequest)->Bool{true}
    override class func canonicalRequest(for request:URLRequest)->URLRequest{request}
    override func startLoading(){
        guard let fixture=OptiFineDownloadRegistry.shared.get(),let url=request.url else {client?.urlProtocol(self,didFailWithError:URLError(.badURL));return}
        var status=200,data=Data()
        if url.host=="bmclapi2.bangbang93.com" {
            fixture.record("mirror")
            switch fixture.mode {
            case .mirrorSuccess:data=fixture.jar
            case .mirrorInvalidArchive:data=Data(repeating:1,count:310_000)
            default:status=502
            }
        } else if url.host=="optifine.net",url.path=="/adloadx" {
            fixture.record("official-page")
            if fixture.mode == .bothFail {status=503}
            else {data=Data(("<a href=\"downloadx?f="+fixture.version.filename+"&amp;fixture=1\">Download</a>").utf8)}
        } else if url.host=="optifine.net",url.path=="/downloadx" {fixture.record("official-file");data=fixture.jar}
        else {XCTFail("Unexpected OptiFine fixture request");status=404}
        client?.urlProtocol(self,didReceive:HTTPURLResponse(url:url,statusCode:status,httpVersion:"HTTP/1.1",headerFields:["Content-Length":String(data.count)])!,cacheStoragePolicy:.notAllowed)
        client?.urlProtocol(self,didLoad:data);client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading(){}
}

private func optiFineZIP(_ files:[(String,Data)]) -> Data {
    var output=Data(),central=Data()
    func u16(_ value:Int)->Data {var n=UInt16(value).littleEndian;return withUnsafeBytes(of:&n){Data($0)}}
    func u32(_ value:Int)->Data {var n=UInt32(value).littleEndian;return withUnsafeBytes(of:&n){Data($0)}}
    for (name,data) in files {
        let nameBytes=Data(name.utf8),offset=output.count
        let crc=data.withUnsafeBytes{Int(crc32(0,$0.bindMemory(to:Bytef.self).baseAddress,uInt(data.count)))}
        for part in [u32(0x04034b50),u16(20),u16(0),u16(0),u16(0),u16(0),u32(crc),u32(data.count),u32(data.count),u16(nameBytes.count),u16(0),nameBytes,data] { output.append(part) }
        for part in [u32(0x02014b50),u16(20),u16(20),u16(0),u16(0),u16(0),u16(0),u32(crc),u32(data.count),u32(data.count),u16(nameBytes.count),u16(0),u16(0),u16(0),u16(0),u32(0),u32(offset),nameBytes] { central.append(part) }
    }
    let start=output.count;output.append(central)
    for part in [u32(0x06054b50),u16(0),u16(0),u16(files.count),u16(files.count),u32(central.count),u32(start),u16(0)] { output.append(part) }
    return output
}
