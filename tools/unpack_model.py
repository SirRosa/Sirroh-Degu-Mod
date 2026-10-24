import base64
import lzma
import pathlib
import hashlib
import struct
import sys

root = pathlib.Path(__file__).resolve().parents[1]
srcs = [root / "Assets" / f"Degu.part{i}.b64" for i in (1,2,3)]
dst = root / "Assets" / "Degu.meshbin"
if not all(src.is_file() for src in srcs):
    raise SystemExit("Missing Degu mesh payload; all 3 parts are required.")
raw = lzma.decompress(base64.b64decode(b"".join(src.read_bytes().strip() for src in srcs), validate=True))
assert hashlib.sha256(raw).hexdigest() == "e19d03f8722ad1ce6db22e0000a99a785e428720cd8a4c2319f3a7a5953ee203", "Original Degu mesh payload SHA mismatch"
assert raw[:4] == b"SDG3", "Bad mesh header"
v, indices, bones = struct.unpack_from("<HHB", raw, 4)
assert (v, indices, bones) == (1477, 7740, 34), (v, indices, bones)
assert len(raw) == 9 + 17 * v + 2 * indices, "Bad payload size"
for i in range(v):
    rec = raw[9+i*17:9+(i+1)*17]
    js = rec[10:14]
    ws = rec[14:17]
    assert max(js) < bones
    assert sum(ws) <= 255
for (index,) in struct.iter_unpack("<H", raw[9+17*v:]):
    assert index < v
dst.parent.mkdir(parents=True,exist_ok=True)
dst.write_bytes(raw)
print(f"PASS: {v} vertices, {indices//3} triangles, {bones} original wolf bone indices; {len(raw)} bytes.")
