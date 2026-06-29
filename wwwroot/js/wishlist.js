async function openWishlistModal(btn, action) {
    const isOrder = action === 'Order';
    const form = document.getElementById('wishlistForm');

    document.getElementById('wlCardID').value = btn.dataset.cardId;
    document.getElementById('wlImageID').value = btn.dataset.imageId;
    document.getElementById('wlSetCode').value = btn.dataset.setCode;
    document.getElementById('wlSetName').textContent = btn.dataset.setName;

    const rarity = btn.dataset.rarityName || '';
    document.getElementById('wlRarityName').value = rarity;
    document.getElementById('wlRarityDisplay').textContent = rarity;

    document.getElementById('wishlistModalLabel').textContent = isOrder ? 'Order Printing' : 'Already Own This Printing';

    const submitBtn = document.getElementById('wlSubmitBtn');
    submitBtn.textContent = isOrder ? 'Confirm Order' : 'Mark as Owned';
    submitBtn.className = isOrder ? 'btn btn-primary' : 'btn btn-success';

    document.getElementById('wlAcquisitionGroup').style.display = isOrder ? 'none' : 'block';
    document.getElementById('wlCondition').value = CardDefaults.Condition;
    document.getElementById('wlEdition').value = CardDefaults.Edition;
    document.getElementById('wlAcquisition').value = CardDefaults.Acquisition;
    document.getElementById('wlQuantity').value = 1;
    setPickerDate('wlPurchaseDate', isOrder ? new Date() : (btn.dataset.tcgDate || null));
    document.getElementById('wlPurchasePrice').value = '';

    const marketPriceEl = document.getElementById('wlMarketPrice');
    marketPriceEl.value = '';
    marketPriceEl.placeholder = 'Loading…';

    form.action = '?handler=' + action;

    new bootstrap.Modal(document.getElementById('wishlistModal')).show();

    const cardID = btn.dataset.cardId;
    const setCode = btn.dataset.setCode;
    if (cardID && setCode && rarity) {
        try {
            const resp = await fetch(`/api/price?cardID=${cardID}&setCode=${encodeURIComponent(setCode)}&rarityName=${encodeURIComponent(rarity)}`);
            if (resp.ok) {
                const { price } = await resp.json();
                if (price) marketPriceEl.value = price.toFixed(2);
            }
        } catch (err) { console.warn('Failed to fetch price:', err); }
    }
    marketPriceEl.placeholder = '0.00';
}
