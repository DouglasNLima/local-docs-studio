"""Validate the inactive successor proposal without changing active governance."""
import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
BASE = '20aedc2d2257ef52cfa02fe0ef328f3f1d570adf'
CANDIDATE = '498ff0c40f9ddbb7eb1cd89b3c091ad79b5cd34c'
TESTED = '6e9c70a81bb37ce789533c2438e3ca6438aa4f30'
PREFIX = 'docs/open-source/evidence/outline-toc-ownership-v2/'
PREDECESSOR = 'docs/open-source/evidence/outline-toc/'
MANIFEST_SHA256 = 'aa7bb08472517ae75e92e67451bec437972a8d896a022df5c52a2b3f40861af8'
MANIFEST_BYTES = 33664
PREPARATION_FILES = {PREFIX + p for p in (
    '.gitattributes', 'candidate-refresh.json', 'proposal.md', 'engineering-receipt.md',
    'validate-proposal.py', 'validation-results.json', 'independent-review.md')}
ACTIVATION_PATHS = ['scripts/compliance/licence_scope.py', 'scripts/compliance/test_governance.py',
    *(PREFIX + p for p in ('owner-approval.json', 'approved-refresh.json',
                         'activation-validation.json', 'activation-independent-review.md')),
    'docs/open-source/evidence/goal62/rights-ledger.csv']


def require(value, reason):
    if not value:
        raise ValueError(reason)


def git(*args, data=None):
    env = os.environ.copy()
    for key in ('GIT_DIR', 'GIT_WORK_TREE', 'GIT_INDEX_FILE', 'GIT_COMMON_DIR',
                'GIT_OBJECT_DIRECTORY', 'GIT_ALTERNATE_OBJECT_DIRECTORIES',
                'GIT_CONFIG_PARAMETERS', 'GIT_CONFIG_COUNT'):
        env.pop(key, None)
    return subprocess.check_output(['git', '--no-optional-locks', '-c', 'core.fsmonitor=false',
                                    *args], cwd=ROOT, env=env, input=data)


def blob(revision, path):
    return git('cat-file', 'blob', revision + ':' + path)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def tree(revision, path=None):
    return git('rev-parse', revision + (':' + path if path else '^{tree}')).decode().strip()


def unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate JSON key: ' + key)
        result[key] = value
    return result


def objects(revision):
    result = {}
    for row in git('ls-tree', '-rz', revision).split(b'\0'):
        if row:
            metadata, name = row.split(b'\t', 1)
            result[name.decode()] = metadata.decode().split()
    return result


def verify(contract):
    require(contract['schemaVersion'] == 1 and
            contract['proposalId'] == 'advanced-outline-toc-ownership-v2' and
            contract['status'] == 'PROPOSED_NOT_APPROVED' and contract['ownerApproval'] is None and
            contract['v2State'] == 'INACTIVE / BLOCKED / UNSEALED', 'Inactive proposal identity changed')
    require(contract['repository'] == 'DouglasNLima/local-docs-studio', 'Repository changed')
    for role, revision in (('baseline', BASE), ('candidate', CANDIDATE)):
        require(contract[role] == {'commit': revision, 'tree': tree(revision)}, 'Pinned source changed')
    require(contract['testedProduct']['commit'] == TESTED and
            contract['testedProduct']['tree'] == tree(TESTED), 'Tested source changed')
    require(contract['acceptedDevelop'] == {
        'commit': '50bc2354e49662c9831338310d23c91cfe7a7b8a',
        'tree': '7685cd6939275d3bef442b3ff1df579616a348aa'}, 'Accepted baseline changed')
    require(contract['activationPathsOnly'] == ACTIVATION_PATHS, 'Activation scope changed')
    require(contract['excludedBoundaries'] == ['R3', 'R4', 'R5', 'R6', 'R7'] and
            contract['rightsGrantChangesAuthorised'] is False and
            contract['historicalFailuresMayBeRelabelled'] is False, 'Rights or evidence boundary changed')
    require(contract['publicRelease'] == contract['nativeStable'] == 'NO-GO', 'Release status changed')
    old_raw = blob(BASE, PREDECESSOR + 'candidate-refresh.json')
    old = json.loads(old_raw, object_pairs_hook=unique)
    protected = json.loads(blob(BASE, 'docs/open-source/evidence/goal62/preserved-files.json'))['sha256']
    granted = set(json.loads(blob(BASE, 'docs/open-source/configuration/mit-scope.json'))['paths'])
    require(contract['protectedPathCount'] == len(protected) == 2421 and
            contract['mitAllowlistPathCount'] == len(granted) == 1081, 'Protected or granted inventory changed')
    require(contract['predecessor'] == {
        'proposalId': old['proposalId'], 'manifestSha256': sha(old_raw), 'manifestBytes': len(old_raw),
        'approvedSubstitutions': old['files'], 'approvedAddedFilePins': old['addedFiles'],
        'requireImmutableApprovalAndReplay': True}, 'Immutable predecessor evidence changed')
    require(len(old['files']) == 11 and len(old['addedFiles']) == 88, 'Predecessor counts changed')
    expected_base = objects(BASE)
    expected_candidate = objects(CANDIDATE)
    require(set(expected_base) <= set(expected_candidate), 'Deletion from source candidate')
    modified = {p for p in expected_base if expected_base[p] != expected_candidate[p]}
    added = set(expected_candidate) - set(expected_base)
    require(set(contract['files']) == modified & set(protected) and len(contract['files']) == 7,
            'Protected substitution scope changed')
    require(set(contract['amendedPredecessorAddedFiles']) == modified - set(protected) and
            len(contract['amendedPredecessorAddedFiles']) == 3, 'Amended predecessor scope changed')
    require(set(contract['addedFiles']) == added and len(added) == 12, 'Added source scope changed')
    for field, old_field in (('files', 'files'), ('amendedPredecessorAddedFiles', 'addedFiles')):
        for path, pair in contract[field].items():
            require(pair == {'previous': sha(blob(BASE, path)), 'current': sha(blob(CANDIDATE, path))},
                    'Source digest changed: ' + path)
            previous = old[old_field][path]
            if isinstance(previous, dict):
                previous = previous['current']
            require(previous == pair['previous'], 'Predecessor amendment is not continuous: ' + path)
    for path, pin in contract['addedFiles'].items():
        mode, kind, oid = expected_candidate[path]
        require(kind == 'blob' and mode == '100644' and pin == {
            'sha256': sha(blob(CANDIDATE, path)), 'gitBlob': oid, 'mode': mode},
            'Added file identity or mode changed: ' + path)
    require(contract['previousRuntimeSourceTrees'] == old['runtimeSourceTrees'], 'Previous runtime pins changed')
    for field, paths in (
        ('runtimeSourceTrees', ('src/native', 'src/native/LensDocsStudio.Native',
                               'src/native/LensDocsStudio.Core', 'src/native/LensDocsStudio.Native.Validation')),
        ('controlTrees', ('tests/native/outline-toc', 'tests/native/outline-ownership'))):
        require(contract[field] == {p: tree(CANDIDATE, p) for p in paths}, 'Runtime/control scope changed')
    for path in ('src/native', 'tests/native/outline-toc'):
        require(tree(CANDIDATE, path) == tree(TESTED, path), 'Qualified executable tree changed: ' + path)
    preserve = set(old['preservedEvidence']) | {'LICENSE', 'LICENSING.md',
        'docs/open-source/configuration/mit-scope.json', 'docs/open-source/evidence/goal62/preserved-files.json',
        'docs/open-source/evidence/goal62/rights-ledger.csv'} | {
        PREDECESSOR + p for p in ('candidate-refresh.json', 'owner-approval.json', 'approved-refresh.json')}
    require(set(contract['preservedEvidence']) == preserve, 'Preservation scope changed')
    for path, pin in contract['preservedEvidence'].items():
        require(pin == sha(blob(BASE, path)) == sha(blob(CANDIDATE, path)), 'Prior evidence changed: ' + path)
    unchanged = set(old['unchangedTrees']) | {'.github', 'assets', 'src/windows',
        'src/native/LensDocsStudio.Native/Rendering', 'src/native/LensDocsStudio.Core'}
    require(contract['unchangedTrees'] == {p: tree(BASE, p) for p in unchanged}, 'Preserved tree scope changed')
    for path, oid in contract['unchangedTrees'].items():
        require(tree(CANDIDATE, path) == oid, 'Preserved tree changed: ' + path)
    sys.path.insert(0, str(ROOT / 'scripts/compliance'))
    from rights_ledger import boundary
    expected_rights = {p: dict(zip(('boundary', 'existingGrantStatus', 'reason'), boundary(p, granted)))
                      for p in modified | added}
    require(contract['rightsReview'] == expected_rights, 'Rights classification or grant status changed')
    return {'protectedPaths': len(protected), 'mitAllowlist': len(granted),
            'protectedSubstitutions': 7, 'amendedV1AddedFiles': 3, 'newSourceAndEvidenceFiles': len(added),
            'preservedV1Substitutions': 11, 'preservedV1AddedPins': 88}


def verify_current():
    candidate = objects(CANDIDATE)
    head = objects('HEAD')
    require(set(candidate) <= set(head) and all(head[p] == pin for p, pin in candidate.items()),
            'Committed source no longer matches the candidate')
    require(set(head) - set(candidate) <= PREPARATION_FILES, 'Unrelated committed preparation file')
    require(not git('diff', '--cached', '--name-only', '--no-renames', 'HEAD').strip(),
            'Staged index identities differ from HEAD')
    require(not git('diff', '--name-only', 'HEAD').strip(), 'Tracked working/index modifications remain')
    untracked = set(git('ls-files', '--others', '--exclude-standard').decode().splitlines())
    require(untracked <= PREPARATION_FILES, 'Unrelated untracked source')
    names = sorted(head)
    actual = git('-c', 'core.safecrlf=false', 'hash-object', '--stdin-paths',
                 data=('\n'.join(names) + '\n').encode()).decode().splitlines()
    require({p: pin[2] for p, pin in head.items()} == dict(zip(names, actual, strict=True)),
            'Git-clean working identities differ from committed objects')
    for path in PREPARATION_FILES:
        if (ROOT / path).exists():
            require(not (ROOT / path).is_symlink(), 'Unexpected preparation symlink')
    return {'head': git('rev-parse', 'HEAD').decode().strip(), 'tree': tree('HEAD'),
            'checkedCommittedFiles': len(head), 'untrackedPreparationFiles': sorted(untracked)}


def main():
    raw = (HERE / 'candidate-refresh.json').read_bytes()
    require(len(raw) == MANIFEST_BYTES and sha(raw) == MANIFEST_SHA256, 'Exact manifest bytes changed')
    contract = json.loads(raw, object_pairs_hook=unique)
    current = verify_current()
    summary = verify(contract)
    first = next(iter(contract['files']))
    mutations = [
        ('invented owner approval', lambda c: c.update(ownerApproval={'approved': True})),
        ('expanded activation scope', lambda c: c['activationPathsOnly'].append('extra')),
        ('removed excluded rights boundary', lambda c: c['excludedBoundaries'].pop()),
        ('new rights grant', lambda c: c.update(rightsGrantChangesAuthorised=True)),
        ('altered predecessor substitution', lambda c: c['predecessor']['approvedSubstitutions'][first].update(current='0' * 64)),
        ('removed predecessor added-file pin', lambda c: c['predecessor']['approvedAddedFilePins'].pop(next(iter(c['predecessor']['approvedAddedFilePins'])))),
        ('changed previous source', lambda c: c['files'][first].update(previous='0' * 64)),
        ('changed current source', lambda c: c['files'][first].update(current='0' * 64)),
        ('missing new source file', lambda c: c['addedFiles'].pop(next(iter(c['addedFiles'])))),
        ('changed validation tree', lambda c: c['runtimeSourceTrees'].update({'src/native/LensDocsStudio.Native.Validation': '0' * 40})),
        ('removed preserved evidence', lambda c: c['preservedEvidence'].pop(next(iter(c['preservedEvidence'])))),
        ('changed grant classification', lambda c: c['rightsReview'][first].update(existingGrantStatus='invented')),
        ('historical failure relabelled', lambda c: c.update(historicalFailuresMayBeRelabelled=True)),
        ('release status changed', lambda c: c.update(publicRelease='GO')),
    ]
    rejected = []
    for name, change in mutations:
        altered = copy.deepcopy(contract)
        change(altered)
        try:
            verify(altered)
        except ValueError:
            rejected.append(name)
        else:
            raise ValueError('Negative control accepted: ' + name)
    print(json.dumps({'status': 'PASS_INACTIVE_PROPOSAL_ONLY', 'manifestSha256': MANIFEST_SHA256,
        'manifestBytes': MANIFEST_BYTES, **summary, 'currentSourceCheck': current,
        'rejectedMutations': rejected, 'provenanceActivation': False,
        'mandatoryDevelopQualification': 'NOT SATISFIED BY THIS VALIDATION'}, indent=2))


if __name__ == '__main__':
    main()
