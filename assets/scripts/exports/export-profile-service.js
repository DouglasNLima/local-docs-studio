const BUILT_IN_EXPORT_PROFILES = [
  {
    id: 'generic-documentation',
    label: 'Generic documentation',
    description: 'Default local Markdown, Mermaid, and documentation exports.',
  },
  {
    id: 'github-pages-docs-site',
    label: 'GitHub Pages docs site',
    description: 'Docs Site defaults for a static Pages-ready ZIP. No deployment is started.',
    docsSite: true,
  },
  {
    id: 'azure-devops-wiki-markdown',
    label: 'Azure DevOps Wiki markdown',
    description: 'Uses the existing DevOps Mermaid Markdown conversion for bundle exports.',
    sessionDevopsMarkdownExport: true,
  },
  {
    id: 'artefact-review-pack',
    label: 'Artefact review pack',
    description: 'Current-session review defaults for a loaded artefact bundle.',
    requiresArtifactBundle: true,
    artifactReview: true,
  },
];

export function createExportProfileService({
  state,
  dom,
  callbacks = {},
} = {}) {
  const {
    builtInExportProfileList,
    activeExportProfileLabel,
    devopsMarkdownExportToggle,
    exportArtifactReviewPackButton,
  } = dom;
  const {
    getExportTitle = () => '',
    setStatus = () => {},
  } = callbacks;

  function renderBuiltInProfiles() {
    renderActiveProfileLabel();
    renderReviewExportButton();
    if (!builtInExportProfileList) return;
    builtInExportProfileList.replaceChildren();
    BUILT_IN_EXPORT_PROFILES.forEach((profile) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'export-profile-preset';
      button.dataset.builtInExportProfile = profile.id;
      const unavailableReason = getUnavailableReason(profile);
      button.disabled = Boolean(unavailableReason);
      button.title = unavailableReason || profile.description;
      button.setAttribute('aria-pressed', String(state.activeBuiltInExportProfile === profile.id));

      const label = document.createElement('span');
      label.className = 'export-profile-preset-label';
      label.textContent = profile.label;
      const description = document.createElement('span');
      description.className = 'export-profile-preset-description';
      description.textContent = unavailableReason || profile.description;
      button.append(label, description);
      builtInExportProfileList.appendChild(button);
    });
  }

  function installBuiltInProfileHandlers() {
    if (!builtInExportProfileList) return;
    builtInExportProfileList.addEventListener('click', (event) => {
      const button = event.target.closest('[data-built-in-export-profile]');
      if (!button || button.disabled) return;
      event.stopPropagation();
      applyBuiltInProfile(button.dataset.builtInExportProfile);
    });
  }

  function applyBuiltInProfile(profileId) {
    const profile = BUILT_IN_EXPORT_PROFILES.find((item) => item.id === profileId);
    if (!profile) {
      setStatus('Built-in export profile was not found.', 'warning');
      return false;
    }

    const unavailableReason = getUnavailableReason(profile);
    if (unavailableReason) {
      setStatus(unavailableReason, 'warning');
      renderBuiltInProfiles();
      return false;
    }

    state.activeBuiltInExportProfile = profile.id;
    state.sessionDevopsMarkdownExport = profile.sessionDevopsMarkdownExport === true
      ? true
      : profile.id === 'generic-documentation'
        ? false
        : null;
    state.exportProfileDefaults = buildSessionDefaults(profile);
    restoreEffectiveDevopsToggle();
    renderBuiltInProfiles();
    setStatus(`Applied built-in export profile "${profile.label}" for this session.`, 'ok');
    return true;
  }

  function applySavedLocalProfile(profile) {
    state.activeBuiltInExportProfile = '';
    state.sessionDevopsMarkdownExport = null;
    state.exportProfileDefaults = profile;
    restoreEffectiveDevopsToggle();
    renderBuiltInProfiles();
  }

  function resetSessionProfile() {
    const hadBuiltInProfile = Boolean(state.activeBuiltInExportProfile);
    state.activeBuiltInExportProfile = '';
    state.sessionDevopsMarkdownExport = null;
    if (hadBuiltInProfile) state.exportProfileDefaults = null;
    restoreEffectiveDevopsToggle();
    renderBuiltInProfiles();
  }

  function getEffectiveDevopsMarkdownExport() {
    return state.sessionDevopsMarkdownExport === null || state.sessionDevopsMarkdownExport === undefined
      ? Boolean(state.devopsMarkdownExport)
      : Boolean(state.sessionDevopsMarkdownExport);
  }

  function restoreEffectiveDevopsToggle() {
    if (!devopsMarkdownExportToggle) return;
    devopsMarkdownExportToggle.checked = getEffectiveDevopsMarkdownExport();
    devopsMarkdownExportToggle.title = state.sessionDevopsMarkdownExport === null || state.sessionDevopsMarkdownExport === undefined
      ? 'Persisted Azure DevOps Mermaid export preference'
      : 'Session export profile override; using this toggle will update the persisted preference';
  }

  function handlePersistedDevopsToggleChange() {
    state.sessionDevopsMarkdownExport = null;
    state.activeBuiltInExportProfile = '';
    state.exportProfileDefaults = null;
    renderBuiltInProfiles();
  }

  function renderActiveProfileLabel() {
    if (!activeExportProfileLabel) return;
    const profile = BUILT_IN_EXPORT_PROFILES.find((item) => item.id === state.activeBuiltInExportProfile);
    activeExportProfileLabel.textContent = profile
      ? `Active built-in profile: ${profile.label} (session only)`
      : 'Active built-in profile: None';
  }

  function renderReviewExportButton() {
    if (!exportArtifactReviewPackButton) return;
    const disabled = !state.artifactBundle;
    exportArtifactReviewPackButton.disabled = disabled;
    exportArtifactReviewPackButton.title = disabled
      ? 'Artefact review pack requires an imported artefact bundle.'
      : 'Export Markdown files with a safe rebuilt artefact bundle manifest.';
  }

  function buildSessionDefaults(profile) {
    const title = state.folderName || getExportTitle() || 'Docs site';
    if (profile.id === 'github-pages-docs-site') {
      return {
        label: profile.label,
        docsSite: {
          title,
          description: `Static documentation bundle with ${Math.max(state.files.length, 1)} page${state.files.length === 1 ? '' : 's'}.`,
          theme: 'system',
        },
      };
    }
    if (profile.id === 'artefact-review-pack') {
      return {
        label: profile.label,
        docsSite: {
          title: state.artifactBundle?.title || title,
          description: `Static documentation review bundle with ${Math.max(state.files.length, 1)} page${state.files.length === 1 ? '' : 's'}.`,
          theme: 'system',
        },
        artifactReview: true,
      };
    }
    return {
      label: profile.label,
      docsSite: null,
    };
  }

  function getUnavailableReason(profile) {
    if (profile.requiresArtifactBundle && !state.artifactBundle) {
      return 'Artefact review pack requires an imported artefact bundle.';
    }
    return '';
  }

  return {
    renderBuiltInProfiles,
    installBuiltInProfileHandlers,
    applySavedLocalProfile,
    resetSessionProfile,
    getEffectiveDevopsMarkdownExport,
    restoreEffectiveDevopsToggle,
    handlePersistedDevopsToggleChange,
  };
}
