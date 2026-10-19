"""Read-only raw-result comparison. No clocks, retries, filtering or percentiles."""
import argparse
import csv
import hashlib
import json
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("roots", nargs="+", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    samples, groups, manifests = [], [], []
    for root in args.roots:
        host = str(root.resolve())
        protocol = read(root / "protocol-v2.json")
        grouped = {}
        files = [(file, "CompletedObservation") for file in sorted(root.rglob("sample.json"))]
        files.extend((file, "TimedBoundaryOnly_CorrectnessIncomplete") for file in sorted(root.rglob("timing-boundary.json")) if not (file.parent / "sample.json").exists())
        for file, receipt_kind in files:
            data = read(file)
            data["ReceiptKind"] = receipt_kind
            row = {"Source": data.get("SourceRevision"), "HostRoot": host,
                   "Path": str(file.relative_to(root)), **data}
            samples.append(row)
            key = (data["Adapter"], data["Fixture"], data.get("RunGroup"))
            grouped.setdefault(key, []).append(data)
        for key, values in grouped.items():
            def observation(field):
                observed = [v[field] for v in values if isinstance(v.get(field), (int, float))]
                return {"Observed": len(observed), "Minimum": min(observed) if observed else None,
                        "Maximum": max(observed) if observed else None}
            groups.append({"Source": protocol["Source"], "HostRoot": host,
                           "Adapter": key[0], "Fixture": key[1], "RunGroup": key[2], "Samples": len(values),
                           "LayoutClock": "Stock original first-layout" if key[0] == "stock" else "Before first required work through complete install/Input",
                           "CompleteLayout": observation("CompleteLayoutInputMilliseconds"),
                           "PreparationPresentation": observation("EndToEndReadinessMilliseconds"),
                           "TimingInputGap": observation("MaximumInputGapMilliseconds"),
                           "InteractionInputGap": observation("MaximumInteractionInputGapMilliseconds"),
                           "AllocatedBytes": observation("AllocatedBytes"), "ManagedBytes": observation("ManagedBytes"),
                           "PrivateMemoryBytes": observation("PrivateMemorySize64")})
        manifest = []
        for file in sorted(root.rglob("*")):
            if file.is_file():
                with file.open("rb") as stream:
                    digest = hashlib.file_digest(stream, "sha256").hexdigest()
                manifest.append({"Path": str(file.relative_to(root)), "Bytes": file.stat().st_size, "Sha256": digest})
        manifests.append({"HostRoot": host, "Source": protocol["Source"], "SourceDirty": protocol["SourceDirty"],
                          "Protocol": protocol, "Files": manifest})
    (args.output / "summary.json").write_text(json.dumps({"Groups": groups, "OriginalFileManifests": manifests}, indent=2), encoding="utf-8")
    fields = ["Source", "HostRoot", "Path", "ReceiptKind", "Adapter", "Fixture", "RunGroup", "Repetition", "Edited", "Temperature",
              "CompleteLayoutInputMilliseconds", "EndToEndReadinessMilliseconds", "MaximumInputGapMilliseconds",
              "MaximumInteractionInputGapMilliseconds", "AllocatedBytes", "ManagedBytes", "PrivateMemorySize64",
              "NaturalCollections", "UiStartThread", "ThemePreparationThread", "PreparationThread", "WorkerLayoutThread", "UiEndThread",
              "HarnessSha256", "FixtureSha256", "SourceSha256", "CompilationConfiguration", "Runtime", "Cpu", "CpuAvailable", "Dpi", "Zoom", "Appearance", "SourceDirty"]
    with (args.output / "every-sample.csv").open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(samples)
    print(f"Retained {len(samples)} samples in {len(groups)} separate source/host/fixture/temperature/adapter groups.")


if __name__ == "__main__":
    main()
