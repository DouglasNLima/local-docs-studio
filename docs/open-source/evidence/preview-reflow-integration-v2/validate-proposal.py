"""Verify an inert proposal using committed bytes and separate working-source checks."""
import copy
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
BASE = '3cd08bc2de31564e06973c5211e1aa231731f9cb'
START = '16b4b6864fd6cd72e3e7cf2bfb42d25af713ea85'
CANDIDATE = '932335027314fe710671d54266367a008fca5547'
PROPOSAL = 'preview-reflow-integration-three-v2'
MANIFEST_PATH = 'docs/open-source/evidence/preview-reflow-integration-v2/candidate-refresh.json'
MANIFEST_SHA256 = '340745a23758a94cb50b9aadeb761b817697777481a39762dbe0465ef64a8681'
FILES = {
    'src/native/LensDocsStudio.Native/MainWindow.Find.cs',
    'src/native/LensDocsStudio.Native/MainWindow.Synchronisation.cs',
    'src/native/LensDocsStudio.Native/MainWindow.xaml.cs',
}
PREFIXES = ('src/native', 'tests/fixtures/native-flowchart',
            'tests/native/preview-reflow', 'tests/fixtures/native-preview-reflow')
HISTORICAL = ('docs/native/evidence/preview-reflow-integration',
              'docs/open-source/evidence/preview-reflow-integration')


def git(*args, data=None, root=ROOT):
    return subprocess.check_output(['git', *args], cwd=root, input=data)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def blob(revision, path):
    # subprocess returns the raw object bytes: no text mode or shell pipeline.
    return git('cat-file', 'blob', revision + ':' + path)


def tree(revision, path=None):
    return git('rev-parse', revision + (':' + path if path else '^{tree}')).decode().strip()


def committed_manifest(root, revision, path, expected_sha256):
    entries = git('ls-tree', '-z', revision, '--', path, root=root).split(b'\0')
    require(len(entries) == 2 and not entries[1], 'Committed manifest is missing or ambiguous')
    metadata, name = entries[0].split(b'\t', 1)
    mode, kind, oid = metadata.split()
    require(name.decode() == path and mode == b'100644' and kind == b'blob',
            'Committed manifest must be a regular source blob')
    raw = git('cat-file', 'blob', oid.decode(), root=root)
    require(digest(raw) == expected_sha256, 'Canonical committed manifest bytes changed')
    return raw, oid


def verify_manifest_working_tree(root, path, oid):
    # A correct committed pin does not authorise modified, missing or staged content.
    working = Path(root) / path
    require(working.is_file() and not working.is_symlink(), 'Working manifest is missing or substituted')
    index = git('ls-files', '--stage', '-z', '--', path, root=root)
    require(index == b'100644 ' + oid + b' 0\t' + path.encode() + b'\0',
            'Manifest index differs from the committed identity')
    working_bytes = working.read_bytes()
    committed_bytes = git('cat-file', 'blob', oid.decode(), root=root)
    require(working_bytes.replace(b'\r\n', b'\n') == committed_bytes.replace(b'\r\n', b'\n'),
            'Working manifest differs beyond checkout line endings')
    # Apply Git's path-specific clean conversion; LF and CRLF checkouts refer to
    # the same source blob. Never parse/re-serialise JSON to manufacture identity.
    cleaned = git('-c', 'core.safecrlf=false', 'hash-object', '--path=' + path, '--stdin',
                  data=working_bytes, root=root).strip()
    require(cleaned == oid, 'Working manifest differs after Git clean conversion')


def read_verified_manifest(root, revision, path, expected_sha256):
    raw, oid = committed_manifest(root, revision, path, expected_sha256)
    verify_manifest_working_tree(root, path, oid)
    return raw


def verify(contract):
    require(contract['status'] == 'PROPOSED_NOT_APPROVED' and contract['ownerApproval'] is None,
            'An inert proposal cannot contain an approval')
    require(contract['proposalId'] == PROPOSAL, 'Wrong proposal identity')
    for role, revision in [('baseline', BASE), ('candidate', CANDIDATE)]:
        require(contract[role] == {'commit': revision, 'tree': tree(revision)}, 'Source identity changed')
    require(contract['testedProduct'] == contract['candidate'], 'Tested product identity changed')
    require(set(contract['files']) == FILES, 'Protected scope changed')
    require(contract['excludedBoundaries'] == ['R3', 'R4', 'R5', 'R6', 'R7'], 'Rights exclusions changed')
    protected = json.loads(blob(BASE, 'docs/open-source/evidence/goal62/preserved-files.json'))['sha256']
    modified = set(git('diff', '--name-only', '--diff-filter=MDR', BASE, CANDIDATE, '--', *PREFIXES).decode().splitlines())
    require(modified == FILES and modified <= set(protected), 'Unexpected protected modification')
    for path, pair in contract['files'].items():
        require(pair == {'previous': digest(blob(BASE, path)), 'current': digest(blob(CANDIDATE, path))},
                'Protected digest changed: ' + path)
    added = set(git('diff', '--name-only', '--diff-filter=A', BASE, CANDIDATE, '--', *PREFIXES).decode().splitlines())
    require(set(contract['addedFiles']) == added, 'Added source inventory changed')
    for path, expected in contract['addedFiles'].items():
        require(digest(blob(CANDIDATE, path)) == expected, 'Added source digest changed: ' + path)
    for field, revision in [('previousRuntimeSourceTrees', BASE), ('runtimeSourceTrees', CANDIDATE),
                            ('controlTrees', CANDIDATE)]:
        expected_paths = set(PREFIXES[2:] if field == 'controlTrees' else PREFIXES[:2])
        require(set(contract[field]) == expected_paths, 'Source tree scope changed')
        for path, expected in contract[field].items():
            require(tree(revision, path) == expected, 'Source subtree changed: ' + path)
    for path, expected in contract['preservedEvidence'].items():
        require(digest(blob(BASE, path)) == expected and blob('HEAD', path) == blob(BASE, path),
                'Previous approval or scope changed: ' + path)
    require(set(contract['historicalEvidenceTrees']) == set(HISTORICAL), 'Historical evidence scope changed')
    for path, expected in contract['historicalEvidenceTrees'].items():
        require(tree(START, path) == expected and tree('HEAD', path) == expected,
                'Original observations or superseded proposal changed: ' + path)
    changes = set(git('diff', '--name-only', '--diff-filter=MDR', BASE, 'HEAD').decode().splitlines())
    require(changes <= FILES | {'docs/open-source/evidence/goal62/rights-ledger.csv'},
            'Unrelated pre-existing bytes changed')
    return len(protected), len(added)


def verify_working_source(contract):
    expected = {}
    for row in git('ls-tree', '-rz', '--full-tree', CANDIDATE, '--', *PREFIXES).split(b'\0'):
        if not row:
            continue
        metadata, name = row.split(b'\t', 1)
        mode, kind, oid = metadata.decode().split()
        require(kind == 'blob' and mode in ('100644', '100755'), 'Unexpected source object')
        expected[name.decode()] = oid
    paths = sorted(set(git('ls-files', '--cached', '--others', '--exclude-standard', '--', *PREFIXES).decode().splitlines()))
    objects = git('-c', 'core.safecrlf=false', 'hash-object', '--stdin-paths',
                  data=('\n'.join(paths) + '\n').encode()).decode().splitlines()
    require(expected == dict(zip(paths, objects, strict=True)), 'Candidate working source changed')
    for path in PREFIXES:
        require(tree('HEAD', path) == tree(CANDIDATE, path), 'Candidate committed source changed')


def main():
    raw = read_verified_manifest(ROOT, 'HEAD', MANIFEST_PATH, MANIFEST_SHA256)
    contract = json.loads(raw)
    protected, added = verify(contract)
    verify_working_source(contract)
    mutations = [
        ('extra protected path', lambda d: d['files'].update({'unexpected.cs': {'previous': '0' * 64, 'current': '1' * 64}})),
        ('changed previous digest', lambda d: d['files'][sorted(FILES)[0]].update(previous='0' * 64)),
        ('changed current digest', lambda d: d['files'][sorted(FILES)[0]].update(current='0' * 64)),
        ('changed source subtree', lambda d: d['runtimeSourceTrees'].update({'src/native': '0' * 40})),
        ('invented owner approval', lambda d: d.update(ownerApproval={'approved': True})),
        ('removed exclusion', lambda d: d['excludedBoundaries'].remove('R3')),
        ('missing added path', lambda d: d['addedFiles'].pop(sorted(d['addedFiles'])[0])),
        ('changed added digest', lambda d: d['addedFiles'].update({sorted(d['addedFiles'])[0]: '0' * 64})),
        ('changed historical evidence', lambda d: d['historicalEvidenceTrees'].update({HISTORICAL[0]: '0' * 40})),
    ]
    rejected = []
    for name, mutate in mutations:
        changed = copy.deepcopy(contract); mutate(changed)
        try:
            verify(changed)
        except ValueError:
            rejected.append(name)
        else:
            raise ValueError('Negative control accepted: ' + name)
    print(json.dumps({'status': 'PASS_INERT_PROPOSAL_ONLY', 'protectedFiles': protected,
                      'changedProtectedFiles': sorted(FILES), 'addedFiles': added,
                      'manifestSha256': digest(raw), 'canonicalRepresentation': 'Exact committed Git blob bytes',
                      'workingTree': 'Index and Git-clean source objects verified separately',
                      'candidate': contract['candidate'], 'nativeTree': contract['runtimeSourceTrees']['src/native'],
                      'negativeControlsRejected': rejected,
                      'activePolicy': 'UNCHANGED; owner approval and bounded activation still required'}, indent=2))


if __name__ == '__main__':
    main()
