"""Select diagnostic native-stack windows; never filter acceptance observations."""
import argparse
import json
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("observations", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
if args.output.exists():
    raise SystemExit("Use a fresh derived windows file.")
read = lambda path: json.loads(path.read_text(encoding="utf-8-sig"))
windows = []


def add(start, end, reason):
    windows.append({"StartTimestamp": int(start), "EndTimestamp": int(end), "Reason": reason})


for path in args.observations.rglob("*.json"):
    if path.name in ("reading-input.json", "appearance.json", "appearance-stock.json", "appearance-control.json"):
        rows = read(path).get("InputTimeline", [])
        if rows:
            maximum = max(rows, key=lambda row: row.get("Milliseconds", row.get("GapMilliseconds", 0)))
            for row in rows:
                gap = row.get("Milliseconds", row.get("GapMilliseconds", 0))
                if gap >= 100 or row is maximum:
                    end = row.get("EndTimestamp", row.get("Timestamp"))
                    add(row.get("StartTimestamp", end - gap * 10000), end, str(path.relative_to(args.observations)) + ":Input")
    elif path.name == "reading-client.json":
        stages = {row["Stage"]: row["Timestamp"] for row in read(path)["Stages"]}
        if "focus-enter" in stages and "keyboard-selection-visible" in stages:
            add(stages["focus-enter"], stages["keyboard-selection-visible"], "first-focus-through-keyboard-visible")
    elif path.name.endswith("-trace.json"):
        trace = read(path)
        starts = [row for row in trace if row["Stage"] == "trace-start"]
        if starts and starts[0]["Detail"]["Frequency"] != 10000000:
            raise SystemExit("This selector's explicit Windows QPC conversion requires 10 MHz; do not infer another host's frequency.")
        for row in trace:
            detail = row.get("Detail") or {}
            if row["Stage"] in ("input-delivered", "native-delivered") and detail.get("Milliseconds", 0) >= 100:
                add(detail["Scheduled"], row["Timestamp"], row["Stage"])
            elif row["Stage"] == "native-message-exit" and detail.get("Message") == 0x110:
                add(detail["StartTimestamp"], row["Timestamp"], "WM_INITDIALOG")
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps({"DiagnosticOnly": True, "OriginalEtwRemainsComplete": True,
    "Meaning": "All >=100 ms application/probe intervals, each timer maximum, first focus/keyboard and every native dialogue initialisation. Native rows only are clipped; no acceptance sample is filtered.",
    "Windows": windows}, indent=2), encoding="utf-8")
print(f"Selected {len(windows)} original-QPC windows.")
