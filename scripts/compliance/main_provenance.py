"""Verify the reviewed main source boundary; never infer title or clear a gap."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[2]
SCOPE = 'docs/open-source/main-scope.json'
LEDGER = 'docs/open-source/main-rights-ledger.json'
PROVENANCE = 'docs/open-source/main-provenance.json'
MIT_SHA256 = 'f4126eb8680101babaa6c1ee89b9693320eb204139f3ee3fcd08abb66eb31261'
BASELINE = '0d100cc29dde2ba80755fb1e8af60ffd9d006b16'
APPROVED = '709564dda30a57cf1e98ba07566f510c1636e05c'
PUBLIC_FILES = ['LICENSE', 'LICENSING.md', 'SECURITY.md', 'THIRD_PARTY_NOTICES.md',
                SCOPE, LEDGER, PROVENANCE, 'docs/open-source/main-promotion.md',
                'docs/open-source/third-party-closeout.md',
                'docs/open-source/licences/windows-app-sdk.txt',
                'docs/open-source/licences/webview2-sdk.txt',
                'docs/open-source/licences/playwright.txt',
                'docs/open-source/licences/playwright-notice.txt',
                'docs/open-source/code-signing-policy.md',
                'docs/open-source/privacy.md',
                'docs/open-source/downloads.md',
                'docs/open-source/public-policy-review.md']


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def read_json(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8'))


def paths():
    return set(subprocess.check_output(
        ['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'],
        cwd=ROOT).decode('utf-8').split('\0')) - {''}


def category(path):
    if path == 'assets/vendor/manifest.json':
        return 'R1'
    if path.startswith(('assets/vendor/', 'docs/open-source/licences/')):
        return 'R6'
    if path == LEDGER or path == PROVENANCE:
        return 'R5'
    if path.lower().endswith(('.png', '.jpg', '.jpeg', '.svg', '.ico', '.webp')):
        return 'R4'
    if path.startswith(('tests/fixtures/', 'assets/scripts/registries/',
                        '.github/ISSUE_TEMPLATE/', 'docs/release/templates/')):
        return 'R3'
    return 'R2' if path.endswith('.md') else 'R1'


def canonical_digests(names):
    names = sorted(names)
    require(not any('\n' in p or '\r' in p for p in names), 'Unsupported source path')
    objects = subprocess.check_output(
        ['git', '-c', 'core.safecrlf=false', 'hash-object', '-w', '--stdin-paths'],
        input='\n'.join(names) + '\n', cwd=ROOT, text=True, encoding='utf-8').splitlines()
    result = {}
    with subprocess.Popen(['git', 'cat-file', '--batch'], cwd=ROOT,
                          stdin=subprocess.PIPE, stdout=subprocess.PIPE) as process:
        for name, oid in zip(names, objects, strict=True):
            process.stdin.write((oid + '\n').encode())
            process.stdin.flush()
            size = int(process.stdout.readline().split()[2])
            raw = process.stdout.read(size)
            require(len(raw) == size and process.stdout.read(1) == b'\n', 'Incomplete Git blob')
            result[name] = digest(raw)
        process.stdin.close()
        require(process.wait() == 0, 'Git blob reader failed')
    return result


def validate_scope(scope, names):
    require(scope['baseline'] == BASELINE and scope['approvedSource'] == APPROVED,
            'Unexpected review baseline')
    require(scope['copyright'] == 'Copyright (c) 2026 DouglasNLima', 'Wrong attribution')
    require(scope['scope'] == 'Original first-party portions only; embedded R3-R7 content excluded',
            'Blanket or altered grant')
    require(scope['excludedBoundaries'] == ['R3', 'R4', 'R5', 'R6', 'R7'], 'Exclusions changed')
    grants = scope['paths']
    require(grants == sorted(set(grants)) and set(grants) <= names,
            'Duplicate, missing or unsorted grant paths')
    require(all(category(p) in ('R1', 'R2') for p in grants), 'Excluded path granted')
    # New original material is blocked until a reviewer explicitly extends the scope.
    require({p for p in names if category(p) in ('R1', 'R2')} == set(grants),
            'Unreviewed first-party path: BLOCKED')
    return set(grants)


def validate_ledger(rows, names, hashes, grants):
    require(len(rows) == len({r['path'] for r in rows}) and {r['path'] for r in rows} == names,
            'Incomplete or duplicate path inventory')
    for row in rows:
        path = row['path']
        require(row['category'] == category(path), 'Wrong classification: ' + path)
        expected = 'MIT_ORIGINAL_PORTIONS_ONLY' if path in grants else 'EXCLUDED_FROM_FIRST_PARTY_GRANT'
        require(row['grant'] == expected, 'Wrong grant: ' + path)
        expected_hash = 'BOUND_BY_FINAL_GIT_TREE' if path == LEDGER else hashes[path]
        require(row['sha256'] == expected_hash, 'Stale source digest: ' + path)


def validate_notices(provenance, hashes, pack):
    require(provenance['baseline'] == BASELINE and provenance['approvedSource'] == APPROVED,
            'Wrong vendor review baseline')
    vendor = provenance['vendorFiles']
    require(set(vendor) == {p for p in hashes if p.startswith('assets/vendor/')
                            and p not in ('assets/vendor/manifest.json', 'assets/vendor/THIRD-PARTY-NOTICES.txt')},
            'Vendor inventory incomplete')
    require(all(hashes[p] == sha for p, sha in vendor.items()), 'Vendor runtime bytes changed')
    require(digest(pack) == provenance['noticePackSha256'], 'Web notice pack changed')
    require(len(provenance['notices']) == 68, 'Original notice index incomplete')
    for item in provenance['notices']:
        raw = pack[item['offset']:item['offset'] + item['length']]
        require(digest(raw) == item['sha256'], 'Original notice bytes missing: ' + item['component'])
    for path, item in provenance['additionalNotices'].items():
        require(hashes.get(path) == item['sha256'], 'Additional notice changed: ' + path)
    require(provenance['completeEmbeddedClosure'] is False
            and provenance['componentsWithoutIndexedNotice'] == ['dingbat-to-unicode@1.0.1'],
            'Unresolved notice gap was hidden')


def validate_links(doc, text, root=ROOT):
    prose = re.sub(r'(?ms)^ {0,3}(`{3,}|~{3,})[^\n]*\n.*?^ {0,3}\1[ \t]*$', '', text)
    prose = re.sub(r'`[^`\n]*`', '', prose)
    for target in re.findall(r'\]\(([^)]+)\)', prose):
        if re.match(r'^[a-z]+:', target) or target.startswith('#'):
            continue
        relative = unquote(target.split('#', 1)[0])
        path = (root / doc).parent / relative
        require(path.resolve().is_relative_to(root.resolve()) and path.exists(),
                'Missing/escaping policy link: ' + doc + ': ' + target)


def validate_policy_cache(sw):
    require("const CACHE_NAME = 'lens-docs-studio-v69';" in sw, 'Review the cache version')
    require(all("'./" + p + "'" in sw for p in PUBLIC_FILES), 'Public policy missing from offline cache')


def validate_packaging():
    sw = (ROOT / 'service-worker.js').read_text(encoding='utf-8')
    validate_policy_cache(sw)
    manifest = read_json('assets/vendor/manifest.json')
    require('./assets/vendor/THIRD-PARTY-NOTICES.txt' in manifest, 'Web notices not cached')
    workflow = (ROOT / '.github/workflows/pages.yml').read_text(encoding='utf-8')
    copy = next(line for line in workflow.splitlines() if 'Copy-Item -Path index.html,' in line)
    require(all(p in copy for p in PUBLIC_FILES[:4]), 'Public root documents absent from Pages artefact')
    require('Copy-Item -Path docs -Destination pages-artifact -Recurse' in workflow,
            'Policy dependencies absent from Pages artefact')
    legacy = (ROOT / 'src/windows/LensDocsStudio.Windows/LensDocsStudio.Windows.csproj').read_text(encoding='utf-8')
    require(all('TargetPath="StaticApp\\' + p + '"' in legacy for p in PUBLIC_FILES[:4]),
            'Root policy files absent from legacy static package')
    require("github.event_name != 'pull_request' && github.ref == format('refs/heads/{0}', github.event.repository.default_branch)" in workflow,
            'Production deployment boundary changed')


def main(write=False):
    names = paths() | {LEDGER}
    grants = validate_scope(read_json(SCOPE), names)
    hashes = canonical_digests(names - {LEDGER})
    if write:
        rows = [{'path':p, 'category':category(p),
                 'grant':'MIT_ORIGINAL_PORTIONS_ONLY' if p in grants else 'EXCLUDED_FROM_FIRST_PARTY_GRANT',
                 'sha256':'BOUND_BY_FINAL_GIT_TREE' if p == LEDGER else hashes[p]} for p in sorted(names)]
        (ROOT / LEDGER).write_text(json.dumps(rows, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
    validate_ledger(read_json(LEDGER), names, hashes, grants)
    require(digest((ROOT / 'LICENSE').read_bytes()) == MIT_SHA256, 'Approved exact MIT bytes changed')
    validate_notices(read_json(PROVENANCE), hashes, (ROOT / 'assets/vendor/THIRD-PARTY-NOTICES.txt').read_bytes())
    for path in ['README.md', 'LICENSING.md', 'SECURITY.md', 'THIRD_PARTY_NOTICES.md',
                 'docs/open-source/main-promotion.md', 'docs/open-source/third-party-closeout.md',
                 'docs/open-source/code-signing-policy.md', 'docs/open-source/privacy.md',
                 'docs/open-source/downloads.md', 'docs/open-source/public-policy-review.md']:
        validate_links(path, (ROOT / path).read_text(encoding='utf-8'))
    require(read_json('package.json')['license'] == read_json('package-lock.json')['packages']['']['license']
            == 'SEE LICENSE IN LICENSING.md', 'Package metadata overstates the grant')
    validate_packaging()
    print(json.dumps({'baseline':BASELINE, 'grantPaths':len(grants), 'ledgerPaths':len(names),
                      'vendorFiles':len(read_json(PROVENANCE)['vendorFiles']), 'originalNoticeReferences':68,
                      'completeEmbeddedClosure':False, 'publicationAuthorised':False}))


if __name__ == '__main__':
    require(sys.argv[1:] in ([], ['--write-ledger']), 'Unsupported operation')
    main(sys.argv[1:] == ['--write-ledger'])
