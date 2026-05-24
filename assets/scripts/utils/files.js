import { supportedFilePattern } from '../state/config.js';

export function isSupportedFile(name) {
  return supportedFilePattern.test(name);
}

export function uniqueByPath(records) {
  const seen = new Set();
  return records.filter((record) => {
    const key = record.path.toLowerCase();
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

export function compareRecords(left, right) {
  return left.path.localeCompare(right.path, undefined, { sensitivity: 'base' });
}

export function getFolderNameFromFileList(files) {
  const firstPath = files.find((file) => file.webkitRelativePath)?.webkitRelativePath;
  return firstPath ? firstPath.split('/')[0] : '';
}

export function getFileExtensionLabel(fileName) {
  const match = fileName.match(/\.([^.]+)$/);
  return match ? match[1].slice(0, 3) : 'txt';
}

export function isMermaidLanguage(language) {
  return ['mermaid', 'mmd'].includes(getCodeLanguage(language));
}

export function getCodeLanguage(language) {
  return String(language ?? '').trim().split(/\s+/)[0].toLowerCase();
}

export function normalisePath(path) {
  return path.replace(/\\/g, '/').replace(/^\/+/, '');
}
