"""Retrieve exact npm archives as audit evidence; inspect without extraction/execution."""
import argparse
import base64
import csv
import hashlib
import io
import json
import pathlib
import re
import tarfile
import urllib.parse
import urllib.request


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inventory', required=True, type=pathlib.Path)
    parser.add_argument('--output', required=True, type=pathlib.Path)
    args = parser.parse_args()
    inventory = json.loads(args.inventory.read_text(encoding='utf-8'))
    args.output.mkdir(parents=True, exist_ok=False)
    files = list(csv.DictReader(args.inventory.with_name('tracked-files.csv').open(encoding='utf-8')))
    vendor = {row['path']: row['sha256'] for row in files if row['path'].startswith('assets/vendor/')}
    matches = {path: [] for path in vendor}
    module_path_matches = {path: [] for path in vendor}
    extension_matches = {path: [] for path in vendor}
    results = []
    for package in inventory['npm']:
        record = dict(name=package['name'], version=package['version'], status='UNAVAILABLE')
        try:
            url = package['dist']['tarball']
            parsed = urllib.parse.urlparse(url)
            if parsed.scheme != 'https' or parsed.hostname != 'registry.npmjs.org':
                raise ValueError('Unexpected package source')
            with urllib.request.urlopen(url, timeout=45) as response:
                data = response.read(150 * 1024 * 1024 + 1)
            if len(data) > 150 * 1024 * 1024:
                raise ValueError('Archive too large')
            integrity = package['dist'].get('integrity')
            if integrity:
                algorithm, expected = integrity.split('-', 1)
                if algorithm not in ('sha512', 'sha256'):
                    raise ValueError('Unsupported integrity algorithm')
                actual = base64.b64encode(hashlib.new(algorithm, data).digest()).decode()
                if actual != expected:
                    raise ValueError('Archive integrity mismatch')
            elif hashlib.sha1(data).hexdigest() != package['dist']['shasum']:
                raise ValueError('Archive shasum mismatch')
            record.update(archiveSha256=hashlib.sha256(data).hexdigest(), source=url,
                          integrityVerified=True, notices=[], status='INSPECTED')
            with tarfile.open(fileobj=io.BytesIO(data), mode='r:gz') as archive:
                total = 0
                for member in archive:
                    if not member.isfile():
                        continue
                    total += member.size
                    if member.size > 60 * 1024 * 1024 or total > 400 * 1024 * 1024:
                        raise ValueError('Uncompressed archive exceeds audit limit')
                    content = archive.extractfile(member).read()
                    digest = hashlib.sha256(content).hexdigest()
                    for path, expected in vendor.items():
                        if digest == expected:
                            matches[path].append(f'{package["name"]}@{package["version"]}:{member.name}')
                    if package['name'] == 'mermaid' and member.name.endswith('.mjs'):
                        remapped = re.sub(rb'(["\x27])([^"\x27]+)\.mjs\1', rb'\1\2.js\1', content)
                        remapped_digest = hashlib.sha256(remapped).hexdigest()
                        for path, expected in vendor.items():
                            if remapped_digest == expected and digest != expected:
                                module_path_matches[path].append(member.name)
                    if package['name'] in ('mermaid', 'dompurify') and member.name.endswith('.mjs'):
                        extension_digest = hashlib.sha256(content.replace(b'.mjs', b'.js')).hexdigest()
                        for path, expected in vendor.items():
                            if extension_digest == expected and digest != expected and not module_path_matches[path]:
                                extension_matches[path].append(member.name)
                    if re.search(r'^(licen[cs]e|notice|copying|third.party.notices)(\.|$)', pathlib.PurePosixPath(member.name).name, re.I):
                        # Use the content hash as the local name, never an archive-controlled path.
                        target = args.output / (digest + '.txt')
                        target.write_bytes(content)
                        record['notices'].append(dict(upstreamPath=member.name, file=target.name, sha256=digest))
        except Exception as error:
            record.update(status='INSPECTION_FAILED', errorType=type(error).__name__)
        results.append(record)
    report = dict(sourceRevision=inventory['sourceRevision'], sourceTree=inventory['sourceTree'],
                  packages=results, exactVendorMatches=matches,
                  mermaidModulePathOnlyMatches=module_path_matches,
                  moduleExtensionOnlyMatches=extension_matches,
                  transformation='Quoted .mjs module paths changed to .js; all other bytes identical to the Git blob.',
                  extensionTransformation='All .mjs extensions changed to .js, including licence-comment paths or sourceMappingURL; all other bytes identical.',
                  limits='Archive integrity authenticates bytes against registry metadata; does not certify ownership or a complete transitive inventory.')
    (args.output / 'archive-evidence.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(packages=len(results), failed=sum(x['status'] != 'INSPECTED' for x in results),
                          vendorFiles=len(vendor), exactMatches=sum(bool(v) for v in matches.values()),
                          modulePathOnlyMatches=sum(bool(v) for v in module_path_matches.values()),
                          extensionOnlyMatches=sum(bool(v) for v in extension_matches.values()))))


if __name__ == '__main__':
    main()
