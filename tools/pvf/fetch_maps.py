"""Fetch every town area's .map script and look for portal/gate definitions."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from pvfapi import get_files  # noqa: E402

OUT = Path(r"D:\70S3A3\local-run\pvf\export")


def parse_areas(text):
    out = []
    for block in re.split(r"\[area\]", text)[1:]:
        m = re.match(r"\s*(\d+)\s*\r?\n\s*`([^`]+)`", block)
        if not m:
            continue
        area_id = int(m.group(1))
        map_path = m.group(2)
        kind = None
        g = re.search(r"`\[(gate|dungeon gate|normal)\]`\s*([-\d\s]*)", block)
        if g:
            kind = g.group(1)
        out.append((area_id, map_path, kind))
    return out


def main() -> int:
    lst = get_files(["town/town.lst"])["town/town.lst"]
    towns = [(int(i), n) for i, n in re.findall(r"(\d+)\s+`([^`]+)`", lst)]
    twn_paths = ["town/%s" % n.lower() for _, n in towns]
    twns = get_files(twn_paths)
    all_areas = {}
    map_paths = []
    for town_id, name in towns:
        key = "town/%s" % name.lower()
        text = twns.get(key)
        if not text:
            print("MISSING", key)
            continue
        areas = parse_areas(text)
        all_areas[town_id] = areas
        print("town %2d %-18s areas: %s" % (town_id, name, [(a, k) for a, _, k in areas]))
        for area_id, mp, kind in areas:
            map_paths.append("map/" + mp.lower())
    print("\nfetching %d map files..." % len(map_paths))
    maps = get_files(sorted(set(map_paths)))
    print("got %d map files" % len(maps))
    d = OUT / "map"
    d.mkdir(parents=True, exist_ok=True)
    for k, v in maps.items():
        f = d / (k.replace("/", "_") + ".txt")
        f.write_text(v, encoding="utf-8")
    # show the alfhlyra town map (area 0 of town 4) to inspect door syntax
    sample = maps.get("map/alfhlyra/alfhlyra.map")
    if sample:
        print("\n=== map/alfhlyra/alfhlyra.map (%d chars) ===" % len(sample))
        print(sample[:3000])
    return 0


if __name__ == "__main__":
    sys.exit(main())
