"""Deterministic review inventory; --check is read-only and performs no collection."""
import argparse
import json
from pathlib import Path
from qualification import cells, template
from sources import ROOT, START, blob, sha, verify_sources


def inventory():
    plan, identities = verify_sources()
    paths = sorted(p for p in (ROOT/'scripts/native/qualification').rglob('*') if p.is_file() and '__pycache__' not in p.parts)
    paths += [ROOT/'tests/unit/test_native_qualification.py']
    return {'schemaVersion': 1, 'status': 'TOOLING_PREPARATION_NOT_EXECUTED', 'campaignExecutionApproved': False,
            'sourcePins': identities, 'preparationGitSha256': sha(blob(START, 'docs/native/qualification/goal77-preparation.json')),
            'toolFiles': {p.relative_to(ROOT).as_posix(): {'sha256CanonicalLf': sha(p.read_bytes().replace(b'\r\n',b'\n'))} for p in paths},
            'R01': {'cells': cells('R01'), 'boundaries': plan['r01']['fixedBoundaries'], 'framesPerBoundary': 3,
                    'maximumProcesses': 4, 'maximumFullFrames': 84, 'maximumCrops': 252, 'observationSeconds': 30},
            'R02': {'processes': cells('R02'), 'maximumProcesses': 18, 'preflight': 8, 'application': 8, 'stock': 2,
                    'maximumPressedFramesPerApplication': 3, 'contrastRequirement': 4.5, 'originalUiWaitSeconds': 20,
                    'firstWpfRead': 'Original NativeUxScreenshots sequence; entry/notification observer is Win32 only'},
            'supervision': template()['limits'], 'runtimePinsAndHost': 'REQUIRED_UNSELECTED',
            'binaryHashes': 'Generated clean-source build receipts required before execution; no invented runtime identities',
            'calibrationPilotEnabled': False, 'riskClosures': [], 'pr76Integration': 'NO-GO', 'operationalTransition': 'NOT_ACTIVATED'}


def main():
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument('--check',action='store_true'); args=parser.parse_args()
    documents={'goal77-execution.json':inventory(),'goal77-execution-config.template.json':template()}
    for name,value in documents.items():
        path=ROOT/'docs/native/qualification'/name
        text=json.dumps(value,indent=2,ensure_ascii=False,allow_nan=False)+'\n'
        if args.check:
            if path.read_text(encoding='utf-8') != text: raise ValueError('Execution inventory/config template drift: '+name)
        else: path.write_text(text,encoding='utf-8',newline='\n')
    print('EXECUTION_INVENTORY_VALIDATED_NOT_EXECUTED' if args.check else 'EXECUTION_INVENTORY_PREPARED_NOT_EXECUTED')


if __name__=='__main__': main()
