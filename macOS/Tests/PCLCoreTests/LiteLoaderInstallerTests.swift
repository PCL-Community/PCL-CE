import CryptoKit
import Foundation
import XCTest
import zlib
@testable import PCLCore

final class LiteLoaderInstallerTests: XCTestCase {
    private let tweaker = "com.mumfrey.liteloader.launch.LiteLoaderTweaker"
    private func root() throws -> URL {
        let value = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-LiteLoader-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: value, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: value) }
        return value
    }
    private func fixture() throws -> LiteFixture {
        let jar = liteZIP([(tweaker.replacingOccurrences(of: ".", with: "/") + ".class", Data("fixture-class".utf8))])
        return LiteFixture(jar: jar, tweaker: tweaker)
    }
    private func session(_ fixture: LiteFixture) -> URLSession {
        LiteProtocol.register(fixture)
        let config = URLSessionConfiguration.ephemeral; config.protocolClasses = [LiteProtocol.self]
        config.httpAdditionalHeaders = ["X-PCL-Lite-Fixture": fixture.id, "Authorization": "must-not-leave-session", "Cookie": "private=1"]
        let session = URLSession(configuration: config)
        addTeardownBlock { session.invalidateAndCancel(); LiteProtocol.unregister(fixture.id) }
        return session
    }
    private func profile(_ root: URL, modern: Bool = false, forge: Bool = false) throws -> URL {
        let target = root.appendingPathComponent("versions/Prepared/Prepared.json")
        try FileManager.default.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
        var data: [String: Any] = ["id":"Prepared", "pclOriginalVersion":"1.12.2", "jar":"1.12.2", "mainClass":"net.minecraft.client.main.Main", "assets":"fixture", "libraries":[]]
        let arguments = ["--username", "${auth_player_name}", "--gameDir", "${game_directory}"] + (forge ? ["--tweakClass", "net.minecraftforge.fml.common.launcher.FMLTweaker"] : [])
        if modern { data["arguments"] = ["game":arguments] }
        else { data["minecraftArguments"] = arguments.joined(separator:" ") }
        if forge {
            data["pclLoader"] = ["kind":"forge","version":"14.23.5.2860","minecraftVersion":"1.12.2"]
            data["libraries"] = [["name":"org.ow2.asm:asm-all:5.2", "downloads":["artifact":["path":"org/ow2/asm/asm-all/5.2/asm-all-5.2.jar", "url":"https://repo.maven.apache.org/maven2/org/ow2/asm/asm-all/5.2/asm-all-5.2.jar", "sha1":MinecraftInstaller.hash(Data("asm".utf8))]]]]
        }
        try JSONSerialization.data(withJSONObject: data).write(to: target)
        return target
    }
    private func installCachedDependencies(_ root: URL) throws {
        for (path, bytes) in [("net/minecraft/launchwrapper/1.12/launchwrapper-1.12.jar",Data("wrapper".utf8)),("org/ow2/asm/asm-all/5.2/asm-all-5.2.jar",Data("asm".utf8))] {
            let file = root.appendingPathComponent("libraries/" + path)
            try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
            try bytes.write(to: file)
        }
    }

    func testCatalogueRetainsHistoricBuildsDeduplicatesLatestAndFiltersExactMinecraft() async throws {
        let f = try fixture(), installer = LiteLoaderInstaller(session: session(f))
        let versions = try await installer.fetchVersions(minecraftVersion:"1.12.2")
        XCTAssertEqual(versions.count,1); XCTAssertEqual(versions[0].version,"1.12.2-SNAPSHOT")
        XCTAssertTrue(versions[0].preview)
        let unsupported = try await installer.fetchVersions(minecraftVersion:"1.20.1")
        XCTAssertTrue(unsupported.isEmpty)
        XCTAssertTrue(f.credentialHeaders.isEmpty,"Public catalogue must not receive inherited authentication/cookies")
    }

    func testLegacyApplyPreservesForgeTweakerAndPublishesOnlyAfterAllVerifiedLibraries() async throws {
        let root = try root(), target = try profile(root, forge:true), f = try fixture()
        try installCachedDependencies(root)
        let installer = LiteLoaderInstaller(session: session(f))
        let versions = try await installer.fetchVersions(minecraftVersion:"1.12.2")
        let version = try XCTUnwrap(versions.first)
        try await installer.apply(version:version,toProfile:"Prepared",gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/unused"),progress:{_ in})
        let data = try MinecraftVersionMetadata.load(versionID:"Prepared",gameDirectory:root)
        XCTAssertEqual(data["mainClass"] as? String,"net.minecraft.launchwrapper.Launch")
        let arguments = try MinecraftArguments.tokenize(try XCTUnwrap(data["minecraftArguments"] as? String))
        XCTAssertTrue(arguments.contains("net.minecraftforge.fml.common.launcher.FMLTweaker")); XCTAssertTrue(arguments.contains(tweaker))
        XCTAssertEqual(arguments.filter{$0==tweaker}.count,1)
        XCTAssertEqual(try LiteLoaderInstaller.detect(metadata:data)?.version,version.version)
        XCTAssertEqual((data["pclLoader"] as? [String:String])?["kind"],"forge")
        let libs = try XCTUnwrap(data["libraries"] as? [[String:Any]])
        XCTAssertEqual(libs.filter{($0["name"] as? String)?.hasPrefix("org.ow2.asm:asm-all:")==true}.count,1)
        XCTAssertTrue(f.paths.contains{ $0.contains("liteloader-1.12.2-SNAPSHOT.jar") })
        let written = try Data(contentsOf:target)
        try await installer.apply(version:version,toProfile:"Prepared",gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/unused"),progress:{_ in})
        XCTAssertEqual(try Data(contentsOf:target),written,"Reapplying must not duplicate tweakers or metadata")
    }

    func testModernArgumentsRetainOptiFineTweakerAndAreNotReplacedByLegacyString() async throws {
        let root = try root(), target = try profile(root,modern:true), f = try fixture()
        try installCachedDependencies(root)
        var metadata = try JSONSerialization.jsonObject(with:Data(contentsOf:target)) as! [String:Any]
        metadata["arguments"] = ["game":["--username","${auth_player_name}","--tweakClass","optifine.OptiFineTweaker"]]
        try JSONSerialization.data(withJSONObject:metadata).write(to:target)
        let installer = LiteLoaderInstaller(session:session(f)), version = f.model
        try await installer.apply(version:version,toProfile:"Prepared",gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/unused"),progress:{_ in})
        let after = try MinecraftVersionMetadata.load(versionID:"Prepared",gameDirectory:root)
        let game = (after["arguments"] as? [String:Any])?["game"] as? [String]
        XCTAssertTrue(game?.contains("optifine.OptiFineTweaker")==true);XCTAssertTrue(game?.contains(tweaker)==true)
        XCTAssertTrue(game?.contains("${auth_player_name}")==true)
    }

    func testChecksumFailureLeavesExistingProfileByteIdentical() async throws {
        let root = try root(), target = try profile(root), f = try fixture()
        let original = try Data(contentsOf:target)
        f.corruptJar = true
        let installer = LiteLoaderInstaller(session:session(f))
        do { try await installer.apply(version:f.model,toProfile:"Prepared",gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/unused"),progress:{_ in});XCTFail("Should reject corrupt JAR") } catch {}
        XCTAssertEqual(try Data(contentsOf:target),original)
    }

    func testReplacingDifferentBuildWithSameVersionUpdatesSelectedRuntimeHash() async throws {
        let root = try root(), target = try profile(root), first = try fixture()
        try installCachedDependencies(root)
        try await LiteLoaderInstaller(session:session(first)).apply(version:first.model,toProfile:"Prepared",gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/unused"),progress:{_ in})
        let second = LiteFixture(jar:liteZIP([(tweaker.replacingOccurrences(of:".",with:"/")+".class",Data("different-build".utf8))]),tweaker:tweaker)
        try await LiteLoaderInstaller(session:session(second)).apply(version:second.model,toProfile:"Prepared",gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/unused"),progress:{_ in})
        let metadata = try JSONSerialization.jsonObject(with:Data(contentsOf:target)) as! [String:Any]
        let libraries = metadata["libraries"] as! [[String:Any]]
        let lite = try XCTUnwrap(libraries.first{($0["name"] as? String)?.hasPrefix("com.mumfrey:liteloader:")==true})
        XCTAssertEqual(((lite["downloads"] as? [String:Any])?["artifact"] as? [String:Any])?["sha1"] as? String,MinecraftInstaller.hash(second.jar))
        XCTAssertEqual(try LiteLoaderInstaller.detect(metadata:metadata)?.md5,second.digest)
    }

    func testStalePreparedProfileAndUnsafeTargetCannotBeOverwritten() async throws {
        let root = try root(), target = try profile(root), f = try fixture()
        try installCachedDependencies(root)
        let external = Data(#"{"id":"external change"}"#.utf8)
        f.onJar = {try? external.write(to:target)}
        let installer = LiteLoaderInstaller(session:session(f))
        do {try await installer.apply(version:f.model,toProfile:"Prepared",gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/unused"),progress:{_ in});XCTFail("Should reject externally changed profile")}catch{}
        XCTAssertEqual(try Data(contentsOf:target),external)
        try FileManager.default.removeItem(at:target)
        let outside = root.appendingPathComponent("outside.json");try external.write(to:outside)
        try FileManager.default.createSymbolicLink(at:target,withDestinationURL:outside)
        do {try await installer.apply(version:f.model,toProfile:"Prepared",gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/unused"),progress:{_ in});XCTFail("Should reject symlink")}catch{}
        XCTAssertEqual(try Data(contentsOf:outside),external)
    }

    func testOfflineDetectionRecognizesOldUnmarkedLibraryAndRejectsBrokenMarker() throws {
        let metadata:[String:Any] = ["id":"pack","pclOriginalVersion":"1.7.10","libraries":[["name":"com.mumfrey:liteloader:1.7.10_04"]]]
        let detected = try XCTUnwrap(LiteLoaderInstaller.detect(metadata:metadata))
        XCTAssertEqual(detected.minecraftVersion,"1.7.10");XCTAssertEqual(detected.version,"1.7.10_04");XCTAssertEqual(detected.md5,"")
        XCTAssertNil(try LiteLoaderInstaller.detect(metadata:["libraries":[]]))
        XCTAssertThrowsError(try LiteLoaderInstaller.detect(metadata:["pclLiteLoader":["version":"bad"]]))
    }

    func testLiveOfficialLiteLoaderDownloadAndVerifiedLibraries() async throws {
        guard ProcessInfo.processInfo.environment["PCL_LITELOADER_LIVE"] == "1" else {throw XCTSkip("Set PCL_LITELOADER_LIVE=1 for official catalogue and library downloads")}
        let work = ProcessInfo.processInfo.environment["PCL_LITELOADER_LIVE_OUTPUT"].map { URL(fileURLWithPath:$0) }
            ?? URL(fileURLWithPath:#filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent(".build/legacy-components-validation")
        let root = work.appendingPathComponent("lite-live-"+UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        let target = try profile(root)
        let minecraft = ProcessInfo.processInfo.environment["PCL_LITELOADER_LIVE_MC"] ?? "1.12.2"
        if minecraft != "1.12.2" {
            var base = try JSONSerialization.jsonObject(with:Data(contentsOf:target)) as! [String:Any]
            base["pclOriginalVersion"] = minecraft; base["jar"] = minecraft
            try JSONSerialization.data(withJSONObject:base).write(to:target)
        }
        let installer = LiteLoaderInstaller(), entries = try await installer.fetchVersions(minecraftVersion:minecraft)
        let selected = try XCTUnwrap(entries.first)
        try await installer.apply(version:selected,toProfile:"Prepared",gameDirectory:root,javaExecutable:URL(fileURLWithPath:"/unused"),progress:{print("LITE_LIVE \($0.message)")})
        let resolved = try MinecraftVersionMetadata.resolve(versionID:"Prepared",gameDirectory:root)
        let libraries = resolved["libraries"] as! [[String:Any]]
        for library in libraries {
            let path = try XCTUnwrap(MinecraftVersionMetadata.libraryPath(library)), file = root.appendingPathComponent("libraries/"+path)
            let artifact = (library["downloads"] as? [String:Any])?["artifact"] as? [String:Any]
            XCTAssertTrue(try MinecraftInstaller.matches(file,sha1:try XCTUnwrap(artifact?["sha1"] as? String),size:nil))
        }
        let report:[String:Any] = ["root":root.path,"profile":target.path,"version":selected.version,"minecraft":selected.minecraftVersion,"verifiedLibraryCount":libraries.count,"mainClass":resolved["mainClass"]!,"gameplayLaunched":false]
        let reportName = minecraft == "1.12.2" ? "liteloader-live.json" : "liteloader-live-\(minecraft).json"
        try JSONSerialization.data(withJSONObject:report,options:[.prettyPrinted,.sortedKeys]).write(to:work.appendingPathComponent(reportName))
        print("LITE_LIVE_VERIFIED \(root.path)")
    }
}

private final class LiteFixture: @unchecked Sendable {
    let id=UUID().uuidString, jar:Data, tweaker:String
    var corruptJar=false
    var onJar:(()->Void)?
    private let lock=NSLock()
    private var visited:[String]=[], credentials:[String]=[]
    init(jar:Data,tweaker:String){self.jar=jar;self.tweaker=tweaker}
    var paths:[String]{lock.lock();defer{lock.unlock()};return visited}
    var credentialHeaders:[String]{lock.lock();defer{lock.unlock()};return credentials}
    var digest:String{Insecure.MD5.hash(data:jar).map{String(format:"%02x",$0)}.joined()}
    var token:[String:Any]{["version":"1.12.2-SNAPSHOT","file":"liteloader-1.12.2-SNAPSHOT.jar","md5":digest,"tweakClass":tweaker,"stream":"SNAPSHOT","timestamp":"1511880271","libraries":[["name":"net.minecraft:launchwrapper:1.12"],["name":"org.ow2.asm:asm-all:5.2"]]]}
    var model:LiteLoaderVersion{.init(minecraftVersion:"1.12.2",version:"1.12.2-SNAPSHOT",filename:"liteloader-1.12.2-SNAPSHOT.jar",metadata:try! JSONSerialization.data(withJSONObject:token),md5:digest,preview:true)}
    func data(_ request:URLRequest)throws->Data?{
        let path=request.url!.path
        lock.lock();visited.append(path)
        if request.value(forHTTPHeaderField:"Authorization") != nil || request.value(forHTTPHeaderField:"Cookie") != nil {credentials.append(path)}
        lock.unlock()
        if path.hasSuffix("versions.json"){return try JSONSerialization.data(withJSONObject:["versions":["1.12.2":["snapshots":["com.mumfrey:liteloader":[digest:token,"latest":token]]]]])}
        if path.hasSuffix("liteloader-1.12.2-SNAPSHOT.jar"){onJar?();return corruptJar ? Data("bad".utf8):jar}
        if path.hasSuffix("launchwrapper-1.12.jar.sha1"){return Data(MinecraftInstaller.hash(Data("wrapper".utf8)).utf8)}
        if path.hasSuffix("asm-all-5.2.jar.sha1"){return Data(MinecraftInstaller.hash(Data("asm".utf8)).utf8)}
        return nil
    }
}
private final class LiteProtocol:URLProtocol,@unchecked Sendable{
    static let lock=NSLock();static var fixtures:[String:LiteFixture]=[:]
    static func register(_ f:LiteFixture){lock.lock();fixtures[f.id]=f;lock.unlock()}
    static func unregister(_ id:String){lock.lock();fixtures[id]=nil;lock.unlock()}
    override class func canInit(with request:URLRequest)->Bool{true}
    override class func canonicalRequest(for request:URLRequest)->URLRequest{request}
    override func startLoading(){
        Self.lock.lock();let fixture=request.value(forHTTPHeaderField:"X-PCL-Lite-Fixture").flatMap{Self.fixtures[$0]};Self.lock.unlock()
        guard let f=fixture,let data=try? f.data(request),let url=request.url else{client?.urlProtocol(self,didFailWithError:URLError(.resourceUnavailable));return}
        client?.urlProtocol(self,didReceive:HTTPURLResponse(url:url,statusCode:200,httpVersion:nil,headerFields:["Content-Length":String(data.count)])!,cacheStoragePolicy:.notAllowed)
        client?.urlProtocol(self,didLoad:data);client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading(){}
}
private func liteZIP(_ files:[(String,Data)])->Data{
    var out=Data(),central=Data()
    func w<T:FixedWidthInteger>(_ v:T)->Data{withUnsafeBytes(of:v.littleEndian){Data($0)}}
    for (name,data) in files{
        let n=Data(name.utf8),crc=data.withUnsafeBytes{UInt32(zlib.crc32(0,$0.bindMemory(to:Bytef.self).baseAddress,uInt(data.count)))},offset=UInt32(out.count)
        out += w(UInt32(0x04034b50));out += w(UInt16(20));out += w(UInt16(0x800));out += w(UInt16(0));out += w(UInt16(0));out += w(UInt16(0));out += w(crc);out += w(UInt32(data.count));out += w(UInt32(data.count));out += w(UInt16(n.count));out += w(UInt16(0));out += n;out += data
        central += w(UInt32(0x02014b50));central += w(UInt16(20));central += w(UInt16(20));central += w(UInt16(0x800));central += w(UInt16(0));central += w(UInt16(0));central += w(UInt16(0));central += w(crc);central += w(UInt32(data.count));central += w(UInt32(data.count));central += w(UInt16(n.count));central += w(UInt16(0));central += w(UInt16(0));central += w(UInt16(0));central += w(UInt16(0));central += w(UInt32(0));central += w(offset);central += n
    }
    let start=UInt32(out.count);out += central;out += w(UInt32(0x06054b50));out += w(UInt16(0));out += w(UInt16(0));out += w(UInt16(files.count));out += w(UInt16(files.count));out += w(UInt32(central.count));out += w(start);out += w(UInt16(0));return out
}
