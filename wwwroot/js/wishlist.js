async function openWishlistOwnModal(btn) {
    const form = document.getElementById('wishlistForm');

    document.getElementById('wlCardID').value = btn.dataset.cardId;
    document.getElementById('wlImageID').value = btn.dataset.imageId;
    document.getElementById('wlSetCode').value = btn.dataset.setCode;
    document.getElementById('wlSetName').textContent = btn.dataset.setName;

    const rarity = btn.dataset.rarityName || '';
    document.getElementById('wlRarityName').value = rarity;
    document.getElementById('wlRarityDisplay').textContent = rarity;

    document.getElementById('wishlistModalLabel').textContent = 'Already Own This Printing';

    document.getElementById('wlCondition').value = CardDefaults.Condition;
    document.getElementById('wlEdition').value = CardDefaults.Edition;
    document.getElementById('wlAcquisition').value = CardDefaults.Acquisition;
    setQuantityButtons('wlQuantity', 1);
    setPickerDate('wlPurchaseDate', btn.dataset.tcgDate || null);
    document.getElementById('wlPurchasePrice').value = '';

    const marketPriceEl = document.getElementById('wlMarketPrice');

    form.action = '?' + (btn.dataset.filterParams || '') + '&handler=Own';

    const row = btn.closest('[id^="wishlist-row-"]');
    if (row) form.dataset.ajaxTarget = row.id;

    new bootstrap.Modal(document.getElementById('wishlistModal')).show();

    const cardID = btn.dataset.cardId;
    const setCode = btn.dataset.setCode;
    const editionSelect = document.getElementById('wlEdition');

    await initLiveMarketPrice(editionSelect, marketPriceEl, () => ({ cardID, setCode, rarityName: rarity }));
}

async function openWishlistOrderModal(btn) {
    const ds = btn.dataset;
    document.getElementById('woCardID').value = ds.cardId;
    document.getElementById('woImageID').value = ds.imageId;
    document.getElementById('woSetCode').value = ds.setCode;
    document.getElementById('woRarityName').value = ds.rarityName || '';
    document.getElementById('woCardName').textContent = ds.cardName;
    document.getElementById('woSetNameLabel').textContent = ds.setName;
    document.getElementById('woRarityLabel').textContent = ds.rarityName || '';

    const qty = Math.min(3, Math.max(1, Number(ds.quantityNeeded) || 1));
    setQuantityButtons('woQuantity', qty);

    const form = document.getElementById('wishlistOrderForm');
    form.action = '?' + (ds.filterParams || '') + '&handler=AddToCart';

    const row = btn.closest('[id^="wishlist-row-"]');
    if (row) form.dataset.ajaxTarget = row.id;

    new bootstrap.Modal(document.getElementById('wishlistOrderModal')).show();

    // Wishlist rows don't have a pre-computed live price (unlike Buy List, which already
    // fetches one per row for its own display) — fetch one now, defaulting to 1st Edition
    // since the Cart page is where Edition actually gets chosen.
    await fetchWishlistOrderMarketPrice(ds.cardId, ds.setCode, ds.rarityName || '');
}

async function fetchWishlistOrderMarketPrice(cardID, setCode, rarityName) {
    const marketPriceEl = document.getElementById('woMarketPrice');
    marketPriceEl.value = '';
    if (!(cardID && setCode && rarityName)) return;

    marketPriceEl.placeholder = 'Loading…';
    try {
        const resp = await fetch(`/api/price?cardID=${cardID}&setCode=${encodeURIComponent(setCode)}&rarityName=${encodeURIComponent(rarityName)}&edition=${CardDefaults.Edition}`);
        if (resp.ok) {
            const { price } = await resp.json();
            if (price !== null && price !== undefined) marketPriceEl.value = price.toFixed(2);
        }
    } catch (err) {
        console.warn('Failed to fetch price:', err);
    } finally {
        marketPriceEl.placeholder = '0.00';
    }
}
