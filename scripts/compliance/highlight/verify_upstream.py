"""Verify the historical map and every installed Highlight.js source against npm."""
import base64
import hashlib
import io
import json
from pathlib import Path
import tarfile
import urllib.request

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ARCHIVE = 'https://registry.npmjs.org/highlight.js/-/highlight.js-11.11.1.tgz'
ARCHIVE_SHA = 'fbc9443263e976583bd5640e550b77f84d8d7e19fc8408dfe53776a5bae7ed39'
MAP = 'https://cdn.jsdelivr.net/sm/28ec7303afe7b37888f587daa79c82654af73c2e239f8f61fee3a3b1e6b716e1.map'
MAP_SHA = 'b850e00e00a5006f0c484e0751063281a9d3a8789ef18acd59769612288ac06d'
ORIGINAL_SHA = '980aa9a979a537d5f05c76d313c5f8b48f0bccba293a75e3c271975fce968aa0'


def fetch(url, expected=None):
    with urllib.request.urlopen(url, timeout=45) as response:
        data = response.read(10 * 1024 * 1024 + 1)
    if len(data) > 10 * 1024 * 1024:
        raise ValueError('Evidence exceeds the download bound')
    if expected and hashlib.sha256(data).hexdigest() != expected:
        raise ValueError('Upstream SHA-256 mismatch: ' + url)
    return data


def main():
    archive = fetch(ARCHIVE, ARCHIVE_SHA)
    lock = json.loads((HERE / 'package-lock.json').read_text())
    integrity = lock['packages']['node_modules/highlight.js']['integrity']
    algorithm, expected = integrity.split('-', 1)
    if algorithm != 'sha512' or base64.b64encode(hashlib.sha512(archive).digest()).decode() != expected:
        raise ValueError('Pinned npm integrity mismatch')
    source_map = json.loads(fetch(MAP, MAP_SHA))
    if len(source_map['sources']) != len(source_map['sourcesContent']):
        raise ValueError('Incomplete historical source map')
    records = []
    installed_count = 0
    with tarfile.open(fileobj=io.BytesIO(archive), mode='r:gz') as package:
        members = {m.name: m for m in package.getmembers() if m.isfile()}
        for name, member in members.items():
            relative = Path(name.removeprefix('package/'))
            if relative.is_absolute() or '..' in relative.parts or member.size > 10 * 1024 * 1024:
                raise ValueError('Unsafe upstream archive member')
            target = HERE / 'node_modules/highlight.js' / relative
            if not target.is_file() or target.read_bytes() != package.extractfile(member).read():
                raise ValueError('Installed source differs from archive: ' + name)
            installed_count += 1
        for name, content in zip(source_map['sources'], source_map['sourcesContent'], strict=True):
            raw = content.encode('utf-8')
            upstream = 'package/' + name.removeprefix('/npm/highlight.js@11.11.1/')
            if name == 'jsdelivr-header.js':
                status = 'CDN-generated header; not an upstream source'
            else:
                if upstream not in members or package.extractfile(members[upstream]).read() != raw:
                    raise ValueError('Historical map source differs from archive: ' + name)
                status = 'EXACT_ARCHIVE_BYTES'
            records.append(dict(source=name, sha256=hashlib.sha256(raw).hexdigest(), bytes=len(raw), result=status))
    report = dict(schemaVersion=1, upstreamVersion='11.11.1', originalBundleSha256=ORIGINAL_SHA,
                  archive=dict(url=ARCHIVE, sha256=ARCHIVE_SHA, integrity=integrity, verified=True),
                  historicalMap=dict(url=MAP, sha256=MAP_SHA, sourceCount=len(records)),
                  installedFilesVerified=installed_count, sources=records,
                  limitation='The map authenticates its source contents against npm. It does not independently authenticate the old minifier output or prove its build recipe.')
    output = ROOT / 'test-results/highlight-rebuild'
    output.mkdir(parents=True, exist_ok=True)
    (output / 'upstream-verification.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(installedFilesVerified=installed_count, exactMapSources=len(records) - 1,
                          syntheticMapSources=1, archiveSha256=ARCHIVE_SHA)))


if __name__ == '__main__':
    main()
