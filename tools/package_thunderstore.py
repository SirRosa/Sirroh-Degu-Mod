#!/usr/bin/env python3
"""Build and verify an installable Thunderstore/r2modman Valheim ZIP.

Root entries: manifest.json, README.md, icon.png, SirrohDegu.dll.
No parent folder in the ZIP. This is separate from the legacy manual ZIP.
"""
import argparse
import json
import re
import struct
import zipfile
from pathlib import Path


def generate_icon(path: Path):
    from PIL import Image, ImageDraw, ImageFont

    s = 3
    img = Image.new("RGB", (256 * s, 256 * s), "#28302e")
    d = ImageDraw.Draw(img)
    def ellipse(box, fill, outline=None, width=1):
        d.ellipse(tuple(int(z * s) for z in box), fill=fill,
                  outline=outline, width=width * s)
    def line(coords, color, width=1):
        d.line([(int(x*s), int(y*s)) for x,y in coords], fill=color,
               width=width * s, joint="curve")

    # Stylized degu head, simple contrasting forms readable at 64 pixels.
    ellipse((24, 18, 232, 226), "#3f4b42")
    ellipse((42, 31, 108, 108), "#96775a")
    ellipse((148, 31, 214, 108), "#96775a")
    ellipse((52, 42, 97, 100), "#bc9880")
    ellipse((159, 42, 204, 100), "#bc9880")
    ellipse((62, 61, 194, 204), "#a98968")
    ellipse((75, 91, 181, 198), "#c0a181")
    ellipse((88, 117, 168, 193), "#d6b99a")
    ellipse((97, 114, 112, 129), "#364037")
    ellipse((144, 114, 159, 129), "#364037")
    ellipse((101, 118, 106, 123), "#f0d7bb")
    ellipse((148, 118, 153, 123), "#f0d7bb")
    d.polygon([(119*s,153*s),(137*s,153*s),(128*s,165*s)],fill="#65463f")
    line([(128,164),(128,176)], "#65463f",2)
    line([(128,175),(120,181)], "#65463f",2)
    line([(128,175),(136,181)], "#65463f",2)
    for a,b in [((110,165),(60,157)),((110,173),(58,177)),
                ((146,165),(196,157)),((146,173),(198,177))]:
        line([a,b], "#e3ccb0",2)
    d.rounded_rectangle((58*s,211*s,198*s,245*s), radius=10*s, fill="#28302e")
    try:
        font=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 21*s)
    except OSError:
        try: font=ImageFont.truetype("DejaVuSans-Bold.ttf",21*s)
        except OSError: font=ImageFont.load_default()
    d.text((128*s,228*s),"DEGU",font=font,anchor="mm",fill="#e6d5b7")
    path.parent.mkdir(parents=True,exist_ok=True)
    img.resize((256,256), Image.Resampling.LANCZOS).save(path,format="PNG")


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--dll",type=Path,required=True)
    parser.add_argument("--manifest",type=Path,default=Path("manifest.json"))
    parser.add_argument("--readme",type=Path,default=Path("README.md"))
    parser.add_argument("--icon",type=Path,default=Path("release/SirrohDegu/icon.png"))
    parser.add_argument("--out",type=Path,required=True)
    args=parser.parse_args()

    manifest=json.loads(args.manifest.read_text(encoding="utf-8"))
    required={"name","version_number","website_url","description","dependencies"}
    assert set(manifest)==required, "Thunderstore manifest fields must match schema"
    assert re.fullmatch(r"[A-Za-z0-9_]+",manifest["name"])
    assert re.fullmatch(r"\d+\.\d+\.\d+",manifest["version_number"])
    assert isinstance(manifest["description"],str) and 1<=len(manifest["description"])<=250
    assert isinstance(manifest["dependencies"],list)
    assert all(isinstance(x,str) and x.count("-")>=2 for x in manifest["dependencies"])
    assert args.dll.is_file() and args.dll.stat().st_size>1024, "Compiled DLL is missing"
    assert args.dll.open("rb").read(2)==b"MZ", "Plugin must be a compiled PE/.NET DLL"
    assert args.readme.is_file() and args.readme.stat().st_size>0

    if not args.icon.is_file():
        generate_icon(args.icon)
    raw=args.icon.read_bytes()
    assert raw[:8]==b"\x89PNG\r\n\x1a\n", "icon.png must be PNG"
    assert struct.unpack(">II",raw[16:24])==(256,256), "Icon must be exactly 256x256"

    args.out.parent.mkdir(parents=True,exist_ok=True)
    entries={
        "manifest.json":args.manifest,
        "README.md":args.readme,
        "icon.png":args.icon,
        "SirrohDegu.dll":args.dll,
    }
    with zipfile.ZipFile(args.out,"w",zipfile.ZIP_DEFLATED,compresslevel=9) as archive:
        for arc,path in entries.items():
            archive.write(path, arcname=arc)

    with zipfile.ZipFile(args.out) as archive:
        assert set(archive.namelist())==set(entries), "Unexpected ZIP layout"
        assert archive.testzip() is None, "CRC mismatch"
        embedded=json.loads(archive.read("manifest.json"))
        assert embedded==manifest
        assert archive.read("SirrohDegu.dll")==args.dll.read_bytes()
        assert archive.read("icon.png")==raw
    print("PASS: Thunderstore package", args.out,
          "version",manifest["version_number"],
          "ZIP bytes", args.out.stat().st_size)


if __name__=="__main__":
    main()
