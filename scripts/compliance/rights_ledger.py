"""List review boundaries for tracked/proposed files; never infer copyright title."""
import csv
from pathlib import Path
import subprocess

from audit_open_source import category

OUTPUT = 'docs/open-source/evidence/goal60/rights-ledger.csv'


def boundary(path):
    if path == 'assets/vendor/manifest.json':
        return 'R1', 'OWNER APPROVAL REQUIRED', 'First-party catalogue candidate; exact public notice and scope pending'
    if (path.startswith('assets/vendor/') or '/PinnedMermaider/' in path
            or path.startswith('docs/native/licences/')
            or path == 'src/native/LensDocsStudio.Core/Rendering/BoundedErLayout.cs'):
        return 'R6', 'NOT APPLICABLE', 'Upstream or adapted material; preserve separate grants and modification notices'
    if path.startswith('docs/open-source/evidence/'):
        return 'R5', 'NOT APPLICABLE', 'Historical/audit evidence retains provenance and original rights; excluded from default MIT'
    kind = category(path)
    if kind == 'recorded-evidence':
        return 'R5', 'NOT APPLICABLE', 'Historical machine evidence; visible artwork also requires R4 clearance; excluded from default MIT'
    if kind == 'artwork-or-fixture':
        return 'R4', 'OWNER APPROVAL REQUIRED', 'Excluded until separate visual copyright and branding review'
    if (kind == 'fixture' or path.startswith('assets/scripts/registries/')
            or path.endswith('.mmd') or '/templates/' in path
            or path == 'src/native/LensDocsStudio.Native.Validation/ContinuousPreviewFixtures.cs'):
        return 'R3', 'OWNER APPROVAL REQUIRED', 'Excluded until individual original or upstream-licensed provenance is verified'
    if kind == 'documentation' or path.endswith('.md'):
        return 'R2', 'OWNER APPROVAL REQUIRED', 'Original documentation candidate; exclude upstream text and uncleared embedded examples/assets'
    return 'R1', 'OWNER APPROVAL REQUIRED', 'Personal-project implementation candidate; exact notice/scope pending; embedded examples excluded'


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
