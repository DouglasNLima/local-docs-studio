export function createDialogService({ dom }) {
  const {
    appDialog,
    appDialogForm,
    appDialogKicker,
    appDialogTitle,
    appDialogMessage,
    appDialogFields,
    appDialogCloseButton,
    appDialogCancelButton,
    appDialogConfirmButton,
  } = dom;

  let activeResolve = null;
  let validatePrompt = null;

  function confirm(message, options = {}) {
    return openDialog({
      mode: 'confirm',
      title: options.title || 'Confirm action',
      message,
      kicker: options.kicker || 'Confirm',
      confirmLabel: options.confirmLabel || 'Continue',
      cancelLabel: options.cancelLabel || 'Cancel',
      danger: Boolean(options.danger),
    });
  }

  function prompt(options = {}) {
    return openDialog({
      mode: 'prompt',
      title: options.title || 'Enter value',
      message: options.message || '',
      kicker: options.kicker || 'Input',
      label: options.label || options.title || 'Value',
      value: options.value || '',
      placeholder: options.placeholder || '',
      hint: options.hint || '',
      confirmLabel: options.confirmLabel || 'OK',
      cancelLabel: options.cancelLabel || 'Cancel',
      validate: options.validate,
    });
  }

  function installDialogHandlers() {
    appDialogForm?.addEventListener('submit', handleSubmit);
    appDialog?.addEventListener('cancel', (event) => {
      event.preventDefault();
      closeDialog(null);
    });
    appDialog?.addEventListener('click', (event) => {
      if (event.target === appDialog || event.target.closest('[data-app-dialog-cancel]')) {
        closeDialog(null);
      }
    });
    appDialogCloseButton?.addEventListener('click', () => closeDialog(null));
    appDialogCancelButton?.addEventListener('click', () => closeDialog(null));
  }

  function openDialog(options) {
    if (!appDialog || !appDialogForm || !appDialogFields) {
      return Promise.resolve(options.mode === 'confirm' ? true : null);
    }

    if (activeResolve) closeDialog(null);

    appDialog.dataset.dialogMode = options.mode;
    appDialogKicker.textContent = options.kicker || '';
    appDialogTitle.textContent = options.title || '';
    appDialogMessage.textContent = options.message || '';
    appDialogMessage.hidden = !options.message;
    appDialogFields.innerHTML = '';
    appDialogConfirmButton.textContent = options.confirmLabel || 'OK';
    appDialogCancelButton.textContent = options.cancelLabel || 'Cancel';
    appDialogConfirmButton.classList.toggle('danger', Boolean(options.danger));
    appDialogConfirmButton.classList.add('primary');
    validatePrompt = null;

    if (options.mode === 'prompt') {
      renderPromptFields(options);
    }

    if (typeof appDialog.showModal === 'function') {
      appDialog.showModal();
    } else {
      appDialog.setAttribute('open', '');
    }

    return new Promise((resolve) => {
      activeResolve = resolve;
      window.requestAnimationFrame(() => {
        const firstField = appDialogFields.querySelector('input, textarea, select');
        if (firstField) {
          firstField.focus();
          if (typeof firstField.select === 'function') firstField.select();
        } else {
          appDialogConfirmButton.focus();
        }
      });
    });
  }

  function renderPromptFields(options) {
    const field = document.createElement('label');
    field.className = 'template-field';

    const label = document.createElement('span');
    label.className = 'field-label';
    label.textContent = options.label || 'Value';

    const input = document.createElement('input');
    input.id = 'appDialogPromptInput';
    input.name = 'value';
    input.type = 'text';
    input.autocomplete = 'off';
    input.value = options.value || '';
    input.placeholder = options.placeholder || '';

    const error = document.createElement('small');
    error.className = 'dialog-error-message';
    error.setAttribute('role', 'alert');
    error.hidden = true;

    field.append(label, input);
    if (options.hint) {
      const hint = document.createElement('small');
      hint.textContent = options.hint;
      field.appendChild(hint);
    }
    field.appendChild(error);
    appDialogFields.appendChild(field);

    validatePrompt = () => {
      const value = input.value.trim();
      const result = options.validate?.(value);
      const message = typeof result === 'string' ? result : '';
      if (message) {
        error.textContent = message;
        error.hidden = false;
        input.focus();
        input.select();
        return { ok: false };
      }
      error.textContent = '';
      error.hidden = true;
      return { ok: true, value };
    };
  }

  function handleSubmit(event) {
    event.preventDefault();
    if (appDialog?.dataset.dialogMode === 'prompt') {
      const result = validatePrompt?.();
      if (!result?.ok) return;
      closeDialog(result.value);
      return;
    }

    closeDialog(true);
  }

  function closeDialog(value) {
    const resolve = activeResolve;
    activeResolve = null;
    validatePrompt = null;

    if (appDialog?.open && typeof appDialog.close === 'function') {
      appDialog.close();
    } else {
      appDialog?.removeAttribute('open');
    }

    resolve?.(value);
  }

  return {
    confirm,
    prompt,
    installDialogHandlers,
  };
}
