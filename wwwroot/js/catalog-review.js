// Forms marked data-confirm (e.g. "Dismiss all") ask before they submit. Registered in the capture phase so it runs
// before collection-groups.js's AJAX submit handler, which skips events this one cancels.
function confirmBeforeSubmit(event) {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || !form.dataset.confirm) return;

    if (!window.confirm(form.dataset.confirm)) {
        event.preventDefault();
        event.stopImmediatePropagation();
    }
}

document.addEventListener('submit', confirmBeforeSubmit, true);

// A filter marked data-auto-submit (the Kind dropdown) applies as soon as it changes, so the list always matches it.
function submitOnChange(event) {
    const field = event.target;
    if (!(field instanceof HTMLSelectElement) || !field.hasAttribute('data-auto-submit') || !field.form) return;

    field.form.requestSubmit();
}

document.addEventListener('change', submitOnChange);
