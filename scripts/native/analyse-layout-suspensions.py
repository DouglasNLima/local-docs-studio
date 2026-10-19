"""Read-only correlation of retained EventPipe and application QPC observations."""
import argparse
import hashlib
import json
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def union_milliseconds(intervals, begin, end, frequency):
    clipped = sorted((max(begin, a), min(end, b)) for a, b in intervals if b > begin and a < end)
    total, previous = 0, begin
    for a, b in clipped:
        total += max(0, b - max(a, previous))
        previous = max(previous, b)
    return total * 1000 / frequency


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("trace_json", type=Path)
parser.add_argument("observations", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
identity = read(args.observations / "execution-identity.json")
if "--appearance" not in identity["Arguments"]:
    raise SystemExit("The child did not execute --appearance; a profile directory name is not workload proof.")
appearance = read(args.observations / "appearance.json")
probe = next(row for row in read(args.observations / "appearance-trace.json") if row["Stage"] == "trace-start")
frequency, process = probe["Detail"]["Frequency"], probe["Detail"]["Process"]
if process != identity["Process"]:
    raise SystemExit("Application and probe process identities differ.")
trace = read(args.trace_json)
active, unpaired, unpaired_ends, pauses = {}, [], [], []
for row in sorted(trace["Events"], key=lambda item: item["Qpc"]):
    if row["ProcessID"] != process:
        raise SystemExit("The retained runtime events belong to another process.")
    payload = {value["Name"]: value["Value"] for value in row["Payload"]}
    key = payload.get("ClrInstanceID")
    if row["Event"] == "GC/SuspendEEStart":
        if key in active:
            unpaired.append(active[key])
        active[key] = row
    elif row["Event"] == "GC/RestartEEStop":
        first = active.pop(key, None)
        if first is not None:
            reason = {value["Name"]: value["Value"] for value in first["Payload"]}.get("Reason")
            pauses.append({"StartTimestamp": first["Qpc"], "EndTimestamp": row["Qpc"], "Reason": reason,
                           "Milliseconds": (row["Qpc"] - first["Qpc"]) * 1000 / frequency,
                           "NativeStartThread": first["ThreadID"], "NativeEndThread": row["ThreadID"]})
        else:
            unpaired_ends.append(row)
unpaired.extend(active.values())
rows = []
for gap in appearance["InputTimeline"]:
    end = gap["Timestamp"]
    begin = end - gap["GapMilliseconds"] * frequency / 1000
    matching = [p for p in pauses if p["EndTimestamp"] > begin and p["StartTimestamp"] < end]
    intervals = lambda values: [(p["StartTimestamp"], p["EndTimestamp"]) for p in values]
    rows.append({**gap, "StartTimestamp": begin,
                 "PairedRuntimeSuspensionUnionMilliseconds": union_milliseconds(intervals(matching), begin, end, frequency),
                 "PairedGcReasonUnionMilliseconds": union_milliseconds(intervals([p for p in matching if "GC" in (p["Reason"] or "")]), begin, end, frequency),
                 "UnpairedStartsInsideInterval": [p for p in unpaired if begin <= p["Qpc"] <= end],
                 "UnpairedEndsInsideInterval": [p for p in unpaired_ends if begin <= p["Qpc"] <= end]})
args.output.mkdir(parents=True, exist_ok=False)
summary = {"DiagnosticOnly": True, "Causality": "Overlap is not attribution; profiling adds its own suspension overhead.",
           "ExecutionIdentity": identity, "Process": process, "Frequency": frequency, "TraceSha256": trace["InputSha256"],
           "DecodedEventsSha256": hashlib.sha256(args.trace_json.read_bytes()).hexdigest(),
           "EventsLost": trace.get("EventsLost"), "PairedSuspensions": len(pauses), "UnpairedStarts": unpaired, "UnpairedEnds": unpaired_ends,
           "MaximumPairedRuntimeSuspensionMilliseconds": max((p["Milliseconds"] for p in pauses), default=None),
           "AllInputIntervals": rows, "AllPairedSuspensions": pauses}
(args.output / "suspension-overlap.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
for row in sorted(rows, key=lambda item: item["GapMilliseconds"], reverse=True)[:4]:
    print(json.dumps(row))
