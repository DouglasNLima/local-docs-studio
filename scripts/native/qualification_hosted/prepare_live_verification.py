"""Compile/seal an exact committed collector locally. NEVER execute the collector."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile

sys.path.insert(0,str(Path(__file__).resolve().parent))
from live_verification import ROOT, BOUNDS, COLLECTOR_SOURCES, IMPLEMENTATION, validate_config
from discover_desktop import digest, read_json, write


def prepare(revision, output):
    if os.environ.get('GITHUB_ACTIONS') or os.name != 'nt': raise ValueError('Local compile-only preparation required')
    git=lambda *args:subprocess.check_output(['git','-C',str(ROOT),*args])
    if git('rev-parse','HEAD').decode().strip()!=revision or git('status','--porcelain'):
        raise ValueError('Exact clean source commit required')
    output=output.resolve()
    if not output.is_relative_to(ROOT/'artifacts') or output.exists():raise ValueError('New artifacts directory required')
    output.mkdir(parents=True);source=output/'source';source.mkdir()
    receipt=dict(status='INCOMPLETE',sourceCommit=revision,sourceTree=git('rev-parse',revision+'^{tree}').decode().strip(),
                 sourceFiles={},collectorExecuted=False,sdk='10.0.401',runtimeTarget='10.0.14')
    try:
        for path in COLLECTOR_SOURCES:
            data=git('show',revision+':'+path);(source/Path(path).name).write_bytes(data)
            receipt['sourceFiles'][path]=hashlib.sha256(data).hexdigest()
        write(source/'global.json',dict(sdk=dict(version='10.0.401',rollForward='disable',allowPrerelease=False)))
        sdk=subprocess.check_output(['dotnet','--version'],cwd=source).decode().strip()
        if sdk!='10.0.401':raise ValueError('Wrong build SDK')
        args=['dotnet','build','DesktopProbe.csproj','-c','Release','-o',str(output/'collector'),'--nologo',
              '-p:SourceRevisionId='+revision,'-p:InformationalVersion=1.0.0+'+revision,
              '-p:IncludeSourceRevisionInInformationalVersion=false','-p:ContinuousIntegrationBuild=true',
              '-p:PathMap='+str(source)+'=/_/goal77/desktop_probe']
        with (output/'build.stdout.txt').open('xb') as stdout,(output/'build.stderr.txt').open('xb') as stderr:
            result=subprocess.run(args,cwd=source,stdout=stdout,stderr=stderr,timeout=180)
        receipt.update(command=args,exitCode=result.returncode)
        if result.returncode:raise ValueError('Compile-only preparation failed; original logs retained')
        files={p.name:digest(p) for p in (output/'collector').iterdir() if p.is_file()}
        archive=output/'collector.zip'
        with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED) as z:
            for name in sorted(files):
                info=zipfile.ZipInfo(name,date_time=(2026,11,8,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED
                z.writestr(info,(output/'collector'/name).read_bytes())
        original=read_json(ROOT/'docs/native/evidence/hosted-environment-lock/environment-lock.json')
        core_archive=ROOT/'artifacts/goal77-live-readiness/runtime.zip'
        definition=original['runtimes']['10.0.14']['archives']['runtime']
        if digest(core_archive)!=definition['sha256']:raise ValueError('Observed runtime archive missing or changed')
        with zipfile.ZipFile(core_archive) as z:
            runtime_files={m.filename:hashlib.sha256(z.read(m)).hexdigest() for m in z.infolist()}
        if not all(original['runtimes']['10.0.14']['files'][p]==value for p,value in runtime_files.items()):raise ValueError('Runtime differs from original lock')
        implementation={path:hashlib.sha256(git('show',revision+':'+path)).hexdigest() for path in IMPLEMENTATION}
        config=dict(schemaVersion=1,kind='CORRECTED_HIGHCONTRAST_SINGLE_PROCESS',status='NOT_AUTHORISED',
                    campaignExecutionApproved=False,readOnlyVerificationApproved=False,bounds=BOUNDS,
                    supervisorQualification='BLOCKED_LOCAL_JOB_ACCOUNTING_UNRESOLVED',
                    workspace=r'D:\a\local-docs-studio\local-docs-studio',environment=original['environment'],implementationFiles=implementation,
                    collector=dict(sourceCommit=revision,sourceTree=receipt['sourceTree'],sourceFiles=receipt['sourceFiles'],
                                   archivePath='docs/native/evidence/highcontrast-live-readiness/collector.zip',archiveSha256=digest(archive),files=files),
                    runtime=dict(version='10.0.14',archive={k:definition[k] for k in ('url','sha256','sha512','bytes')},files=runtime_files))
        validate_config(config);write(output/'config.json',config)
        sdk_root=Path(shutil.which('dotnet')).parent
        receipt.update(status='COMPILED_NOT_EXECUTED',files=files,archiveSha256=digest(archive),configurationSha256=digest(output/'config.json'),
            implementationFiles=implementation,sdkFiles={str(p):digest(p) for p in [sdk_root/'dotnet.exe',sdk_root/'sdk/10.0.401/MSBuild.dll',sdk_root/'sdk/10.0.401/Roslyn/bincore/csc.dll']})
    finally:
        receipt['outputFiles']={p.relative_to(output).as_posix():digest(p) for p in output.rglob('*') if p.is_file()}
        write(output/'build-receipt.json',receipt)
    print(json.dumps({k:receipt[k] for k in ('status','sourceCommit','sourceTree','archiveSha256','configurationSha256')}))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--source',required=True);parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();prepare(args.source,args.output)
