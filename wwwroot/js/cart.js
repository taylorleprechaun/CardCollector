// Price inputs save this long after the last keystroke, so typing a price is one save rather than one per key.
const CART_SAVE_DELAY_MS = 600;

document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[id^="cart-row-"]').forEach(initCartRow);

    document.querySelectorAll('.cart-remove-form').forEach(function (form) {
        form.addEventListener('submit', function (event) {
            event.preventDefault();
            removeCartLine(form);
        });
    });

    // Plain POST + redirect, so the button never needs re-enabling — the next page load resets it.
    const submitForm = document.getElementById('cartSubmitForm');
    submitForm?.addEventListener('submit', function () {
        const submitBtn = submitForm.querySelector('button[type="submit"]');
        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.textContent = 'Submitting…';
        }
    });
});

// Every field on a cart line is saved as it changes, so a reload (or a Remove elsewhere in the cart) never loses
// what was typed. Submit All still posts the on-screen values, so a save that hasn't run yet can't lose anything.
function initCartRow(row) {
    // Cart's Market Price starts pre-filled from the stage-time snapshot, so unlike the
    // entry modals this only wires the refresh — it doesn't fetch immediately on load.
    const editionSelect = row.querySelector('select[data-market-price-target]');
    const marketPriceEl = editionSelect ? document.getElementById(editionSelect.dataset.marketPriceTarget) : null;
    if (editionSelect && marketPriceEl) {
        bindPriceRefresh(editionSelect, marketPriceEl, () => ({ printingID: editionSelect.dataset.printingId }), () => saveCartLine(row));
    }

    row.addEventListener('change', () => saveCartLine(row));

    row.addEventListener('input', function (event) {
        if (!event.target.matches('input[type="number"]')) return;
        recomputeCartSummary();
        scheduleCartLineSave(row);
    });

    // The quantity, Today, and copy-price buttons set values from script, which fires no change event.
    // This listener is on the row, so it runs after the button's own onclick has set the value.
    row.addEventListener('click', function (event) {
        if (!event.target.closest('button[type="button"]')) return;
        recomputeCartSummary();
        saveCartLine(row);
    });

    // The calendar's own Today button sets the date without a change event and sits outside the row.
    row.querySelectorAll('.cc-date-picker').forEach(function (dateEl) {
        dateEl._flatpickr?.config.onClose.push(() => saveCartLine(row));
    });
}

// Saves run one at a time per row and read the fields when they start, so the last save always carries the latest values.
async function postCartLine(row) {
    const formData = new FormData();
    ['PendingOrderLineID', 'Condition', 'Edition', 'Quantity', 'PurchaseDate', 'PurchasePrice', 'MarketPriceAtEntry'].forEach(function (field) {
        formData.append(field, row.querySelector(`[name$="].${field}"]`)?.value ?? '');
    });
    const token = document.querySelector('#cartSubmitForm [name="__RequestVerificationToken"]')?.value;
    if (token) formData.append('__RequestVerificationToken', token);

    // Successful saves stay silent: status text appearing and disappearing on every edit shifted the row's layout.
    const statusEl = row.querySelector('[data-save-status]');
    try {
        const response = await postAjax('/Cart?handler=UpdateLine', formData);
        if (!response.ok) {
            console.warn('Failed to save cart line, status', response.status);
            setCartLineSaveError(statusEl, true);
            return;
        }
        setCartLineSaveError(statusEl, false);
    } catch (err) {
        console.warn('Failed to save cart line:', err);
        setCartLineSaveError(statusEl, true);
    }
}

function recomputeCartSummary() {
    let lineCount = 0;
    let cardCount = 0;
    let total = 0;
    document.querySelectorAll('input[id^="linePrice"]').forEach(function (priceEl) {
        const index = priceEl.id.replace('linePrice', '');
        const quantityEl = document.getElementById('lineQuantity' + index);
        const price = parseFloat(priceEl.value) || 0;
        const quantity = quantityEl ? (parseFloat(quantityEl.value) || 0) : 0;
        lineCount++;
        cardCount += quantity;
        total += price * quantity;
    });

    const totalEl = document.getElementById('cartTotal');
    if (totalEl) totalEl.textContent = '$' + total.toFixed(2);

    const summaryEl = document.getElementById('cartSummary');
    if (summaryEl) summaryEl.textContent = `${formatCountLabel(lineCount, 'line')} · ${formatCountLabel(cardCount, 'card')}`;
}

function reloadCartPage() {
    window.location.reload();
}

// Removes the line in place so the other lines keep what was typed into them; the page reloads only once
// the cart is empty, to show the empty-cart message.
async function removeCartLine(form) {
    const row = form.closest('[id^="cart-row-"]');
    const button = form.querySelector('button[type="submit"]');
    if (button) button.disabled = true;

    try {
        const response = await postAjax(form.action, new FormData(form));
        if (!response.ok) throw new Error(`status ${response.status}`);

        const list = row?.parentElement;
        if (row) {
            clearTimeout(row._cartSaveTimer);
            row.remove();
        }
        recomputeCartSummary();
        updateCartBadge(response.headers.get('X-Cart-Count'), response.headers.get('X-Cart-Total'));
        if (list && list.children.length === 0) reloadCartPage();
    } catch (err) {
        console.error('Failed to remove cart line', err);
        if (button) button.disabled = false;
        alert('Something went wrong removing that line. Please refresh and try again.');
    }
}

function saveCartLine(row) {
    clearTimeout(row._cartSaveTimer);
    row._cartSaveChain = (row._cartSaveChain || Promise.resolve()).then(() => postCartLine(row));
    return row._cartSaveChain;
}

function scheduleCartLineSave(row) {
    clearTimeout(row._cartSaveTimer);
    row._cartSaveTimer = setTimeout(() => saveCartLine(row), CART_SAVE_DELAY_MS);
}

function setCartLineSaveError(statusEl, hasError) {
    if (!statusEl) return;
    statusEl.textContent = hasError ? 'Couldn’t save' : '';
}
