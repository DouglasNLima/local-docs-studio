import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createZipBuffer } from './zip.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const artifactBundleFixtureRoot = path.join(root, 'tests', 'fixtures', 'artifact-bundles');
const textFixturePattern = /\.(md|markdown|mmd|mermaid|json|txt|svg|html|css|js)$/i;
const ignoredFixtureNames = new Set(['__MACOSX', 'Thumbs.db', '.DS_Store']);

export async function readArtifactBundleFixtureEntries(name) {
  const directory = path.join(artifactBundleFixtureRoot, name);
  const files = await walkFixtureDirectory(directory);
  const entries = [];

  for (const filePath of files.sort((left, right) => left.localeCompare(right))) {
    const zipPath = path.relative(directory, filePath).replaceAll(path.sep, '/');
    entries.push({
      name: zipPath,
      data: textFixturePattern.test(filePath)
        ? await readFile(filePath, 'utf8')
        : await readFile(filePath),
    });
  }

  return entries;
}

export async function createArtifactBundleFixtureZip(name, options = { compress: true }) {
  return createZipBuffer(await readArtifactBundleFixtureEntries(name), options);
}

async function walkFixtureDirectory(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    if (entry.name.startsWith('.') || ignoredFixtureNames.has(entry.name)) continue;
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      files.push(...await walkFixtureDirectory(fullPath));
      continue;
    }
    if (entry.isFile()) files.push(fullPath);
  }

  return files;
}
