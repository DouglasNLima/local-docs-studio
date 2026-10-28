"""Collect source-labelled licensing evidence; never grant rights or sign a release.

Standard library only. Network access is opt-in and limited to npm metadata.
NuGet evidence comes from restored packages and is checked against lock hashes.
The optional history scan reports rule IDs and blob IDs, never matched text.
"""
import argparse
import base64
import collections
import csv
import hashlib
import json
import pathlib
import re
import subprocess
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET


def git(*args):
    return subprocess.check_output(['git', *args])


def sha(data):
    return hashlib.sha256(data).hexdigest()


def category(path):
    if path.startswith('assets/vendor/'):
        return 'upstream-web'
    if '/PinnedMermaider/' in path:
        return 'adapted-upstream'
    if path.startswith(('docs/native/images/', 'docs/native/evidence/')):
        return 'recorded-evidence'
    if path.endswith(('.png', '.jpg', '.jpeg', '.svg', '.ico')):
        return 'artwork-or-fixture'
    if path.startswith('tests/fixtures/'):
        return 'fixture'
    if path.startswith('src/native/'):
        return 'native-source'
    if path.startswith('src/windows/'):
        return 'legacy-shell-source'
    if path.startswith(('scripts/', 'installer/', '.github/')):
        return 'build-test-distribution'
    if path.startswith('docs/'):
        return 'documentation'
    return 'web-or-project-source'


def blob_stream(objects):
    # Feed one object at a time to avoid pipe deadlock with large source maps.
    process = subprocess.Popen(['git', 'cat-file', '--batch'], stdin=subprocess.PIPE,
                               stdout=subprocess.PIPE)
    try:
        for oid in objects:
            process.stdin.write((oid + '\n').encode())
            process.stdin.flush()
            header = process.stdout.readline().split()
            size = int(header[2])
            data = process.stdout.read(size)
            if len(data) != size or process.stdout.read(1) != b'\n':
                raise RuntimeError('Incomplete Git object read')
            yield oid, header[1].decode(), data
    finally:
        process.stdin.close()
        process.stdout.close()
        process.wait()
        if process.returncode:
            raise RuntimeError('Git object reader failed')


def nuget_record(name, version, references, cache):
    root = cache / name.lower() / version.lower()
    specs = list(root.glob('*.nuspec'))
    result = dict(name=name, version=version, references=references,
                  source=f'https://www.nuget.org/packages/{name}/{version}',
                  status='MISSING_RESTORED_METADATA')
    if not specs:
        return result
    elements = {e.tag.split('}')[-1]: e for e in ET.parse(specs[0]).getroot().iter()}
    licence = elements.get('license')
    for key in ['authors', 'copyright', 'licenseUrl', 'projectUrl']:
        result[key] = elements[key].text if key in elements else None
    result['licence'] = licence.text if licence is not None else None
    result['licenceType'] = licence.get('type') if licence is not None else None
    result['repository'] = elements['repository'].attrib if 'repository' in elements else None
    result['metadataSha256'] = sha(specs[0].read_bytes())
    result['notices'] = [dict(path=f.relative_to(root).as_posix(), sha256=sha(f.read_bytes()))
                         for f in sorted(root.rglob('*')) if f.is_file()
                         and re.search(r'licen[cs]e|notice', f.name, re.I)]
    archive = root / f'{name.lower()}.{version.lower()}.nupkg'
    hashes = {r['contentHash'] for r in references if r.get('contentHash')}
    cache_metadata = root / '.nupkg.metadata'
    result['lockMatchesCacheMetadata'] = bool(hashes) and cache_metadata.is_file() and hashes == {
        json.loads(cache_metadata.read_text(encoding='utf-8-sig')).get('contentHash')}
    if archive.is_file():
        content = archive.read_bytes()
        result['archiveSha256'] = sha(content)
        digest = base64.b64encode(hashlib.sha512(content).digest()).decode()
        sidecar = root / (archive.name + '.sha512')
        result['archiveMatchesCacheSidecar'] = sidecar.is_file() and sidecar.read_text().strip() == digest
    else:
        result['archiveMatchesCacheSidecar'] = False
    result['status'] = 'METADATA_REVIEWED' if licence is not None else 'LICENCE_LINK_REVIEW_REQUIRED'
    if hashes and not result['lockMatchesCacheMetadata']:
        result['status'] = 'CACHE_LOCK_MISMATCH'
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--revision', default='HEAD')
    parser.add_argument('--output', required=True)
    parser.add_argument('--nuget-cache', type=pathlib.Path,
                        default=pathlib.Path.home() / '.nuget/packages')
    parser.add_argument('--fetch-npm', action='store_true')
    parser.add_argument('--history', action='store_true')
    args = parser.parse_args()
    revision = git('rev-parse', '--verify', args.revision + '^{commit}').decode().strip()
    tree = git('rev-parse', revision + '^{tree}').decode().strip()
    output = pathlib.Path(args.output)
    output.mkdir(parents=True, exist_ok=False)
    entries = []
    for record in git('ls-tree', '-rz', '--full-tree', revision).split(b'\0'):
        if not record:
            continue
        fields, path = record.split(b'\t', 1)
        mode, kind, oid = fields.decode().split()
        entries.append(dict(path=path.decode(), mode=mode, kind=kind, blob=oid))
    data_by_path = {}
    by_oid = collections.defaultdict(list)
    for entry in entries:
        by_oid[entry['blob']].append(entry)
    for oid, kind, data in blob_stream(by_oid):
        for entry in by_oid[oid]:
            entry.update(bytes=len(data), sha256=sha(data), category=category(entry['path']))
            data_by_path[entry['path']] = data
    with (output / 'tracked-files.csv').open('w', newline='', encoding='utf-8') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(entries[0]))
        writer.writeheader()
        writer.writerows(entries)

    packages = collections.defaultdict(list)
    npm = collections.defaultdict(set)
    for path, data in data_by_path.items():
        if path.endswith('packages.lock.json'):
            for framework, group in json.loads(data)['dependencies'].items():
                for name, dep in group.items():
                    if dep['type'] != 'Project':
                        packages[(name, dep['resolved'])].append(dict(
                            path=path, framework=framework, type=dep['type'], contentHash=dep['contentHash']))
        if path.startswith('assets/vendor/') and path.endswith('.map'):
            for source in json.loads(data)['sources']:
                match = re.search(r'\.pnpm/([^/]+)', source)
                if match:
                    pinned = match[1].split('_')[0]
                    name, version = pinned.rsplit('@', 1)
                    npm[(name.replace('+', '/'), version)].add(path)
    # Top-level browser distribution versions are explicit in the tracked filenames.
    web = [('dompurify', '3.4.5'), ('fflate', '0.8.2'), ('@highlightjs/cdn-assets', '11.11.1'),
           ('katex', '0.16.25'), ('mammoth', '1.12.0'), ('marked', '16.4.2'),
           ('mermaid', '11.15.0'), ('pdfjs-dist', '5.7.284')]
    for name, version in web:
        npm[(name, version)].add('assets/vendor/manifest.json (top-level distribution)')
    for path, package in json.loads(data_by_path['package-lock.json'])['packages'].items():
        if path:
            npm[(path.removeprefix('node_modules/'), package['version'])].add('package-lock.json (test tooling)')
    # Native self-contained runtime and legacy direct packages are outside native locks.
    extras = [('Microsoft.NETCore.App.Runtime.win-x64', '10.0.13', 'native runtime'),
              ('Microsoft.WindowsDesktop.App.Runtime.win-x64', '10.0.13', 'native runtime'),
              ('Microsoft.WindowsAppSDK', '2.1.3', 'legacy shell, direct only'),
              ('Microsoft.Web.WebView2', '1.0.3967.48', 'legacy shell, direct only')]
    for name, version, scope in extras:
        packages[(name, version)].append(dict(path=scope, type='outside-lock', contentHash=None))
    nuget = [nuget_record(name, version, refs, args.nuget_cache)
             for (name, version), refs in sorted(packages.items())]
    browser = []
    for (name, version), paths in sorted(npm.items()):
        url = f'https://registry.npmjs.org/{urllib.parse.quote(name, safe="")}/{version}'
        record = dict(name=name, version=version, evidence=sorted(paths), source=url,
                      status='METADATA_NOT_FETCHED')
        if args.fetch_npm:
            try:
                with urllib.request.urlopen(url, timeout=30) as response:
                    raw = response.read()
                metadata = json.loads(raw)
                record.update(licence=metadata.get('license'), repository=metadata.get('repository'),
                              dist=metadata.get('dist'), metadataSha256=sha(raw),
                              status='METADATA_REVIEWED')
            except Exception as error:
                record.update(status='METADATA_UNAVAILABLE', errorType=type(error).__name__)
        browser.append(record)

    report = dict(schemaVersion=1, sourceRevision=revision, sourceTree=tree,
                  trackedFiles=len(entries), categories=dict(collections.Counter(e['category'] for e in entries)),
                  nuget=nuget, npm=browser,
                  limits=['Package metadata is evidence, not a grant for first-party code.',
                          'Source-map package versions do not prove complete bundle provenance.',
                          'Legacy shell transitive closure is not locked or certified here.',
                          'Image content, archive contents and ownership require human review.'])
    if args.history:
        # High-confidence patterns only. No matched bytes, email addresses or private paths leave the scanner.
        rules = {
            'private-key': rb'-----BEGIN (?:RSA |EC |OPENSSH |DSA |ENCRYPTED )?PRIVATE KEY-----',
            'github-token': rb'\b(?:gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{60,})\b',
            'aws-access-key': rb'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b',
            'slack-token': rb'\bxox[baprs]-[A-Za-z0-9-]{20,}\b',
            'credential-url': rb'https?://[^\s/:"<>]{1,80}:[^\s/@"<>]{4,120}@[A-Za-z0-9.-]+',
        }
        refs = git('for-each-ref', '--format=%(objectname) %(refname)').decode().splitlines()
        objects = git('rev-list', '--objects', '--all').decode().splitlines()
        findings = []
        counts = collections.Counter()
        for oid, kind, data in blob_stream(line.split(' ', 1)[0] for line in objects):
            if kind != 'blob':
                continue
            counts['blobs'] += 1
            counts['bytes'] += len(data)
            for rule, pattern in rules.items():
                hits = len(re.findall(pattern, data))
                if hits:
                    findings.append(dict(blob=oid, rule=rule, count=hits))
        report['historyScan'] = dict(refs=refs, counts=dict(counts), findings=findings,
                                    limits='Pattern screening only; not proof of absence. Unfetched refs, LFS, release downloads, visual/OCR and unknown secret formats are outside this scan.')
    (output / 'inventory.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(sourceRevision=revision, trackedFiles=len(entries), nuget=len(nuget),
                          npm=len(browser), output=str(output),
                          historyFindings=len(report.get('historyScan', {}).get('findings', [])))))


if __name__ == '__main__':
    main()
