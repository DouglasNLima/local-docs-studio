"""Read-only verification of the inert reflow proposal, never an activation tool."""
import copy
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
BASE = '3cd08bc2de31564e06973c5211e1aa231731f9cb'
CANDIDATE = '752b2c26e6c309b77fe23b812c6f3d1a5114d369'
MANIFEST_SHA256 = '1cecd8b708ea165e1bb73eb98382efb7799bd5dddd21cad3e5d46c586e3db9eb'
FILES = {
    'src/native/LensDocsStudio.Native/MainWindow.Find.cs',
    'src/native/LensDocsStudio.Native/MainWindow.Synchronisation.cs',
    'src/native/LensDocsStudio.Native/MainWindow.xaml.cs',
}
PREFIXES = ('src/native', 'tests/fixtures/native-flowchart',
            'tests/native/preview-reflow', 'tests/fixtures/native-preview-reflow')


def git(*args, data=None):
    return subprocess.check_output(['git', *args], cwd=ROOT, input=data)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def blob(revision, path):
    return git('show', revision + ':' + path)


def tree(revision, path=None):
    return git('rev-parse', revision + (':' + path if path else '^{tree}')).decode().strip()


def verify(contract):
    require(contract['status'] == 'PROPOSED_NOT_APPROVED' and contract['ownerApproval'] is None,
            'An inert proposal cannot contain an approval')
    require(contract['proposalId'] == 'preview-reflow-integration-three-v1', 'Wrong proposal identity')
    for role, revision in [('baseline', BASE), ('candidate', CANDIDATE)]:
        require(contract[role] == {'commit': revision, 'tree': tree(revision)}, 'Source identity changed')
    tested = '2003c1f24de3fa8c7b98e18ec543655cec281cf0'
    require(contract['testedProduct'] == {'commit': tested, 'tree': tree(tested)}, 'Tested identity changed')
    require(tree(tested, 'src/native') == tree(CANDIDATE, 'src/native')
            and tree(tested, 'tests/native/preview-reflow') == tree(CANDIDATE, 'tests/native/preview-reflow'),
            'Tested product or control code changed')
    require(set(contract['files']) == FILES, 'Protected scope changed')
    require(contract['excludedBoundaries'] == ['R3', 'R4', 'R5', 'R6', 'R7'], 'Rights exclusions changed')
    protected = json.loads(blob(BASE, 'docs/open-source/evidence/goal62/preserved-files.json'))['sha256']
    changes = set(git('diff', '--name-only', '--diff-filter=M', BASE, CANDIDATE).decode().splitlines())
    require(changes == FILES and changes <= set(protected), 'Unexpected protected modification')
    for path, pair in contract['files'].items():
        require(pair == {'previous': digest(blob(BASE, path)), 'current': digest(blob(CANDIDATE, path))},
                'Protected digest changed: ' + path)
    added = set(git('diff', '--name-only', '--diff-filter=A', BASE, CANDIDATE).decode().splitlines())
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
    # Every previous tracked byte except the three proposed pairs and the regenerable
    # current inventory stays immutable, including workflows and historical failures.
    current_changes = set(git('diff', '--name-only', '--diff-filter=MDR', BASE, 'HEAD').decode().splitlines())
    require(current_changes <= FILES | {'docs/open-source/evidence/goal62/rights-ledger.csv'},
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
    raw = (HERE / 'candidate-refresh.json').read_bytes()
    require(digest(raw) == MANIFEST_SHA256, 'Manifest bytes changed')
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
    ]
    rejected = []
    for name, mutate in mutations:
        changed = copy.deepcopy(contract)
        mutate(changed)
        try:
            verify(changed)
        except ValueError:
            rejected.append(name)
        else:
            raise ValueError('Negative control accepted: ' + name)
    print(json.dumps({'status': 'PASS_INERT_PROPOSAL_ONLY', 'protectedFiles': protected,
                      'changedProtectedFiles': sorted(FILES), 'addedFiles': added,
                      'manifestSha256': MANIFEST_SHA256, 'negativeControlsRejected': rejected,
                      'activePolicy': 'UNCHANGED; candidate governance remains blocked pending owner approval'}, indent=2))


if __name__ == '__main__':
    main()
