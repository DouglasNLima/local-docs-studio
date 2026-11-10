"""Verify the inactive statistics proposal; never alter the active governance rules."""
import copy
import hashlib
import json
from pathlib import Path
import runpy
import subprocess

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
BASE = '2dbc6d4e61db431001b42bb93b02d8e503eb8096'
CANDIDATE = 'ed521c2bd7b30060580d3fee9967a5e307e0e89b'
PRODUCT = '5b336179b5cbbb97d03880c0cf9674a2decaa000'
MANIFEST = 'docs/open-source/evidence/document-statistics/candidate-refresh.json'
MANIFEST_SHA256 = '7dd1bc0e1a4c1ccf798495cba950a422af06729ee3876d86a04e028f6141ec22'
PROPOSAL_PREFIX = 'docs/open-source/evidence/document-statistics/'
LEDGER = 'docs/open-source/evidence/goal62/rights-ledger.csv'
FILES = {
    'docs/native/' + name for name in ['architecture-decisions.md', 'functional-parity-matrix.md',
        'implementation-progress.md', 'native-experience-improvements.md', 'validation-report.md']
} | {
    'src/native/LensDocsStudio.Core/Review/DocumentReviewService.cs',
    *('src/native/LensDocsStudio.Native/' + name for name in ['DocumentReviewWindow.xaml.cs',
        'MainWindow.Review.cs', 'MainWindow.xaml', 'MainWindow.xaml.cs'])
}
RUNTIME = ('src/native', 'tests/fixtures/native-flowchart')
UNCHANGED = ('.github', 'assets', 'tests/native/preview-reflow',
             'tests/native/preview-loading-presentation', 'tests/fixtures/native-preview-reflow')
PROPOSAL_FILES = {PROPOSAL_PREFIX + name for name in ['candidate-refresh.json', 'proposal.md',
    'validate-proposal.py', 'independent-review.md', 'validation-results.json',
    'foundation.txt', 'governance.txt', 'governance-tests.txt', 'initial-colour-failure.txt',
    'initial-export-fixture-failure.txt', 'initial-timing-failure.txt', 'initial-focus-failure.txt']}
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
    return set(git('diff', '--name-only', '--diff-filter=' + kind, BASE, revision).decode().splitlines())


def verify(contract):
    require(contract['proposalId'] == 'document-statistics-ten-v1'
            and contract['status'] == 'PROPOSED_NOT_APPROVED' and contract['ownerApproval'] is None,
            'Proposal is not the exact inactive identity')
    for role, revision in [('baseline', BASE), ('candidate', CANDIDATE), ('testedProduct', PRODUCT)]:
        require(contract[role] == {'commit': revision, 'tree': tree(revision)}, 'Source identity changed')
    require(contract['activationPathsOnly'] == ACTIVATION, 'Activation boundary changed')
    require(contract['excludedBoundaries'] == ['R3', 'R4', 'R5', 'R6', 'R7'], 'Rights exclusions changed')
    protected = json.loads(blob(BASE, 'docs/open-source/evidence/goal62/preserved-files.json'))['sha256']
    require(changes(CANDIDATE, 'MDR') == FILES == set(contract['files']) and FILES <= set(protected),
            'Protected scope changed')
    for path, pair in contract['files'].items():
        require(pair == {'previous': digest(blob(BASE, path)), 'current': digest(blob(CANDIDATE, path))},
                'Protected digest changed: ' + path)
    added = changes(CANDIDATE, 'A')
    require(set(contract['addedFiles']) == added, 'Added source/evidence inventory changed')
    for path, expected in contract['addedFiles'].items():
        require(digest(blob(CANDIDATE, path)) == expected, 'Added digest changed: ' + path)
    for field, revision in [('previousRuntimeSourceTrees', BASE), ('runtimeSourceTrees', CANDIDATE)]:
        require(contract[field] == {p: tree(revision, p) for p in RUNTIME}, 'Runtime tree changed')
    require(contract['controlTrees'] == {'tests/native/document-statistics': tree(PRODUCT, 'tests/native/document-statistics')},
            'Control tree changed')
    require(tree(CANDIDATE, 'src/native') == tree(PRODUCT, 'src/native'), 'Product changed after testing')
    require(contract['unchangedTrees'] == {p: tree(BASE, p) for p in UNCHANGED}, 'Preserved tree scope changed')
    for path, expected in contract['unchangedTrees'].items():
        require(tree('HEAD', path) == expected, 'Workflow, web or accepted control changed: ' + path)
    evidence = {'docs/open-source/configuration/mit-scope.json',
                'docs/open-source/evidence/goal62/preserved-files.json',
                'scripts/compliance/licence_scope.py', 'scripts/compliance/test_governance.py'}
    for parent in ['flowchart-provenance-81', 'preview-loading-visibility', 'preview-reflow-integration-v2']:
        evidence.update('docs/open-source/evidence/' + parent + '/' + name
                        for name in ['candidate-refresh.json', 'approved-refresh.json', 'owner-approval.json'])
    require(set(contract['preservedEvidence']) == evidence, 'Prior approval scope changed')
    for path, expected in contract['preservedEvidence'].items():
        require(digest(blob(BASE, path)) == expected and blob('HEAD', path) == blob(BASE, path),
                'Prior approval or active governance changed: ' + path)
    require(changes('HEAD', 'MDR') <= FILES | {LEDGER}, 'Unrelated protected bytes changed')
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
    require(tree('HEAD', 'tests/native/document-statistics') == tree(PRODUCT, 'tests/native/document-statistics'),
            'Current statistics controls changed')
    mutations = [
        ('extra protected path', lambda c: c['files'].update({'extra': {}})),
        ('changed previous digest', lambda c: c['files'][sorted(FILES)[0]].update(previous='0' * 64)),
        ('changed current digest', lambda c: c['files'][sorted(FILES)[0]].update(current='0' * 64)),
        ('missing added file', lambda c: c['addedFiles'].pop(next(iter(c['addedFiles'])))),
        ('changed native tree', lambda c: c['runtimeSourceTrees'].update({'src/native': '0' * 40})),
        ('changed control tree', lambda c: c['controlTrees'].update({'tests/native/document-statistics': '0' * 40})),
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
    print(json.dumps({'status': 'PASS_INERT_ONLY', 'manifestSha256': MANIFEST_SHA256,
        'protectedSubstitutions': len(FILES), 'frozenProtectedPaths': protected,
        'addedCandidatePaths': added, 'rejectedMutations': rejected,
        'activeProvenance': 'UNAPPROVED; unchanged strict source identity must still fail'}, indent=2))


if __name__ == '__main__':
    main()
