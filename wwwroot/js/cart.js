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
});

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
