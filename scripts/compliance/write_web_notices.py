"""Assemble known web notices from hash-verified original texts, without relicensing."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/open-source/evidence'
TARGET = ROOT / 'assets/vendor/THIRD-PARTY-NOTICES.txt'
TOOLING = {'@playwright/test', 'playwright', 'playwright-core'}


def main():
    inventory = json.loads((EVIDENCE / 'inventory.json').read_text(encoding='utf-8'))
    runtime = {p['name'] + '@' + p['version'] for p in inventory['npm'] if p['name'] not in TOOLING}
    index = json.loads((EVIDENCE / 'upstream-notices/index.json').read_text(encoding='utf-8'))
    selected = sorted((r for r in index if r['component'] in runtime), key=lambda r: (r['component'], r['upstreamPath']))
    content = bytearray(
        b'Lens Docs Studio - known third-party web distribution notices\n\n'
        b'No first-party licence is adopted by this file. Upstream grants apply only\n'
        b'to their own material. Original notice bytes follow each labelled header.\n'
        b'This pack includes upstream package notices conservatively; inclusion does\n'
        b'not establish that every package subcomponent is shipped. In particular,\n'
        b'the vendored PDF.js files do not include its optional font/WASM directories.\n'
        b'Mammoth embedded dependency closure remains under review. This pack is not\n'
        b'a claim of complete redistribution clearance. See docs/open-source/third-party-closeout.md.\n\n')
    for record in selected:
        original = (EVIDENCE / 'upstream-notices' / record['file']).read_bytes()
        if hashlib.sha256(original).hexdigest() != record['sha256']:
            raise ValueError('Original notice hash mismatch: ' + record['file'])
        label = f"===== {record['component']} | {record['upstreamPath']} =====\nSource: {record['source']}\nSHA-256: {record['sha256']}\n\n"
        content.extend(label.encode('utf-8'))
        content.extend(original)
        content.extend(b'\n\n')
    TARGET.write_bytes(content)
    report = dict(noticePack='assets/vendor/THIRD-PARTY-NOTICES.txt', sha256=hashlib.sha256(content).hexdigest(),
                  components=len({r['component'] for r in selected}), originalNoticeReferences=len(selected),
                  componentsWithoutIndexedNotice=sorted(runtime - {r['component'] for r in selected}),
                  originalNoticeBytesPreserved=True, completeEmbeddedClosure=False, notices=selected)
    destination = EVIDENCE / 'goal58/web-notice-pack.json'
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k != 'notices'}))


if __name__ == '__main__':
    main()
