# SPDX-License-Identifier: MIT
"""Validate the approved original-portion grant and preserve excluded material."""
import hashlib
import json
from pathlib import Path
import re

from rights_ledger import ROOT, SCOPE, boundary, current_paths, current_digests

MIT_SHA256 = 'f4126eb8680101babaa6c1ee89b9693320eb204139f3ee3fcd08abb66eb31261'
SCOPED_METADATA = 'SEE LICENSE IN LICENSING.md'
SCOPE_TEXT = 'Original first-party portions only; embedded R3-R7 content excluded'


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
    # Authorised current-policy and Goal #56 validation-source refreshes supersede
    # only these exact historical digests; signing/privacy follows verified #67.
    # Keep the frozen evidence and all notice/trust/performance
    # invariants intact; any other change still fails validation.
    preserved = dict(invariants['sha256'])
    policy_refresh = {
        # Goal #56 authorises this exact independently reviewed validation correction.
        # Keep the historical evidence and every other trust/performance digest frozen.
        'src/native/LensDocsStudio.Native.Validation/MermaidCreationUiChecks.cs': (
            '1154b7eaf1b61aaea7c272b560173dc06a304189d8d43840954bf4bca2de7ed5',
            '88f54cdd8a3d706cc837527df199eb7078f64fec0f23740f9d505a013d6802d1'),
        'src/native/LensDocsStudio.Native.Validation/MermaidCreationSessionChecks.cs': (
            'add5a2b8a04a354848c58f6f1826ef27a3ca83635445ef24503d83a7066147bc',
            '7dfa51726c6ca561f2e121cbeca8a71619b9de517a6c26cb26d3550ffe5a7e78'),
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
    validate_preserved(preserved, current_digests(set(preserved)))
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
