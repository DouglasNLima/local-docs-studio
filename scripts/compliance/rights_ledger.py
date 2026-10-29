"""List review boundaries for tracked/proposed files; never infer copyright title."""
import csv
import hashlib
import json
from pathlib import Path
import subprocess

from audit_open_source import category

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = 'docs/open-source/evidence/goal62/rights-ledger.csv'
SCOPE = ROOT / 'docs/open-source/configuration/mit-scope.json'


def classification(path):
    if path == 'assets/vendor/manifest.json':
        return 'R1', 'OWNER APPROVAL REQUIRED', 'First-party catalogue candidate; exact public notice and scope pending'
    if (path.startswith('assets/vendor/') or '/PinnedMermaider/' in path
            or path.startswith('docs/native/licences/')
            or path == 'src/native/LensDocsStudio.Core/Rendering/BoundedErLayout.cs'):
        return 'R6', 'NOT APPLICABLE', 'Upstream or adapted material; preserve separate grants and modification notices'
    if (path.startswith('docs/open-source/evidence/')
            or path == 'docs/native/native-853-original-evidence.json'):
        return 'R5', 'NOT APPLICABLE', 'Historical/audit evidence retains provenance and original rights; excluded from default MIT'
    kind = category(path)
    if kind == 'recorded-evidence':
        return 'R5', 'NOT APPLICABLE', 'Historical machine evidence; visible artwork also requires R4 clearance; excluded from default MIT'
    if (kind == 'artwork-or-fixture'
            or path == 'src/native/LensDocsStudio.Native/Resources/CommandIcons.xaml'):
        return 'R4', 'OWNER APPROVAL REQUIRED', 'Excluded until separate visual copyright and branding review'
    if (kind == 'fixture' or path.startswith('assets/scripts/registries/')
            or path.startswith('.github/ISSUE_TEMPLATE/')
            or path == 'src/native/LensDocsStudio.Core/Content/built-in-catalogue.json'
            or path.endswith('.mmd') or '/templates/' in path.lower()
            or '/DocsSiteTemplates/' in path
            or path == 'scripts/native/Generate-ComplexErFixtures.mjs'
            or path == 'src/native/LensDocsStudio.Native.Validation/ContinuousPreviewFixtures.cs'):
        return 'R3', 'OWNER APPROVAL REQUIRED', 'Excluded until individual original or upstream-licensed provenance is verified'
    if kind == 'documentation' or path.endswith('.md'):
        return 'R2', 'OWNER APPROVAL REQUIRED', 'Original documentation candidate; exclude upstream text and uncleared embedded examples/assets'
    return 'R1', 'OWNER APPROVAL REQUIRED', 'Personal-project implementation candidate; exact notice/scope pending; embedded examples excluded'



def boundary(path, granted=None):
    decision, status, reason = classification(path)
    if decision in ('R1', 'R2'):
        if granted is None:
            granted = set(json.loads(SCOPE.read_text(encoding='utf-8'))['paths'])
        if path in granted:
            return decision, 'MIT_ORIGINAL_PORTIONS_ONLY', 'Owner-approved original portions only; embedded R3-R7 and third-party content excluded'
        return decision, 'BLOCKED', 'Unreviewed path; no first-party grant is inferred from its classification'
    return decision, status, reason


def current_paths():
    return set(subprocess.check_output(
        ['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'],
        cwd=ROOT).decode('utf-8').split('\0')) - {''}


def canonical_bytes(path):
    # Ask Git to apply the actual clean filters, including per-path -text rules.
    oid = subprocess.check_output(['git', 'hash-object', '-w', '--', path],
                                  cwd=ROOT, text=True).strip()
    return subprocess.check_output(['git', 'cat-file', 'blob', oid], cwd=ROOT)


def current_digests(paths):
    names = sorted(paths)
    if not names:
        return {}
    if any('\n' in path or '\r' in path for path in names):
        raise ValueError('Unsupported newline in a source path')
    objects = subprocess.check_output(['git', '-c', 'core.safecrlf=false', 'hash-object', '-w', '--stdin-paths'],
                                      input='\n'.join(names) + '\n', text=True,
                                      encoding='utf-8', cwd=ROOT).splitlines()
    process = subprocess.Popen(['git', 'cat-file', '--batch'], cwd=ROOT,
                               stdin=subprocess.PIPE, stdout=subprocess.PIPE)
    result = {}
    try:
        for path, oid in zip(names, objects, strict=True):
            process.stdin.write((oid + '\n').encode())
            process.stdin.flush()
            header = process.stdout.readline().split()
            size = int(header[2])
            data = process.stdout.read(size)
            if len(data) != size or process.stdout.read(1) != b'\n':
                raise RuntimeError('Incomplete Git object read')
            result[path] = hashlib.sha256(data).hexdigest()
    finally:
        process.stdin.close()
        process.stdout.close()
        process.wait()
    if process.returncode:
        raise RuntimeError('Git object reader failed')
    return result


def main():
    paths = current_paths() | {OUTPUT}
    granted = set(json.loads(SCOPE.read_text(encoding='utf-8'))['paths'])
    digests = current_digests(paths - {OUTPUT})
    target = ROOT / OUTPUT
    target.parent.mkdir(parents=True, exist_ok=True)
    with target.open('w', encoding='utf-8', newline='') as stream:
        writer = csv.writer(stream, lineterminator='\n')
        writer.writerow(['path', 'decisionId', 'ownerDecision', 'reason', 'sha256'])
        for path in sorted(paths):
            digest = ('BOUND_BY_FINAL_GIT_TREE' if path == OUTPUT else
                      digests[path])
            writer.writerow([path, *boundary(path, granted), digest])
    print(f'Rights boundaries recorded for {len(paths)} paths; only the explicit reviewed allowlist grants original portions.')


if __name__ == '__main__':
    main()
