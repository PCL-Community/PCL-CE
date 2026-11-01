#!/usr/bin/env python3
"""Rebuild the embedded Java 8 agent; ordinary app builds require no Java JDK.

Usage: python3 scripts/make-legacy-opengl-agent.py --java-home /path/to/jdk8/Contents/Home [--check]
The ZIP entry order, timestamps, permissions, and uncompressed content are fixed.
Compile without debug/source paths so identical Java 8 compiler output produces
an identical JAR and SHA-256. No external ASM or other dependencies are fetched.
"""
import argparse
import base64
import hashlib
import io
import pathlib
import re
import subprocess
import tempfile
import textwrap
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--java-home", type=pathlib.Path, required=True)
parser.add_argument("--check", action="store_true")
args = parser.parse_args()
macos = pathlib.Path(__file__).resolve().parents[1]
source = macos / "Support/LegacyOpenGL/PCLLegacyContextAgent.java"
swift = macos / "Sources/PCLCore/LegacyOpenGLCompatibility.swift"
with tempfile.TemporaryDirectory(prefix="pcl-opengl-agent-") as temporary:
    classes = pathlib.Path(temporary)
    subprocess.run([str(args.java_home / "bin/javac"), "-XDignore.symbol.file", "-g:none", "-encoding", "UTF-8",
                    "-source", "8", "-target", "8", "-d", str(classes), str(source)], check=True)
    entries = [("META-INF/MANIFEST.MF", b"Manifest-Version: 1.0\r\nPremain-Class: PCLLegacyContextAgent\r\n\r\n")]
    entries.extend((file.name, file.read_bytes()) for file in sorted(classes.glob("*.class")))
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", compression=zipfile.ZIP_STORED) as archive:
        for name, content in entries:
            info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            archive.writestr(info, content)
data = buffer.getvalue()
digest = hashlib.sha256(data).hexdigest()
encoded = "\n".join("        " + line for line in textwrap.wrap(base64.b64encode(data).decode(), 100))
block = ("    // BEGIN GENERATED LEGACY OPENGL AGENT\n"
         f'    private static let agentSHA256 = "{digest}"\n'
         '    private static let agentBase64 = """\n' + encoded + '\n        """\n'
         "    // END GENERATED LEGACY OPENGL AGENT")
original = swift.read_text()
generated, count = re.subn(r"    // BEGIN GENERATED LEGACY OPENGL AGENT.*?    // END GENERATED LEGACY OPENGL AGENT",
                            lambda _: block, original, count=1, flags=re.DOTALL)
if count != 1:
    raise SystemExit("Missing generated Swift agent markers")
if args.check:
    if generated != original:
        raise SystemExit("Embedded agent differs; regenerate with this Java 8 compiler first")
else:
    swift.write_text(generated)
print(f"Legacy OpenGL agent: {len(data)} bytes, SHA-256 {digest}, {len(entries) - 1} class files")
