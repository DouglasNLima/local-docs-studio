"""Independent scalar oracle comparison, outside all timing boundaries."""
import json
import re
import sys
from pathlib import Path

stock, candidate = (json.loads(Path(path).read_text(encoding="utf-8-sig")) for path in sys.argv[1:3])
expected = re.sub(r"(?m)^(?:\u2022|\d+\.)\t", "", stock["LogicalText"])
failures = []
if expected != candidate["LogicalText"]:
    failures.append("Logical text differs after explicit removal of stock semantic marker/tab serialisation")
if stock["Items"] != candidate["Items"] or stock["SemanticLists"] != candidate["SemanticLists"]:
    failures.append("Semantic list/item counts differ")
if abs(stock["Height"] - candidate["Height"]) > .01:
    failures.append("Complete extent differs")
max_top = max_width = max_height = 0.0
for before, after in zip(stock["Lines"], candidate["Lines"], strict=True):
    if before["IsEmpty"]:
        failures.append("Stock oracle geometry unavailable")
        continue
    carets = after["Carets"]
    max_top = max(max_top, abs(before["Top"] - after["Top"]))
    max_width = max(max_width, abs(before["X"] - carets[0]["X"]), abs(before["EndX"] - carets[-1]["X"]))
    max_height = max(max_height, abs(before["Height"] - after["Height"]))
    if before["Text"] != candidate["LogicalText"][after["Start"]:after["End"]]:
        failures.append(f"Actual item text differs at {before['Item']}")
if max(max_top, max_width, max_height) > .01:
    failures.append("Shaped line geometry differs beyond 0.01 DIP (three WPF text units)")
result = dict(Status="FAIL" if failures else "PASS", Failures=failures, ComparedItems=stock["Items"],
              MaximumTopDifference=max_top, MaximumHorizontalDifference=max_width, MaximumHeightDifference=max_height,
              StockExtent=stock["Height"], CandidateExtent=candidate["Height"],
              MarkerSerialisation="Stock serialises bullet + tab; candidate logical ranges exclude semantic markers. Clipboard parity is a separate OPEN requirement.",
              Raster="Separate visual comparison required; geometry equality is not byte equality")
Path(sys.argv[3]).write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result))
sys.exit(bool(failures))
