"""Offline read-only review of an inert nine-file proposal, never an activation tool.

Run with python -B. Only Git object/file reads and in-memory negative controls
are used; stdout is the evidence output. No clean-filter object writes, network,
native execution, ledger generation or production validator replacement occurs.
"""
import ast
import copy
import csv
import hashlib
import importlib
import io
import json
from pathlib import Path
import subprocess
import sys
import unittest

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
PREFIX = 'docs/open-source/evidence/flowchart-provenance-81/'
LEDGER = 'docs/open-source/evidence/goal62/rights-ledger.csv'
PREPARATION = {
    PREFIX + name for name in (
        'candidate-refresh.json', 'source-observations.json', 'proposal.md',
        'validate-proposal.py', 'validation-results.json', 'independent-review.md')
} | {LEDGER}
IDENTITIES = {
    'baseline': {'commit': '99349d82eb06960eb1f9785cf384db2373aed4fa',
                 'tree': 'e4d137a12cbdd827583fcb841612890dd88736ec'},
    'candidate': {'commit': 'b29dbb862bd6fe2eebc26deba25bab072857048f',
                  'tree': '53aa0f0c18455ca61cb2bc7b4abf111124de50dc'},
    'evaluation': {'commit': '3a8f10d737791a1182d092676ecb17a47e2fde30',
                   'tree': '1f13a1afa64fd0b4419303bcaf0136f1ffcecfe7'},
    'originalEvidence': {'commit': '0890ec9b2ade8af24b9b3fca6cbe98cd7fd329ba',
                         'tree': 'fd0e968cd8fb3ece8ebb73d78a859705e1605fe3'},
}
PAIRS = {
    'src/native/LensDocsStudio.Core.Tests/LensDocsStudio.Core.Tests.csproj': (
        '5744f4a8b7e8784519a74b362d3a7f7f5a1d657b2c295b6c3d4d2b66ff585316',
        'bcc5936898b464e3da8116937349c04a411755db2d78e4376d3d763017c275b4'),
    'src/native/LensDocsStudio.Core/Arrangement/ArrangementEngine.cs': (
        '8e12c1260ad97b08b509b1210dafe0124c2b117701f5d2a160b3a59f356af859',
        'ae71b419863ebf55de83e48984106023c583cf76520edd77d12d93d966120d77'),
    'src/native/LensDocsStudio.Core/Mermaid/MermaidOperations.cs': (
        'bc11d28656c15935077a20ff0d981fd4aaafaaae099b805fa87f03bfe6c31245',
        'fde06abc85a0f3a68e33064d55c6df3f4fd9355eca2ef9f02ee5b8f27f894f70'),
    'src/native/LensDocsStudio.Core/Mermaid/MermaidSemanticValidation.cs': (
        'd2860c996f00e0a6a145ed1e006069b1118aab033c75d249ab13fc384e49a6a5',
        '311c66b3e3136767838aab0eeadc40bedd8ba35d4a368b01444da2ddc00cdeca'),
    'src/native/LensDocsStudio.Core/Mermaid/MermaidSourceParser.cs': (
        'ca956434be85b52c1ec5c66d3bcded2b4145fc4517de72f0c77fac18464dce8c',
        '7e8304ebb33911ceb58ec63401d3f17cc67b11ef26d912abca8b6e07a20af5d3'),
    'src/native/LensDocsStudio.Core/Rendering/DiagramService.cs': (
        'c8a3b7b97bf0fc91e3aa7adfbce99646709380083fd53015eea9742192df4d0d',
        '158554c6f6d661e9563c8515f91a6985234da59aec123eeaf06246ae60961275'),
    'src/native/LensDocsStudio.Core/SourceAuthoring/SourceText.cs': (
        '33615f2d44fc1faecbbdf5f72e506769073bc9cbbd95a437f7c291a8b50fd738',
        '7c99911849b28f54be0b0615b4c91374a0b791d86e897a40fa177e6d0cad888c'),
    'src/native/LensDocsStudio.Native.Validation/Program.cs': (
        '3f7abecc36518604a7e644cc122d98fac8ef772d9e4e4ecbb574fc56977c1219',
        'e2e1977ff193a343de5dfe4f3ed1c8fc8f44621b9345c1957fa6726b39d2f2ca'),
    'src/native/LensDocsStudio.Native/Rendering/NativeMarkdownRenderer.cs': (
        '60ab0564756fc1a336656d437eea5ae16705db2620c3913dc2c797afe7c9c649',
        'a71d05d445699f1ed8adf8fa385c886b55613a74bd822af86ea98fbd20f3083c'),
}
CONTRACT = {
    'schemaVersion': 1, 'proposalId': 'pr81-flowchart-provenance-nine-v1',
    'status': 'PROPOSED_NOT_APPROVED', 'ownerApproval': None,
    'repository': 'DouglasNLima/local-docs-studio', **IDENTITIES,
    'scope': 'Protected-byte provenance only; no licence grant, policy activation or execution acceptance',
    'excludedBoundaries': ['R3', 'R4', 'R5', 'R6', 'R7'],
    'files': {path: {'previous': pair[0], 'current': pair[1]} for path, pair in PAIRS.items()},
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def git(*args, data=None):
    return subprocess.check_output(['git', *args], input=data, cwd=ROOT)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def proposal_digest():
    path = PREFIX + 'candidate-refresh.json'
    canonical = (ROOT / path).read_bytes().replace(b'\r\n', b'\n')
    require(git('hash-object', '--no-filters', '--stdin', data=canonical).strip() ==
            git('hash-object', '--', path).strip(), 'Unexpected proposal clean-filter behaviour')
    return sha(canonical)


def blob(revision, path):
    return git('cat-file', 'blob', revision + ':' + path)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate JSON key: ' + key)
        result[key] = value
    return result


def read_json(data):
    return json.loads(data, object_pairs_hook=unique_object)


def tree(revision):
    result = {}
    for row in git('ls-tree', '-rz', '--full-tree', revision).split(b'\0'):
        if row:
            meta, path = row.split(b'\t', 1)
            mode, kind, oid = meta.decode().split()
            require(kind == 'blob', 'Unexpected non-blob tree entry')
            result[path.decode()] = (mode, oid)
    return result


def digests(revision, names):
    result = {}
    with subprocess.Popen(['git', 'cat-file', '--batch'], cwd=ROOT,
                          stdin=subprocess.PIPE, stdout=subprocess.PIPE) as process:
        for path in sorted(names):
            process.stdin.write((revision + ':' + path + '\n').encode())
            process.stdin.flush()
            header = process.stdout.readline().split()
            require(len(header) == 3 and header[1] == b'blob', 'Missing blob: ' + path)
            content = process.stdout.read(int(header[2]))
            require(len(content) == int(header[2]) and process.stdout.read(1) == b'\n',
                    'Incomplete Git object read: ' + path)
            result[path] = sha(content)
        process.stdin.close()
        require(process.wait() == 0, 'Git object reader failed')
    return result


def verify_unchanged_preparation():
    candidate = IDENTITIES['candidate']['commit']
    git('merge-base', '--is-ancestor', candidate, 'HEAD')
    original = tree(candidate)
    head = tree('HEAD')
    changed = {p for p in original.keys() | head.keys() if original.get(p) != head.get(p)}
    changed |= set(git('diff', '--name-only', candidate, '--').decode().splitlines())
    changed |= set(git('diff', '--cached', '--name-only', candidate, '--').decode().splitlines())
    changed |= set(git('ls-files', '--others', '--exclude-standard', '-z').decode().split('\0')) - {''}
    require(changed <= PREPARATION, 'Preparation changed an unauthorised path: ' + str(sorted(changed - PREPARATION)))
    names = sorted(set(original) - {LEDGER})
    # No -w: compare filtered Git OIDs, without writing the object database.
    current = git('-c', 'core.safecrlf=false', 'hash-object', '--stdin-paths',
                  data=('\n'.join(names) + '\n').encode()).decode().splitlines()
    require(len(current) == len(names), 'Missing working-tree object identities')
    for path, oid in zip(names, current, strict=True):
        require(oid == original[path][1], 'Existing source/evidence changed: ' + path)
    return original, len(names)


def effective_pins(licence):
    candidate = IDENTITIES['candidate']['commit']
    frozen = read_json(blob(candidate, 'docs/open-source/evidence/goal62/preserved-files.json'))
    preserved = dict(frozen['sha256'])
    source = ast.parse(blob(candidate, 'scripts/compliance/licence_scope.py').decode())
    policies = [ast.literal_eval(node.value) for node in ast.walk(source)
                if isinstance(node, ast.Assign)
                and any(isinstance(t, ast.Name) and t.id == 'policy_refresh' for t in node.targets)]
    require(len(policies) == 1, 'Historical policy refresh is ambiguous')
    for path, (previous, current) in policies[0].items():
        require(preserved[path] == previous, 'Historical policy evidence changed: ' + path)
        preserved[path] = current
    for name, apply in [('find-caret', licence.apply_find_caret_refresh),
                        ('selection-sync', licence.apply_selection_sync_refresh)]:
        preserved = apply(preserved, read_json(blob(candidate,
                          'docs/open-source/evidence/' + name + '/preserved-refresh.json')))
    return frozen, preserved


def validate_proposal(document, preserved, actual, granted, licence):
    require(type(document.get('schemaVersion')) is int, 'Invalid schema version type')
    require(set(document) == set(CONTRACT), 'Unexpected proposal fields')
    require({k: v for k, v in document.items() if k != 'files'} ==
            {k: v for k, v in CONTRACT.items() if k != 'files'},
            'Proposal identity, approval state or exclusions changed')
    require(document['files'] == CONTRACT['files'], 'Exact nine previous/current pairs required')
    require(set(document['files']) <= granted, 'Unapproved path cannot receive an amendment')
    before = dict(preserved)
    proposed = dict(preserved)
    for path, entry in document['files'].items():
        require(preserved.get(path) == entry['previous'], 'Previous effective pin mismatch: ' + path)
        require(actual.get(path) == entry['current'], 'Candidate bytes mismatch: ' + path)
        proposed[path] = entry['current']
    # This pure existing predicate runs against a disposable map only.
    licence.validate_preserved(proposed, actual)
    require(preserved == before, 'Input protected pins mutated')
    require({p for p in proposed if proposed[p] != preserved[p]} == set(PAIRS),
            'Simulation changed more or fewer than nine pins')
    return proposed


def negative_controls(document, preserved, actual, granted, licence):
    passed = []
    def reject(label, candidate=None, pins=None, content=None, scope=None):
        try:
            validate_proposal(document if candidate is None else candidate,
                              preserved if pins is None else pins,
                              actual if content is None else content,
                              granted if scope is None else scope, licence)
        except ValueError:
            passed.append(label)
        else:
            raise ValueError('Negative control accepted: ' + label)
    first = next(iter(PAIRS))
    for label, change in [
        ('missing_path', lambda d: d['files'].pop(first)),
        ('extra_path', lambda d: d['files'].update({'LICENSE': d['files'][first]})),
        ('mismatched_previous_hash', lambda d: d['files'][first].update(previous='0' * 64)),
        ('well_formed_but_unreviewed_current_hash', lambda d: d['files'][first].update(current='0' * 64)),
        ('malformed_current_hash', lambda d: d['files'][first].update(current='not-a-digest')),
        ('wrong_base_commit', lambda d: d['baseline'].update(commit='0' * 40)),
        ('wrong_base_tree', lambda d: d['baseline'].update(tree='0' * 40)),
        ('wrong_candidate_commit', lambda d: d['candidate'].update(commit='0' * 40)),
        ('wrong_candidate_tree', lambda d: d['candidate'].update(tree='0' * 40)),
        ('wrong_evaluation_commit', lambda d: d['evaluation'].update(commit='0' * 40)),
        ('rewritten_goal62_identity', lambda d: d['originalEvidence'].update(tree='0' * 40)),
        ('removed_exclusion', lambda d: d['excludedBoundaries'].pop()),
        ('unapproved_activation_status', lambda d: d.update(status='APPROVED')),
        ('self_asserted_owner_approval', lambda d: d.update(ownerApproval='https://example.invalid/approval')),
        ('extra_override_field', lambda d: d.update(allowUnapproved=True)),
    ]:
        changed = copy.deepcopy(document)
        change(changed)
        reject(label, candidate=changed)
    for path in PAIRS:
        reject('unapproved_grant_path:' + path, scope=granted - {path})
    changed = dict(preserved)
    changed[first] = '0' * 64
    reject('changed_effective_previous_pin', pins=changed)
    changed = dict(actual)
    changed[first] = '0' * 64
    reject('actual_candidate_byte_mismatch', content=changed)
    outside = next(p for p in preserved if p not in PAIRS)
    changed = dict(actual)
    changed[outside] = '0' * 64
    reject('outside_scope_protected_byte_mismatch', content=changed)
    for boundary, path in [
        ('R3', 'tests/fixtures/native-flowchart/spc-application-architecture.md'),
        ('R4', 'src/native/LensDocsStudio.Native/Resources/CommandIcons.xaml'),
        ('R5', 'docs/open-source/evidence/goal62/preserved-files.json'),
        ('R6', 'src/native/LensDocsStudio.Core/Rendering/BoundedErLayout.cs'),
        ('R7', 'user-content/private.md'),
        ('trust', 'SECURITY.md'),
        ('performance', '.github/workflows/native-continuous-performance.yml'),
    ]:
        changed = copy.deepcopy(document)
        changed['files'][path] = changed['files'].pop(first)
        reject('unapproved_scope_substitution:' + boundary, candidate=changed)
    try:
        read_json('{"files": {}, "files": {}}')
    except ValueError:
        passed.append('duplicate_json_key')
    else:
        raise ValueError('Duplicate JSON key accepted')
    return passed


def check_staged_ledger(rights):
    # Optional after staging the preparation and regenerating its current ledger.
    # Reading the index avoids clean-filter writes even for new evidence files.
    paths = set(git('ls-files', '-z', '--cached', '--others', '--exclude-standard').decode().split('\0')) - {''}
    rows = list(csv.DictReader(io.StringIO((ROOT / LEDGER).read_text(encoding='utf-8'))))
    require(len(rows) == len({r['path'] for r in rows}) and {r['path'] for r in rows} == paths,
            'Current ledger coverage mismatch')
    original = list(csv.DictReader(io.StringIO(blob(IDENTITIES['candidate']['commit'], LEDGER).decode())))
    by_path = {r['path']: r for r in rows}
    for row in original:
        require(by_path[row['path']] == row, 'Existing ledger row changed: ' + row['path'])
    index_hashes = digests('', paths - {LEDGER})
    for row in rows:
        expected = 'BOUND_BY_FINAL_GIT_TREE' if row['path'] == LEDGER else index_hashes[row['path']]
        require(row['sha256'] == expected, 'Staged ledger digest mismatch: ' + row['path'])
        require((row['decisionId'], row['ownerDecision'], row['reason']) == rights.boundary(row['path']),
                'Ledger classification changed: ' + row['path'])
    return len(rows)


def read_only_governance_controls():
    module = importlib.import_module('test_governance')
    omitted = {
        'test_native_payload_contains_exact_licence_and_scoped_disclosure',
        'test_native_payload_refuses_missing_scope_before_copying',
        'test_package_licence_contract_rejects_removed_or_tampered_notices',
        'test_missing_source_cannot_masquerade_as_pre_adoption',
    }
    suite = unittest.TestSuite()
    for cls in (module.GovernanceBoundaryTests, module.ScopedLicenceTests):
        for name in unittest.defaultTestLoader.getTestCaseNames(cls):
            if name not in omitted:
                suite.addTest(cls(name))
    output = io.StringIO()
    result = unittest.TextTestRunner(stream=output).run(suite)
    require(result.wasSuccessful() and result.testsRun == 20, output.getvalue())
    return {'passed': result.testsRun, 'skipped': len(result.skipped),
            'omittedPackageWritingTests': sorted(omitted),
            'omissionReason': 'Evidence-only preparation requires offline read-only validation.'}


def check_local_evidence():
    observed = read_json((HERE / 'source-observations.json').read_bytes())
    for entry in observed['retainedExecutionEvidence']:
        path = (ROOT / entry['path']).resolve()
        require(path.is_relative_to(ROOT / 'artifacts'), 'Evidence path outside local artefacts')
        require(sha(path.read_bytes()) == entry['sha256'], 'Retained execution evidence changed: ' + entry['path'])
    return len(observed['retainedExecutionEvidence'])


def main():
    original, unchanged_count = verify_unchanged_preparation()
    # Imports occur only after the existing production modules match the pinned tree.
    sys.path.insert(0, str(ROOT / 'scripts/compliance'))
    licence = importlib.import_module('licence_scope')
    rights = importlib.import_module('rights_ledger')
    document = read_json((HERE / 'candidate-refresh.json').read_bytes())
    for name, identity in IDENTITIES.items():
        require(git('rev-parse', identity['commit'] + '^{tree}').decode().strip() == identity['tree'],
                'Git identity mismatch: ' + name)
    frozen, preserved = effective_pins(licence)
    baseline = digests(IDENTITIES['baseline']['commit'], preserved)
    actual = digests(IDENTITIES['candidate']['commit'], preserved)
    licence.validate_preserved(preserved, baseline)
    conflicts = {p for p in preserved if preserved[p] != actual[p]}
    require(conflicts == set(PAIRS), 'Conflicts differ from the exact nine reviewed paths')
    scope = read_json(blob(IDENTITIES['candidate']['commit'], 'docs/open-source/configuration/mit-scope.json'))
    granted = licence.validate_scope(scope, set(original))
    for path in PAIRS:
        require(rights.boundary(path, granted)[:2] == ('R1', 'MIT_ORIGINAL_PORTIONS_ONLY'),
                'Unexpected ownership classification: ' + path)
    before = dict(preserved)
    try:
        licence.validate_preserved(preserved, actual)
    except ValueError as error:
        retained_failure = str(error)
    else:
        raise ValueError('Original protected-byte failure was lost')
    rows = []
    for path, (previous, current) in PAIRS.items():
        base_bytes = blob(IDENTITIES['baseline']['commit'], path)
        candidate_bytes = blob(IDENTITIES['candidate']['commit'], path)
        evaluation_bytes = blob(IDENTITIES['evaluation']['commit'], path)
        require(base_bytes != candidate_bytes and candidate_bytes == evaluation_bytes,
                'Byte comparison failed: ' + path)
        rows.append({
            'path': path, 'previousEffectiveSha256': previous, 'candidateSha256': current,
            'originalGoal62Sha256': frozen['sha256'][path],
            'baseBlob': git('rev-parse', IDENTITIES['baseline']['commit'] + ':' + path).decode().strip(),
            'candidateBlob': original[path][1],
            'baseByteLength': len(base_bytes), 'candidateByteLength': len(candidate_bytes),
            'differentFromDevelop': True, 'byteIdenticalToValidatedEvaluation': True,
            'decisionId': 'R1', 'ownerDecision': 'MIT_ORIGINAL_PORTIONS_ONLY',
        })
    validate_proposal(document, preserved, actual, granted, licence)
    controls = negative_controls(document, preserved, actual, granted, licence)
    require(preserved == before, 'Negative controls mutated effective pins')
    evidence_paths = [
        'docs/open-source/evidence/goal62/preserved-files.json',
        'docs/open-source/configuration/mit-scope.json', 'scripts/compliance/licence_scope.py',
        'docs/open-source/evidence/find-caret/preserved-refresh.json',
        'docs/open-source/evidence/selection-sync/preserved-refresh.json',
        '.github/workflows/native-continuous-performance.yml',
    ]
    record = {
        'proposalValidation': 'PASS; preparation only', 'amendmentActive': False,
        'ownerApproval': None, 'proposalCanonicalSha256': proposal_digest(),
        'identities': IDENTITIES, 'protectedFileCount': len(preserved),
        'baselineProtectedMismatches': 0, 'candidateProtectedMismatches': len(conflicts),
        'unchangedExistingFilesExcludingCurrentLedger': unchanged_count,
        'productionProtectedBytePredicate': 'FAIL', 'retainedFailure': retained_failure,
        'disposableMapSimulation': 'PASS; exactly nine replacements, never installed',
        'negativeControlsPassed': len(controls), 'negativeControls': controls,
        'existingReadOnlyGovernanceTests': read_only_governance_controls(),
        'files': rows,
        'unchangedGovernanceCanonicalSha256': digests(IDENTITIES['candidate']['commit'], evidence_paths),
        'limitations': [
            'Not full hosted governance or native execution; existing evidence is retained.',
            'No owner approval is inferred from path classification or this technical PASS.',
            'No workflow, licence scope, historical pin, trust or performance policy is changed.',
        ],
    }
    if '--check-ledger' in sys.argv[1:]:
        record['stagedCurrentLedgerPaths'] = check_staged_ledger(rights)
    if '--check-local-evidence' in sys.argv[1:]:
        record['retainedLocalExecutionEvidenceFiles'] = check_local_evidence()
    require(set(sys.argv[1:]) <= {'--check-ledger', '--check-local-evidence'}, 'Unsupported option; no activation mode exists')
    print(json.dumps(record, indent=2))


if __name__ == '__main__':
    main()
