"""Check recorded provenance, notice integrity, path decisions and local doc links."""
import csv
import hashlib
import json
from pathlib import Path
import re
import subprocess

from rights_ledger import boundary

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/open-source/evidence/goal58'


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8'))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    build = read_json(EVIDENCE / 'highlight-reconstruction.json')
    require(build['exactHistoricalReconstruction'] and build['exactHistoricalBody'], 'Historical reconstruction is not exact')
    vendor = subprocess.check_output(['git', 'show', 'HEAD:assets/vendor/highlight-11.11.1.esm.js'], cwd=ROOT)
    require(hashlib.sha256(vendor).hexdigest() == build['candidateSha256'], 'Tracked Highlight.js differs from the reconstructed bytes')
    generated = ROOT / 'test-results/highlight-rebuild/build-result.json'
    if generated.exists():
        current = read_json(generated)
        require(current['candidateSha256'] == build['candidateSha256'] and current['exactHistoricalReconstruction'], 'Fresh reconstruction failed')
    upstream = read_json(EVIDENCE / 'highlight-upstream.json')
    require(upstream['archive']['verified'] and len(upstream['sources']) == 195, 'Incomplete historical map evidence')
    require(sum(s['result'] == 'EXACT_ARCHIVE_BYTES' for s in upstream['sources']) == 194, 'Incomplete upstream source comparison')

    pack = read_json(EVIDENCE / 'web-notice-pack.json')
    pack_bytes = (ROOT / pack['noticePack']).read_bytes()
    require(hashlib.sha256(pack_bytes).hexdigest() == pack['sha256'], 'Web notice pack hash mismatch')
    for record in pack['notices']:
        original = (ROOT / 'docs/open-source/evidence/upstream-notices' / record['file']).read_bytes()
        require(hashlib.sha256(original).hexdigest() == record['sha256'], 'Original notice hash mismatch: ' + record['file'])
        require(original in pack_bytes, 'Original notice bytes omitted from the web pack')
    require(not pack['componentsWithoutIndexedNotice'] and not pack['completeEmbeddedClosure'], 'Incorrect notice completeness claim')
    manifest = read_json(ROOT / 'assets/vendor/manifest.json')
    require('./' + pack['noticePack'] in manifest, 'Web notices are not cached')

    sbom = read_json(EVIDENCE / 'native-preview-957-sbom.json')
    files = sbom['distribution']['files']
    require(len(files) == 537 and len({f['path'] for f in files}) == 537, 'Incomplete or duplicate native file table')
    require(all(re.fullmatch(r'[0-9a-f]{64}', f['sha256']) and f['licenceConcluded'] == 'NOASSERTION' for f in files), 'Invalid native hash/licence assertion')
    component_ids = {c['id'] for c in sbom['components']}
    require(len(component_ids) == 20 and all(c['registryArchiveVerified'] for c in sbom['components']), 'Missing registry archive verification')
    require(sum(bool(f['upstreamMatches']) for f in files) == 522, 'Native origin matching count changed')
    require(all(m['component'] in component_ids and m['archiveMembers'] for f in files for m in f['upstreamMatches']), 'Invalid native component reference')
    require(sbom['sourceRevision'] == sbom['versionMetadata']['sourceRevision'] and sbom['sourceTree'] == sbom['versionMetadata']['sourceTree'], 'Native source metadata mismatch')

    tracked = set(subprocess.check_output(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=ROOT).decode().split('\0')) - {''}
    rows = list(csv.DictReader((EVIDENCE / 'rights-ledger.csv').open(encoding='utf-8', newline='')))
    require(len(rows) == len({r['path'] for r in rows}) and {r['path'] for r in rows} == tracked, 'Rights ledger does not cover the current tracked/proposed paths')
    for row in rows:
        decision, status, _ = boundary(row['path'])
        require((row['decisionId'], row['ownerDecision']) == (decision, status), 'Incorrect rights boundary: ' + row['path'])
    codeowners = (ROOT / '.github/CODEOWNERS').read_text(encoding='utf-8')
    require('* @DouglasNLima' in codeowners and all(line.strip().endswith('@DouglasNLima') for line in codeowners.splitlines() if line.strip() and not line.startswith('#')), 'Unverified CODEOWNERS identity')

    checked_links = 0
    docs = list((ROOT / 'docs/open-source').glob('*.md')) + [ROOT / 'scripts/compliance/highlight/README.md']
    for doc in docs:
        for target in re.findall(r'\]\(([^)]+)\)', doc.read_text(encoding='utf-8')):
            if re.match(r'^[a-z]+:', target) or target.startswith('#'):
                continue
            path = target.split('#', 1)[0]
            require((doc.parent / path).exists(), f'Missing local link in {doc.relative_to(ROOT)}: {target}')
            checked_links += 1
    print(json.dumps(dict(highlight='exact historical bytes', originalWebNotices=len(pack['notices']),
                          nativeFiles=len(files), exactNativeMatches=522, rightsPaths=len(rows),
                          localLinks=checked_links, operativeLicenceApproval=False)))


if __name__ == '__main__':
    main()
