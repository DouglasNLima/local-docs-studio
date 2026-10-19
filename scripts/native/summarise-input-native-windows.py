"""Read-only, streamed native-window correlation. Counts/overlap are not causal attribution."""
import argparse
import collections
import hashlib
import json
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("derived", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
if args.output.exists():
    raise SystemExit("Use a fresh summary file.")
read = lambda path: json.loads(path.read_text(encoding="utf-8-sig"))
metadata = read(args.derived / "native-ui-events.json")
threads = set(metadata["UiNativeThreads"])
windows = metadata["SelectedOriginalQpcWindows"]["Windows"]
union = []
for row in sorted(windows, key=lambda row: row["StartTimestamp"]):
    a, b = row["StartTimestamp"], row["EndTimestamp"]
    if union and a <= union[-1][1]:
        union[-1] = (union[-1][0], max(b, union[-1][1]))
    else:
        union.append((a, b))


def covered(a, b):
    return sum(max(0, min(b, y) - max(a, x)) for x, y in union) == b - a


def frames(row):
    return [value["ModuleName"] + "!" + value["FullMethodName"] for value in row.get("Stack", []) if value["FullMethodName"]]


pending, intervals, omitted = {}, [], 0
cpu = []
for line in (args.derived / "ui-events.jsonl").open(encoding="utf-8"):
    row = json.loads(line)
    at = row["Qpc"]
    payload = {value["Name"]: value["Value"] for value in row.get("Payload", [])}
    if row["Kind"] == "CpuSample":
        cpu.append({"Qpc": at, "FirstResolvedNativeAncestor": frames(row)[:1], "NamedAncestors": frames(row),
                    "UnresolvedFrames": sum(not frame["FullMethodName"] for frame in row.get("Stack", []))})
    elif row["Kind"] == "ReadyThread":
        target = int(payload["AwakenedThreadID"])
        if target in pending and pending[target].get("ReadyTimestamp") is None:
            pending[target]["ReadyTimestamp"] = at
    elif row["Kind"] == "ContextSwitch":
        new = row["NewThreadID"]
        if new in threads and new in pending:
            interval = pending.pop(new)
            if covered(interval["StartTimestamp"], at):
                interval["EndTimestamp"] = at
                interval["Milliseconds"] = (at - interval["StartTimestamp"]) / 10000
                intervals.append(interval)
            else:
                omitted += 1
        old = row["OldThreadID"]
        if old in threads:
            pending[old] = {"Thread": old, "StartTimestamp": at, "ReadyTimestamp": at if payload["OldThreadState"] == "Ready" else None,
                            "OldThreadState": payload["OldThreadState"], "OldWaitReasonRaw": payload["OldThreadWaitReason"],
                            "BlockingThreadNamedAncestors": frames(row)}
rows = []
for window in windows:
    a, b = window["StartTimestamp"], window["EndTimestamp"]
    matching = [row for row in intervals if row["EndTimestamp"] > a and row["StartTimestamp"] < b]
    sampled = [row for row in cpu if a <= row["Qpc"] <= b]
    counts = collections.Counter(name for row in sampled for name in row["FirstResolvedNativeAncestor"])
    relevant = collections.Counter(name for row in sampled for name in set(row["NamedAncestors"]) if name.split("!")[0] in ("msctf", "uxtheme", "wpfgfx_cor3", "shell32", "windows.storage", "combase", "coremessaging"))
    rows.append({**window, "DurationMilliseconds": (b - a) / 10000, "CpuSamples": len(sampled),
                 "SamplesWithUnresolvedFrames": sum(row["UnresolvedFrames"] > 0 for row in sampled),
                 "FirstResolvedNativeAncestorCountsNotSelfCpu": counts.most_common(12), "SelectedNamedAncestorCountsNotSelfCpu": relevant.most_common(16),
                 "FullyPairedObservedOffCpuMilliseconds": sum(max(0, min(b, row["EndTimestamp"]) - max(a, row["StartTimestamp"])) / 10000 for row in matching),
                 "ObservedReadyToRunningMilliseconds": sum(max(0, min(b, row["EndTimestamp"]) - max(a, row["ReadyTimestamp"])) / 10000 for row in matching if row["ReadyTimestamp"] is not None),
                 "LongestPairedOffCpuIntervals": sorted(matching, key=lambda row: row["Milliseconds"], reverse=True)[:6]})
with (args.derived / "ui-events.jsonl").open("rb") as stream:
    digest = hashlib.file_digest(stream, "sha256").hexdigest()
result = {"DiagnosticOnly": True, "NativeMetadata": metadata, "DecodedRowsSha256": digest, "Windows": rows,
          "PairsOmittedAcrossUnobservedSelectedWindowGaps": omitted, "UnpairedEnds": list(pending.values()),
          "Limits": "Window-edge/missing pairs remain unassigned. Off-CPU includes normal waits, not necessarily a fault. Ready-to-running requires an observed Ready state/event. Native ancestors are not exclusive/self CPU or identities of unresolved managed callbacks. No runtime GC/suspension coverage is inferred from zero captured CLR events. Profiling overhead is outside acceptance."}
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps({"Windows": len(rows), "CpuSamples": len(cpu), "PairedOffCpuIntervals": len(intervals), "UnobservedGapPairs": omitted}))
