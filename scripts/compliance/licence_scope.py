# SPDX-License-Identifier: MIT
"""Validate the approved original-portion grant and preserve excluded material."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

from rights_ledger import ROOT, SCOPE, boundary, current_paths, current_digests

MIT_SHA256 = 'f4126eb8680101babaa6c1ee89b9693320eb204139f3ee3fcd08abb66eb31261'
SCOPED_METADATA = 'SEE LICENSE IN LICENSING.md'
SCOPE_TEXT = 'Original first-party portions only; embedded R3-R7 content excluded'

FLOWCHART_EVIDENCE = 'docs/open-source/evidence/flowchart-provenance-81/'
FLOWCHART_PROPOSAL = 'pr81-flowchart-provenance-nine-v1'
FLOWCHART_MANIFEST_SHA256 = '97582b1d4e1c7bf1d2f8762bf812a50c2b8aae976be1491daacb9e75bb8abb4c'
FLOWCHART_APPROVAL_SHA256 = '7232a84cb6f90256a818b0e027c3a22f95acc69e3c1a21faa5f7edcffc6aaec5'
FLOWCHART_PREPARATION = {
    "commit": "5c3210d7acd39593f6ae6495a32c74564b50fa98",
    "tree": "0599acd4077d75b71fb065a9564d574fb3d18950"
}
FLOWCHART_IDENTITIES = {
    "baseline": {
        "commit": "99349d82eb06960eb1f9785cf384db2373aed4fa",
        "tree": "e4d137a12cbdd827583fcb841612890dd88736ec"
    },
    "candidate": {
        "commit": "b29dbb862bd6fe2eebc26deba25bab072857048f",
        "tree": "53aa0f0c18455ca61cb2bc7b4abf111124de50dc"
    },
    "evaluation": {
        "commit": "3a8f10d737791a1182d092676ecb17a47e2fde30",
        "tree": "1f13a1afa64fd0b4419303bcaf0136f1ffcecfe7"
    },
    "originalEvidence": {
        "commit": "0890ec9b2ade8af24b9b3fca6cbe98cd7fd329ba",
        "tree": "fd0e968cd8fb3ece8ebb73d78a859705e1605fe3"
    }
}
FLOWCHART_SOURCE_TREES = {
    "src/native": "8c90d7e3491615d4f156b3e33b3abb7b13cbc59f",
    "tests/fixtures/native-flowchart": "4b0a5ae1e829c87e70d0796e5bbaed9a1e6d4b35"
}
FLOWCHART_PAIRS = {
    "src/native/LensDocsStudio.Core.Tests/LensDocsStudio.Core.Tests.csproj": {
        "previous": "5744f4a8b7e8784519a74b362d3a7f7f5a1d657b2c295b6c3d4d2b66ff585316",
        "current": "bcc5936898b464e3da8116937349c04a411755db2d78e4376d3d763017c275b4"
    },
    "src/native/LensDocsStudio.Core/Arrangement/ArrangementEngine.cs": {
        "previous": "8e12c1260ad97b08b509b1210dafe0124c2b117701f5d2a160b3a59f356af859",
        "current": "ae71b419863ebf55de83e48984106023c583cf76520edd77d12d93d966120d77"
    },
    "src/native/LensDocsStudio.Core/Mermaid/MermaidOperations.cs": {
        "previous": "bc11d28656c15935077a20ff0d981fd4aaafaaae099b805fa87f03bfe6c31245",
        "current": "fde06abc85a0f3a68e33064d55c6df3f4fd9355eca2ef9f02ee5b8f27f894f70"
    },
    "src/native/LensDocsStudio.Core/Mermaid/MermaidSemanticValidation.cs": {
        "previous": "d2860c996f00e0a6a145ed1e006069b1118aab033c75d249ab13fc384e49a6a5",
        "current": "311c66b3e3136767838aab0eeadc40bedd8ba35d4a368b01444da2ddc00cdeca"
    },
    "src/native/LensDocsStudio.Core/Mermaid/MermaidSourceParser.cs": {
        "previous": "ca956434be85b52c1ec5c66d3bcded2b4145fc4517de72f0c77fac18464dce8c",
        "current": "7e8304ebb33911ceb58ec63401d3f17cc67b11ef26d912abca8b6e07a20af5d3"
    },
    "src/native/LensDocsStudio.Core/Rendering/DiagramService.cs": {
        "previous": "c8a3b7b97bf0fc91e3aa7adfbce99646709380083fd53015eea9742192df4d0d",
        "current": "158554c6f6d661e9563c8515f91a6985234da59aec123eeaf06246ae60961275"
    },
    "src/native/LensDocsStudio.Core/SourceAuthoring/SourceText.cs": {
        "previous": "33615f2d44fc1faecbbdf5f72e506769073bc9cbbd95a437f7c291a8b50fd738",
        "current": "7c99911849b28f54be0b0615b4c91374a0b791d86e897a40fa177e6d0cad888c"
    },
    "src/native/LensDocsStudio.Native.Validation/Program.cs": {
        "previous": "3f7abecc36518604a7e644cc122d98fac8ef772d9e4e4ecbb574fc56977c1219",
        "current": "e2e1977ff193a343de5dfe4f3ed1c8fc8f44621b9345c1957fa6726b39d2f2ca"
    },
    "src/native/LensDocsStudio.Native/Rendering/NativeMarkdownRenderer.cs": {
        "previous": "60ab0564756fc1a336656d437eea5ae16705db2620c3913dc2c797afe7c9c649",
        "current": "a71d05d445699f1ed8adf8fa385c886b55613a74bd822af86ea98fbd20f3083c"
    }
}

PREVIEW_LOADING_EVIDENCE = 'docs/open-source/evidence/preview-loading-visibility/'
PREVIEW_LOADING_PROPOSAL = 'preview-loading-visibility-one-v1'
PREVIEW_LOADING_MANIFEST_SHA256 = '0dc374121eafa93c66c1f67a9562d326472a8c58aa41c64a94e6346ee5511e32'
PREVIEW_LOADING_APPROVAL_SHA256 = 'f38b0799d5b85680e27cb1509b1dc5dd0facf226a8a9c3c3c1b3f1021e0425ea'
PREVIEW_LOADING_IDENTITIES = {
    'baseline': {'commit': 'cc465fd7f3bfbf949c460563c9be2d73a72193aa',
                 'tree': 'b3cd0c8335aca772cfe011a0895792aca7ce9f91'},
    'candidate': {'commit': '504f71eda248c6d4162f7b1f73e9bea555cb2e28',
                  'tree': '556cdcfc6f2541a082932a2f9529dc3ae3fabd2c'},
    'preparation': {'commit': '0fcab63d2d9f8db8df90c6f1f60cf3eac6301043',
                    'tree': '556cdcfc6f2541a082932a2f9529dc3ae3fabd2c'},
}
PREVIEW_LOADING_SOURCE_TREES = {
    'src/native': 'cc2bc7b62695a55e52b980147285bbf4ad759e8d',
    'tests/fixtures/native-flowchart': '4b0a5ae1e829c87e70d0796e5bbaed9a1e6d4b35',
}
PREVIEW_LOADING_PAIRS = {
    'src/native/LensDocsStudio.Native/MainWindow.PreviewLoading.cs': {
        'previous': 'a87587c5c6c3403e766b895c1ad41ccb715a6472ffd0f7530cdfb0eb0abfe0ad',
        'current': 'aec177b20c1176d1e05ae8d0f9de17c22c437b3044ad3aac058a9227ba9c9053',
    },
}

PREVIEW_REFLOW_EVIDENCE = 'docs/open-source/evidence/preview-reflow-integration-v2/'
PREVIEW_REFLOW_PROPOSAL = 'preview-reflow-integration-three-v2'
PREVIEW_REFLOW_MANIFEST_SHA256 = '340745a23758a94cb50b9aadeb761b817697777481a39762dbe0465ef64a8681'
PREVIEW_REFLOW_APPROVAL_SHA256 = '3f8bc670c9361973621bde8ab391f40f8b0d37919defa96ece86bb7a0469dabb'
PREVIEW_REFLOW_DECISION = 'https://github.com/DouglasNLima/local-docs-studio/pull/83#issuecomment-6765036155'
PREVIEW_REFLOW_PREPARATION = {
    'commit': '662870e83aceccaa19f3677ec7ed2bc4cb565bb4',
    'tree': 'c72610508f6a430d29918c26827d6433d92d244d',
}
PREVIEW_REFLOW_SOURCE_TREES = {
    'src/native': '4deeb6f0b54fd2e982525968500fbbe94030563d',
    'tests/fixtures/native-flowchart': '4b0a5ae1e829c87e70d0796e5bbaed9a1e6d4b35',
    'tests/native/preview-reflow': 'a04548454b40bb63f954d240e02cea5ac0b0ec7d',
    'tests/fixtures/native-preview-reflow': '73c29573efa453df1e9a473c0256358bd6e4309a',
}
PREVIEW_REFLOW_PAIRS = {
    'src/native/LensDocsStudio.Native/MainWindow.Find.cs': {
        'previous': '9e8bcd8b2fbbcdf7d073a61f98b500e54c2c58dd6341e9dc506fab5fa6e7ac7e',
        'current': '30bb76cb045afd2b27be472a560a22d86eaa176d9649b0eb2e663e0564b7c974',
    },
    'src/native/LensDocsStudio.Native/MainWindow.Synchronisation.cs': {
        'previous': 'fb55e56f82805ec4d3de82dc5fa132db50639d89eb61b4182e4765ab2a087ec9',
        'current': '8a7e197f2e850c9eebf4d2e14d312e87cb20a1568a462ab44b5d4cb63b504907',
    },
    'src/native/LensDocsStudio.Native/MainWindow.xaml.cs': {
        'previous': '50888389971d81a765bc00b3607baa26cc5e9982f5679779e3d87cbebbfa4585',
        'current': '65781f39bb3e41508960f53f510b503e64dcc09724ea98a2f51656431ab325c1',
    },
}
# Earlier amendment receipts are retained, never replaced by successor authority.
PREVIEW_REFLOW_EARLIER_REFRESHES = {
    'docs/open-source/evidence/find-caret/preserved-refresh.json':
        'a5ce3c4a9a8d1ce6b6a86f79b10b98ee8ca94296a2ca7de95ca8e037f0624237',
    'docs/open-source/evidence/selection-sync/preserved-refresh.json':
        'f29e60b557cf1c4e0060262cda244431cea451108dad865122d087a744c74694',
}

DOCUMENT_STATISTICS_EVIDENCE = 'docs/open-source/evidence/document-statistics/'
DOCUMENT_STATISTICS_PROPOSAL = 'document-statistics-ten-v1'
DOCUMENT_STATISTICS_MANIFEST_SHA256 = '7dd1bc0e1a4c1ccf798495cba950a422af06729ee3876d86a04e028f6141ec22'
DOCUMENT_STATISTICS_APPROVAL_SHA256 = '263f7fa6bb4d58b6dfe7371784a123a4daf75d81550c043caf8b02aae6cb24a7'
DOCUMENT_STATISTICS_REFRESH_SHA256 = '17fc365494190bee00de5d678875e788de665e3cb96adfd578ca229ca72afff8'
DOCUMENT_STATISTICS_DECISION = 'https://github.com/DouglasNLima/local-docs-studio/pull/84#issuecomment-6769313461'
DOCUMENT_STATISTICS_PREPARATION = {
    'commit': '90b5218e2af82f90eedfb3809e59235a716ed8f9',
    'tree': 'bcae1db353c79955f935852c4c1aff9494035539',
}
# These two pre-activation hashes remain in the immutable candidate manifest.
# Only their current implementations are authorised to change during activation.
DOCUMENT_STATISTICS_GOVERNANCE_PATHS = {
    'scripts/compliance/licence_scope.py', 'scripts/compliance/test_governance.py',
}



def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate_licence(text):
    require(hashlib.sha256(text.encode('utf-8')).hexdigest() == MIT_SHA256,
            'LICENSE must retain the exact unmodified MIT terms and approved notice')


def validate_scope(scope, paths):
    require(scope['scope'] == SCOPE_TEXT, 'A blanket first-party grant is forbidden')
    require(scope['excludedBoundaries'] == ['R3', 'R4', 'R5', 'R6', 'R7'],
            'Protected exclusions are missing')
    granted = scope['paths']
    require(granted == sorted(set(granted)) and set(granted) <= paths,
            'Grant paths must be unique, sorted and present')
    for path in granted:
        require(boundary(path, set(granted))[0] in ('R1', 'R2'),
                'Excluded content cannot enter the first-party grant: ' + path)
    require(set(scope['spdxPaths']) <= set(granted), 'SPDX path is outside the grant')
    return set(granted)


def validate_metadata(package, lock):
    require(package.get('license') == lock['packages'][''].get('license') == SCOPED_METADATA,
            'Package metadata must disclose the restricted grant')
    require(package.get('private') is True, 'The maintenance package must remain private')


def validate_preserved(expected, actual):
    for name, digest in expected.items():
        require(actual.get(name) == digest, 'Protected notice/trust/performance bytes changed: ' + name)


def apply_find_caret_refresh(preserved, refresh):
    # Keep the Goal #62 snapshot immutable. This focused correction replaces only
    # exact reviewed source/doc digests; attribution, trust and performance pins remain.
    allowed = {
        '.github/workflows/native.yml',
        'assets/scripts/app-controller.js',
        'assets/scripts/editor/find-replace-service.js',
        'docs/native/architecture-decisions.md', 'docs/native/engineering-handover.md',
        'docs/native/functional-parity-matrix.md', 'docs/native/implementation-progress.md',
        'docs/native/user-guide.md', 'docs/native/validation-report.md', 'docs/tool-guide.md',
        'src/native/LensDocsStudio.Native/MainWindow.Find.cs',
        'src/native/LensDocsStudio.Native/MainWindow.xaml.cs',
        'src/native/LensDocsStudio.Native.Validation/NativeDesktopUxChecks.cs',
        'src/native/LensDocsStudio.Native.Validation/NativeDesktopUxUiChecks.cs',
        'src/native/LensDocsStudio.Native.Validation/NativeFindRoutingChecks.cs',
        'tests/browser/app-smoke.spec.mjs',
    }
    require(refresh['baseCommit'] == '86d60322f6f1f1dec06974bf5f9e1738a3bb80c7',
            'Find amendment must identify the inspected develop baseline')
    require(set(refresh['files']) == allowed, 'Find amendment must retain its exact reviewed path scope')
    result = dict(preserved)
    for path, entry in refresh['files'].items():
        require(set(entry) == {'previous', 'current'} and preserved.get(path) == entry['previous'],
                'Find amendment changed historical evidence: ' + path)
        require(re.fullmatch(r'[0-9a-f]{64}', entry['current']) is not None,
                'Find amendment requires an exact canonical source digest: ' + path)
        result[path] = entry['current']
    return result


def apply_selection_sync_refresh(preserved, refresh):
    # Only this native investigation's source and tracking notes may supersede
    # their develop pins. Original evidence, notices and performance gates remain.
    allowed = {
        'docs/native/architecture-decisions.md', 'docs/native/functional-parity-matrix.md',
        'docs/native/implementation-progress.md', 'docs/native/native-experience-improvements.md',
        'docs/native/validation-report.md',
        'src/native/LensDocsStudio.Native.Validation/NativeSelectionMappingChecks.cs',
        'src/native/LensDocsStudio.Native.Validation/NativeUiSynchronisationChecks.cs',
        'src/native/LensDocsStudio.Native.Validation/Program.cs',
        'src/native/LensDocsStudio.Native/MainWindow.Synchronisation.cs',
        'src/native/LensDocsStudio.Native/MainWindow.xaml.cs',
        'src/native/LensDocsStudio.Native/Rendering/DocumentPreview.cs',
        'src/native/LensDocsStudio.Native/Rendering/PreviewNavigationService.cs',
    }
    require(refresh['baseCommit'] == 'ab21919a42ec8b3d17d9ba2e39b4e7e41459e206',
            'Selection amendment must identify the inspected develop baseline')
    require(set(refresh['files']) == allowed, 'Selection amendment must retain its exact reviewed path scope')
    result = dict(preserved)
    for path, entry in refresh['files'].items():
        require(set(entry) == {'previous', 'current'} and preserved.get(path) == entry['previous'],
                'Selection amendment changed historical evidence: ' + path)
        require(re.fullmatch(r'[0-9a-f]{64}', entry['current']) is not None,
                'Selection amendment requires an exact canonical source digest: ' + path)
        result[path] = entry['current']
    return result


def flowchart_json(raw, expected_sha256=None):
    """Read exact amendment evidence; duplicate fields never override decisions."""
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, 'Duplicate Flowchart amendment field: ' + key)
            result[key] = value
        return result
    require(isinstance(raw, bytes), 'Missing Flowchart amendment evidence bytes')
    canonical = raw.replace(b'\r\n', b'\n')
    value = json.loads(canonical.decode('utf-8'), object_pairs_hook=unique)
    if expected_sha256 is not None:
        require(hashlib.sha256(canonical).hexdigest() == expected_sha256,
                'Flowchart approval or candidate manifest bytes changed')
    return value


def apply_flowchart_styling_refresh(preserved, refresh, approval_bytes, candidate_bytes, actual, granted):
    # The direct owner decision is pinned as evidence, not inferred from a flag.
    # Original Goal #62 and earlier refresh records are never rewritten.
    approval = flowchart_json(approval_bytes, FLOWCHART_APPROVAL_SHA256)
    candidate = flowchart_json(candidate_bytes, FLOWCHART_MANIFEST_SHA256)
    require(approval['decision'] == 'APPROVED_NINE_PAIR_GOVERNANCE_AMENDMENT_ONLY'
            and approval['proposalId'] == FLOWCHART_PROPOSAL
            and approval['preparation'] == FLOWCHART_PREPARATION
            and approval['candidateManifestSha256'] == FLOWCHART_MANIFEST_SHA256,
            'Flowchart amendment lacks the exact owner decision')
    expected = {
        'schemaVersion': 1, 'proposalId': FLOWCHART_PROPOSAL,
        'status': 'OWNER_APPROVED', 'repository': 'DouglasNLima/local-docs-studio',
        'preparation': FLOWCHART_PREPARATION,
        'candidateManifestSha256': FLOWCHART_MANIFEST_SHA256,
        'ownerApprovalSha256': FLOWCHART_APPROVAL_SHA256,
        **FLOWCHART_IDENTITIES, 'excludedBoundaries': ['R3', 'R4', 'R5', 'R6', 'R7'],
        'runtimeSourceTrees': FLOWCHART_SOURCE_TREES, 'files': FLOWCHART_PAIRS,
    }
    require(type(refresh) is dict and type(refresh.get('schemaVersion')) is int
            and refresh == expected,
            'Flowchart amendment must retain the exact approved paths, pairs and identities')
    require(candidate['files'] == FLOWCHART_PAIRS
            and all(candidate[key] == value for key, value in FLOWCHART_IDENTITIES.items()),
            'Flowchart candidate differs from the approved source identities')
    result = dict(preserved)
    for path, entry in FLOWCHART_PAIRS.items():
        require(path in granted and boundary(path, granted)[:2] == ('R1', 'MIT_ORIGINAL_PORTIONS_ONLY'),
                'Unapproved Flowchart amendment path: ' + path)
        require(preserved.get(path) == entry['previous'],
                'Flowchart amendment changed the previous effective pin: ' + path)
        require(actual.get(path) == entry['current'],
                'Flowchart amendment candidate bytes changed: ' + path)
        result[path] = entry['current']
    return result


def validate_flowchart_evidence_digests(actual):
    # These digests use Git's actual clean filters, not assumed line endings.
    for name, digest in [('candidate-refresh.json', FLOWCHART_MANIFEST_SHA256),
                         ('owner-approval.json', FLOWCHART_APPROVAL_SHA256)]:
        require(actual.get(FLOWCHART_EVIDENCE + name) == digest,
                'Flowchart canonical evidence bytes changed: ' + name)


def validate_flowchart_source_identity(trees, expected_objects, actual_objects):
    validate_source_identity(trees, FLOWCHART_SOURCE_TREES, expected_objects, actual_objects)


def validate_source_identity(trees, approved_trees, expected_objects, actual_objects):
    require(trees == approved_trees,
            'Flowchart amendment source tree identity changed')
    require(bool(expected_objects) and set(actual_objects) == set(expected_objects),
            'Flowchart source identity has missing or additional paths')
    require(actual_objects == expected_objects, 'Flowchart source working bytes changed')


def check_flowchart_source_identity(paths, approved_trees=FLOWCHART_SOURCE_TREES):
    # All objects below exist in a depth-1 checkout. Historical commit/tree labels
    # are exact pinned approval metadata; their full-history proof is retained in
    # activation-validation.json. No absent-history fallback or network fetch.
    def git(*args, data=None):
        return subprocess.check_output(['git', *args], cwd=ROOT, input=data)
    trees = {path: git('rev-parse', 'HEAD:' + path).decode().strip()
             for path in approved_trees}
    expected = {}
    for row in git('ls-tree', '-rz', '--full-tree', 'HEAD', '--', *approved_trees).split(b'\0'):
        if not row:
            continue
        metadata, name = row.split(b'\t', 1)
        mode, kind, oid = metadata.decode().split()
        require(kind == 'blob' and mode in ('100644', '100755'),
                'Unexpected Flowchart source object')
        expected[name.decode('utf-8')] = oid
    names = sorted(path for path in paths
                   if any(path.startswith(prefix + '/') for prefix in approved_trees))
    require(all('\n' not in path and '\r' not in path for path in names),
            'Unsupported newline in a Flowchart source path')
    objects = git('-c', 'core.safecrlf=false', 'hash-object', '--stdin-paths',
                  data=('\n'.join(names) + '\n').encode('utf-8')).decode().splitlines()
    require(len(objects) == len(names), 'Incomplete Flowchart source object inventory')
    validate_source_identity(trees, approved_trees, expected, dict(zip(names, objects, strict=True)))


def validate_preview_loading_approval(refresh, approval_bytes, candidate_bytes):
    # The predecessor's constants and evidence stay immutable. A separate exact
    # owner receipt is required before accepting this one-path successor tree.
    approval = flowchart_json(approval_bytes, PREVIEW_LOADING_APPROVAL_SHA256)
    candidate = flowchart_json(candidate_bytes, PREVIEW_LOADING_MANIFEST_SHA256)
    require(approval['decision'] == 'APPROVED_ONE_PAIR_GOVERNANCE_AMENDMENT_ONLY'
            and approval['proposalId'] == PREVIEW_LOADING_PROPOSAL
            and approval['preparation'] == PREVIEW_LOADING_IDENTITIES['preparation']
            and approval['candidateManifestSha256'] == PREVIEW_LOADING_MANIFEST_SHA256,
            'Preview loading amendment lacks the exact owner decision')
    expected = {
        'schemaVersion': 1, 'proposalId': PREVIEW_LOADING_PROPOSAL,
        'status': 'OWNER_APPROVED', 'repository': 'DouglasNLima/local-docs-studio',
        'candidateManifestSha256': PREVIEW_LOADING_MANIFEST_SHA256,
        'ownerApprovalSha256': PREVIEW_LOADING_APPROVAL_SHA256,
        **PREVIEW_LOADING_IDENTITIES, 'excludedBoundaries': ['R3', 'R4', 'R5', 'R6', 'R7'],
        'previousRuntimeSourceTrees': FLOWCHART_SOURCE_TREES,
        'runtimeSourceTrees': PREVIEW_LOADING_SOURCE_TREES, 'files': PREVIEW_LOADING_PAIRS,
    }
    require(type(refresh) is dict and type(refresh.get('schemaVersion')) is int and refresh == expected,
            'Preview loading amendment must retain its exact approved path, pair and identities')
    require(candidate['files'] == PREVIEW_LOADING_PAIRS
            and candidate['previousRuntimeSourceTrees'] == FLOWCHART_SOURCE_TREES
            and candidate['runtimeSourceTrees'] == PREVIEW_LOADING_SOURCE_TREES
            and all(candidate[key] == value for key, value in PREVIEW_LOADING_IDENTITIES.items()),
            'Preview loading candidate differs from the approved identities')
    return dict(PREVIEW_LOADING_SOURCE_TREES)


def validate_preview_loading_evidence_digests(actual):
    for name, digest in [('candidate-refresh.json', PREVIEW_LOADING_MANIFEST_SHA256),
                         ('owner-approval.json', PREVIEW_LOADING_APPROVAL_SHA256)]:
        require(actual.get(PREVIEW_LOADING_EVIDENCE + name) == digest,
                'Preview loading canonical evidence bytes changed: ' + name)


def check_preview_loading_source_identity(paths):
    refresh = flowchart_json((ROOT / (PREVIEW_LOADING_EVIDENCE + 'approved-refresh.json')).read_bytes())
    approval = (ROOT / (PREVIEW_LOADING_EVIDENCE + 'owner-approval.json')).read_bytes()
    candidate = (ROOT / (PREVIEW_LOADING_EVIDENCE + 'candidate-refresh.json')).read_bytes()
    trees = validate_preview_loading_approval(refresh, approval, candidate)
    validate_preview_loading_evidence_digests(current_digests({PREVIEW_LOADING_EVIDENCE + name
        for name in ('candidate-refresh.json', 'owner-approval.json')}))
    check_flowchart_source_identity(paths, trees)
    return refresh, approval, candidate


def apply_preview_loading_refresh(preserved, refresh, approval_bytes, candidate_bytes, actual, granted):
    validate_preview_loading_approval(refresh, approval_bytes, candidate_bytes)
    result = dict(preserved)
    for path, entry in PREVIEW_LOADING_PAIRS.items():
        require(path in granted and boundary(path, granted)[:2] == ('R1', 'MIT_ORIGINAL_PORTIONS_ONLY'),
                'Unapproved preview loading amendment path: ' + path)
        require(preserved.get(path) == entry['previous'],
                'Preview loading amendment changed the previous effective pin: ' + path)
        require(actual.get(path) == entry['current'],
                'Preview loading amendment candidate bytes changed: ' + path)
        result[path] = entry['current']
    return result


def provenance_git(*args, data=None):
    # Binary plumbing only: never shell-pipe or decode blob bytes before hashing.
    return subprocess.check_output(['git', *args], cwd=ROOT, input=data)


def canonical_evidence_json(raw, expected_sha256):
    require(isinstance(raw, bytes) and hashlib.sha256(raw).hexdigest() == expected_sha256,
            'Canonical committed provenance evidence bytes changed')
    return flowchart_json(raw)


def read_working_evidence(path, expected_sha256):
    working = ROOT / path
    require(working.is_file() and not working.is_symlink(),
            'Working provenance evidence is missing or substituted: ' + path)
    raw = working.read_bytes()
    oid = provenance_git('-c', 'core.safecrlf=false', 'hash-object', '-w',
                         '--path=' + path, '--stdin', data=raw).strip()
    canonical = provenance_git('cat-file', 'blob', oid.decode())
    require(raw.replace(b'\r\n', b'\n') == canonical.replace(b'\r\n', b'\n'),
            'Working provenance evidence differs beyond checkout line endings: ' + path)
    canonical_evidence_json(canonical, expected_sha256)
    return canonical, oid


def read_committed_evidence(path, expected_sha256):
    # HEAD contains these immutable records even in depth-1 clones. There is no
    # historical-object exception or fallback to checkout/serialised JSON bytes.
    rows = provenance_git('ls-tree', '-z', 'HEAD', '--', path).split(b'\0')
    require(len(rows) == 2 and not rows[1], 'Committed provenance evidence is missing')
    metadata, name = rows[0].split(b'\t', 1)
    mode, kind, oid = metadata.split()
    require(name == path.encode() and mode == b'100644' and kind == b'blob',
            'Committed provenance evidence must be a regular source blob')
    raw = provenance_git('cat-file', 'blob', oid.decode())
    canonical_evidence_json(raw, expected_sha256)
    require(provenance_git('ls-files', '--stage', '-z', '--', path) ==
            b'100644 ' + oid + b' 0\t' + path.encode() + b'\0',
            'Provenance evidence index differs from the committed identity')
    working, working_oid = read_working_evidence(path, expected_sha256)
    require(working == raw and working_oid == oid,
            'Provenance evidence Git-clean object differs from the committed identity')
    return raw


def validate_preview_reflow_approval(refresh, approval_bytes, candidate_bytes):
    approval = canonical_evidence_json(approval_bytes, PREVIEW_REFLOW_APPROVAL_SHA256)
    candidate = canonical_evidence_json(candidate_bytes, PREVIEW_REFLOW_MANIFEST_SHA256)
    require(approval['decision'] == 'APPROVED_THREE_PAIR_GOVERNANCE_AMENDMENT_ONLY'
            and approval['proposalId'] == PREVIEW_REFLOW_PROPOSAL
            and approval['preparation'] == PREVIEW_REFLOW_PREPARATION
            and approval['candidateManifestSha256'] == PREVIEW_REFLOW_MANIFEST_SHA256
            and approval['source']['url'] == PREVIEW_REFLOW_DECISION
            and approval['source']['author'] == 'DouglasNLima',
            'Preview reflow amendment lacks the exact owner decision')
    require(candidate['proposalId'] == PREVIEW_REFLOW_PROPOSAL
            and candidate['files'] == PREVIEW_REFLOW_PAIRS
            and candidate['previousRuntimeSourceTrees'] == PREVIEW_LOADING_SOURCE_TREES
            and {**candidate['runtimeSourceTrees'], **candidate['controlTrees']} == PREVIEW_REFLOW_SOURCE_TREES,
            'Preview reflow candidate differs from the approved identities')
    expected = {
        'schemaVersion': 1, 'proposalId': PREVIEW_REFLOW_PROPOSAL,
        'status': 'OWNER_APPROVED', 'repository': 'DouglasNLima/local-docs-studio',
        'preparation': PREVIEW_REFLOW_PREPARATION,
        'candidateManifestSha256': PREVIEW_REFLOW_MANIFEST_SHA256,
        'ownerApprovalSha256': PREVIEW_REFLOW_APPROVAL_SHA256,
        'ownerDecision': PREVIEW_REFLOW_DECISION,
        **{key: candidate[key] for key in ('baseline', 'candidate', 'historicalSource',
            'previousRuntimeSourceTrees', 'runtimeSourceTrees', 'controlTrees', 'files',
            'addedFiles', 'preservedEvidence', 'historicalEvidenceTrees', 'excludedBoundaries',
            'activationPathsOnly')},
    }
    require(type(refresh) is dict and type(refresh.get('schemaVersion')) is int and refresh == expected,
            'Preview reflow amendment must retain its exact approved paths, pairs and identities')
    return candidate


def read_preview_reflow_evidence():
    candidate_bytes = read_committed_evidence(
        PREVIEW_REFLOW_EVIDENCE + 'candidate-refresh.json', PREVIEW_REFLOW_MANIFEST_SHA256)
    approval_bytes, _ = read_working_evidence(
        PREVIEW_REFLOW_EVIDENCE + 'owner-approval.json', PREVIEW_REFLOW_APPROVAL_SHA256)
    refresh = flowchart_json((ROOT / (PREVIEW_REFLOW_EVIDENCE + 'approved-refresh.json')).read_bytes())
    candidate = validate_preview_reflow_approval(refresh, approval_bytes, candidate_bytes)
    for path, digest in {**candidate['preservedEvidence'], **PREVIEW_REFLOW_EARLIER_REFRESHES}.items():
        read_committed_evidence(path, digest)
    validate_preserved(candidate['addedFiles'], current_digests(set(candidate['addedFiles'])))
    return refresh, approval_bytes, candidate_bytes


def check_preview_reflow_source_identity(paths):
    evidence = read_preview_reflow_evidence()
    candidate = validate_preview_reflow_approval(*evidence)
    # The same pinned-HEAD mechanism as #81/#82, now also covering controls and
    # immutable observations. Full-history identities are proved at activation;
    # runtime validation always requires every current approved tree and object.
    trees = {**PREVIEW_REFLOW_SOURCE_TREES, **candidate['historicalEvidenceTrees']}
    require(all((ROOT / path).is_file() and not (ROOT / path).is_symlink()
                for path in paths if any(path.startswith(prefix + '/') for prefix in trees)),
            'Preview reflow source is missing or substituted')
    check_flowchart_source_identity(paths, trees)
    return evidence


def apply_preview_reflow_refresh(preserved, refresh, approval_bytes, candidate_bytes, actual, granted):
    validate_preview_reflow_approval(refresh, approval_bytes, candidate_bytes)
    require(len(preserved) == 2421, 'Preview reflow must retain all previously protected paths')
    result = dict(preserved)
    for path, entry in PREVIEW_REFLOW_PAIRS.items():
        require(path in granted and boundary(path, granted)[:2] == ('R1', 'MIT_ORIGINAL_PORTIONS_ONLY'),
                'Unapproved preview reflow amendment path: ' + path)
        require(preserved.get(path) == entry['previous'],
                'Preview reflow amendment changed the previous effective pin: ' + path)
        require(actual.get(path) == entry['current'],
                'Preview reflow amendment candidate bytes changed: ' + path)
        result[path] = entry['current']
    return result


def validate_document_statistics_approval(refresh, approval_bytes, candidate_bytes):
    # Pin both decisions before inspecting or accepting any successor identity.
    # The canonical manifest fixes every pair and added file without a new grant.
    approval = canonical_evidence_json(approval_bytes, DOCUMENT_STATISTICS_APPROVAL_SHA256)
    candidate = canonical_evidence_json(candidate_bytes, DOCUMENT_STATISTICS_MANIFEST_SHA256)
    require(approval['decision'] == 'APPROVED_TEN_PAIR_GOVERNANCE_AMENDMENT_ONLY'
            and approval['proposalId'] == DOCUMENT_STATISTICS_PROPOSAL
            and approval['preparation'] == DOCUMENT_STATISTICS_PREPARATION
            and approval['candidateManifestSha256'] == DOCUMENT_STATISTICS_MANIFEST_SHA256
            and approval['source']['url'] == DOCUMENT_STATISTICS_DECISION
            and approval['source']['owner'] == 'DouglasNLima',
            'Document statistics amendment lacks the exact owner decision')
    require(candidate['proposalId'] == DOCUMENT_STATISTICS_PROPOSAL
            and len(candidate['files']) == 10 and len(candidate['addedFiles']) == 28
            and candidate['previousRuntimeSourceTrees'] == {
                path: PREVIEW_REFLOW_SOURCE_TREES[path] for path in candidate['runtimeSourceTrees']},
            'Document statistics candidate differs from the approved predecessor')
    expected = {
        'schemaVersion': 1, 'proposalId': DOCUMENT_STATISTICS_PROPOSAL,
        'status': 'OWNER_APPROVED', 'repository': 'DouglasNLima/local-docs-studio',
        'preparation': DOCUMENT_STATISTICS_PREPARATION,
        'candidateManifestSha256': DOCUMENT_STATISTICS_MANIFEST_SHA256,
        'ownerApprovalSha256': DOCUMENT_STATISTICS_APPROVAL_SHA256,
        'ownerDecision': DOCUMENT_STATISTICS_DECISION,
        **{key: candidate[key] for key in ('baseline', 'candidate', 'testedProduct',
            'historicalReference', 'previousRuntimeSourceTrees', 'runtimeSourceTrees',
            'controlTrees', 'files', 'addedFiles', 'preservedEvidence', 'unchangedTrees',
            'excludedBoundaries', 'activationPathsOnly', 'constraints')},
    }
    require(type(refresh) is dict and type(refresh.get('schemaVersion')) is int and refresh == expected,
            'Document statistics amendment must retain its exact approved paths, pairs and identities')
    return candidate


def check_committed_file_identities(expected):
    # Compare HEAD, index and actual Git-clean working objects, including modes.
    # All required blobs are in HEAD, so this also works at fetch-depth 1.
    committed = {}
    for row in provenance_git('ls-tree', '-rz', 'HEAD').split(b'\0'):
        if row:
            metadata, name = row.split(b'\t', 1)
            if name.decode('utf-8') in expected:
                mode, kind, oid = metadata.split()
                require(mode in (b'100644', b'100755') and kind == b'blob',
                        'Protected committed object is substituted')
                committed[name.decode('utf-8')] = (mode, oid)
    require(set(committed) == set(expected), 'Protected committed files are missing')
    index = {}
    for row in provenance_git('ls-files', '--stage', '-z').split(b'\0'):
        if row:
            metadata, name = row.split(b'\t', 1)
            if name.decode('utf-8') in expected:
                mode, oid, stage = metadata.split()
                require(stage == b'0' and name not in index, 'Protected index is unmerged')
                index[name] = (mode, oid)
    require(index == {p.encode(): entry for p, entry in committed.items()},
            'Protected index differs from the committed identity')
    names = sorted(expected)
    require(all('\n' not in p and '\r' not in p and (ROOT / p).is_file()
                and not (ROOT / p).is_symlink() for p in names),
            'Protected working file is missing or substituted')
    objects = provenance_git('-c', 'core.safecrlf=false', 'hash-object', '--stdin-paths',
                             data=('\n'.join(names) + '\n').encode('utf-8')).splitlines()
    require(objects == [committed[p][1] for p in names],
            'Protected Git-clean working objects differ from the committed identity')
    # With object equality established, these are also the exact committed blob
    # SHA-256 identities, not hashes of decoded or reserialised text.
    validate_preserved(expected, current_digests(set(expected)))


def check_document_statistics_source_identity(paths):
    candidate_bytes = read_committed_evidence(
        DOCUMENT_STATISTICS_EVIDENCE + 'candidate-refresh.json', DOCUMENT_STATISTICS_MANIFEST_SHA256)
    approval_bytes = read_committed_evidence(
        DOCUMENT_STATISTICS_EVIDENCE + 'owner-approval.json', DOCUMENT_STATISTICS_APPROVAL_SHA256)
    refresh = flowchart_json(read_committed_evidence(
        DOCUMENT_STATISTICS_EVIDENCE + 'approved-refresh.json', DOCUMENT_STATISTICS_REFRESH_SHA256))
    candidate = validate_document_statistics_approval(refresh, approval_bytes, candidate_bytes)
    for path, digest in candidate['preservedEvidence'].items():
        if path not in DOCUMENT_STATISTICS_GOVERNANCE_PATHS:
            read_committed_evidence(path, digest)
    predecessor = validate_preview_reflow_approval(*read_preview_reflow_evidence())
    trees = {**PREVIEW_REFLOW_SOURCE_TREES, **predecessor['historicalEvidenceTrees'],
             **candidate['runtimeSourceTrees'], **candidate['controlTrees'], **candidate['unchangedTrees']}
    require(all((ROOT / path).is_file() and not (ROOT / path).is_symlink()
                for path in paths if any(path.startswith(prefix + '/') for prefix in trees)),
            'Document statistics source is missing or substituted')
    check_flowchart_source_identity(paths, trees)
    require(not provenance_git('diff', '--cached', '--name-only', '--no-renames', 'HEAD', '--', *trees).strip(),
            'Document statistics source index differs from the committed trees')
    check_committed_file_identities({**candidate['addedFiles'],
        **{path: pair['current'] for path, pair in candidate['files'].items()}})
    return refresh, approval_bytes, candidate_bytes


def document_statistics_predecessor_digests(refresh, approval_bytes, candidate_bytes, actual):
    candidate = validate_document_statistics_approval(refresh, approval_bytes, candidate_bytes)
    previous = dict(actual)
    for path, pair in candidate['files'].items():
        require(actual.get(path) == pair['current'],
                'Document statistics candidate bytes changed: ' + path)
        previous[path] = pair['previous']
    # This view is solely for replaying immutable predecessor amendments. The
    # final inventory is checked against actual current bytes after the successor.
    return previous


def apply_document_statistics_refresh(preserved, refresh, approval_bytes, candidate_bytes, actual, granted):
    candidate = validate_document_statistics_approval(refresh, approval_bytes, candidate_bytes)
    require(len(preserved) == 2421, 'Document statistics must retain all previously protected paths')
    result = dict(preserved)
    for path, pair in candidate['files'].items():
        require(path in granted and boundary(path, granted)[:2] in (
                    ('R1', 'MIT_ORIGINAL_PORTIONS_ONLY'), ('R2', 'MIT_ORIGINAL_PORTIONS_ONLY')),
                'Unapproved document statistics amendment path: ' + path)
        require(preserved.get(path) == pair['previous'],
                'Document statistics amendment changed the previous effective pin: ' + path)
        require(actual.get(path) == pair['current'],
                'Document statistics amendment candidate bytes changed: ' + path)
        result[path] = pair['current']
    return result


def main():
    validate_licence((ROOT / 'LICENSE').read_text(encoding='utf-8'))
    scope = json.loads(SCOPE.read_text(encoding='utf-8'))
    paths = current_paths()
    granted = validate_scope(scope, paths)
    validate_metadata(json.loads((ROOT / 'package.json').read_text(encoding='utf-8')),
                      json.loads((ROOT / 'package-lock.json').read_text(encoding='utf-8')))
    disclosure = (ROOT / 'LICENSING.md').read_text(encoding='utf-8')
    for token in ['R1', 'R2', 'R3', 'R4', 'R5', 'R6', 'R7', 'Mixed files',
                  'BoundedErLayout.cs', 'CommandIcons.xaml', 'SEE LICENSE IN LICENSING.md',
                  'Copyright (c) 2026 DouglasNLima', 'BLOCKED']:
        require(token in disclosure, 'Scope disclosure omitted ' + token)
    invariants = json.loads((ROOT / 'docs/open-source/evidence/goal62/preserved-files.json')
                           .read_text(encoding='utf-8'))
    # Goal #62's authorised current-policy refresh supersedes only these historical
    # policy digests (signing/privacy refreshed after verified #67 publication).
    # Keep the frozen evidence and all notice/trust/performance
    # invariants intact; any other change still fails validation.
    preserved = dict(invariants['sha256'])
    policy_refresh = {
        'SECURITY.md': (
            'c9ee2727aba092d930435e02d0d2091b541fe486be0d50ff22709a587c2d4f3d',
            '425de96d2be0ebeb288ade4f7fa84e089cbff437bd1de603f356196015de1c6a'),
        'SUPPORT.md': (
            '52d230bfddcc85655d0c76830e7b5643042442ef8b33d755465658510d1d5667',
            '2d0bea9170d75472c0c2e3cd313f1e76a6c6083b33256f83be089dfca450f7e1'),
        'CODE_OF_CONDUCT.md': (
            'a1ec3816a7e60e90b27cf913f6d238bcccc0ee2cbe661429c55158d3031f5a60',
            '0d96e62ee9054c63b73cbd51106797b6ca293757d97abb05aa33d551a33c917c'),
        'docs/open-source/code-signing-policy.md': (
            '633c3225762226e1b2a1a5d4b17a2ddec4a560583020fd11c1067b05abfb0972',
            'b1d0e06a86e5d08e3e2b394b9568ed1e7c2079ea8d952e17ccc284590e88cc43'),
        'docs/open-source/privacy.md': (
            'bfe3721da4981db13fb4c454c042e416cd571b68796371b1def4f1afd43ca2fe',
            'e18a9ce1cb048a49f3bbfa5a819aa609e7f512db7b5c28b6e143e45b58431343'),
    }
    for path, (previous, current) in policy_refresh.items():
        require(preserved[path] == previous, 'Historical policy evidence changed: ' + path)
        preserved[path] = current
    refresh = json.loads((ROOT / 'docs/open-source/evidence/find-caret/preserved-refresh.json').read_text(encoding='utf-8'))
    preserved = apply_find_caret_refresh(preserved, refresh)
    selection_refresh = json.loads((ROOT / 'docs/open-source/evidence/selection-sync/preserved-refresh.json').read_text(encoding='utf-8'))
    preserved = apply_selection_sync_refresh(preserved, selection_refresh)
    flowchart_refresh = flowchart_json((ROOT / (FLOWCHART_EVIDENCE + 'approved-refresh.json')).read_bytes())
    statistics_evidence = check_document_statistics_source_identity(paths)
    preview_reflow_evidence = read_preview_reflow_evidence()
    preview_loading_evidence = (
        flowchart_json((ROOT / (PREVIEW_LOADING_EVIDENCE + 'approved-refresh.json')).read_bytes()),
        (ROOT / (PREVIEW_LOADING_EVIDENCE + 'owner-approval.json')).read_bytes(),
        (ROOT / (PREVIEW_LOADING_EVIDENCE + 'candidate-refresh.json')).read_bytes(),
    )
    actual = current_digests(set(preserved) | {
        FLOWCHART_EVIDENCE + 'candidate-refresh.json', FLOWCHART_EVIDENCE + 'owner-approval.json'})
    validate_flowchart_evidence_digests(actual)
    predecessor_actual = document_statistics_predecessor_digests(*statistics_evidence, actual)
    preserved = apply_flowchart_styling_refresh(
        preserved, flowchart_refresh,
        (ROOT / (FLOWCHART_EVIDENCE + 'owner-approval.json')).read_bytes(),
        (ROOT / (FLOWCHART_EVIDENCE + 'candidate-refresh.json')).read_bytes(), actual, granted)
    preserved = apply_preview_loading_refresh(preserved, *preview_loading_evidence, actual, granted)
    preserved = apply_preview_reflow_refresh(preserved, *preview_reflow_evidence, predecessor_actual, granted)
    preserved = apply_document_statistics_refresh(preserved, *statistics_evidence, actual, granted)
    validate_preserved(preserved, actual)
    check_committed_file_identities(preserved)
    marker = re.compile(r'^\s*(?://|#|/\*|<!--)\s*SPDX-License-Identifier:\s*MIT\b', re.M)
    source_extensions = {'.py', '.ps1', '.cs', '.js', '.mjs', '.css', '.xaml', '.html', '.yml'}
    baseline_spdx = set(invariants['existingMitSpdxPaths'])
    seen = set()
    for path in paths:
        if Path(path).suffix not in source_extensions or boundary(path)[0] == 'R5':
            continue
        if marker.search((ROOT / path).read_text(encoding='utf-8', errors='replace')):
            seen.add(path)
    require(seen == baseline_spdx | set(scope['spdxPaths']),
            'Unexpected or missing MIT SPDX header; review complete-file rights')
    print(json.dumps({'operativeMIT': 'original enumerated portions only',
                      'grantedPaths': len(granted), 'protectedFiles': len(invariants['sha256']),
                      'newSpdxPaths': len(scope['spdxPaths'])}))


if __name__ == '__main__':
    main()
