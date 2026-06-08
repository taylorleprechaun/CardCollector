function setSelect(id, value) {
    document.getElementById(id).value = value;
}

function openEditModal(btn) {
    const ds = btn.dataset;
    document.getElementById('editEntryID').value = ds.entryId;
    document.getElementById('editRarityName').value = ds.rarityName || '';
    document.getElementById('editRarityDisplay').textContent = ds.rarityName || '';
    document.getElementById('editQuantity').value = ds.quantity;
    setSelect('editCondition', ds.condition);
    setSelect('editEdition', ds.edition);
    setSelect('editAcquisition', ds.acquisitionMethod);
    document.getElementById('editPurchaseDate').value = ds.purchaseDate;
    document.getElementById('editPurchasePrice').value = ds.purchasePrice;
    document.getElementById('editMarketPrice').value = ds.marketPrice;
    new bootstrap.Modal(document.getElementById('editModal')).show();
}

async function openAddModal(btn) {
    document.getElementById('addCardID').value = btn.dataset.cardId;
    document.getElementById('addImageID').value = btn.dataset.imageId;
    document.getElementById('addSetCode').value = btn.dataset.setCode;

    const rarity = btn.dataset.rarityName || '';
    document.getElementById('atcRarityName').value = rarity;
    document.getElementById('atcRarityDisplay').textContent = rarity;
    document.getElementById('atcAcquisitionGroup').style.display = 'block';
    document.getElementById('atcQuantity').value = 1;
    setSelect('atcCondition', CardDefaults.Condition);
    setSelect('atcEdition', CardDefaults.Edition);
    setSelect('atcAcquisition', CardDefaults.Acquisition);
    document.getElementById('atcPurchaseDate').value = btn.dataset.tcgDate || '';
    document.getElementById('atcPurchasePrice').value = '';
    document.getElementById('atcSetAsPreferred').checked = true;

    const marketPriceEl = document.getElementById('atcMarketPrice');
    marketPriceEl.value = '';
    marketPriceEl.placeholder = 'Loading…';

    new bootstrap.Modal(document.getElementById('addModal')).show();

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
