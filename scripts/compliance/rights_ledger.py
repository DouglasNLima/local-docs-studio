"""List review boundaries for tracked/proposed files; never infer copyright title."""
import csv
from pathlib import Path
import subprocess

from audit_open_source import category

OUTPUT = 'docs/open-source/evidence/goal58/rights-ledger.csv'


def boundary(path):
    if path == 'assets/vendor/manifest.json':
        return 'R1', 'OWNER ACTION REQUIRED', 'Project asset catalogue; authorship and title unconfirmed'
    if path.startswith('assets/vendor/') or '/PinnedMermaider/' in path or path.startswith('docs/native/licences/'):
        return 'R6', 'NOT APPLICABLE', 'Upstream or adapted material; preserve separate grants and modification notices'
    if path.startswith('docs/open-source/evidence/'):
        return 'R5', 'OWNER ACTION REQUIRED', 'Audit data or preserved third-party text; no blanket MIT over quoted upstream grants'
    kind = category(path)
    if kind in ('recorded-evidence', 'artwork-or-fixture'):
        return 'R4', 'OWNER ACTION REQUIRED', 'Confirm artwork, visible content, publication permission and trademark boundary'
    if kind == 'fixture' or path.startswith('assets/scripts/registries/'):
        return 'R3', 'OWNER ACTION REQUIRED', 'Confirm synthetic/original or licensed provenance; exclude customer content'
    if kind == 'documentation' or path.endswith('.md'):
        return 'R2', 'OWNER ACTION REQUIRED', 'Confirm documentation title and permission for examples/quoted material'
    return 'R1', 'OWNER ACTION REQUIRED', 'Candidate first-party implementation; repository authorship is not proof of title'


def main():
    paths = set(subprocess.check_output(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard']).decode().split('\0'))
    paths.discard('')
    paths.add(OUTPUT)
    target = Path(OUTPUT)
    target.parent.mkdir(parents=True, exist_ok=True)
    with target.open('w', encoding='utf-8', newline='') as stream:
        writer = csv.writer(stream, lineterminator='\n')
        writer.writerow(['path', 'decisionId', 'ownerDecision', 'reason'])
        for path in sorted(paths):
            writer.writerow([path, *boundary(path)])
    print(f'Rights decisions recorded for {len(paths)} current/proposed paths; no title or licence grant inferred.')


if __name__ == '__main__':
    main()
