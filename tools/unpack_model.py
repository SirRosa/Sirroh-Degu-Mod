import base64
import lzma
import pathlib
import struct
import sys

root = pathlib.Path(__file__).resolve().parents[1]
src = root / "Assets" / "Degu.meshbin.xz.b85"
dst = root / "Assets" / "Degu.meshbin"
if not src.is_file():
    raise SystemExit("Missing Degu mesh payload; exact source mesh required.")
raw = lzma.decompress(base64.b85decode(src.read_bytes().strip()))
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
