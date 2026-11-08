"""Synthetic identity/closure controls; never launches discovery or qualification."""
import copy
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import warnings
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'scripts/native/qualification'))
import discover_hosted as d
import environment_lock as lock
import qualification as q
from sources import sha


def synthetic(root):
    actual = {'imageOS':'win25','imageVersion':'20261102.1.0','runnerOS':'Windows','runnerArch':'X64',
              'runnerEnvironment':'github-hosted','windows':{'version':'10.0.26100','CurrentBuildNumber':'26100',
              'UBR':999,'EditionID':'ServerDatacenter','BuildLabEx':'synthetic','ProductName':'Synthetic Windows'}}
    cfg = q.template(); cfg.update(toolingCommit='a'*40,sourceReceipt='synthetic-source.json',sourceReceiptSha256='b'*64,
                                  imageVersion=actual['imageVersion'],osVersion=actual['windows']['version'])
    runtimes = {}
    for i,version in enumerate(q.RUNTIMES):
        folder=root/version; files={}; assemblies={}; modules={}
        names={'core':f'shared/Microsoft.NETCore.App/{version}/System.Private.CoreLib.dll',
               'framework':f'shared/Microsoft.WindowsDesktop.App/{version}/PresentationFramework.dll',
               'presentationCore':f'shared/Microsoft.WindowsDesktop.App/{version}/PresentationCore.dll',
               'windowsBase':f'shared/Microsoft.WindowsDesktop.App/{version}/WindowsBase.dll'}
        for key,relative in names.items():
            path=folder/relative; path.parent.mkdir(parents=True,exist_ok=True); path.write_bytes((key+version).encode())
            files[relative]=sha(path.read_bytes()); assemblies[key]={'path':str(path),'sha256':files[relative]}
        for name,relative in [('dotnet.exe','dotnet.exe'),('hostfxr.dll',f'host/fxr/{version}/hostfxr.dll'),
                              ('hostpolicy.dll',f'shared/Microsoft.NETCore.App/{version}/hostpolicy.dll'),
                              ('coreclr.dll',f'shared/Microsoft.NETCore.App/{version}/coreclr.dll')]:
            path=folder/relative; path.parent.mkdir(parents=True,exist_ok=True); path.write_bytes((name+version).encode())
            files[relative]=sha(path.read_bytes());modules[name]={'path':str(path),'sha256':files[relative]}
        for key,rel in [('dotnet','dotnet.exe'),('core',names['core']),('wpf',names['framework'])]:
            cfg['runtimePins'][version][key]=str(folder/rel);cfg['runtimePins'][version][key+'Sha256']=files[rel]
        runtimes[version]={'root':str(folder),'files':files,'loaded':{'runtimeVersion':version,'architecture':'X64','processId':i+101,
            'assemblies':assemblies,'modules':modules,'windowOrAppearanceApisUsed':False}}
    sdk=root/'sdk.dll';sdk.write_bytes(b'synthetic SDK')
    record={'schemaVersion':1,'status':'DISCOVERED_NOT_AUTHORISED','campaignExecutionApproved':False,'toolingCommit':cfg['toolingCommit'],
        'runtimePins':cfg['runtimePins'],'sourceReceipt':{'path':cfg['sourceReceipt'],'sha256':cfg['sourceReceiptSha256']},'bundles':cfg['bundles'],
        'environment':actual,'processBounds':{'R01':4,'R02':18,'calibration':0},'runtimes':runtimes,
        'sdk':{'identityFiles':[{'path':str(sdk),'sha256':sha(sdk.read_bytes())}]}}
    cfg['environmentLock']=str(root/'lock.json')
    return cfg,record,actual


def seal(cfg,record):
    p=Path(cfg['environmentLock']);p.write_text(json.dumps(record),encoding='utf-8')
    cfg['environmentLockSha256']=sha(p.read_bytes())


class HostedLock(unittest.TestCase):
    def test_full_synthetic_lock_passes_read_only_and_keeps_authority_false(self):
        with tempfile.TemporaryDirectory() as t:
            cfg,record,actual=synthetic(Path(t));seal(cfg,record);lock.validate_lock(cfg,actual)
            self.assertIs(cfg['campaignExecutionApproved'],False);self.assertEqual(cfg['status'],'NOT_AUTHORISED')

    def test_image_os_ubr_and_architecture_drift_rejected(self):
        with tempfile.TemporaryDirectory() as t:
            cfg,record,actual=synthetic(Path(t));seal(cfg,record)
            for key,value in [('imageVersion','moving'),('imageOS','win22'),('runnerArch','ARM64')]:
                bad=copy.deepcopy(actual);bad[key]=value
                with self.assertRaises(ValueError):lock.validate_lock(cfg,bad)
            for key,value in [('version','10.0.99999'),('UBR',1000),('EditionID','different')]:
                bad=copy.deepcopy(actual);bad['windows'][key]=value
                with self.assertRaises(ValueError):lock.validate_lock(cfg,bad)

    def test_missing_extra_changed_runtime_files_and_sdk_change_fail(self):
        with tempfile.TemporaryDirectory() as t:
            cfg,record,actual=synthetic(Path(t));seal(cfg,record)
            folder=Path(record['runtimes']['10.0.13']['root']);file=folder/'dotnet.exe';original=file.read_bytes()
            file.write_bytes(b'wrong')
            with self.assertRaises(ValueError):lock.validate_lock(cfg,actual)
            file.unlink()
            with self.assertRaises(ValueError):lock.validate_lock(cfg,actual)
            file.write_bytes(original);extra=folder/'injected.dll';extra.write_bytes(b'new')
            with self.assertRaises(ValueError):lock.validate_lock(cfg,actual)
            extra.unlink();(Path(t)/'sdk.dll').write_bytes(b'changed')
            with self.assertRaises(ValueError):lock.validate_lock(cfg,actual)

    def test_wrong_loaded_runtime_process_assembly_and_native_host_fail(self):
        with tempfile.TemporaryDirectory() as t:
            cfg,record,actual=synthetic(Path(t))
            changes=[lambda r:r['runtimes']['10.0.13']['loaded'].update(runtimeVersion='10.0.14'),
                     lambda r:r['runtimes']['10.0.13']['loaded'].update(processId=102),
                     lambda r:r['runtimes']['10.0.13']['loaded']['assemblies']['core'].update(sha256='0'*64),
                     lambda r:r['runtimes']['10.0.13']['loaded']['modules']['hostfxr.dll'].update(path=str(Path(t)/'outside.dll')),
                     lambda r:r['runtimes']['10.0.13']['loaded']['modules'].pop('coreclr.dll'),
                     lambda r:r['processBounds'].update(R02=19),lambda r:r.update(campaignExecutionApproved=True),
                     lambda r:r['runtimePins']['10.0.13'].update(dotnetSha256='0'*64)]
            for change in changes:
                candidate=copy.deepcopy(record);change(candidate);seal(cfg,candidate)
                with self.assertRaises(ValueError):lock.validate_lock(cfg,actual)

    def test_missing_truncated_duplicate_and_unbound_lock_rejected(self):
        with tempfile.TemporaryDirectory() as t:
            cfg,record,actual=synthetic(Path(t));seal(cfg,record)
            path=Path(cfg['environmentLock']);path.write_bytes(b'{')
            with self.assertRaises(ValueError):lock.validate_lock(cfg,actual)
            for data in (b'{',b'{"status":1,"status":2}'):
                path.write_bytes(data);cfg['environmentLockSha256']=sha(data)
                with self.assertRaises(ValueError):lock.validate_lock(cfg,actual)

    def test_unauthorised_execution_stops_before_lock_or_windows_import(self):
        cfg=q.template()
        with patch('qualification.git'),patch('environment_lock.validate_lock') as validate:
            with self.assertRaisesRegex(ValueError,'authorisation'):q.validate_config(cfg,'R01')
            validate.assert_not_called()
        cfg['campaignExecutionApproved']=True
        with self.assertRaisesRegex(ValueError,'NOT_AUTHORISED'):q.validate_candidate_config(cfg,'R01')

    def test_discovery_rejects_developer_desktop_before_output_or_process(self):
        with patch.dict(os.environ,{'GITHUB_ACTIONS':'false'}),patch('discover_hosted.subprocess.run') as process:
            with self.assertRaisesRegex(ValueError,'hosted discovery'):d.discovery('must-not-exist')
            process.assert_not_called()

    def test_product_checkout_does_not_expand_historical_evidence(self):
        revision='a'*40;path=Path('synthetic-product-root')
        with patch('discover_hosted.git',side_effect=[b'',b'',b'',revision.encode(),b'']) as git:
            d.product_worktree(path,revision)
            self.assertIn('--no-checkout',git.call_args_list[0].args)
            self.assertEqual(git.call_args_list[1].args,('sparse-checkout','set','--cone','src/native/LensDocsStudio.Core','src/native/LensDocsStudio.Native'))
            self.assertEqual(git.call_args_list[2].args,('read-tree','-mu','HEAD'))
        with patch('discover_hosted.git',side_effect=[b'',b'',b'',b'wrong',b'']):
            with self.assertRaisesRegex(ValueError,'clean pinned'):d.product_worktree(path,revision)

    def test_exact_frameworks_disable_roll_forward_and_scrub_loader_environment(self):
        for v in q.RUNTIMES:
            r=d.runtime_config(v)['runtimeOptions'];self.assertEqual(r['rollForward'],'Disable')
            self.assertEqual({f['version'] for f in r['frameworks']},{v})
        with self.assertRaises(ValueError):d.runtime_config('10.0.x')
        with patch.dict(os.environ,{'LENS_QUALIFICATION_EXECUTION_ID':'x','DOTNET_STARTUP_HOOKS':'x','COMPLUS_X':'x'}):
            e=d.clean_environment();self.assertNotIn('LENS_QUALIFICATION_EXECUTION_ID',e);self.assertNotIn('DOTNET_STARTUP_HOOKS',e);self.assertNotIn('COMPLUS_X',e)

    def test_archive_selection_requires_exact_both_components_and_unique_release(self):
        records={'releases':[{'release-version':'10.0.13',**{key:{'version':'10.0.13','files':[{'name':name,'rid':'win-x64','url':'url','hash':'a'*128}]}
            for key,name in [('runtime','dotnet-runtime-win-x64.zip'),('windowsdesktop','windowsdesktop-runtime-win-x64.zip')]}}]}
        self.assertEqual(set(d.select_archives(records,'10.0.13')),{'runtime','windowsdesktop'})
        with self.assertRaises(ValueError):d.select_archives(records,'10.0.14')
        records['releases'].append(copy.deepcopy(records['releases'][0]))
        with self.assertRaises(ValueError):d.select_archives(records,'10.0.13')

    def test_archive_path_traversal_duplicate_and_conflicting_overlap_fail(self):
        with tempfile.TemporaryDirectory() as t:
            root=Path(t)
            for i,names in enumerate([['../outside'],['C:/outside'],['a','a'],['NUL'],['a.'],['name:stream']]):
                archive=root/f'{i}.zip'
                with warnings.catch_warnings():
                    warnings.simplefilter('ignore')
                    with zipfile.ZipFile(archive,'w') as z:
                        for name in names:z.writestr(name,b'one')
                with self.assertRaises(ValueError):d.extract_archive(archive,root/f'extracted{i}')
            for i,content in enumerate([b'one',b'one',b'two']):
                archive=root/f'overlap{i}.zip'
                with zipfile.ZipFile(archive,'w') as z:z.writestr('file',content)
                if i<2:d.extract_archive(archive,root/'overlap')
                else:
                    with self.assertRaisesRegex(ValueError,'Conflicting'):d.extract_archive(archive,root/'overlap')

    def test_wrong_archive_digest_is_rejected_before_extraction(self):
        with tempfile.TemporaryDirectory() as t:
            archive=Path(t)/'runtime.zip';archive.write_bytes(b'invalid download')
            with self.assertRaisesRegex(ValueError,'SHA-512'):d.verify_archive(archive,{'sha512':'0'*128})

    def test_untrusted_archive_origin_is_rejected_before_network(self):
        with patch('urllib.request.urlopen') as request:
            for url in ('http://builds.dotnet.microsoft.com/dotnet/x','https://example.com/runtime.zip','https://builds.dotnet.microsoft.com@evil.test/dotnet/x'):
                with self.assertRaises(ValueError):d.fetch(url,'not-created',1)
            request.assert_not_called()

    def test_discovery_remains_opt_in_with_original_jobs_excluded(self):
        text=(ROOT/'.github/workflows/native-ux-performance.yml').read_text()
        original=text.split('  environment-lock:\n')[0]
        self.assertEqual(original.count('inputs.environment_lock_only != true'),3)
        self.assertIn('inputs.environment_lock_only == true',text)
        discovery=text.split('  environment-lock:\n')[1].split('  bounded-qualification:\n')[0]
        for item in ('persist-credentials: false','github.triggering_actor','EXPECTED_TOOLING_SHA','contents: read','windows-2025'):
            self.assertIn(item,discovery)
        for item in ('Measure-NativeUx','qualification.py execute','secrets.','LENS_QUALIFICATION_EXECUTION_ID','strategy:'):
            self.assertNotIn(item,discovery)
        probe=(ROOT/'scripts/native/qualification/RuntimeIdentityProbe.cs').read_text()
        for item in ('SystemParameters','SystemColors','new Application','new Window','SendInput','SystemParametersInfo'):
            self.assertNotIn(item,probe)


if __name__=='__main__':unittest.main()
