"""Twentieth probe: robust TOC walk + extraction of the .NET bundle payload."""
import struct
import sys
from pathlib import Path

SRC = Path(r"D:\PVF\pvfUtility.exe")
OUT = Path(r"D:\70S3A3\local-run\tools\pvfUtility-bundle")
d = SRC.read_bytes()
SIZE = len(d)

ANCHOR = 0x11AD4BF7  # fields start for pvfUtility.runtimeconfig.json (verified)


def entry_at(F):
    if F < 0 or F + 26 > SIZE:
        return None
    off, size, csize = struct.unpack_from("<3Q", d, F)
    t = d[F + 24]
    ln = d[F + 25]
    if not (1 <= ln <= 200):
        return None
    name = d[F + 26 : F + 26 + ln]
    try:
        name = name.decode("ascii")
    except UnicodeDecodeError:
        return None
    return {"F": F, "next": F + 26 + ln, "off": off, "size": size, "csize": csize, "type": t, "name": name}


def prev_F(F):
    for L in range(1, 200):
        nt = F - L - 2
        if nt < 0:
            break
        if d[F - L - 1] != L:
            continue
        t = d[nt]
        if t > 8:
            continue
        cand = nt - 24
        e = entry_at(cand)
        if e and e["next"] == F:
            return cand
    return None


def main() -> int:
    e = entry_at(ANCHOR)
    print("anchor entry:", e)
    entries = [e]
    # forward
    F = e["next"]
    while True:
        nxt = entry_at(F)
        if nxt is None:
            break
        entries.append(nxt)
        F = nxt["next"]
    # backward
    F = ANCHOR
    while True:
        p = prev_F(F)
        if p is None:
            break
        pe = entry_at(p)
        entries.insert(0, pe)
        F = p
    print("total entries:", len(entries))
    print("first:", entries[0])
    print("last:", entries[-1])
    cum_ok = sum(1 for i in range(len(entries) - 1) if entries[i + 1]["off"] == entries[i]["off"] + entries[i]["size"])
    print("cumulative offset matches: %d/%d" % (cum_ok, len(entries) - 1))
    # find pvfUtility.dll entry
    for en in entries:
        if en["name"] == "pvfUtility.dll":
            print("pvfUtility.dll:", en)
            o = en["off"]
            print("  bytes at off:", d[o : o + 8])
            if d[o : o + 2] == b"MZ":
                el = struct.unpack_from("<I", d, o + 0x3C)[0]
                print("  PE sig:", d[o + el : o + el + 4], "machine:", hex(struct.unpack_from("<H", d, o + el + 4)[0]))

    OUT.mkdir(parents=True, exist_ok=True)
    n = 0
    for en in entries:
        if en["size"] <= 0 or en["off"] + en["size"] > SIZE:
            continue
        target = OUT / en["name"].replace("/", "\\")
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(d[en["off"] : en["off"] + en["size"]])
        n += 1
    print("extracted %d files to %s" % (n, OUT))
    return 0


if __name__ == "__main__":
    sys.exit(main())
