import { storageKeys } from '../state/config.js';
import { downloadBlob } from '../utils/browser.js';

export function exportAppearanceSettings(state, setStatus) {
  try {
    const storedTheme = localStorage.getItem(storageKeys.theme);
    const theme = storedTheme === 'light' ? 'Light' : storedTheme === 'dark' ? 'Dark' : 'System';
    const layout = state.editorLayout === 'editor' ? 'Editor' : state.editorLayout === 'preview' ? 'Preview' : 'Split';
    const payload = {
      formatVersion: 'lens-docs-studio-settings-1.0',
      theme,
      layout,
      showOutline: Boolean(state.outlineOpen),
    };
    downloadBlob(new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json;charset=utf-8' }), 'lens-docs-studio-settings.json');
    setStatus('Appearance settings exported. Import them in native Settings, review the choices and choose Apply.', 'ok');
  } catch (error) {
    setStatus('Appearance settings could not be exported. Your settings are unchanged.', 'danger');
    console.error(error);
  }
}
