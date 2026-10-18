// The catalog card's details panel shows its fields read-only. "Edit fields" swaps in the form; Cancel puts back the
// read-only view and resets the form, so a half-made edit isn't still there the next time it opens.
function toggleFieldEditor(panel, editing) {
    const view = panel.querySelector('[data-field-view]');
    const editor = panel.querySelector('[data-field-editor]');
    const openButton = panel.querySelector('[data-field-edit="open"]');
    if (!view || !editor) return;

    view.hidden = editing;
    editor.hidden = !editing;
    if (openButton) openButton.hidden = editing;

    if (editing) {
        editor.querySelector('input, textarea, select')?.focus();
    } else {
        editor.querySelector('form')?.reset();
        openButton?.focus();
    }
}

function handleFieldEditClick(event) {
    const button = event.target instanceof Element ? event.target.closest('[data-field-edit]') : null;
    const panel = button?.closest('[data-field-panel]');
    if (!panel) return;

    toggleFieldEditor(panel, button.dataset.fieldEdit === 'open');
}

document.addEventListener('click', handleFieldEditClick);
