"""Read ZIP contents without extraction or execution; emit redacted audit evidence."""
import argparse
import hashlib
import json
import pathlib
import re
import zipfile


def inspect(path):
    records = []
    findings = []
    metadata = []
    with zipfile.ZipFile(path) as archive:
        total = 0
        for member in archive.infolist():
            if member.is_dir():
                continue
            total += member.file_size
            if member.file_size > 200 * 1024 * 1024 or total > 2 * 1024 * 1024 * 1024:
                raise ValueError('Archive exceeds the audit limit')
            data = archive.read(member)
            records.append(dict(path=member.filename, bytes=len(data), sha256=hashlib.sha256(data).hexdigest()))
            if re.search(r'\.(pfx|p12|pem|key|env)$', member.filename, re.I):
                findings.append(dict(path=member.filename, rule='sensitive-file-extension'))
            for rule, pattern in {
                'private-key': rb'-----BEGIN (?:RSA |EC |OPENSSH |DSA |ENCRYPTED )?PRIVATE KEY-----',
                'github-token': rb'\b(?:gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{60,})\b',
                'aws-access-key': rb'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b',
            }.items():
                if re.search(pattern, data):
                    findings.append(dict(path=member.filename, rule=rule))
            if member.filename == 'version.json':
                value = json.loads(data)
                metadata.append({k: value.get(k) for k in ['product', 'version', 'sourceRevision', 'sourceTree', 'internalPreview', 'signingProvider']})
    with path.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return dict(archive=path.name, sha256=digest, bytes=path.stat().st_size,
                files=records, findings=findings, version=metadata,
                noticePaths=[r['path'] for r in records if re.search(r'licen[cs]e|notice', r['path'], re.I)])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, type=pathlib.Path)
    parser.add_argument('archives', nargs='+', type=pathlib.Path)
    args = parser.parse_args()
    report = dict(archives=[inspect(path) for path in args.archives],
                  limits='Read-only ZIP member bytes and names; no executable launch, nested archive decompression, OCR, malware verdict, ownership clearance or complete secret detection.')
    with args.output.open('x', encoding='utf-8') as stream:
        json.dump(report, stream, indent=2)
        stream.write('\n')
    print(json.dumps([dict(archive=a['archive'], files=len(a['files']), notices=len(a['noticePaths']),
                           findings=len(a['findings'])) for a in report['archives']]))


if __name__ == '__main__':
    main()
