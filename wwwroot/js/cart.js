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
});
