#!/usr/bin/env python3
"""Convert the upstream PCL action geometries to deterministic template PDFs.

The original MyIconButton stretches each WPF Geometry uniformly. These PDFs
therefore use the geometry's tight bounds rather than a padded 1024 px canvas.
WPF's default even-odd fill rule is preserved. SVG elliptical arcs are expressed
as cubic segments spanning at most 30 degrees (subpixel error at icon sizes).
No external Python packages or rasterization are required.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import re


SOURCE_NAMES = {
    "Favorite": "IconButtonLikeLine",
    "FavoriteFilled": "IconButtonLikeFill",
    "Delete": "IconButtonDelete",
    "Settings": "IconButtonSetup",
}
NUMBER = re.compile(r"[A-Za-z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?")
COUNTS = {"M": 2, "L": 2, "H": 1, "V": 1, "C": 6, "S": 4, "Q": 4, "T": 2, "A": 7}


def arc(start, end, rx, ry, angle, large, sweep):
    """SVG endpoint-parameterized ellipse, converted to absolute cubic curves."""
    if start == end:
        return []
    rx, ry = abs(rx), abs(ry)
    if not rx or not ry:
        return [("L", *end)]
    phi = math.radians(angle % 360)
    cp, sp = math.cos(phi), math.sin(phi)
    dx, dy = (start[0] - end[0]) / 2, (start[1] - end[1]) / 2
    xp, yp = cp * dx + sp * dy, -sp * dx + cp * dy
    correction = xp * xp / (rx * rx) + yp * yp / (ry * ry)
    if correction > 1:
        scale = math.sqrt(correction)
        rx, ry = rx * scale, ry * scale
    numerator = max(0.0, rx * rx * ry * ry - rx * rx * yp * yp - ry * ry * xp * xp)
    denominator = rx * rx * yp * yp + ry * ry * xp * xp
    sign = -1 if bool(large) == bool(sweep) else 1
    factor = sign * math.sqrt(numerator / denominator) if denominator else 0
    cxp, cyp = factor * rx * yp / ry, factor * -ry * xp / rx
    cx = cp * cxp - sp * cyp + (start[0] + end[0]) / 2
    cy = sp * cxp + cp * cyp + (start[1] + end[1]) / 2
    ux, uy = (xp - cxp) / rx, (yp - cyp) / ry
    vx, vy = (-xp - cxp) / rx, (-yp - cyp) / ry
    theta = math.atan2(uy, ux)
    delta = math.atan2(ux * vy - uy * vx, ux * vx + uy * vy)
    if not sweep and delta > 0:
        delta -= 2 * math.pi
    if sweep and delta < 0:
        delta += 2 * math.pi
    count = max(1, math.ceil(abs(delta) / (math.pi / 6)))
    step = delta / count

    def position(t):
        return cx + rx * cp * math.cos(t) - ry * sp * math.sin(t), cy + rx * sp * math.cos(t) + ry * cp * math.sin(t)

    def derivative(t):
        return -rx * cp * math.sin(t) - ry * sp * math.cos(t), -rx * sp * math.sin(t) + ry * cp * math.cos(t)

    curves = []
    for i in range(count):
        a, b = theta + i * step, theta + (i + 1) * step
        p0, p3 = position(a), end if i == count - 1 else position(b)
        d0, d3 = derivative(a), derivative(b)
        k = 4 / 3 * math.tan(step / 4)
        curves.append(("C", p0[0] + k * d0[0], p0[1] + k * d0[1], p3[0] - k * d3[0], p3[1] - k * d3[1], *p3))
    return curves


def parse_path(source):
    tokens = NUMBER.findall(source)
    if NUMBER.sub("", source).strip(" ,\t\r\n"):
        raise ValueError("Unsupported geometry characters")
    index, command = 0, None
    point, subpath = (0.0, 0.0), (0.0, 0.0)
    previous, cubic_control, quadratic_control = None, None, None
    output = []
    while index < len(tokens):
        if tokens[index].isalpha():
            command = tokens[index]
            index += 1
        if command is None:
            raise ValueError("Path must begin with a command")
        operation = command.upper()
        if operation == "Z":
            output.append(("Z",))
            point, previous, command = subpath, "Z", None
            cubic_control = quadratic_control = None
            continue
        if operation not in COUNTS:
            raise ValueError(f"Unsupported geometry command: {command}")
        count = COUNTS[operation]
        values = list(map(float, tokens[index:index + count]))
        if len(values) != count:
            raise ValueError("Incomplete geometry command")
        index += count
        relative = command.islower()

        def xy(x, y):
            return (point[0] + x, point[1] + y) if relative else (x, y)

        old = point
        new_cubic = new_quadratic = None
        if operation in ("M", "L"):
            point = xy(*values)
            output.append((operation, *point))
            if operation == "M":
                subpath = point
                command = "l" if relative else "L"
        elif operation in ("H", "V"):
            if operation == "H":
                point = (point[0] + values[0] if relative else values[0], point[1])
            else:
                point = (point[0], point[1] + values[0] if relative else values[0])
            output.append(("L", *point))
        elif operation in ("C", "S"):
            if operation == "C":
                p1, p2, point = xy(*values[:2]), xy(*values[2:4]), xy(*values[4:])
            else:
                p1 = (2 * point[0] - cubic_control[0], 2 * point[1] - cubic_control[1]) if previous in ("C", "S") else point
                p2, point = xy(*values[:2]), xy(*values[2:])
            output.append(("C", *p1, *p2, *point))
            new_cubic = p2
        elif operation in ("Q", "T"):
            if operation == "Q":
                control, point = xy(*values[:2]), xy(*values[2:])
            else:
                control = (2 * point[0] - quadratic_control[0], 2 * point[1] - quadratic_control[1]) if previous in ("Q", "T") else point
                point = xy(*values)
            p1 = tuple(old[i] + 2 / 3 * (control[i] - old[i]) for i in (0, 1))
            p2 = tuple(point[i] + 2 / 3 * (control[i] - point[i]) for i in (0, 1))
            output.append(("C", *p1, *p2, *point))
            new_quadratic = control
        elif operation == "A":
            point = xy(*values[5:])
            if values[3] not in (0, 1) or values[4] not in (0, 1):
                raise ValueError("Invalid SVG arc flags")
            output.extend(arc(old, point, *values[:5]))
        previous, cubic_control, quadratic_control = operation, new_cubic, new_quadratic
    return output


def geometry_bounds(commands):
    points = []
    point = subpath = (0.0, 0.0)
    for command, *values in commands:
        if command == "M":
            point = subpath = tuple(values)
        elif command == "L":
            end = tuple(values)
            points.extend((point, end))
            point = end
        elif command == "C":
            p0, p1, p2, p3 = point, values[:2], values[2:4], values[4:]
            points.extend((p0, p3))
            for axis in (0, 1):
                a = -p0[axis] + 3 * p1[axis] - 3 * p2[axis] + p3[axis]
                b = 2 * (p0[axis] - 2 * p1[axis] + p2[axis])
                c = p1[axis] - p0[axis]
                if abs(a) < 1e-12:
                    roots = [-c / b] if abs(b) > 1e-12 else []
                else:
                    discriminant = b * b - 4 * a * c
                    roots = [(-b + sign * math.sqrt(discriminant)) / (2 * a) for sign in (-1, 1)] if discriminant >= 0 else []
                for t in roots:
                    if 0 < t < 1:
                        points.append(tuple((1 - t) ** 3 * p0[i] + 3 * (1 - t) ** 2 * t * p1[i] + 3 * (1 - t) * t * t * p2[i] + t ** 3 * p3[i] for i in (0, 1)))
            point = tuple(p3)
        elif command == "Z":
            points.extend((point, subpath))
            point = subpath
    xs, ys = zip(*points)
    return min(xs), min(ys), max(xs), max(ys)


def fmt(value):
    return f"{value:.8f}".rstrip("0").rstrip(".") if value else "0"


def make_pdf(commands, bounds, *, even_odd=True):
    x0, y0, x1, y1 = bounds
    content = [f"q 1 0 0 -1 {fmt(-x0)} {fmt(y1)} cm 0 g"]
    pdf_operations = {"M": "m", "L": "l", "C": "c", "Z": "h"}
    for operation, *values in commands:
        content.append(" ".join([*(fmt(v) for v in values), pdf_operations[operation]]))
    content.append("f* Q" if even_odd else "f Q")
    stream = ("\n".join(content) + "\n").encode("ascii")
    objects = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        f"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {fmt(x1 - x0)} {fmt(y1 - y0)}] /Resources << >> /Contents 4 0 R >>".encode("ascii"),
        b"<< /Length " + str(len(stream)).encode("ascii") + b" >>\nstream\n" + stream + b"endstream",
    ]
    output = bytearray(b"%PDF-1.4\n%\xe2\xe3\xcf\xd3\n")
    offsets = [0]
    for index, obj in enumerate(objects, 1):
        offsets.append(len(output))
        output.extend(f"{index} 0 obj\n".encode("ascii") + obj + b"\nendobj\n")
    start = len(output)
    output.extend(f"xref\n0 {len(objects) + 1}\n0000000000 65535 f \n".encode("ascii"))
    for offset in offsets[1:]:
        output.extend(f"{offset:010d} 00000 n \n".encode("ascii"))
    output.extend(f"trailer\n<< /Size {len(objects) + 1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n".encode("ascii"))
    return output


def load_icon_sources():
    """Read the pinned upstream artwork; no Windows checkout or network is needed."""
    directory = Path(__file__).resolve().parents[1] / "Support/IconSources"
    inputs = json.loads((directory / "geometry.json").read_text(encoding="utf-8"))
    if inputs.get("schemaVersion") != 1:
        raise ValueError("Unsupported pinned icon input format")
    for asset in [*inputs["bitmaps"].values(), inputs["appIcon"]]:
        data = (directory / asset["input"]).read_bytes()
        if hashlib.sha256(data).hexdigest() != asset["sha256"]:
            raise ValueError(f"Pinned artwork has changed: {asset['input']}")
    return inputs


def write_or_check(path, data, check):
    if check:
        if not path.is_file() or path.read_bytes() != data:
            raise SystemExit(f"Generated icon differs: {path}")
    else:
        path.write_bytes(data)


def main():
    base = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=base / "Resources/VersionActions")
    parser.add_argument("--check", action="store_true", help="Verify committed assets without writing files")
    args = parser.parse_args()
    if not args.check:
        args.output.mkdir(parents=True, exist_ok=True)
    report = {}
    for name, asset in load_icon_sources()["versionActions"].items():
        geometry = asset["geometry"]
        commands = parse_path(geometry)
        bounds = geometry_bounds(commands)
        pdf = make_pdf(commands, bounds)
        write_or_check(args.output / f"{name}.pdf", pdf, args.check)
        report[name] = {"sourceConstant": asset["sourceConstant"], "sourcePathSHA256": hashlib.sha256(geometry.encode()).hexdigest(), "pdfSHA256": hashlib.sha256(pdf).hexdigest(), "geometryBounds": list(bounds), "pdfCommands": len(commands)}
    manifest = (json.dumps(report, indent=2, ensure_ascii=False) + "\n").encode()
    write_or_check(args.output / "source-map.json", manifest, args.check)
    print(f"{'Verified' if args.check else 'Generated'} {len(report)} original PCL vector icons in {args.output}")


if __name__ == "__main__":
    main()
