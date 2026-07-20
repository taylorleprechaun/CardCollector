document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('select[data-market-price-target]').forEach(function (editionSelect) {
        const marketPriceEl = document.getElementById(editionSelect.dataset.marketPriceTarget);
        if (!marketPriceEl) return;

        // Cart's Market Price starts pre-filled from the stage-time snapshot, so unlike the
        // entry modals this only wires the refresh — it doesn't fetch immediately on load.
        bindPriceRefresh(editionSelect, marketPriceEl, () => ({
            cardID: editionSelect.dataset.cardId,
            setCode: editionSelect.dataset.setCode,
            rarityName: editionSelect.dataset.rarityName
        }));
    });

    // Purchase Price starts blank on every line (it's unknown until staged), so the total
    // has to be recomputed client-side as each row's Price/Quantity is filled in.
    document.querySelectorAll('input[id^="linePrice"]').forEach(function (priceEl) {
        priceEl.addEventListener('input', recomputeCartTotal);
    });
    document.querySelectorAll('[data-qty-target^="lineQuantity"]').forEach(function (btn) {
        btn.addEventListener('click', recomputeCartTotal);
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

// The quantity buttons are the one field on a cart line that has a meaning outside the
// submit-time overrides (it drives the In Cart badges shown on Wishlist/Buy List), so unlike
// Condition/Edition/Price/Market Price — which only take effect once "Submit All Orders" is
// clicked — a quantity change is persisted immediately.
function selectCartLineQuantity(btn) {
    selectQuantity(btn);
    persistCartLineQuantity(btn.dataset.pendingOrderLineId, btn.dataset.qtyValue);
}

async function persistCartLineQuantity(pendingOrderLineID, quantity) {
    const token = document.querySelector('#cartSubmitForm [name="__RequestVerificationToken"]')?.value;
    const formData = new FormData();
    formData.append('id', pendingOrderLineID);
    formData.append('quantity', quantity);
    if (token) formData.append('__RequestVerificationToken', token);

    try {
        const response = await fetch('/Cart?handler=UpdateQuantity', {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        if (!response.ok) console.warn('Failed to save cart line quantity, status', response.status);
    } catch (err) {
        console.warn('Failed to save cart line quantity:', err);
    }
}

function recomputeCartTotal() {
    const totalEl = document.getElementById('cartTotal');
    if (!totalEl) return;

    let total = 0;
    document.querySelectorAll('input[id^="linePrice"]').forEach(function (priceEl) {
        const index = priceEl.id.replace('linePrice', '');
        const quantityEl = document.getElementById('lineQuantity' + index);
        const price = parseFloat(priceEl.value) || 0;
        const quantity = quantityEl ? (parseFloat(quantityEl.value) || 0) : 0;
        total += price * quantity;
    });

    totalEl.textContent = '$' + total.toFixed(2);
}
