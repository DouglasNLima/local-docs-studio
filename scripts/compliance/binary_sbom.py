"""Map every portable payload file to exact NuGet bytes; never execute a payload.

Reuses the bounded distribution inspector. Registry verification is opt-in and
compares complete official archives with the local archives used for matching.
Declared licences are not a file-level legal conclusion.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import re
import subprocess
import urllib.request
import zipfile

from inspect_distribution import inspect


def package_record(item, cache, declarations, fetch_registry):
    name, version = item.split('/')
    if not re.fullmatch(r'[A-Za-z0-9_.-]+', name) or not re.fullmatch(r'[0-9A-Za-z.+-]+', version):
        raise ValueError('Unsafe package identity')
    filename = f'{name.lower()}.{version.lower()}.nupkg'
    archive = cache / name.lower() / version.lower() / filename
    raw = archive.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    url = f'https://api.nuget.org/v3-flatcontainer/{name.lower()}/{version.lower()}/{filename}'
    if fetch_registry:
        remote_hash = hashlib.sha256()
        total = 0
        with urllib.request.urlopen(url, timeout=90) as response:
            while chunk := response.read(1024 * 1024):
                total += len(chunk)
                if total > 200 * 1024 * 1024:
                    raise ValueError('Registry archive exceeds the bound')
                remote_hash.update(chunk)
        if remote_hash.hexdigest() != digest:
            raise ValueError('Registry archive differs from cache: ' + item)
    declared = declarations.get((name.lower(), version), {})
    entries = {}
    with zipfile.ZipFile(archive) as package:
        total = 0
        for member in package.infolist():
            if member.is_dir():
                continue
            total += member.file_size
            if member.file_size > 200 * 1024 * 1024 or total > 600 * 1024 * 1024:
                raise ValueError('Uncompressed package exceeds the bound')
            h = hashlib.sha256(package.read(member)).hexdigest()
            entries.setdefault(h, []).append(member.filename)
    return dict(id=item, source=url, archiveSha256=digest,
                registryArchiveVerified=fetch_registry,
                declaredLicence=declared.get('licence', 'NOASSERTION'),
                declarationType=declared.get('licenceType'),
                sourceRepository=declared.get('repository'),
                scope='Upstream package bytes; not first-party MIT or independent source-build proof'), entries


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--inventory', type=Path, default=Path('docs/open-source/evidence/inventory.json'))
    parser.add_argument('--nuget-cache', type=Path, default=Path.home() / '.nuget/packages')
    parser.add_argument('--fetch-registry', action='store_true')
    parser.add_argument('--expected-source', help='Expected immutable source commit; defaults to the current HEAD')
    args = parser.parse_args()
    distribution = inspect(args.archive)
    if distribution['findings']:
        raise ValueError('Distribution scanner findings require review before generating the SBOM')
    with zipfile.ZipFile(args.archive) as payload:
        names = payload.namelist()
        if len(names) != len(set(names)):
            raise ValueError('Duplicate payload names')
        deps = json.loads(payload.read('LensDocsStudio.Native.deps.json'))
        metadata = json.loads(payload.read('version.json'))
        runtime = json.loads(payload.read('LensDocsStudio.Native.runtimeconfig.json'))
    source = metadata['sourceRevision']
    if not re.fullmatch(r'[0-9a-f]{40}', source):
        raise ValueError('Invalid source revision')
    expected_source = args.expected_source or subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
    if source != expected_source or metadata.get('sourceDirty') is not False or metadata.get('product') != 'LensDocsStudio.Native':
        raise ValueError('Candidate identity or clean source does not match the expected native build')
    tree = subprocess.check_output(['git', 'rev-parse', source + '^{tree}'], text=True).strip()
    if tree != metadata['sourceTree']:
        raise ValueError('Source tree differs from package metadata')
    packages = [k for k, v in deps['libraries'].items() if v['type'] == 'package']
    frameworks = runtime['runtimeOptions']['includedFrameworks']
    for framework in frameworks:
        if framework['name'] not in ('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App'):
            raise ValueError('Unexpected runtime framework')
        packages.append(framework['name'] + '.Runtime.win-x64/' + framework['version'])
    inventory = json.loads(args.inventory.read_text(encoding='utf-8'))
    declarations = {(p['name'].lower(), p['version']): p for p in inventory['nuget']}
    with ThreadPoolExecutor(max_workers=4) as pool:
        results = list(pool.map(lambda p: package_record(p, args.nuget_cache, declarations, args.fetch_registry), sorted(set(packages))))
    digest_index = {}
    components = []
    for record, entries in results:
        components.append(record)
        for digest, members in entries.items():
            digest_index.setdefault(digest, []).append(dict(component=record['id'], archiveMembers=members))
    for file in distribution['files']:
        file['licenceConcluded'] = 'NOASSERTION'
        file['upstreamMatches'] = digest_index.get(file['sha256'], [])
        if file['upstreamMatches']:
            file['originEvidence'] = 'Exact SHA-256 match to package member bytes'
            file['firstPartyMIT'] = 'NOT APPLICABLE'
        elif file['path'] in ('LensDocsStudio.Core.dll', 'LensDocsStudio.Native.dll', 'LensDocsStudio.Native.exe'):
            file['originEvidence'] = 'CI output bound by version.json; no independent deterministic binary rebuild claimed'
            file['firstPartyMIT'] = 'OWNER ACTION REQUIRED'
        elif file['path'].startswith('licences/') or file['path'] == 'THIRD-PARTY-NOTICES.md':
            file['originEvidence'] = 'Separately attributed notice; not a first-party copyright grant'
            file['firstPartyMIT'] = 'NOT APPLICABLE'
        else:
            file['originEvidence'] = 'Generated metadata, guide or separately attributed notice; inspect its path and contents'
            file['firstPartyMIT'] = 'OWNER ACTION REQUIRED'
    report = dict(schemaVersion=1, kind='file-level binary SBOM with exact-byte origin evidence',
                  sourceRevision=source, sourceTree=tree, versionMetadata=metadata,
                  runtimeFrameworks=frameworks, components=components, distribution=distribution,
                  limits=[
                      'Source/dependency attribution is distinct from proof of the shipped bytes.',
                      'Exact package matches do not prove independent upstream source-to-binary reproducibility or ownership.',
                      'Embedded resources retain component notices, including WpfMath font and runtime subcomponent terms.',
                      'The portable ZIP is fully enumerated. Installer container provenance is recorded separately.',
                      'No malware verdict, licence-compatibility certification or production-signing approval.',
                  ])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('x', encoding='utf-8') as stream:
        json.dump(report, stream, indent=2)
        stream.write('\n')
    print(json.dumps(dict(files=len(distribution['files']), components=len(components),
                          exactMatches=sum(bool(f['upstreamMatches']) for f in distribution['files']),
                          registryVerified=sum(c['registryArchiveVerified'] for c in components))))


if __name__ == '__main__':
    main()
