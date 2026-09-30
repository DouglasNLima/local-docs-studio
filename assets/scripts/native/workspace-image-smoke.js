import { collectImageTokens } from '../utils/image-references.js';

const PNG = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP432H3HwAHFALF2h7vpgAAAABJRU5ErkJggg==';

// Runs only when the real Windows host advertises an explicit image smoke mode
// with its own fixture root. Browser bridge mocks cannot enter this route.
export async function runWorkspaceImageSmoke({ bridgeClient, app, mode }) {
  const steps = [];
  const errors = [];
  const check = (name, passed, details = {}) => {
    steps.push({ name, passed: Boolean(passed), ...details });
    if (!passed) { errors.push(name); throw new Error(name); }
  };
  const editor = document.querySelector('#editor');
  const imageFile = (name) => new File([Uint8Array.from(atob(PNG), (char) => char.charCodeAt(0))], name, { type: 'image/png' });
  const frame = () => new Promise((resolve) => requestAnimationFrame(resolve));
  async function settled() { await app.renderPreview(); await frame(); await frame(); }
  async function waitFor(predicate) {
    const deadline = performance.now() + 15000;
    while (!predicate()) { if (performance.now() > deadline) throw new Error('Image smoke operation timed out.'); await new Promise((resolve) => setTimeout(resolve, 30)); }
  }
  async function insertFromWorkspace(path, caption = '') {
    document.querySelector('[data-command="image"]').click();
    await waitFor(() => document.querySelector(`[data-image-path="${path}"]`));
    document.querySelector(`[data-image-path="${path}"]`).click();
    await waitFor(() => !document.querySelector('[data-image-insert]').disabled);
    document.querySelector('#imageAltInput').value = 'Capture first, enrich later';
    document.querySelector('#imageCaptionInput').value = caption;
    document.querySelector('[data-image-insert]').click();
    await settled();
  }
  async function importThroughDialog(name) {
    document.querySelector('[data-command="image"]').click();
    const source = document.querySelector('#imageSourceInput'); source.value = 'import'; source.dispatchEvent(new Event('change'));
    const transfer = new DataTransfer(); transfer.items.add(imageFile(name));
    const input = document.querySelector('#imageImportInput'); input.files = transfer.files; input.dispatchEvent(new Event('change'));
    await waitFor(() => !document.querySelector('[data-image-insert]').disabled);
    document.querySelector('#imageAltInput').value = 'Imported capture';
    document.querySelector('[data-image-insert]').click();
    await settled();
  }
  try {
    const workspace = await bridgeClient.openSmokeFixtureWorkspace();
    check('Real Windows workspace opened', workspace.ok && workspace.response?.payload?.nativeWorkspaceId);
    await app.loadNativeSmokeWorkspace(workspace.response.payload);
    await app.selectFile('README.md');
    const id = app.state.nativeWorkspaceId;
    const inventory = await bridgeClient.listWorkspaceImages({ nativeWorkspaceId: id });
    check('Existing physical images inventoried', inventory.ok && inventory.response.payload.images.some((image) => image.path === 'assets/capture.png'));
    check('One Insert image toolbar action', document.querySelectorAll('[data-command="image"], [data-command="imageFigure"]').length === 1);

    if (mode === 'create') {
      const content = editor.value;
      const cursor = content.indexOf('Line 250');
      editor.focus({ preventScroll: true }); editor.setSelectionRange(cursor, cursor);
      editor.scrollTop = 250 * parseFloat(getComputedStyle(editor).lineHeight) - 100;
      const top = editor.scrollTop;
      await insertFromWorkspace('assets/capture.png');
      check('Existing image inserted as document-relative Markdown', editor.value.includes('![Capture first, enrich later](assets/capture.png)'));
      check('Editor scroll stable after rendering and image decoding', Math.abs(editor.scrollTop - top) <= 2, { before: top, after: editor.scrollTop });
      check('Image persistence does not mark Markdown saved', app.state.dirtyPaths.has('README.md'));
      await insertFromWorkspace('assets/capture.png', 'Architecture overview.');
      check('Caption uses the shared figure pipeline', editor.value.includes('<figcaption>Architecture overview.</figcaption>'));
      const old = editor.value;
      const transfer = new DataTransfer(); transfer.items.add(imageFile('pasted-capture.png'));
      editor.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: transfer }));
      await waitFor(() => editor.value !== old); await settled();
      check('Paste writes and verifies physical image bytes', (await bridgeClient.readWorkspaceImage({ nativeWorkspaceId: id, path: 'assets/images/pasted-capture.png' })).response?.payload?.base64 === PNG);
      const previous = editor.value;
      document.querySelector('#editorShell').dispatchEvent(new DragEvent('drop', { bubbles: true, cancelable: true, dataTransfer: transfer }));
      await waitFor(() => editor.value !== previous); await settled();
      check('Drop avoids an on-disk name collision', editor.value.includes('assets/images/pasted-capture-2.png'));
      await importThroughDialog('imported-capture.png');
      check('Import file creates physical workspace bytes', (await bridgeClient.readWorkspaceImage({ nativeWorkspaceId: id, path: 'assets/images/imported-capture.png' })).response?.payload?.base64 === PNG);
      await app.saveActiveFile();
      check('Markdown saved explicitly', !app.state.dirtyPaths.has('README.md'));
      await app.selectFile('docs/details.md');
      editor.setSelectionRange(editor.value.length, editor.value.length);
      await insertFromWorkspace('assets/capture.png', 'Subfolder architecture.');
      await importThroughDialog('subfolder-capture.png');
      check('Subfolder figure and Markdown use parent-relative references', editor.value.includes('src="../assets/capture.png"') && editor.value.includes('../assets/images/subfolder-capture.png'));
      await app.saveActiveFile();
      for (const path of ['../outside.png', 'C:\\outside.png', 'assets/../../outside.png', 'assets\\escape\\outside.png', 'assets/junction/outside.png']) {
        const blocked = await bridgeClient.readWorkspaceImage({ nativeWorkspaceId: id, path });
        check(`Unsafe or unavailable image rejected: ${path}`, !blocked.ok);
      }
      const spoof = await bridgeClient.createWorkspaceImage({ nativeWorkspaceId: id, name: 'spoof.png', base64: btoa('<svg onload="alert(1)"></svg>'), mimeType: 'image/png' });
      check('Host rejects spoofed raster bytes', !spoof.ok);
      const badWorkspace = await bridgeClient.createWorkspaceImage({ nativeWorkspaceId: 'not-authorised', name: 'capture.png', base64: PNG, mimeType: 'image/png' });
      check('Host rejects an unauthorised workspace capability', !badWorkspace.ok);
      const canvas = document.createElement('canvas'); canvas.width = 1; canvas.height = 1;
      canvas.getContext('2d').fillRect(0, 0, 1, 1);
      for (const [extension, mimeType] of [['jpg', 'image/jpeg'], ['webp', 'image/webp']]) {
        const encoded = canvas.toDataURL(mimeType).split(',')[1];
        const saved = await bridgeClient.createWorkspaceImage({ nativeWorkspaceId: id, name: `codec.${extension}`, base64: encoded, mimeType });
        check(`Real host validates and writes ${extension} bytes`, saved.ok && saved.response?.payload?.base64 === encoded);
      }
      const gif = 'R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';
      const savedGif = await bridgeClient.createWorkspaceImage({ nativeWorkspaceId: id, name: 'codec.gif', base64: gif, mimeType: 'image/gif' });
      check('Real host validates and writes GIF bytes', savedGif.ok && savedGif.response?.payload?.base64 === gif);

      await bridgeClient.createWorkspaceImage({ nativeWorkspaceId: id, name: 'pending.png', base64: PNG, mimeType: 'image/png' });
      // Deliberately create a session asset before restoring the real capability;
      // materialisation below goes through the actual host and physical collision.
      app.state.nativeWorkspaceId = '';
      try { await app.imageAssets.acquireFile(imageFile('pending.png')); }
      finally { app.state.nativeWorkspaceId = id; }
      const rootSource = app.state.fileCache.get('README.md');
      app.state.fileCache.set('README.md', `${rootSource}\n![Pending](assets/images/pending.png)\n\n\`\`\`md\n![Example](assets/images/pending.png)\n\`\`\`\n`);
      app.state.dirtyPaths.add('README.md');
      editor.value += '\n<figure data-image-figure><img src="../assets/images/pending.png" alt="Pending"><figcaption>Pending caption.</figcaption></figure>\n';
      editor.dispatchEvent(new Event('input', { bubbles: true }));
      const pendingSource = editor.value;
      const cancelledSave = app.saveActiveFile();
      await waitFor(() => document.querySelector('#appDialog').open);
      check('Real Windows save displays a readable pending-image confirmation', document.querySelector('#appDialogTitle').textContent === 'Images are not saved to the folder'
        && document.querySelector('#appDialogMessage').textContent.includes('Saving Markdown writes references only.')
        && !document.querySelector('#appDialogMessage').textContent.includes('[object Object]')
        && document.querySelector('#appDialogConfirmButton').textContent === 'Save Markdown links'
        && document.querySelector('#appDialogCancelButton').textContent === 'Keep editing');
      document.querySelector('#appDialogCancelButton').click();
      await cancelledSave;
      check('Cancelling the Windows save retains pending Markdown edits', editor.value === pendingSource && app.state.dirtyPaths.has('docs/details.md'));
      await app.selectFile('README.md');
      const migrationCursor = editor.value.indexOf('Line 250');
      editor.focus({ preventScroll: true }); editor.setSelectionRange(migrationCursor, migrationCursor);
      editor.scrollTop = 250 * parseFloat(getComputedStyle(editor).lineHeight) - 100;
      const migrationTop = editor.scrollTop;
      const migrated = await app.imageAssets.savePendingImages();
      await settled();
      check('Real pending migration avoids an on-disk collision', migrated.saved === 1 && app.state.managedAssets.has('assets/images/pending-2.png'));
      check('Physical migration updates root Markdown and subfolder figures only', app.state.fileCache.get('README.md').includes('![Pending](assets/images/pending-2.png)')
        && app.state.fileCache.get('README.md').includes('![Example](assets/images/pending.png)') && app.state.fileCache.get('docs/details.md').includes('src="../assets/images/pending-2.png"'));
      check('Pending migration preserves the active editor viewport', Math.abs(editor.scrollTop - migrationTop) <= 2, { before: migrationTop, after: editor.scrollTop });
      const again = await app.imageAssets.savePendingImages();
      check('Repeated physical materialisation creates no duplicate', again.saved === 0 && again.failed === 0);
      await app.saveActiveFile(); await app.selectFile('docs/details.md'); await app.saveActiveFile();
    } else {
      check('Clean profile contains no previous session assets before reading', app.state.managedAssets.get('assets/images/pasted-capture.png')?.storage === 'workspace');
      check('Copied root document renders all referenced images', document.querySelectorAll('#preview img[data-managed-asset-path]').length >= 5 && !document.querySelector('#preview [data-image-missing]'));
      check('Copied root caption survives', document.querySelector('#preview figcaption')?.textContent === 'Architecture overview.');
      await app.selectFile('docs/details.md'); await settled();
      check('Copied subfolder images and caption render', document.querySelectorAll('#preview img[data-managed-asset-path]').length === 3 && document.querySelector('#preview figcaption')?.textContent === 'Subfolder architecture.');
      const bundle = await app.buildMarkdownBundle();
      check('Markdown Bundle built from copied physical workspace', bundle instanceof Blob && bundle.size > 100);
      await app.importZipFile(new File([bundle], 'image-round-trip.zip', { type: 'application/zip' }));
      await app.selectFile('docs/details.md'); await settled();
      check('Bundle round-trip discards physical workspace capability', !app.state.nativeWorkspaceId && !app.state.workspaceDirectoryHandle);
      check('Bundle round-trip renders figure and Markdown from included bytes', document.querySelectorAll('#preview img[data-managed-asset-path]').length === 3 && !document.querySelector('#preview [data-image-missing]'));
      check('Bundle bytes match the physical images', [...app.state.managedAssets.values()].filter((asset) => asset.base64).every((asset) => asset.base64 === PNG));
      check('Bundle references remain document-relative', collectImageTokens(editor.value).every((token) => token.href.startsWith('../assets/')));
    }
  } catch (error) { errors.push(error.message); }
  const result = { success: errors.length === 0, mode, steps, errors, realHost: true, syntheticClipboardAndDropEvents: true, completedAt: new Date().toISOString() };
  await bridgeClient.completeSmoke(result);
  return result;
}
