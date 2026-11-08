"""Read-only review of downloaded discovery bytes; never loads a product/runtime."""
import hashlib
import json
from pathlib import Path, PurePosixPath, PureWindowsPath
import re
import subprocess
import zipfile

REPO = Path(r'C:\Code\MarkdownReader-native-ux-contract')
BASE = REPO / 'artifacts/goal77-environment-lock'
ROOT = BASE / 'run-42484885426'
TOOLING = '77664fbeed39162e17c6d0d2ff24a65f8a7f9d13'
TREE = 'bcdf6ac20347ac8a81fcf417864657a5fd2e4a09'
CANDIDATE = '7b1334b09a02ad2fbfd4a0234798e6c387d3e634'
BASELINE = '99349d82eb06960eb1f9785cf384db2373aed4fa'
REPORT = BASE / 'independent-run-42484885426-report.json'
report = {'scope':'Offline read-only discovery-artifact verification; no execution validator, product or WPF process',
          'runId':42484885426,'artifactId':13618494876,'toolingCommit':TOOLING,'toolingTree':TREE,
          'checks':0,'failures':[],'details':{},'campaignExecutionApproved':False,'pr76Integration':'NO-GO'}
cache = {}
batch = subprocess.Popen(['git','-C',str(REPO),'cat-file','--batch'],stdin=subprocess.PIPE,stdout=subprocess.PIPE)

def require(condition, label):
    report['checks'] += 1
    if not condition: report['failures'].append(label)

def sha(data): return hashlib.sha256(data).hexdigest()

def digest(path):
    path=Path(path)
    if path not in cache:
        h=hashlib.sha256()
        with path.open('rb') as stream:
            for chunk in iter(lambda:stream.read(1048576),b''): h.update(chunk)
        cache[path]=h.hexdigest()
    return cache[path]

def pairs(items):
    value={}
    for key,item in items:
        if key in value: raise ValueError('Duplicate JSON property: '+key)
        value[key]=item
    return value

def read(path): return json.loads(Path(path).read_bytes().decode('utf-8-sig'),object_pairs_hook=pairs)

def blob(revision,path):
    batch.stdin.write((revision+':'+path+'\n').encode());batch.stdin.flush()
    header=batch.stdout.readline().decode().split()
    if len(header)!=3 or header[1]!='blob': raise ValueError('Missing Git blob: '+revision+':'+path)
    data=batch.stdout.read(int(header[2])); separator=batch.stdout.read(1)
    if separator!=b'\n': raise ValueError('Invalid Git batch framing')
    return data

def files(folder):
    result={}
    for path in folder.rglob('*'):
        require(not path.is_symlink(),'Symlink in retained artifact: '+str(path))
        if path.is_file(): result[path.relative_to(folder).as_posix()]=path
    return result

def verify_closure(folder, expected, omit=()):
    actual=files(folder)
    for name in omit: actual.pop(name,None)
    require(set(actual)==set(expected),'Closure file set: '+str(folder))
    for name,value in expected.items():
        require(name in actual and digest(actual[name])==value.lower(),'Closure hash: '+str(folder/name))
    return len(actual)

try:
    remote=read(BASE/'independent-run-42484885426-remote.json')
    artifact=remote['artifacts'][0]
    require(len(remote['artifacts'])==1 and artifact['id']==13618494876,'Remote artifact identity')
    require(artifact['workflow_run']['head_sha']==TOOLING and artifact['workflow_run']['id']==42484885426,'Remote artifact source/run')
    require(not artifact['expired'],'Remote artifact not expired at inspection')
    archive=BASE/'run-42484885426.zip'
    require(archive.stat().st_size==185677817==artifact['size_in_bytes'],'Artifact byte count')
    require(digest(archive)=='8b3d50ff7b9b394867fdc9598fe14e47d980e865c7d74f389d7802be90c7f812'==artifact['digest'].split(':')[1],'Artifact SHA-256')
    jobs=remote['jobs']
    require(len(jobs)==4 and sum(j['conclusion']=='success' for j in jobs)==1 and sum(j['conclusion']=='skipped' for j in jobs)==3,'Discovery-only job outcomes')
    require(next(j for j in jobs if j['conclusion']=='success')['name']=='Hosted environment discovery only (no qualification)','Only discovery job ran')
    retained=files(ROOT)
    with zipfile.ZipFile(archive) as package:
        members={i.filename:i for i in package.infolist() if not i.is_dir()}
        require(set(members)==set(retained),'Extracted file set equals downloaded artifact')
        for name,member in members.items():
            h=hashlib.sha256()
            with package.open(member) as stream:
                for chunk in iter(lambda:stream.read(1048576),b''): h.update(chunk)
            require(name in retained and h.hexdigest()==digest(retained[name]),'Extracted byte identity: '+name)
    report['details']['artifactFiles']=len(retained)
    cfg=read(ROOT/'candidate-config.json'); lock=read(ROOT/'environment-lock.json')
    validation=read(ROOT/'validation.json'); environment=read(ROOT/'environment.json')
    host_base=PureWindowsPath(cfg['environmentLock']).parent
    def local(value):
        relative=PureWindowsPath(value).relative_to(host_base)
        if '..' in relative.parts: raise ValueError('Hosted path traversal')
        return ROOT.joinpath(*relative.parts)
    require(digest(ROOT/'candidate-config.json')=='7d0d520d2045c9b547c968882e93e2c48c408effce32d602fde30f7546af1a2f'==validation['configurationSha256'],'Candidate configuration SHA-256')
    require(digest(ROOT/'environment-lock.json')=='45a6aa515df05d93169521119b3bdab59db9512a439041e987495e373d5c466e'==cfg['environmentLockSha256'],'Environment lock SHA-256')
    require(cfg['status']=='NOT_AUTHORISED' and cfg['campaignExecutionApproved'] is False and cfg['ownerAuthorisationUrl'] is None and cfg['calibrationPilotEnabled'] is False,'Candidate authority remains false')
    require(lock['status']=='DISCOVERED_NOT_AUTHORISED' and lock['campaignExecutionApproved'] is False,'Lock has no execution authority')
    require(validation['status']=='CANDIDATE_VERIFIED_NOT_AUTHORISED' and validation['campaignExecutionApproved'] is False,'Validation is discovery-only')
    require(lock['processBounds']=={'R01':4,'R02':18,'calibration':0} and validation['configurationProtocols']=={'R01':4,'R02':18},'Fixed process bounds')
    require(all(validation[x]==0 for x in ('R01ProcessesExecuted','R02ProcessesExecuted','calibrationProcessesExecuted')),'No qualification process recorded')
    require(lock['discoveryOnly']=={'runtimeConsoleProbes':2,'qualificationProcesses':0,'foregroundActions':0,'appearanceChanges':0},'Discovery observation scope')
    require(cfg['limits']=={'childSeconds':240,'privateBytes':2147483648,'logBytes':16777216},'Unchanged safety bounds')
    require(all(x['toolingCommit']==TOOLING for x in (cfg,lock,validation,environment)) and environment['toolingTree']==TREE,'Recorded tooling source')
    actual_tree=subprocess.check_output(['git','-C',str(REPO),'rev-parse',TOOLING+'^{tree}']).decode().strip()
    require(actual_tree==TREE,'Tooling Git tree')
    require(lock['github']==environment['github'] and lock['github']['GITHUB_SHA']==TOOLING and lock['github']['GITHUB_RUN_ID']=='42484885426' and lock['github']['GITHUB_RUN_ATTEMPT']=='1','Hosted run/attempt/source correlation')
    require(lock['environment']==environment['environment'],'Environment records agree')
    host=lock['environment']
    require(host['imageOS']=='win25-vs2026' and host['imageVersion']=='20261103.298.1'==cfg['imageVersion'] and host['runnerArch']=='X64' and host['runnerEnvironment']=='github-hosted','Hosted image/architecture')
    require(host['windows']['version']=='10.0.26100'==cfg['osVersion'] and host['windows']['UBR']==33674 and host['windows']['EditionID']=='ServerDatacenter','Full observed Windows identity')
    require(cfg['runtimePins']==lock['runtimePins'] and cfg['bundles']==lock['bundles'],'Candidate bindings agree with lock')
    source_path=local(cfg['sourceReceipt']); source=read(source_path)
    require(digest(source_path)==cfg['sourceReceiptSha256']==lock['sourceReceipt']['sha256'] and cfg['sourceReceipt']==lock['sourceReceipt']['path'],'Source receipt binding')
    require(source['toolingCommit']==TOOLING and source['toolingDirty'] is False,'Clean build source identity')
    for name,item in source['identities'].items():
        tree=subprocess.check_output(['git','-C',str(REPO),'rev-parse',item['commit']+'^{tree}']).decode().strip()
        require(tree==item['tree'],'Pinned source tree: '+name)
    tool_eol={'gitBlobExact':0,'windowsCheckoutCRLF':0}
    for path,value in source['toolSources'].items():
        data=blob(TOOLING,path)
        if sha(data)==value: tool_eol['gitBlobExact']+=1
        elif sha(data.replace(b'\r\n',b'\n').replace(b'\n',b'\r\n'))==value: tool_eol['windowsCheckoutCRLF']+=1
        else: require(False,'Tool source differs from Git content: '+path)
    for path,value in source['originalSources'].items():
        require(sha(blob(CANDIDATE,path))==value,'Pinned original source: '+path)
        if PurePosixPath(path).name!='Program.cs':
            require(digest(source_path.parent/'r01'/PurePosixPath(path).name)==value,'Unchanged R01 reused source: '+path)
    product_counts={'candidate':0,'baseline':0}
    for path,value in source['generated'].items():
        target=source_path.parent/path
        require(digest(target)==value,'Generated/fixture/source bytes: '+path)
        if path.startswith('fixtures/'):
            require(sha(blob(CANDIDATE,'tests/fixtures/native-ux/'+path.removeprefix('fixtures/')))==value,'Exact candidate fixture: '+path)
        for label,revision in [('candidate',CANDIDATE),('baseline',BASELINE)]:
            prefix='product-'+label+'/'
            if path.startswith(prefix):
                require(sha(blob(revision,path[len(prefix):]))==value,'Immutable Git snapshot: '+path)
                product_counts[label]+=1
    report['details']['source']={'generatedFiles':len(source['generated']),'pinnedOriginalFiles':len(source['originalSources']),'toolSources':tool_eol,'gitSnapshotFiles':product_counts}
    selected_data=blob(TOOLING,'docs/native/qualification/runtime-downloads.json')
    selected=json.loads(selected_data)
    require(lock['metadata']['selectedInputsSha256'] in (sha(selected_data),sha(selected_data.replace(b'\r\n',b'\n').replace(b'\n',b'\r\n'))),'Selected archive source binding')
    require(digest(ROOT/'release-metadata.json')==lock['metadata']['sha256'],'Retained official metadata SHA-256')
    metadata=read(ROOT/'release-metadata.json')
    runtime_counts={}; runtime_pids=[]
    for version,item in lock['runtimes'].items():
        require(version in ('10.0.13','10.0.14'),'Only exact diagnostic runtimes')
        root=local(item['root']); runtime_counts[version]=verify_closure(root,item['files'])
        pin=cfg['runtimePins'][version]
        for key in ('dotnet','core','wpf'): require(digest(local(pin[key]))==pin[key+'Sha256'],'Pinned runtime bytes: '+version+'/'+key)
        loaded=read(ROOT/(version+'-loaded-runtime.json'))
        require(loaded==item['loaded'] and loaded['runtimeVersion']==version and loaded['architecture']=='X64' and loaded['windowOrAppearanceApisUsed'] is False,'Actual loaded runtime record: '+version)
        runtime_pids.append(loaded['processId'])
        expected_assemblies={'core':f'shared/Microsoft.NETCore.App/{version}/System.Private.CoreLib.dll','framework':f'shared/Microsoft.WindowsDesktop.App/{version}/PresentationFramework.dll','presentationCore':f'shared/Microsoft.WindowsDesktop.App/{version}/PresentationCore.dll','windowsBase':f'shared/Microsoft.WindowsDesktop.App/{version}/WindowsBase.dll'}
        require(set(loaded['assemblies'])==set(expected_assemblies),'Complete loaded WPF/Core records: '+version)
        for key,relative in expected_assemblies.items():
            data=loaded['assemblies'][key]
            require(local(data['path'])==root/relative and digest(root/relative)==data['sha256'].lower(),'Loaded assembly identity: '+version+'/'+key)
        require(set(loaded['modules'])=={'dotnet.exe','hostfxr.dll','hostpolicy.dll','coreclr.dll'},'Complete native host modules: '+version)
        for name,data in loaded['modules'].items():
            target=local(data['path'])
            require(target.is_relative_to(root) and target.name.lower()==name and digest(target)==data['sha256'].lower(),'Loaded native module: '+version+'/'+name)
        release=next(x for x in metadata['releases'] if x['release-version']==version)
        for component,record in item['archives'].items():
            required=selected['versions'][version][component]
            require(record['url']==required['url'] and record['sha512']==record['verifiedSha512']==required['sha512'],'Official selected archive: '+version+'/'+component)
            require(any(f['rid']=='win-x64' and f['url']==record['url'] and f['hash'].lower()==record['sha512'] for f in release[component]['files']),'Archive appears in retained official metadata: '+version+'/'+component)
    require(set(runtime_counts)=={'10.0.13','10.0.14'} and len(set(runtime_pids))==2 and all(p>0 for p in runtime_pids),'Two distinct exact runtime probes')
    report['details']['runtimeClosureFiles']=runtime_counts;report['details']['runtimeProbePids']=runtime_pids
    bundle_counts={}
    for name,pointer in cfg['bundles'].items():
        path=local(pointer['receipt']); require(digest(path)==pointer['receiptSha256'],'Bundle receipt hash: '+name)
        receipt=read(path); revision=BASELINE if name=='r02-baseline' else CANDIDATE
        require(receipt['bundle']==name and receipt['productRevision']==revision and receipt['sourceReceiptSha256']==cfg['sourceReceiptSha256'],'Bundle source correlation: '+name)
        bundle_counts[name]=verify_closure(path.parent,receipt['files'],('bundle-receipt.json',))
        require(receipt['sdk']=='10.0.403'==lock['sdk']['version'],'Observed SDK: '+name)
        for key,file in [('product','LensDocsStudio.Native.dll'),('core','LensDocsStudio.Core.dll'),('harness','LensDocsStudio.Native.UxProbe.dll' if name=='r01' else 'LensDocsStudio.Native.Qualification.dll')]:
            identity=receipt['binaryMetadata'][key]
            require(digest(path.parent/file)==identity['sha256'].lower(),'Compiled binary identity: '+name+'/'+key)
            require(identity['version'].endswith('+'+(TOOLING if key=='harness' else revision)),'Compiled revision metadata: '+name+'/'+key)
            require(identity['version'].encode() in (path.parent/file).read_bytes(),'Revision metadata physically present in binary: '+name+'/'+key)
    report['details']['bundleClosureFiles']=bundle_counts
    build_log=(ROOT/'bundle-build.txt').read_text(encoding='utf-8-sig')
    require(build_log.count('Build succeeded.')==5 and build_log.count('0 Warning(s)')==5 and build_log.count('0 Error(s)')==5,'Two product plus three harness builds passed cleanly')
    report['details']['buildsPassed']=5
    require(len(lock['sdk']['identityFiles'])==4 and all(re.fullmatch('[a-f0-9]{64}',x['sha256']) for x in lock['sdk']['identityFiles']),'Four observed SDK identity records')
    report['limitations']=['SDK/compiler bytes reside on the hosted image and are not included in this artifact; their observed hashes must be rechecked on a future exact host.',
        'Original runtime ZIPs are deliberately omitted; their SHA-512 verification is retained from the reviewed hosted collector. Complete extracted runtime bytes and official selected identities were independently checked.',
        'No local execution validator, product, WPF probe, foreground action or appearance API was run. Hosted absolute paths were mapped read-only to the extracted artifact.',
        'Discovery success is not R01/R02 qualification, owner execution authorisation, policy activation or PR76 integration acceptance.']
except Exception as error:
    report['failures'].append(type(error).__name__+': '+str(error))
finally:
    batch.stdin.close();batch.wait(timeout=10)
    report['status']='PASS_DISCOVERY_ARTIFACT_REVIEW_ONLY' if not report['failures'] else 'FAIL'
    report['reviewScriptSha256']=digest(Path(__file__))
    with REPORT.open('x',encoding='utf-8') as stream: json.dump(report,stream,indent=2);stream.write('\n')
    print(json.dumps(report,indent=2))
raise SystemExit(0 if not report['failures'] else 1)
