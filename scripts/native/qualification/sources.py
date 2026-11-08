"""Exact-source preparation only. This module never launches a product or changes Windows."""
import difflib
import hashlib
import json
import io
from pathlib import Path
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parents[3]
PREPARATION = ROOT / 'docs/native/qualification/goal77-preparation.json'
START = '45eccf7b3bd4c266b3a3223cbfe007248f03f39d'
CANDIDATE = '7b1334b09a02ad2fbfd4a0234798e6c387d3e634'
BASELINE = '99349d82eb06960eb1f9785cf384db2373aed4fa'
CORRECTION = '22c223c6f56fa41fda2a36102e1eed0af44974ec'
PROBE = 'src/native/LensDocsStudio.Native.UxProbe/'
VALIDATION = 'src/native/LensDocsStudio.Native.Validation/'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def git(*args, root=ROOT):
    return subprocess.check_output(['git', '-C', str(root), *args])


def blob(revision, path):
    return git('show', f'{revision}:{path}')


def write_new(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('xb') as stream:
        stream.write(data if isinstance(data, bytes) else data.encode('utf-8'))


def json_new(path, value):
    write_new(path, json.dumps(value, indent=2, ensure_ascii=False, allow_nan=False) + '\n')


def replace_once(text, old, new):
    if text.count(old) != 1:
        raise ValueError('Pinned source fragment drifted: ' + old[:100])
    return text.replace(old, new, 1)


def verify_sources():
    plan_bytes = PREPARATION.read_bytes()
    if plan_bytes.replace(b'\r\n', b'\n') != blob(START, PREPARATION.relative_to(ROOT).as_posix()).replace(b'\r\n', b'\n'):
        raise ValueError('The approved preparation manifest changed')
    plan = json.loads(plan_bytes)
    identities = {name: {'commit': revision, 'tree': git('rev-parse', revision + '^{tree}').decode().strip()}
                  for name, revision in [('start', START), ('candidate', CANDIDATE), ('develop', BASELINE), ('correction', CORRECTION)]}
    corpus = plan['corpus']
    manifest = blob(corpus['fixtureManifestSource'], corpus['fixtureManifestPath'])
    if sha(manifest) != corpus['fixtureManifestSha256']:
        raise ValueError('Fixture manifest drift')
    for fixture in corpus['syntheticAnchors']:
        data = blob(CANDIDATE, 'tests/fixtures/native-ux/' + fixture['file'])
        if sha(data) != fixture['sha256'] or len(data) != fixture['bytes']:
            raise ValueError('Fixture drift: ' + fixture['id'])
    if sha(blob(CANDIDATE, 'tests/fixtures/native-ux/pixel.png')) != corpus['assetSha256']:
        raise ValueError('Image fixture drift')
    return plan, identities


def completion_overlay(original):
    """Only observer plumbing changes. Journey, capture and assertions remain exact."""
    text = original.replace('\r\n', '\n')
    text = replace_once(text, '    private static int Main(string[] args)\n    {',
        '    private static int Main(string[] args)\n    {\n'
        '        if (args.SequenceEqual(new[] { "--identity-offline" }))\n'
        '        { Console.WriteLine(JsonSerializer.Serialize(new { product = Identity(typeof(MainWindow).Assembly), core = Identity(typeof(SessionStore).Assembly), harness = Identity(typeof(Program).Assembly) }, Json)); return 0; }\n'
        '        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||\n'
        '            Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted" ||\n'
        '            Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows" ||\n'
        '            !Guid.TryParse(Environment.GetEnvironmentVariable("LENS_QUALIFICATION_EXECUTION_ID"), out _) ||\n'
        '            Environment.GetEnvironmentVariable("LENS_QUALIFICATION_CONFIG_SHA256")?.Length != 64 ||\n'
        '            args.Length != 6 || args[2] != "mixed" || args[3] != "1" ||\n'
        '            Environment.GetEnvironmentVariable("LENS_NATIVE_COMPLETION_JOURNEY") != "1" ||\n'
        '            Environment.GetEnvironmentVariable("LENS_NATIVE_COMPLETION_SCENARIO") != "pixels" ||\n'
        '            Environment.GetEnvironmentVariable("LENS_NATIVE_COMPLETION_READER") != "primary" ||\n'
        '            Environment.GetEnvironmentVariable("LENS_NATIVE_COMPARISON_MODE") != "Pages")\n'
        '        { Console.Error.WriteLine("BLOCKED: separately authorised fixed completion execution required."); return 2; }')
    text = replace_once(text, 'private readonly StageListener stages = new();',
        'private readonly StageListener? stages = Environment.GetEnvironmentVariable("LENS_NATIVE_COMPLETION_JOURNEY") == "1" ? null : new();')
    # Ordinary journeys are not exposed by this launcher; retain their null contracts.
    text = text.replace('stages.', 'stages!.')
    text = replace_once(text, 'stages!.Dispose();', 'stages?.Dispose();')
    old = ('        window.Dispatcher.Hooks.OperationStarted += OperationStarted;\n'
           '        window.Dispatcher.Hooks.OperationCompleted += OperationCompleted;')
    text = replace_once(text, old, '        if (stages is not null)\n        {\n' + old + '\n        }')
    text = replace_once(text,
        '            Desktop.Save(Desktop.Capture(Desktop.ClientBounds(windowHandle)), Path.Combine(output, "failure.png"));',
        '            if (Environment.GetEnvironmentVariable("LENS_NATIVE_COMPLETION_JOURNEY") != "1")\n'
        '                Desktop.Save(Desktop.Capture(Desktop.ClientBounds(windowHandle)), Path.Combine(output, "failure.png"));\n'
        '            else Program.Write(output, "failure-frame-scope.json", new { extraCapture = false, reason = "Fixed-frame ceiling; prior frames and failure state retained" });')
    text = replace_once(text, 'runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, os = Environment.OSVersion.VersionString,',
        'runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, runtimeCore = Identity(typeof(object).Assembly),\n'
        '                runtimeVersion = Environment.Version.ToString(), genericObserverHooks = false, os = Environment.OSVersion.VersionString,')
    # A read-only scope assertion before the unchanged journey; no geometry repair.
    text = replace_once(text, 'try { if (completionJourney) await probe.RunCompletion(started);',
        'try { if (completionJourney) { await window.Dispatcher.InvokeAsync(() => {\n'
        '                    if (window.Width != 1280 || window.Height != 800 || ((DocumentPreview)window.FindName("Preview")).Zoom != 100)\n'
        '                        throw new IOException("Fixed 1280x800 / 100 percent completion scope unavailable.");\n'
        '                    Write(output, "qualification-scope.json", new { processId = Environment.ProcessId, window.Width, window.Height, zoom = ((DocumentPreview)window.FindName("Preview")).Zoom, genericObserverHooks = false });\n'
        '                }).Task.WaitAsync(TimeSpan.FromSeconds(30)); await probe.RunCompletion(started); }')
    return text


def preflight_source():
    """Extract the original sequence and diagnostic verbatim, with only a narrow entry."""
    text = blob(CANDIDATE, VALIDATION + 'NativeUxScreenshots.cs').decode('utf-8').replace('\r\n', '\n')
    sequence = text.split('            using var contrast = ', 1)[1].split('            using var display = ', 1)[0]
    sequence = '            using var contrast = ' + sequence
    diagnostic = text[text.index('    // Read only, after the original resource evaluation:'):text.rindex('\n}')]
    ratio = blob(CANDIDATE, VALIDATION + 'NativeSyntaxChecks.cs').decode().replace('\r\n', '\n')
    ratio = ratio[ratio.index('    internal static double Contrast('):ratio.rindex('\n}')]
    return ('using System.Diagnostics;\nusing System.Text.Json;\nusing System.Windows.Media;\n'
        'namespace LensDocsStudio.Native.Validation;\ninternal static class NativeUiChecks\n{\n'
        '    private static string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));\n'
        '    internal static void Preflight(string output, string executable, string revision)\n    {\n'
        '        const bool highContrast = true;\n' + sequence + '    }\n' + diagnostic + '\n}\n'
        'internal static class NativeSyntaxChecks\n{\n' + ratio + '\n}\n')


def project(product_root, product_revision, r01=False):
    # The caller verifies exact clean source roots. No product source is copied or patched.
    from xml.sax.saxutils import escape
    product = escape(str(product_root / 'src/native/LensDocsStudio.Native/LensDocsStudio.Native.csproj'))
    manifest = escape(str(product_root / 'src/native/LensDocsStudio.Native/app.manifest'))
    return f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0-windows</TargetFramework><UseWPF>true</UseWPF>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic><ApplicationManifest>{manifest}</ApplicationManifest>
    <AssemblyName>{'LensDocsStudio.Native.UxProbe' if r01 else 'LensDocsStudio.Native.Qualification'}</AssemblyName>
    <Version>0.1.0-preview.1</Version><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="{product}" /><Using Include="System.IO" /></ItemGroup>
</Project>
'''


def build_sources(destination, candidate_root, baseline_root):
    plan, identities = verify_sources()
    destination = Path(destination).resolve()
    if destination.exists():
        raise ValueError('Fresh output required; preparation never overwrites evidence')
    for root, revision in [(candidate_root, CANDIDATE), (baseline_root, BASELINE)]:
        if git('rev-parse', 'HEAD', root=root).decode().strip() != revision or git('status', '--porcelain', '--untracked-files=no', root=root).strip():
            raise ValueError('Product source must be the clean pinned commit: ' + str(root))
    destination.mkdir(parents=True)
    # Compile only exact Git blobs in isolated snapshots. A clean tracked diff is
    # insufficient: SDK globs could otherwise consume untracked or later edited .cs.
    snapshots = {}
    for label, revision in [('candidate', CANDIDATE), ('baseline', BASELINE)]:
        folder = destination / ('product-' + label)
        paths = ['src/native/Directory.Build.props', 'src/native/LensDocsStudio.Core',
                 'src/native/LensDocsStudio.Native', 'docs/native/user-guide.md']
        archive = git('archive', '--format=tar', revision, *paths)
        with tarfile.open(fileobj=io.BytesIO(archive)) as stream:
            for member in stream:
                if member.isdir(): continue
                if not member.isfile() or Path(member.name).is_absolute() or '..' in Path(member.name).parts:
                    raise ValueError('Unsafe product snapshot member')
                write_new(folder / member.name, stream.extractfile(member).read())
        snapshots[label] = folder
    fixtures = destination / 'fixtures'
    for name in [x['file'] for x in plan['corpus']['syntheticAnchors']] + ['manifest.json', 'pixel.png']:
        write_new(fixtures / name, blob(CANDIDATE, 'tests/fixtures/native-ux/' + name))
    r01 = destination / 'r01'
    originals = {}
    for path in git('ls-tree', '-r', '--name-only', CANDIDATE, PROBE).decode().splitlines():
        if not path.endswith('.cs'):
            continue
        data = blob(CANDIDATE, path)
        originals[path] = sha(data)
        name = Path(path).name
        if name == 'Program.cs':
            original = data.decode('utf-8')
            overlaid = completion_overlay(original)
            write_new(r01 / name, overlaid)
            write_new(destination / 'r01-overlay.diff', ''.join(difflib.unified_diff(original.replace('\r\n', '\n').splitlines(True), overlaid.splitlines(True), fromfile='pinned/Program.cs', tofile='qualification/Program.cs')))
        else:
            write_new(r01 / name, data)
    for name in ['NativeTestHighContrast.cs', 'NativeRegressionObservation.cs']:
        data = blob(CANDIDATE, VALIDATION + name)
        originals[VALIDATION + name] = sha(data)
        write_new(r01 / name, data)
    write_new(r01 / 'Qualification.csproj', project(snapshots['candidate'], CANDIDATE, True))
    for label, product_root in snapshots.items():
        folder = destination / ('r02-' + label)
        for path in (ROOT / 'scripts/native/qualification/observer').glob('*.cs'):
            write_new(folder / path.name, path.read_bytes())
        write_new(folder / 'OriginalPreflight.cs', preflight_source())
        write_new(folder / 'NativeTestHighContrast.cs', blob(CANDIDATE, VALIDATION + 'NativeTestHighContrast.cs'))
        for name in ['Desktop.cs', 'CompositionObservation.cs']:
            write_new(folder / name, blob(CANDIDATE, PROBE + name))
        write_new(folder / 'Qualification.csproj', project(product_root, CANDIDATE if label == 'candidate' else BASELINE))
    tool_files = sorted((ROOT / 'scripts/native/qualification').rglob('*'))
    receipt = {'schemaVersion': 1, 'status': 'SOURCE_PREPARATION_ONLY', 'identities': identities,
        'toolingCommit': git('rev-parse', 'HEAD').decode().strip(),
        'toolingDirty': bool(git('status', '--porcelain', '--untracked-files=all').strip()),
        'originalSources': originals,
        'productSnapshots': {label: folder.relative_to(destination).as_posix() for label, folder in snapshots.items()},
        'toolSources': {p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in tool_files if p.is_file() and '__pycache__' not in p.parts},
        'generated': {p.relative_to(destination).as_posix(): sha(p.read_bytes()) for p in sorted(destination.rglob('*')) if p.is_file()}}
    json_new(destination / 'source-receipt.json', receipt)
    return receipt
