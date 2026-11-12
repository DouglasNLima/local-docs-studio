"""Verify the inactive Outline/TOC proposal; never alter the active governance rules."""
import copy
import hashlib
import json
from pathlib import Path
import runpy
import subprocess

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
BASE = '50bc2354e49662c9831338310d23c91cfe7a7b8a'
CANDIDATE = 'c10d673f9d4955e4fe51cf44850edc54bee708de'
PRODUCT = 'c10d673f9d4955e4fe51cf44850edc54bee708de'
MANIFEST = 'docs/open-source/evidence/outline-toc/candidate-refresh.json'
MANIFEST_SHA256 = 'acff902eec465c6fb56c9dfca7943d0beb61f1029e55e9842d69d0efd4bbbcf2'
PROPOSAL_PREFIX = 'docs/open-source/evidence/outline-toc/'
LEDGER = 'docs/open-source/evidence/goal62/rights-ledger.csv'
FILES = {
    'docs/native/architecture-decisions.md',
    'docs/native/functional-parity-matrix.md',
    'docs/native/implementation-progress.md',
    'docs/native/native-experience-improvements.md',
    'docs/native/validation-report.md',
    'src/native/LensDocsStudio.Native.Validation/NativePreviewSchedulingChecks.cs',
    'src/native/LensDocsStudio.Native.Validation/NativeShellLayoutChecks.cs',
    'src/native/LensDocsStudio.Native.Validation/NativeUiOutlineModernisationChecks.cs',
    'src/native/LensDocsStudio.Native/MainWindow.WorkspaceChrome.cs',
    'src/native/LensDocsStudio.Native/MainWindow.xaml',
    'src/native/LensDocsStudio.Native/MainWindow.xaml.cs',
}
RUNTIME = ('src/native', 'tests/fixtures/native-flowchart')
UNCHANGED = ('.github', 'assets', 'src/windows', 'tests/native/document-statistics', 'tests/native/preview-reflow', 'tests/native/preview-loading-presentation', 'tests/fixtures/native-preview-reflow', 'tests/fixtures/native-flowchart')
PROPOSAL_FILES = {PROPOSAL_PREFIX + name for name in ['.gitattributes', 'candidate-refresh.json', 'proposal.md',
    'validate-proposal.py', 'independent-review.md', 'validation-results.json', 'engineering-receipt.md',
    'foundation.txt', 'governance.txt', 'governance-tests.txt']}
ACTIVATION = ['scripts/compliance/licence_scope.py', 'scripts/compliance/test_governance.py',
    *(PROPOSAL_PREFIX + name for name in ['owner-approval.json', 'approved-refresh.json',
        'activation-validation.json', 'activation-independent-review.md']), LEDGER]


def git(*args, data=None):
    return subprocess.check_output(['git', *args], cwd=ROOT, input=data)


def blob(revision, path):
    return git('cat-file', 'blob', revision + ':' + path)


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def tree(revision, path=None):
    return git('rev-parse', revision + (':' + path if path else '^{tree}')).decode().strip()


def require(value, message):
    if not value:
        raise ValueError(message)


def changes(revision, kind):
    return set(git('diff', '--no-renames', '--name-only', '--diff-filter=' + kind, BASE, revision).decode().splitlines())


def candidate_objects(revision, paths):
    objects = {}
    for row in git('ls-tree', '-rz', revision, '--', *sorted(paths)).split(b'\0'):
        if row:
            metadata, name = row.split(b'\t', 1)
            objects[name.decode()] = metadata.decode()
    return objects


def verify_candidate_objects(expected, actual):
    require(expected == actual, 'Pinned candidate source, document or evidence object changed at HEAD')


def verify(contract):
    require(contract['proposalId'] == 'advanced-outline-toc-eleven-v1'
            and contract['status'] == 'PROPOSED_NOT_APPROVED' and contract['ownerApproval'] is None,
            'Proposal is not the exact inactive identity')
    for role, revision in [('baseline', BASE), ('candidate', CANDIDATE), ('testedProduct', PRODUCT)]:
        require(contract[role] == {'commit': revision, 'tree': tree(revision)}, 'Source identity changed')
    require(contract['activationPathsOnly'] == ACTIVATION, 'Activation boundary changed')
    require(contract['excludedBoundaries'] == ['R3', 'R4', 'R5', 'R6', 'R7'], 'Rights exclusions changed')
    protected = json.loads(blob(BASE, 'docs/open-source/evidence/goal62/preserved-files.json'))['sha256']
    require(changes(CANDIDATE, 'MDRT') == FILES == set(contract['files']) and FILES <= set(protected),
            'Protected scope changed')
    for path, pair in contract['files'].items():
        require(pair == {'previous': digest(blob(BASE, path)), 'current': digest(blob(CANDIDATE, path))},
                'Protected digest changed: ' + path)
    added = changes(CANDIDATE, 'A')
    require(set(contract['addedFiles']) == added, 'Added source/evidence inventory changed')
    for path, expected in contract['addedFiles'].items():
        require(digest(blob(CANDIDATE, path)) == expected, 'Added digest changed: ' + path)
    verify_candidate_objects(candidate_objects(CANDIDATE, FILES | added), candidate_objects('HEAD', FILES | added))
    for field, revision in [('previousRuntimeSourceTrees', BASE), ('runtimeSourceTrees', CANDIDATE)]:
        require(contract[field] == {p: tree(revision, p) for p in RUNTIME}, 'Runtime tree changed')
    require(contract['controlTrees'] == {'tests/native/outline-toc': tree(PRODUCT, 'tests/native/outline-toc')},
            'Control tree changed')
    require(tree(CANDIDATE, 'src/native') == tree(PRODUCT, 'src/native'), 'Product changed after testing')
    require(contract['unchangedTrees'] == {p: tree(BASE, p) for p in UNCHANGED}, 'Preserved tree scope changed')
    for path, expected in contract['unchangedTrees'].items():
        require(tree('HEAD', path) == expected, 'Workflow, web or accepted control changed: ' + path)
    evidence = {
        'LICENSE',
        'LICENSING.md',
        'docs/open-source/configuration/mit-scope.json',
        'docs/open-source/evidence/document-statistics/approved-refresh.json',
        'docs/open-source/evidence/document-statistics/candidate-refresh.json',
        'docs/open-source/evidence/document-statistics/owner-approval.json',
        'docs/open-source/evidence/find-caret/preserved-refresh.json',
        'docs/open-source/evidence/flowchart-provenance-81/approved-refresh.json',
        'docs/open-source/evidence/flowchart-provenance-81/candidate-refresh.json',
        'docs/open-source/evidence/flowchart-provenance-81/owner-approval.json',
        'docs/open-source/evidence/goal62/preserved-files.json',
        'docs/open-source/evidence/preview-loading-visibility/approved-refresh.json',
        'docs/open-source/evidence/preview-loading-visibility/candidate-refresh.json',
        'docs/open-source/evidence/preview-loading-visibility/owner-approval.json',
        'docs/open-source/evidence/preview-reflow-integration-v2/approved-refresh.json',
        'docs/open-source/evidence/preview-reflow-integration-v2/candidate-refresh.json',
        'docs/open-source/evidence/preview-reflow-integration-v2/owner-approval.json',
        'docs/open-source/evidence/selection-sync/preserved-refresh.json',
        'scripts/compliance/licence_scope.py',
        'scripts/compliance/test_governance.py',
    }
    require(set(contract['preservedEvidence']) == evidence, 'Prior approval scope changed')
    for path, expected in contract['preservedEvidence'].items():
        require(digest(blob(BASE, path)) == expected and blob('HEAD', path) == blob(BASE, path),
                'Prior approval or active governance changed: ' + path)
    require(changes('HEAD', 'MDRT') <= FILES | {LEDGER}, 'Unrelated protected bytes changed')
    require(changes('HEAD', 'A') <= added | PROPOSAL_FILES, 'Unrelated new source added')
    return len(protected), len(added)


def main():
    # Reuse the approved canonical-byte reader; do not activate or change its policy.
    helpers = runpy.run_path(str(ROOT / 'docs/open-source/evidence/preview-reflow-integration-v2/validate-proposal.py'))
    raw = helpers['read_verified_manifest'](ROOT, 'HEAD', MANIFEST, MANIFEST_SHA256)
    contract = json.loads(raw)
    protected, added = verify(contract)
    # Verify every unchanged and candidate object, as well as all preparation files,
    # independently in the index and path-specific Git-clean working tree.
    require(not git('diff', '--name-only', 'HEAD').strip(), 'Working/index changes remain')
    require(not git('ls-files', '--others', '--exclude-standard').strip(), 'Untracked source remains')
    names = sorted(set(git('ls-files').decode().splitlines()))
    expected = {}
    for row in git('ls-tree', '-rz', 'HEAD').split(b'\0'):
        if row:
            metadata, name = row.split(b'\t', 1)
            mode, kind, oid = metadata.decode().split()
            require(kind == 'blob' and mode in ('100644', '100755'), 'Unexpected source object')
            expected[name.decode()] = oid
    objects = git('-c', 'core.safecrlf=false', 'hash-object', '--stdin-paths',
                  data=('\n'.join(names) + '\n').encode()).decode().splitlines()
    require(expected == dict(zip(names, objects, strict=True)), 'Working clean objects changed')
    require(tree('HEAD', 'src/native') == tree(PRODUCT, 'src/native'), 'Current native source changed')
    require(tree('HEAD', 'tests/native/outline-toc') == tree(PRODUCT, 'tests/native/outline-toc'),
            'Current Outline/TOC controls changed')
    mutations = [
        ('extra protected path', lambda c: c['files'].update({'extra': {}})),
        ('changed previous digest', lambda c: c['files'][sorted(FILES)[0]].update(previous='0' * 64)),
        ('changed current digest', lambda c: c['files'][sorted(FILES)[0]].update(current='0' * 64)),
        ('missing added file', lambda c: c['addedFiles'].pop(next(iter(c['addedFiles'])))),
        ('changed native tree', lambda c: c['runtimeSourceTrees'].update({'src/native': '0' * 40})),
        ('changed control tree', lambda c: c['controlTrees'].update({'tests/native/outline-toc': '0' * 40})),
        ('invented approval', lambda c: c.update(ownerApproval={'approved': True})),
        ('expanded activation', lambda c: c['activationPathsOnly'].append('extra')),
        ('removed rights exclusion', lambda c: c['excludedBoundaries'].pop()),
    ]
    rejected = []
    for label, change in mutations:
        mutated = copy.deepcopy(contract); change(mutated)
        try:
            verify(mutated)
        except ValueError:
            rejected.append(label)
        else:
            raise ValueError('Mutation was accepted: ' + label)
    expected = candidate_objects(CANDIDATE, FILES | set(contract['addedFiles']))
    altered = dict(expected)
    altered['docs/native/validation-report.md'] = '100644 blob ' + '0' * 40
    try:
        verify_candidate_objects(expected, altered)
    except ValueError:
        rejected.append('changed pinned document object at HEAD')
    else:
        raise ValueError('Changed current document object was accepted')
    print(json.dumps({'status': 'PASS_INERT_ONLY', 'manifestSha256': MANIFEST_SHA256,
        'protectedSubstitutions': len(FILES), 'frozenProtectedPaths': protected,
        'addedCandidatePaths': added, 'rejectedMutations': rejected,
        'activeProvenance': 'UNAPPROVED; unchanged strict source identity must still fail'}, indent=2))


if __name__ == '__main__':
    main()
