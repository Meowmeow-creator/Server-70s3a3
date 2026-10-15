"""Export the town scripts (and later their map files) from the running
pvfUtility HTTP API into D:\\70S3A3\\local-run\\pvf\\export\\ ."""
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from pvfapi import get_files  # noqa: E402

OUT = Path(r"D:\70S3A3\local-run\pvf\export")


def save_logical(rel_path: str, content: str):
    target = OUT / rel_path.replace("/", "\\")
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8")
    return target


def main() -> int:
    lst = get_files(["town/town.lst"])
    text = lst.get("town/town.lst", "")
    print("town.lst:\n%s" % text)
    towns = re.findall(r"(\d+)\s+`([^`]+)`", text)
    print("towns:", towns)
    paths = ["town/" + name for _, name in towns]
    got = get_files(paths)
    for p, content in got.items():
        t = save_logical(p, content)
        print("saved %-40s %6d chars -> %s" % (p, len(content), t.name))
    # areas per town
    for p, content in got.items():
        areas = re.findall(r"\[area\]\s*\r?\n\s*(\d+)\s*\r?\n\s*`([^`]+)`", content)
        print("%-28s areas=%d %s" % (p, len(areas), areas[:20]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
