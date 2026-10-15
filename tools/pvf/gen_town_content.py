"""Generate the server's town content table (Town-Content-FG75.cs) from the
PVF-exported town scripts and area maps.

Inputs (exported from the running pvfUtility HTTP API):
  export/town/*.twn.txt    -- town list + areas (id, map, kind)
  export/map/map_*.map.txt -- area maps with:
      [town movable area]  x y w h targetTown targetArea   (doors)
                           rows with target -1/-1 are the DUNGEON entrance portal
                           rows with target -2/-2 are other special markers (kept apart)
      [NPC]                npcid `[facing]` x y z          (NPC spawns in the area)

Generated API (same class name, so no other file changes needed):
  TownSupported / SeriaArea / Exists / Npc / Portals / Linked / DungeonGate
  + DungeonPortal(town,area)  (dungeon entrance rectangle, null when the area has none)
"""
import re
import sys
from pathlib import Path

EXPORT = Path(r"D:\70S3A3\local-run\pvf\export")
OUT_CS = Path(r"D:\70S3A3\Server-70s3a3\src\Tools\VmTestKit\Town-Content-FG75.cs")
SUPPORTED_TOWNS = range(1, 5)


def parse_areas(text):
    out = []
    for block in re.split(r"\[area\]", text)[1:]:
        m = re.match(r"\s*(\d+)\s*\r?\n\s*`([^`]+)`", block)
        if not m:
            continue
        area_id = int(m.group(1))
        map_path = m.group(2)
        kind = "normal"
        km = re.search(r"`\[(gate|dungeon gate|normal)\]`", block)
        if km:
            kind = km.group(1)
        out.append((area_id, map_path, kind))
    return out


def parse_town_movable(map_text):
    """Returns (doors, dungeon_portal, other_markers)."""
    m = re.search(r"\[town movable area\]\r?\n(.*?)\r?\n[ \t]*\[/town movable area\]", map_text, re.S)
    if not m:
        return [], None, []
    doors, dungeon, other = [], None, []
    pattern = r"(?m)^[ \t]*(-?\d+)[ \t]+(-?\d+)[ \t]+(-?\d+)[ \t]+(-?\d+)[ \t]+(-?\d+)[ \t]+(-?\d+)[ \t]*$"
    for nums in re.findall(pattern, m.group(1)):
        vals = [int(x) for x in nums]
        if vals[4] == -1 and vals[5] == -1:
            if dungeon is None:
                dungeon = vals[:4]
        elif vals[4] < 1 or vals[5] < 0:
            other.append(vals[:4])
        else:
            doors.append(vals)
    return doors, dungeon, other


def parse_npcs(map_text):
    m = re.search(r"\[NPC\](.*?)\[/NPC\]", map_text, re.S)
    if not m:
        return []
    ids = []
    for line in m.group(1).split("\n"):
        mm = re.match(r"\s*(\d+)\s+`\[", line)
        if mm:
            ids.append(int(mm.group(1)))
    return ids


def load_towns():
    lst = EXPORT / "town" / "town.lst.txt"
    if lst.is_file():
        mapping = {}
        for i, n in re.findall(r"(\d+)\s+`([^`]+)`", lst.read_text(encoding="utf-8")):
            mapping[int(i)] = n.lower().replace(".twn", "")
    else:
        mapping = {1: "elvengard", 2: "hendonmyre", 3: "westcoast", 4: "alfhlyra",
                   5: "stormpass", 6: "gent", 7: "warfare", 8: "guildagit",
                   9: "lufthafen", 10: "fair_pvp", 11: "chn_tournament", 12: "shonan"}
    towns = {}
    for tid, base in sorted(mapping.items()):
        f = EXPORT / "town" / (base + ".twn.txt")
        if f.is_file():
            towns[tid] = parse_areas(f.read_text(encoding="utf-8"))
    return towns


def main() -> int:
    towns = load_towns()
    doors, npcs, dportals = {}, {}, {}
    missing = []
    for tid, areas in towns.items():
        for area_id, map_path, _kind in areas:
            f = EXPORT / "map" / ("map_" + map_path.lower().replace("/", "_") + ".txt")
            if not f.is_file():
                missing.append((tid, area_id, map_path))
                doors[(tid, area_id)] = []
                npcs[(tid, area_id)] = []
                continue
            text = f.read_text(encoding="utf-8")
            d, dp, _other = parse_town_movable(text)
            doors[(tid, area_id)] = d
            if dp:
                dportals[(tid, area_id)] = dp
            npcs[(tid, area_id)] = parse_npcs(text)

    print("towns with data:", sorted(towns))
    if missing:
        print("MISSING MAPS:", missing)
    for tid in sorted(SUPPORTED_TOWNS):
        for area_id, _map, kind in towns.get(tid, []):
            print("  t%d/a%-2d %-12s doors=%-2d npc=%-24s dungeonPortal=%s"
                  % (tid, area_id, kind, len(doors.get((tid, area_id), [])),
                     npcs.get((tid, area_id), []), dportals.get((tid, area_id))))

    # regression report against the existing file
    old = OUT_CS.read_text(encoding="utf-8-sig")
    old_pairs, old_npc = set(), set()
    for m in re.finditer(r"case (\d+):return new int\[\]\[\]\{(.*?)\};", old, re.S):
        key = int(m.group(1))
        for row in re.finditer(r"new int\[\]\{(-?\d+),(-?\d+),(-?\d+),(-?\d+),(-?\d+),(-?\d+)\}", m.group(2)):
            v = [int(x) for x in row.groups()]
            old_pairs.add((key // 16, key % 16, v[4], v[5]))
    nm = re.search(r"internal static bool Npc\(int town,int area,int npc\)\{(.*?)\n internal static", old, re.S)
    if nm:
        for m in re.finditer(r"case (\d+):(.*)", nm.group(1)):
            key = int(m.group(1))
            for v in re.findall(r"npc==(\d+)", m.group(2)):
                old_npc.add((key // 16, key % 16, int(v)))
    new_pairs = {(t, a, v[4], v[5]) for (t, a), rows in doors.items() for v in rows}
    new_npc = {(t, a, n) for (t, a), ids in npcs.items() for n in ids}
    print("\nlinks: old=%d new=%d   lost=%s" % (len(old_pairs), len(new_pairs), sorted(old_pairs - new_pairs)))
    print("npc  : old=%d new=%d   lost=%s" % (len(old_npc), len(new_npc), sorted(old_npc - new_npc)))

    lines = []
    lines.append("using System;using System.IO;")
    lines.append("// Generated from the CN70 client's Script.pvf (town/*.twn, area .map [town movable area] and [NPC]).")
    lines.append("// Door rows are the client's own portal rectangles: x y w h targetTown targetArea.")
    lines.append("// DungeonPortal is the client's dungeon entrance rectangle (rows with target -1/-1).")
    lines.append("class Offline70TownContentFG75 {")
    lines.append(" internal static bool TownSupported(int town){return town>=1&&town<=4;}")
    lines.append(" internal static int SeriaArea(int town){if(!TownSupported(town))throw new InvalidDataException(\"Unsupported town region\");return town==1?1:town==2?5:town==3?2:0;}")

    lines.append(" internal static bool Exists(int town,int area){switch(town*16+area){")
    for tid in sorted(SUPPORTED_TOWNS):
        for area_id, _m, _k in towns.get(tid, []):
            lines.append("  case %d:return true;" % (tid * 16 + area_id))
    lines.append(" default:return false;}}")

    lines.append(" internal static bool Npc(int town,int area,int npc){switch(town*16+area){")
    for (tid, area_id) in sorted(npcs.keys()):
        ids = npcs[(tid, area_id)]
        if not ids:
            continue
        body = "||".join("npc==%d" % i for i in sorted(set(ids)))
        lines.append("  case %d:return %s;" % (tid * 16 + area_id, body))
    lines.append(" default:return false;}}")

    lines.append(" internal static int[][] Portals(int town,int area){switch(town*16+area){")
    for (tid, area_id) in sorted(doors.keys()):
        rows = doors[(tid, area_id)]
        body = ",".join("new int[]{%d,%d,%d,%d,%d,%d}" % tuple(v) for v in rows)
        lines.append("  case %d:return new int[][]{%s};" % (tid * 16 + area_id, body))
    lines.append(" default:throw new InvalidDataException(\"Unsupported town area\");}}")

    lines.append(" internal static int[] DungeonPortal(int town,int area){switch(town*16+area){")
    for (tid, area_id) in sorted(dportals.keys()):
        v = dportals[(tid, area_id)]
        lines.append("  case %d:return new int[]{%d,%d,%d,%d};" % (tid * 16 + area_id, v[0], v[1], v[2], v[3]))
    lines.append(" default:return null;}}")

    linked = re.search(r"(internal static bool Linked\(int town,int area,int targetTown,int targetArea\)\{.*?\})\n", old, re.S)
    lines.append(" " + (linked.group(1) if linked else
                 "internal static bool Linked(int town,int area,int targetTown,int targetArea){foreach(int[] p in Portals(town,area))if(p[4]==targetTown&&p[5]==targetArea)return true;return false;}"))
    dg = re.search(r"(internal static bool DungeonGate\(int town,int area\)\{.*?\})\n", old, re.S)
    lines.append(" " + (dg.group(1) if dg else
                 "internal static bool DungeonGate(int town,int area){return town==1&&area==2||town==2&&area==6||town==3&&(area==3||area==4||area==5||area==6);}"))
    lines.append("}")
    OUT_CS.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("\nwrote %s (%d lines)" % (OUT_CS, len(lines)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
