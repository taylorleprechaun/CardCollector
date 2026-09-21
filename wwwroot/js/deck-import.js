const DECK_IMPORT_GENERIC_ERROR = 'Something went wrong saving the deck. Please try again.';
const DECK_IMPORT_MISSING_ERROR = 'That event or deck no longer exists. Refresh the page and try again.';
const DECK_PREVIEW_DELAY_MS = 300;

let deckPreviewTimer = null;
let deckPreviewRequest = 0;

function clearDeckImportErrors() {
    const box = document.getElementById('deckImportErrors');
    box.textContent = '';
    box.hidden = true;
}

function deckImportUrl(form) {
    const existing = form.querySelector('#deckImportExisting');
    return existing && existing.value ? form.dataset.linkUrl : form.dataset.importUrl;
}

function openDeckImportModal(trigger) {
    const data = trigger.dataset;
    const hasDeck = data.eventHasDeck === 'true';
    const others = Number(data.eventOthers || 0);
    const url = data.eventUrl || '';

    document.getElementById('deckImportModalLabel').textContent = hasDeck ? 'Change deck' : url ? 'Import deck' : 'Add deck';
    document.getElementById('deckImportEventID').value = data.eventId;
    document.getElementById('deckImportText').value = '';
    document.getElementById('deckImportName').value = data.eventDeck || '';

    const existing = document.getElementById('deckImportExisting');
    if (existing) {
        existing.value = '';
        document.getElementById('deckImportExistingGroup').open = false;
    }

    document.getElementById('deckImportLinkOthers').hidden = others < 1;
    document.getElementById('deckImportLinkOthersInput').checked = true;
    document.getElementById('deckImportLinkOthersLabel').textContent =
        `Also link ${others} other ${others === 1 ? 'event' : 'events'} that use this decklist URL`;

    const openLink = document.getElementById('deckImportOpenLink');
    const isWebAddress = /^https?:\/\//i.test(url);
    openLink.hidden = !isWebAddress;
    if (isWebAddress) openLink.href = url;

    document.getElementById('deckImportRemove').hidden = !hasDeck;
    document.getElementById('deckImportPreview').textContent = '';
    clearDeckImportErrors();

    new bootstrap.Modal(document.getElementById('deckImportModal')).show();
}

async function postDeckImport(url, form) {
    const buttons = form.querySelectorAll('#deckImportSubmit, #deckImportRemove');

    clearDeckImportErrors();
    buttons.forEach((button) => { button.disabled = true; });

    try {
        const response = await fetch(url, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (response.status === 400) {
            showDeckImportErrors((await response.json()).errors);
            return;
        }

        if (!response.ok) {
            showDeckImportErrors([response.status === 404 ? DECK_IMPORT_MISSING_ERROR : DECK_IMPORT_GENERIC_ERROR]);
            return;
        }

        reloadDeckPage();
    } catch (err) {
        console.error('Saving the deck failed', err);
        showDeckImportErrors([DECK_IMPORT_GENERIC_ERROR]);
    } finally {
        buttons.forEach((button) => { button.disabled = false; });
    }
}

// The server left its message in TempData, so a reload is how the user sees the result.
function reloadDeckPage() {
    window.location.reload();
}

async function previewDeckList() {
    const form = document.getElementById('deckImportForm');
    const preview = document.getElementById('deckImportPreview');
    const request = ++deckPreviewRequest;

    preview.classList.remove('text-danger');
    if (!document.getElementById('deckImportText').value.trim()) {
        preview.textContent = '';
        return;
    }

    try {
        const response = await fetch(form.dataset.parseUrl, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        const body = response.ok || response.status === 400 ? await response.json() : null;
        if (request !== deckPreviewRequest) return;

        if (response.status === 400) {
            preview.textContent = body.errors.join(' ');
            preview.classList.add('text-danger');
        } else if (body) {
            preview.textContent = `${body.main} main · ${body.extra} extra · ${body.side} side · ${body.unknown} unknown`;
        } else {
            preview.textContent = '';
        }
    } catch (err) {
        console.error('Previewing the deck list failed', err);
        if (request === deckPreviewRequest) preview.textContent = '';
    }
}

function scheduleDeckPreview() {
    clearTimeout(deckPreviewTimer);
    deckPreviewTimer = setTimeout(previewDeckList, DECK_PREVIEW_DELAY_MS);
}

function showDeckImportErrors(errors) {
    const box = document.getElementById('deckImportErrors');
    box.textContent = errors.join(' ');
    box.hidden = false;
}

// One set of delegated listeners for the page, bound once no matter how many times this script is evaluated.
if (!document.ccDeckImportListenersBound) {
    document.ccDeckImportListenersBound = true;

    document.addEventListener('input', (event) => {
        if (event.target.id === 'deckImportText') scheduleDeckPreview();
    });

    document.addEventListener('submit', (event) => {
        if (event.target.id !== 'deckImportForm') return;
        event.preventDefault();
        postDeckImport(deckImportUrl(event.target), event.target);
    });

    document.addEventListener('click', (event) => {
        const remove = event.target.closest?.('#deckImportRemove');
        if (!remove) return;
        const form = document.getElementById('deckImportForm');
        postDeckImport(form.dataset.unlinkUrl, form);
    });
}
