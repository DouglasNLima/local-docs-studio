"""Preserve grants for exact modules declared by Mammoth's upstream browser bundle.

This is supplier attribution evidence, not proof of complete nested-bundle closure.
Archives are inspected as data; no package scripts or downloaded code run.
"""
import base64
from concurrent.futures import ThreadPoolExecutor
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import tarfile
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'docs/open-source/evidence/goal58/mammoth-notices'


def fetch(url):
    parsed = urllib.parse.urlparse(url)
    if parsed.scheme != 'https' or parsed.hostname != 'registry.npmjs.org':
        raise ValueError('Unexpected registry source')
    with urllib.request.urlopen(url, timeout=45) as response:
        data = response.read(30 * 1024 * 1024 + 1)
    if len(data) > 30 * 1024 * 1024:
        raise ValueError('Registry response exceeds the bound')
    return data


def package(name, version):
    metadata = json.loads(fetch('https://registry.npmjs.org/' + urllib.parse.quote(name, safe='') + '/' + version))
    dist = metadata['dist']
    raw = fetch(dist['tarball'])
    algorithm, expected = dist['integrity'].split('-', 1)
    if algorithm not in ('sha512', 'sha256') or base64.b64encode(hashlib.new(algorithm, raw).digest()).decode() != expected:
        raise ValueError('Registry integrity mismatch: ' + name)
    return metadata, raw


def notices(identity):
    name, version = identity
    metadata, raw = package(name, version)
    records = []
    with tarfile.open(fileobj=io.BytesIO(raw), mode='r:gz') as archive:
        total = 0
        for member in archive:
            total += member.size
            if total > 200 * 1024 * 1024:
                raise ValueError('Uncompressed archive exceeds the bound')
            readme_grant = name == 'isarray' and version == '1.0.0' and member.name == 'package/README.md'
            if member.isfile() and (readme_grant or re.match(r'^(licen[cs]e|notice|copying)([._-]|$)', Path(member.name).name, re.I)):
                if member.size > 2 * 1024 * 1024:
                    raise ValueError('Notice exceeds the bound')
                content = archive.extractfile(member).read()
                upstream_path = member.name
                if readme_grant:
                    content = content[content.index(b'## License\n'):]
                    upstream_path += '#License (original section bytes)'
                digest = hashlib.sha256(content).hexdigest()
                (OUTPUT / (digest + '.txt')).write_bytes(content)
                records.append(dict(component=name + '@' + version, source=metadata['dist']['tarball'],
                                    upstreamPath=upstream_path, sha256=digest, file=digest + '.txt', evidenceSet='mammoth'))
    return dict(name=name, version=version, declaredLicence=metadata.get('license'),
                source=metadata['dist']['tarball'], integrity=metadata['dist']['integrity'],
                archiveSha256=hashlib.sha256(raw).hexdigest(), integrityVerified=True, notices=records)


def main():
    metadata, raw = package('mammoth', '1.12.0')
    expected = '5c0c97ec9f8422b6a48f67554fc3060616302ca52715fb6e18c38e798a1e633c'
    if hashlib.sha256(raw).hexdigest() != expected:
        raise ValueError('Pinned Mammoth archive changed')
    with tarfile.open(fileobj=io.BytesIO(raw), mode='r:gz') as archive:
        source = archive.extractfile('package/mammoth.browser.js').read()
        minified = archive.extractfile('package/mammoth.browser.min.js').read()
    original = subprocess.check_output(['git', 'show', 'HEAD:assets/vendor/mammoth-1.12.0.browser.min.js'], cwd=ROOT)
    if minified != original:
        raise ValueError('The shipped Mammoth bundle differs from the pinned archive')
    header = source[:source.index(b'(function(f)')]
    identities = re.findall(r'^// Module: (.+)@([^@\r\n]+)$', header.decode(), re.M)
    if len(identities) != 14:
        raise ValueError('Unexpected upstream supplier declaration')
    OUTPUT.mkdir(parents=True, exist_ok=True)
    with ThreadPoolExecutor(max_workers=4) as pool:
        components = list(pool.map(notices, identities))
    report = dict(source='https://registry.npmjs.org/mammoth/-/mammoth-1.12.0.tgz', archiveSha256=expected,
                  minifiedSha256=hashlib.sha256(minified).hexdigest(), minifiedMatchesTrackedAsset=True,
                  unminifiedSha256=hashlib.sha256(source).hexdigest(), supplierHeader=header.decode(),
                  sourceGitRevision=metadata.get('gitHead'), components=components,
                  limits='The unminified companion declares these exact modules. Its relationship to the minified companion is supplier evidence, not an independent full rebuild. Nested JSZip/browser polyfill closure remains unproved.')
    (OUTPUT.parent / 'mammoth-supplier-evidence.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    (OUTPUT / 'index.json').write_text(json.dumps([n for c in components for n in c['notices']], indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(declaredComponents=len(components), noticeReferences=sum(len(c['notices']) for c in components),
                          withoutNotice=[c['name'] for c in components if not c['notices']], completeNestedClosure=False)))


if __name__ == '__main__':
    main()
