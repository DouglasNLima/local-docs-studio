import { test, expect } from '@playwright/test';
import { createZipBuffer, readZipEntries, getZipText } from './helpers/zip.mjs';
const png = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP432H3HwAHFALF2h7vpgAAAABJRU5ErkJggg==';
const image = (name = 'capture.png') => ({ name, mimeType: 'image/png', buffer: Buffer.from(png, 'base64') });
async function ready(page, source = '# Images\n\n') {
  await page.goto('/'); await expect(page.locator('html')).toHaveAttribute('data-app-ready', 'true');
  await page.locator('#fileInput').setInputFiles({ name: 'images.md', mimeType: 'text/markdown', buffer: Buffer.from(source) });
  await expect(page.locator('#activeFileLabel')).toContainText('images.md');
  await expect(page.locator('#status')).toContainText('Rendered');
}
async function importImage(page, caption = '') {
  await page.getByRole('button', { name: 'Insert image', exact: true }).click();
  await page.locator('#imageSourceInput').selectOption('import');
  await page.locator('#imageImportInput').setInputFiles(image());
  await expect(page.locator('[data-image-insert]')).toBeEnabled();
  await page.locator('#imageAltInput').fill('Capture first');
  await page.locator('#imageCaptionInput').fill(caption);
  await page.locator('[data-image-insert]').click();
  await expect(page.locator('#preview img[data-managed-asset-path]')).toHaveAttribute('src', /^blob:/);
  await page.locator('#preview img').evaluate((img) => img.decode());
}

test('one image action supports import, genuinely optional captions, URL state and Escape', async ({ page }) => {
  await ready(page);
  await expect(page.locator('[data-command="image"], [data-command="imageFigure"]')).toHaveCount(1);
  await importImage(page);
  await expect(page.locator('#editor')).toHaveValue(/!\[Capture first\]\(assets\/images\/capture.png\)/);
  await expect(page.locator('#editor')).not.toHaveValue(/Optional caption|<figure/);
  const source = await page.locator('#editor').inputValue();
  await page.getByRole('button', { name: 'Insert image', exact: true }).click();
  await page.locator('#imageSourceInput').selectOption('url');
  await page.locator('#imageReferenceInput').fill('https://example.test/capture.png');
  await page.getByRole('button', { name: 'Preview reference' }).click();
  await expect(page.locator('.image-storage-status')).toHaveText(/External image/);
  await page.keyboard.press('Escape');
  await expect(page.locator('.image-insertion-dialog')).toHaveCount(0);
  expect(await page.locator('#editor').inputValue()).toBe(source);
  await expect(page.locator('#editor')).toBeFocused();
});

for (const saveAction of ['save', 'saveAs']) {
  test(`${saveAction} explains pending image bytes and honours cancellation before saving Markdown`, async ({ page }) => {
    await page.addInitScript(() => {
      window.__imageSaveCalls = { picker: 0, writes: [] };
      let saved = '';
      window.showSaveFilePicker = async () => {
        window.__imageSaveCalls.picker++;
        return {
          kind: 'file', name: 'images.md',
          async createWritable() {
            return { async write(content) { saved = content; window.__imageSaveCalls.writes.push(content); }, async close() {} };
          },
          async getFile() { return new File([saved], 'images.md', { type: 'text/markdown' }); },
        };
      };
    });
    await ready(page);
    await importImage(page);
    const source = await page.locator('#editor').inputValue();
    const button = page.locator(saveAction === 'save' ? '#saveButton' : '#saveAsButton');
    await button.click();
    await expect(page.locator('#appDialogTitle')).toHaveText('Images are not saved to the folder', { timeout: 3000 });
    await expect(page.locator('#appDialogMessage')).toHaveText('Saving Markdown writes references only. Use Manage assets > Save pending images to workspace, or Export Markdown Bundle for a portable copy. Continue saving Markdown links?');
    await expect(page.locator('#appDialogMessage')).not.toContainText('[object Object]');
    await expect(page.locator('#appDialogConfirmButton')).toHaveText('Save Markdown links');
    await expect(page.locator('#appDialogCancelButton')).toHaveText('Keep editing');
    await page.locator('#appDialogCancelButton').click();
    expect(await page.evaluate(() => window.__imageSaveCalls)).toEqual({ picker: 0, writes: [] });
    await expect(page.locator('#editor')).toHaveValue(source);

    await button.click();
    await expect(page.locator('#appDialogTitle')).toHaveText('Images are not saved to the folder');
    await page.locator('#appDialogConfirmButton').click();
    await expect.poll(() => page.evaluate(() => window.__imageSaveCalls)).toEqual({ picker: 1, writes: [source] });
    await expect(page.locator('#appDialog')).not.toBeVisible();
    await expect(page.locator('#status')).toContainText('not saved to folder');
    await expect(page.locator('#editor')).toHaveValue(source);
  });
}

for (const sync of [true, false]) for (const region of ['start', 'middle', 'end']) {
  test(`image insertion, selection, Escape and Undo/Redo preserve the ${region} viewport with sync ${sync}`, async ({ page }) => {
    const source = Array.from({length: 600}, (_, index) => `Line ${index}: local documentation content.`).join('\n');
    await ready(page, source);
    await page.locator('#scrollSyncToggle').setChecked(sync);
    const original = await page.locator('#editor').evaluate((el, region) => {
      const index = region === 'start' ? 2 : region === 'middle' ? el.value.indexOf('Line 300') : el.value.length - 10;
      el.focus({ preventScroll: true }); el.setSelectionRange(index, index + 3);
      const line = el.value.slice(0,index).split('\n').length - 1;
      el.scrollTop = Math.max(0, line * parseFloat(getComputedStyle(el).lineHeight) - 100);
      return { top: el.scrollTop, left: el.scrollLeft, start: index, end: index + 3 };
    }, region);
    await page.getByRole('button', { name: 'Insert image', exact: true }).click();
    await page.keyboard.press('Escape');
    expect(await page.locator('#editor').evaluate(el => ({top:el.scrollTop,left:el.scrollLeft,start:el.selectionStart,end:el.selectionEnd}))).toEqual(original);
    await importImage(page, region === 'middle' ? 'Architecture overview.' : '');
    const inserted = await page.locator('#editor').inputValue();
    expect(inserted.slice(0,original.start)).toBe(source.slice(0,original.start));
    await page.waitForTimeout(700); // Observation window includes debounced rendering and selection sync.
    const after = await page.locator('#editor').evaluate(el => ({top:el.scrollTop,left:el.scrollLeft}));
    // At the document end, revealing newly inserted lines needs a small local
    // displacement; it must remain bounded by the insertion's height.
    expect(Math.abs(after.top - original.top)).toBeLessThanOrEqual(90);
    expect(after.left).toBe(original.left);
    await page.locator('#editor').press('Control+z');
    await expect(page.locator('#editor')).toHaveValue(source);
    await page.locator('#editor').press('Control+y');
    await expect(page.locator('#editor')).toHaveValue(inserted);
  });
}

test('workspace and local-reference insertion resolve exact paths for Markdown and figures', async ({ page }) => {
  await page.goto('/'); await expect(page.locator('html')).toHaveAttribute('data-app-ready', 'true');
  const special = 'assets/ação (v1) # 100%.png';
  const source = '![Root](../assets/architecture.png)\n\n<img src="./architecture.png" alt="Subfolder">\n\n![Special](../assets/a%C3%A7%C3%A3o%20%28v1%29%20%23%20100%25.png)\n';
  await page.locator('#zipInput').setInputFiles({ name: 'workspace.zip', mimeType: 'application/zip', buffer: createZipBuffer([
    { name: 'docs/start.md', data: source }, { name: 'assets/architecture.png', data: Buffer.from(png,'base64') },
    { name: 'docs/architecture.png', data: Buffer.from(png,'base64') }, { name: special, data: Buffer.from(png,'base64') },
  ]) });
  await expect(page.locator('#preview img[data-managed-asset-path]')).toHaveCount(3);
  expect(await page.locator('#preview img').evaluateAll(images => images.map(img => img.dataset.managedAssetPath))).toEqual(['assets/architecture.png','docs/architecture.png',special]);
  await page.getByRole('button', { name: 'Insert image', exact: true }).click();
  await page.locator('#imageSearchInput').fill('ação');
  await expect(page.locator('[data-image-path]:visible')).toHaveCount(1);
  await page.locator(`[data-image-path="${special}"]`).click();
  await expect(page.locator('#imageFinalReference')).toHaveValue('../assets/a%C3%A7%C3%A3o%20%28v1%29%20%23%20100%25.png');
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Insert image', exact: true }).click();
  await page.locator('#imageSourceInput').selectOption('url');
  await page.locator('#imageReferenceInput').fill('..\\assets\\architecture.png');
  await page.getByRole('button', { name: 'Preview reference' }).click();
  await expect(page.locator('#imageFinalReference')).toHaveValue('../assets/architecture.png');
  await page.locator('#imageCaptionInput').fill('Local figure');
  await page.locator('[data-image-insert]').click();
  await expect(page.locator('#preview figure[data-image-figure] figcaption')).toHaveText('Local figure');
});

test('cancelled and concurrently edited image imports never apply stale positions or lose recoverable bytes', async ({ page }) => {
  await ready(page);
  await page.evaluate(() => { const original = window.createImageBitmap; window.createImageBitmap = async (...args) => { await new Promise(resolve=>setTimeout(resolve,200)); return original(...args); }; });
  const source = await page.locator('#editor').inputValue();
  await page.getByRole('button', { name: 'Insert image', exact: true }).click();
  await page.locator('#imageSourceInput').selectOption('import');
  await page.locator('#imageImportInput').setInputFiles(image());
  await page.keyboard.press('Escape');
  await page.waitForTimeout(400);
  expect(await page.locator('#editor').inputValue()).toBe(source);
  await page.getByRole('button', { name: 'Insert image', exact: true }).click();
  await page.locator('#imageSourceInput').selectOption('import');
  await page.locator('#imageImportInput').setInputFiles(image('second.png'));
  await page.locator('#editor').evaluate(el => { el.value += '\nConcurrent edit'; el.dispatchEvent(new Event('input',{bubbles:true})); });
  await expect(page.locator('[data-image-insert]')).toBeEnabled();
  await page.locator('[data-image-insert]').click();
  await expect(page.locator('.image-insertion-error')).toContainText('document changed');
  await expect(page.locator('#editor')).toHaveValue(/Concurrent edit$/);
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Insert image', exact: true }).click();
  await page.locator('#imageSourceInput').selectOption('import');
  await page.locator('#imageImportInput').setInputFiles(image('source-changed.png'));
  await page.locator('#imageSourceInput').selectOption('url');
  await expect(page.locator('#status')).toContainText('source changed');
  await expect(page.locator('[data-image-insert]')).toBeDisabled();
  await expect(page.locator('#imageFinalReference')).toHaveValue('');
  await page.keyboard.press('Escape');
});

test('image pastes into other fields and all image actions on read-only documents preserve content', async ({ page }) => {
  await ready(page);
  await page.getByRole('button', { name: 'Insert image', exact: true }).click();
  const source = await page.locator('#editor').inputValue();
  await page.locator('#imageAltInput').evaluate((field, base64) => {
    const transfer = new DataTransfer(); transfer.items.add(new File([Uint8Array.from(atob(base64),c=>c.charCodeAt(0))],'capture.png',{type:'image/png'}));
    field.dispatchEvent(new ClipboardEvent('paste',{bubbles:true,cancelable:true,clipboardData:transfer}));
    field.dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:transfer}));
  }, png);
  expect(await page.locator('#editor').inputValue()).toBe(source);
  await page.keyboard.press('Escape');
  await page.locator('summary').filter({hasText:/^Help$/}).click();
  await page.getByRole('button',{name:'Open feature guide'}).click();
  await expect(page.locator('#editor')).toHaveAttribute('readonly','');
  const guide = await page.locator('#editor').inputValue();
  await page.locator('#editor').evaluate((el,base64)=>{
    const transfer=new DataTransfer();transfer.items.add(new File([Uint8Array.from(atob(base64),c=>c.charCodeAt(0))],'capture.png',{type:'image/png'}));
    el.dispatchEvent(new ClipboardEvent('paste',{bubbles:true,cancelable:true,clipboardData:transfer}));
    el.dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:transfer}));
    document.querySelector('[data-command="image"]').click();
  },png);
  expect(await page.locator('#editor').inputValue()).toBe(guide);
  await expect(page.locator('.image-insertion-dialog')).toHaveCount(0);
});

test('pending migration uses confirmed collision paths and preserves code, unrelated text and subfolder figures', async ({ page }) => {
  await ready(page);
  const result = await page.evaluate(async(base64) => {
    const {createImageAssetService}=await import('/assets/scripts/files/image-asset-service.js');
    const disk=new Map([['assets/images/pending.png', 'existing-other-bytes']]); let writes=0;
    const state={ managedAssets:new Map(), nativeWorkspaceId:'mock-workspace', workspaceDirectoryHandle:null, imageWorkspaceVersion:1, workspaceRootPath:'', files:[{path:'root.md'},{path:'docs/start.md'}] };
    const sources=new Map([
      ['root.md','![Capture](assets/pending.png)\n\nPlain assets/pending.png\n\n```md\n![Code](assets/pending.png)\n```'],
      ['docs/start.md','<figure data-image-figure><img src="../assets/pending.png" alt="Capture"><figcaption>Caption</figcaption></figure>\n\n`![Code](../assets/pending.png)`'],
    ]);
    const asset={path:'assets/pending.png',name:'pending.png',base64,mimeType:'image/png',size:70,storage:'session'};
    state.managedAssets.set(asset.path,asset);
    const bridge={async createWorkspaceImage(payload){writes++;const path='assets/images/pending-2.png';disk.set(path,payload.base64);return {ok:true,response:{payload:{created:true,path,base64:payload.base64}}};}};
    const service=createImageAssetService({state,nativeBridgeClient:bridge,callbacks:{readRecordText:async(record)=>sources.get(record.path),replaceDocumentContent:(record,next)=>sources.set(record.path,next)}});
    const first=await service.savePendingImages(); const second=await service.savePendingImages();
    const locked={...asset,path:'assets/locked.png',name:'locked.png',storage:'session'};
    state.managedAssets.set(locked.path,locked); state.files.push({path:'read-only.md',readOnly:true});
    sources.set('read-only.md','<img src="assets/locked.png" alt="Locked">');
    const protectedResult=await service.savePendingImages();
    return {first,second,protectedResult,lockedError:locked.error,lockedBytes:locked.base64,writes,root:sources.get('root.md'),sub:sources.get('docs/start.md'),original:disk.get('assets/images/pending.png'),saved:disk.get('assets/images/pending-2.png'),storage:asset.storage,keys:[...state.managedAssets.keys()]};
  },png);
  expect(result.first).toEqual({saved:1,failed:0}); expect(result.second).toEqual({saved:0,failed:0}); expect(result.writes).toBe(1);
  expect(result.root).toContain('![Capture](assets/images/pending-2.png)'); expect(result.root).toContain('```md\n![Code](assets/pending.png)\n```');
  expect(result.root).toContain('Plain assets/pending.png'); expect(result.sub).toContain('src="../assets/images/pending-2.png"');
  expect(result.sub).toContain('`![Code](../assets/pending.png)`'); expect(result.original).toBe('existing-other-bytes'); expect(result.saved).toBe(png); expect(result.storage).toBe('workspace');
  expect(result.protectedResult).toEqual({saved:0,failed:1}); expect(result.lockedError).toContain('read-only document'); expect(result.lockedBytes).toBe(png);
});

test('browser directory capabilities query permission before paste, avoid existing names and reuse confirmed handles', async ({ page }) => {
  await ready(page);
  const result = await page.evaluate(async(base64) => {
    const {createImageAssetService}=await import('/assets/scripts/files/image-asset-service.js');
    const pngBytes=Uint8Array.from(atob(base64),c=>c.charCodeAt(0));
    const disk=new Map([['assets/images/capture.png',new Uint8Array([7,8,9])],['assets/existing.png',pngBytes]]);
    let permission='denied',requests=0,writes=0,exclusive=false;
    const notFound=()=>new DOMException('Missing simulated file','NotFoundError');
    function directory(prefix='') { return {
      kind:'directory', name:prefix || 'fixture',
      async queryPermission(){return permission;},async requestPermission(){requests++;permission='granted';return permission;},
      async resolve(handle){return handle.path?.split('/') || null;},
      async getDirectoryHandle(name){return directory(prefix+name+'/');},
      async getFileHandle(name,options={}){
        const path=prefix+name;
        if (!disk.has(path)) {if (!options.create) throw notFound();disk.set(path,new Uint8Array());}
        return { kind:'file',path,async getFile(){return new File([disk.get(path)],name,{type:'image/png'});},async createWritable(options){
          exclusive=options.mode==='exclusive';let data;
          return {async write(bytes){data=bytes;},async close(){writes++;disk.set(path,data);},async abort(){}};
        }};
      },
    }; }
    const root=directory(); const record={path:'docs/start.md'};
    let source='![Capture](../assets/images/capture.png)\n\n```md\n![Example](../assets/images/capture.png)\n```';
    const state={managedAssets:new Map(),workspaceDirectoryHandle:root,nativeWorkspaceId:'',imageWorkspaceVersion:1,files:[record]};
    const service=createImageAssetService({state,callbacks:{readRecordText:async()=>source,replaceDocumentContent:(_record,next)=>source=next}});
    const failed=await service.acquireFile(new File([pngBytes],'capture.png',{type:'image/png'}));
    const denied={state:failed.storage,requests,writes,bytes:failed.base64};
    const migration=await service.savePendingImages();const again=await service.savePendingImages();
    const existingHandle=await (await root.getDirectoryHandle('assets')).getFileHandle('existing.png');
    const existing=await service.acquireFile(await existingHandle.getFile(),{handle:existingHandle});
    return {denied,migration,again,writes,exclusive,source,original:[...disk.get('assets/images/capture.png')],created:[...disk.get('assets/images/capture-2.png')],reuse:existing.path,storage:existing.storage};
  },png);
  expect(result.denied).toEqual({state:'failed',requests:0,writes:0,bytes:png});
  expect(result.migration).toEqual({saved:1,failed:0});expect(result.again).toEqual({saved:0,failed:0});expect(result.writes).toBe(1);expect(result.exclusive).toBe(true);
  expect(result.original).toEqual([7,8,9]);expect(result.created).toEqual([...Buffer.from(png,'base64')]);
  expect(result.source).toContain('![Capture](../assets/images/capture-2.png)');expect(result.source).toContain('![Example](../assets/images/capture.png)');
  expect(result.reuse).toBe('assets/existing.png');expect(result.storage).toBe('workspace');
});

test('only recognised legacy bundle assets use root compatibility and exports canonicalise their image tokens', async ({ page }) => {
  await ready(page);
  const result=await page.evaluate(async()=>{
    const {createImageAssetService}=await import('/assets/scripts/files/image-asset-service.js');
    const state={managedAssets:new Map([['assets/images/legacy.png',{path:'assets/images/legacy.png',legacyRootReference:true}]]),workspaceRootPath:''};
    const service=createImageAssetService({state});
    const source='![Legacy](assets/images/legacy.png)\n\n<img src="assets/images/legacy.png">\n\n```md\n![Example](assets/images/legacy.png)\n```';
    const identified=service.resolve('assets/images/legacy.png','docs/start.md');
    const canonical=service.canonicaliseLegacyReferences(source,'docs/start.md');
    state.managedAssets.get('assets/images/legacy.png').legacyRootReference=false;
    const strict=service.resolve('assets/images/legacy.png','docs/start.md');
    return {identified,canonical,strict};
  });
  expect(result.identified.path).toBe('assets/images/legacy.png');expect(result.identified.legacy).toBe(true);
  expect(result.canonical).toContain('![Legacy](../assets/images/legacy.png)');expect(result.canonical).toContain('src="../assets/images/legacy.png"');
  expect(result.canonical).toContain('![Example](assets/images/legacy.png)');expect(result.strict.path).toBe('docs/assets/images/legacy.png');
});

test('failed persistence preserves bytes, missing recovery is reported, and late workspace writes cannot contaminate the next workspace', async ({ page }) => {
  await ready(page);
  const result = await page.evaluate(async(base64)=>{
    const {createImageAssetService}=await import('/assets/scripts/files/image-asset-service.js');
    const state={managedAssets:new Map(),nativeWorkspaceId:'original',workspaceDirectoryHandle:null,imageWorkspaceVersion:1,files:[]};
    let release; const wait=new Promise(resolve=>release=resolve);
    const bridge={async createWorkspaceImage(){await wait;return {ok:true,response:{payload:{created:true,path:'assets/images/late.png',base64}}};}};
    const service=createImageAssetService({state,nativeBridgeClient:bridge});
    const asset={path:'assets/images/late.png',name:'late.png',base64,mimeType:'image/png',storage:'session'};state.managedAssets.set(asset.path,asset);
    const writing=service.persistAsset(asset); await new Promise(resolve=>setTimeout(resolve,20));
    state.nativeWorkspaceId='next';state.imageWorkspaceVersion++;state.managedAssets=new Map();release();await writing;
    const retained=service.recoverableAssets().length;
    const failed={path:'assets/images/failed.png',name:'failed.png',base64,mimeType:'image/png',storage:'session'};state.managedAssets.set(failed.path,failed);
    bridge.createWorkspaceImage=async()=>({ok:false,message:'Permission denied by simulated host'});
    await service.persistAsset(failed);
    const missing={path:'assets/images/missing.png',name:'missing.png',storage:'session'};state.managedAssets.set(missing.path,missing);
    const migration=await service.savePendingImages();
    return {retained,nextHasLate:state.managedAssets.has('assets/images/late.png'),failed:failed.storage,bytes:failed.base64,error:failed.error,missing:missing.error,migration};
  },png);
  expect(result.retained).toBe(1);expect(result.nextHasLate).toBe(false);expect(result.failed).toBe('failed');expect(result.bytes).toBe(png);
  expect(result.error).toContain('Permission denied');expect(result.missing).toContain('bytes are missing');expect(result.migration.failed).toBe(2);
});

test('copied ZIP assets remain portable in a fresh browser context, including captions and duplicate names', async ({ page, browser },testInfo) => {
  await ready(page);
  await importImage(page,'Architecture overview.');
  const downloading=page.waitForEvent('download');
  await page.locator('summary').filter({hasText:/^Export$/}).click();await page.getByRole('button',{name:'Export Markdown Bundle',exact:true}).click();
  const download=await downloading;const file=testInfo.outputPath('workspace-images.zip');await download.saveAs(file);
  const entries=await readZipEntries(file);expect(entries.get('assets/images/capture.png')).toEqual(Buffer.from(png,'base64'));
  expect(getZipText(entries,'images.md')).toContain('<figcaption>Architecture overview.</figcaption>');
  const context=await browser.newContext();const clean=await context.newPage();await clean.goto(new URL('/',page.url()).href);
  await expect(clean.locator('html')).toHaveAttribute('data-app-ready','true');await clean.locator('#zipInput').setInputFiles(file);
  await expect(clean.locator('#preview figure img')).toHaveAttribute('src',/^blob:/);await expect(clean.locator('#preview figcaption')).toHaveText('Architecture overview.');
  expect(await clean.locator('#preview img').evaluate(img=>img.decode().then(()=>img.naturalWidth))).toBe(1);await context.close();
});
