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


def apply_reading_productivity_refresh(preserved, refresh):
    # Reviewed implementation/current-guidance paths only. No historical evidence,
    # upstream notice, trust decision or performance/High Contrast gate is eligible.
    allowed = {
        '.gitattributes',
        '.github/workflows/native.yml',
        'assets/scripts/app-controller.js',
        'assets/scripts/document/document-ux-service.js',
        'assets/scripts/editor/draft-service.js',
        'assets/scripts/editor/editor-service.js',
        'assets/scripts/editor/find-replace-service.js',
        'assets/scripts/editor/insert-helper-service.js',
        'assets/scripts/editor/table-editor-service.js',
        'assets/scripts/exports/export-service.js',
        'assets/scripts/files/file-service.js',
        'assets/scripts/rendering/render-service.js',
        'assets/scripts/ui/ui-service.js',
        'assets/styles/app.css',
        'docs/native/architecture-decisions.md',
        'docs/native/functional-parity-matrix.md',
        'docs/native/implementation-progress.md',
        'docs/native/user-guide.md',
        'docs/native/validation-report.md',
        'docs/tool-guide.md',
        'index.html',
        'src/native/LensDocsStudio.Core.Tests/LensDocsStudio.Core.Tests.csproj',
        'src/native/LensDocsStudio.Core.Tests/MermaidVisualCorpusTests.cs',
        'src/native/LensDocsStudio.Core/Arrangement/ArrangementEngine.cs',
        'src/native/LensDocsStudio.Core/Editing/MarkdownEditService.cs',
        'src/native/LensDocsStudio.Core/Mermaid/MermaidSemanticValidation.cs',
        'src/native/LensDocsStudio.Core/Mermaid/MermaidSourceParser.cs',
        'src/native/LensDocsStudio.Core/Rendering/DiagramService.cs',
        'src/native/LensDocsStudio.Core/Review/DocumentReviewService.cs',
        'src/native/LensDocsStudio.Core/SourceAuthoring/SourceText.cs',
        'src/native/LensDocsStudio.Native.Validation/Program.cs',
        'src/native/LensDocsStudio.Native.Validation/NativeExpansionLaunch.cs',
        'src/native/LensDocsStudio.Native.Validation/NativeArrangementLatencyChecks.cs',
        'src/native/LensDocsStudio.Native.Validation/NativeSuiteLifecycleChecks.cs',
        'src/native/LensDocsStudio.Native.Validation/NativeUiSynchronisationChecks.cs',
        'src/native/LensDocsStudio.Native.Validation/NativeLaunchReadiness.cs',
        'src/native/LensDocsStudio.Native.Validation/NativeLaunchReadinessChecks.cs',
        'src/native/LensDocsStudio.Native/DocumentReviewWindow.xaml.cs',
        'src/native/LensDocsStudio.Native/MainWindow.Desktop.cs',
        'src/native/LensDocsStudio.Native/MainWindow.Synchronisation.cs',
        'src/native/LensDocsStudio.Native/MainWindow.WorkspaceChrome.cs',
        'src/native/LensDocsStudio.Native/MainWindow.xaml',
        'src/native/LensDocsStudio.Native/MainWindow.xaml.cs',
        'src/native/LensDocsStudio.Native/Rendering/NativeMarkdownRenderer.cs',
        'src/native/LensDocsStudio.Native/ViewModels/DocumentViewModel.cs',
        'src/native/LensDocsStudio.Native/ViewModels/WorkspaceViewModel.cs',
        'tests/browser/app-smoke.spec.mjs',
    }
    require(refresh['baseCommit'] == '99349d82eb06960eb1f9785cf384db2373aed4fa',
            'Reading amendment must identify the inspected develop baseline')
    require(set(refresh['files']) == allowed, 'Reading amendment must retain its exact reviewed path scope')
    result = dict(preserved)
    for path, entry in refresh['files'].items():
        require(set(entry) == {'previous', 'current'} and preserved.get(path) == entry['previous'],
                'Reading amendment changed historical evidence: ' + path)
        require(re.fullmatch(r'[0-9a-f]{64}', entry['current']) is not None,
                'Reading amendment requires an exact canonical source digest: ' + path)
        result[path] = entry['current']
    return result


def apply_preview_reflow_refresh(preserved, refresh):
    allowed = {
        'src/native/LensDocsStudio.Native/MainWindow.Find.cs',
        'src/native/LensDocsStudio.Native/MainWindow.Zoom.cs',
        'src/native/LensDocsStudio.Native/Rendering/PreviewNavigationService.cs',
    }
    require(refresh['baseCommit'] == '75de42474514af0e7a6e521235a6517f4acad14b',
            'Preview integration must identify the exact original PR #76 source')
    require(set(refresh['files']) == allowed, 'Preview integration must retain its exact reviewed source scope')
    result = dict(preserved)
    for path, entry in refresh['files'].items():
        require(set(entry) == {'previous', 'current'} and preserved.get(path) == entry['previous'],
                'Preview integration changed historical evidence: ' + path)
        require(re.fullmatch(r'[0-9a-f]{64}', entry['current']) is not None,
                'Preview integration requires an exact canonical source digest: ' + path)
        result[path] = entry['current']
    return result


def apply_regression_closure_refresh(preserved, refresh):
    path = 'src/native/LensDocsStudio.Native.Validation/NativeExpansionViEditorUiChecks.cs'
    require(refresh['baseCommit'] == '358d2a6f4b8028461fb535f2f71a9b7c71636760',
            'Closure diagnostics must identify the exact retained failure source')
    require(set(refresh['files']) == {path}, 'Closure diagnostics must retain their single harness path scope')
    entry = refresh['files'][path]
    require(set(entry) == {'previous', 'current'} and preserved.get(path) == entry['previous'],
            'Closure diagnostics changed historical evidence: ' + path)
    require(re.fullmatch(r'[0-9a-f]{64}', entry['current']) is not None,
            'Closure diagnostics require an exact canonical source digest')
    return {**preserved, path: entry['current']}


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
    reading_refresh = json.loads((ROOT / 'docs/open-source/evidence/reading-productivity/preserved-refresh.json').read_text(encoding='utf-8'))
    preserved = apply_reading_productivity_refresh(preserved, reading_refresh)
    reflow_refresh = json.loads((ROOT / 'docs/open-source/evidence/preview-reflow-integration/preserved-refresh.json').read_text(encoding='utf-8'))
    preserved = apply_preview_reflow_refresh(preserved, reflow_refresh)
    closure_refresh = json.loads((ROOT / 'docs/open-source/evidence/targeted-regression-closure/preserved-refresh.json').read_text(encoding='utf-8'))
    preserved = apply_regression_closure_refresh(preserved, closure_refresh)
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
