import { decodeZipText } from '../utils/zip.js';
import { normalisePath } from '../utils/files.js';

export const LENS_ARTIFACT_BUNDLE_MANIFEST_NAME = 'lens-artifact-bundle.json';
export const LENS_ARTIFACT_BUNDLE_FORMAT_VERSION = 'lens-artifact-bundle-1.0';
export const MAX_LENS_ARTIFACT_BUNDLE_MANIFEST_BYTES = 256 * 1024;

export const supportedEvidenceLabels = [
  'static evidence',
  'connected metadata evidence',
  'runtime evidence',
  'manual evidence',
  'candidate finding',
  'confirmed finding',
  'blocked/unavailable evidence',
];

const supportedEvidenceLabelSet = new Set(supportedEvidenceLabels);
const maxDisplayTextLength = 180;
const maxSummaryTextLength = 420;

export function parseLensArtifactBundleManifest(bytes, importedRecords = []) {
  const metadataWarnings = [];
  if (!bytes) return { bundle: null, warnings: [] };

  if (bytes.byteLength > MAX_LENS_ARTIFACT_BUNDLE_MANIFEST_BYTES) {
    return {
      bundle: null,
      warnings: ['Artefact bundle manifest is too large.'],
    };
  }

  let manifest;
  try {
    manifest = JSON.parse(decodeZipText(bytes));
  } catch {
    return {
      bundle: null,
      warnings: ['Artefact bundle manifest could not be read.'],
    };
  }

  if (!manifest || typeof manifest !== 'object' || Array.isArray(manifest)) {
    return {
      bundle: null,
      warnings: ['Artefact bundle manifest could not be read.'],
    };
  }

  if (manifest.formatVersion !== LENS_ARTIFACT_BUNDLE_FORMAT_VERSION) {
    return {
      bundle: null,
      warnings: ['Artefact bundle manifest format is not supported.'],
    };
  }

  const importedPathSet = new Set(importedRecords.map((record) => record.path));
  const documentEntries = normaliseManifestEntries(manifest.documents, importedPathSet, 'document', metadataWarnings);
  const diagramEntries = normaliseManifestEntries(manifest.diagrams, importedPathSet, 'diagram', metadataWarnings);
  const manifestWarnings = normaliseManifestWarnings(manifest.warnings, metadataWarnings);
  const entryDocument = normaliseOptionalPath(manifest.entryDocument);
  let safeEntryDocument = '';

  if (manifest.entryDocument !== undefined) {
    if (!entryDocument) {
      metadataWarnings.push('entry document path was ignored.');
    } else if (!importedPathSet.has(entryDocument)) {
      metadataWarnings.push('entry document was not found.');
    } else {
      safeEntryDocument = entryDocument;
    }
  }

  const evidenceLevels = collectEvidenceLevels(manifest.evidenceLevels, [
    ...documentEntries,
    ...diagramEntries,
    ...manifestWarnings,
  ], metadataWarnings);

  const recordMetadataByPath = new Map();
  [...documentEntries, ...diagramEntries].forEach((entry) => {
    if (!recordMetadataByPath.has(entry.path)) {
      recordMetadataByPath.set(entry.path, entry);
    }
  });

  return {
    bundle: {
      formatVersion: LENS_ARTIFACT_BUNDLE_FORMAT_VERSION,
      sourceTool: normaliseDisplayText(manifest.sourceTool),
      sourceToolVersion: normaliseDisplayText(manifest.sourceToolVersion),
      generatedAtUtc: normaliseGeneratedAt(manifest.generatedAtUtc),
      entryDocument: safeEntryDocument,
      title: normaliseDisplayText(manifest.title),
      summary: normaliseDisplayText(manifest.summary, maxSummaryTextLength),
      evidenceLevels,
      documents: documentEntries,
      diagrams: diagramEntries,
      manifestWarnings,
      metadataWarnings: uniqueWarnings(metadataWarnings),
      recordMetadataByPath,
    },
    warnings: uniqueWarnings(metadataWarnings),
  };
}

export function normaliseOptionalPath(value) {
  if (typeof value !== 'string') return '';
  const raw = value.trim();
  if (!raw || /[\u0000-\u001f\u007f]/.test(raw)) return '';
  if (/^[a-z][a-z0-9+.-]*:/i.test(raw)) return '';

  const slashPath = raw.replace(/\\/g, '/');
  if (!slashPath || slashPath.startsWith('/') || /^[a-z]:/i.test(slashPath)) return '';

  const parts = slashPath.split('/').filter(Boolean);
  if (!parts.length) return '';
  if (parts.some((part) => part === '.' || part === '..')) return '';

  const path = normalisePath(parts.join('/'));
  if (!path || /^[a-z][a-z0-9+.-]*:/i.test(path)) return '';
  return path;
}

function normaliseManifestEntries(entries, importedPathSet, kind, metadataWarnings) {
  if (entries === undefined) return [];
  if (!Array.isArray(entries)) {
    metadataWarnings.push(`${kind} metadata was ignored.`);
    return [];
  }

  const unsafePathCount = { value: 0 };
  const missingPathCount = { value: 0 };
  const normalisedEntries = [];

  entries.forEach((entry) => {
    if (!entry || typeof entry !== 'object' || Array.isArray(entry)) return;
    const path = normaliseOptionalPath(entry.path);
    if (!path) {
      unsafePathCount.value += 1;
      return;
    }
    if (!importedPathSet.has(path)) {
      missingPathCount.value += 1;
      return;
    }

    normalisedEntries.push({
      path,
      title: normaliseDisplayText(entry.title),
      kind: normaliseDisplayText(entry.kind),
      evidenceLevel: normaliseEvidenceLabel(entry.evidenceLevel),
      order: normaliseOrder(entry.order),
      type: kind,
    });
  });

  if (unsafePathCount.value) metadataWarnings.push(`${kind} metadata paths were ignored.`);
  if (missingPathCount.value) metadataWarnings.push(`${kind} metadata paths were not found in the ZIP.`);

  return dedupeEntriesByPath(normalisedEntries);
}

function normaliseManifestWarnings(warnings, metadataWarnings) {
  if (warnings === undefined) return [];
  if (!Array.isArray(warnings)) {
    metadataWarnings.push('warning metadata was ignored.');
    return [];
  }

  return warnings
    .filter((warning) => warning && typeof warning === 'object' && !Array.isArray(warning))
    .map((warning) => ({
      code: normaliseDisplayText(warning.code),
      message: normaliseDisplayText(warning.message, maxSummaryTextLength),
      evidenceLevel: normaliseEvidenceLabel(warning.evidenceLevel),
    }))
    .filter((warning) => warning.code || warning.message || warning.evidenceLevel)
    .slice(0, 20);
}

function collectEvidenceLevels(declaredLevels, entries, metadataWarnings) {
  const levels = [];
  const addLevel = (value) => {
    const level = normaliseEvidenceLabel(value);
    if (level && !levels.includes(level)) levels.push(level);
  };

  if (declaredLevels !== undefined) {
    if (Array.isArray(declaredLevels)) {
      declaredLevels.forEach(addLevel);
    } else {
      metadataWarnings.push('evidence level metadata was ignored.');
    }
  }

  entries.forEach((entry) => addLevel(entry.evidenceLevel));
  return levels;
}

function normaliseEvidenceLabel(value) {
  return typeof value === 'string' && supportedEvidenceLabelSet.has(value.trim())
    ? value.trim()
    : '';
}

function normaliseGeneratedAt(value) {
  if (typeof value !== 'string' || !value.trim().endsWith('Z')) return '';
  const date = new Date(value.trim());
  return Number.isNaN(date.getTime()) ? '' : date.toISOString();
}

function normaliseDisplayText(value, maxLength = maxDisplayTextLength) {
  if (typeof value !== 'string') return '';
  return value
    .replace(/[\u0000-\u001f\u007f]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .slice(0, maxLength);
}

function normaliseOrder(value) {
  const order = Number(value);
  return Number.isFinite(order) ? order : null;
}

function dedupeEntriesByPath(entries) {
  const seen = new Set();
  return entries.filter((entry) => {
    const key = entry.path.toLowerCase();
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function uniqueWarnings(warnings) {
  return [...new Set(warnings)].slice(0, 8);
}
