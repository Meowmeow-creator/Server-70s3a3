"""pvfapi.py -- thin client for the running pvfUtility HTTP API (port 27000)."""
import json
import sys
import urllib.request

BASE = "http://127.0.0.1:27000/Api/PvfUtiltiy"


def api(route, payload, timeout=120):
    data = json.dumps(payload).encode("utf-8") if payload is not None else b"{}"
    req = urllib.request.Request(
        BASE + route,
        data=data,
        headers={"Content-Type": "application/json;charset=UTF-8", "Origin": "http://127.0.0.1:27000"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        raw = resp.read()
    try:
        return json.loads(raw.decode("utf-8", "replace"))
    except Exception:
        return {"_raw": raw.decode("utf-8", "replace")}


def get_files(paths):
    """Returns {path: content} using the batch endpoint."""
    r = api("/GetFileContents", {"FileList": list(paths)})
    data = (r or {}).get("Data") or {}
    return data.get("FileContentData") or {}


def search(name):
    return api("/SearchPvf", {"SearchName": name})


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "get":
        out = get_files(sys.argv[2:])
        for k, v in out.items():
            print("=== %s (%d chars) ===" % (k, len(v)))
            print(v[:2000])
    elif cmd == "search":
        print(json.dumps(search(sys.argv[2]), ensure_ascii=False)[:3000])
