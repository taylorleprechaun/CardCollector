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
    setPickerDate('atcPurchaseDate', btn.dataset.tcgDate || null);
    document.getElementById('atcPurchasePrice').value = '';
    document.getElementById('atcSetAsPreferred').checked = true;

    const addForm = document.getElementById('addForm');
    const row = btn.closest('[id^="group-row-"]');
    if (addForm && row) addForm.dataset.ajaxTarget = row.id;

    const marketPriceEl = document.getElementById('atcMarketPrice');
    marketPriceEl.value = '';
    marketPriceEl.placeholder = 'Loading…';

    new bootstrap.Modal(document.getElementById('addModal')).show();

    const cardID = btn.dataset.cardId;
    const setCode = btn.dataset.setCode;
    const editionSelect = document.getElementById('atcEdition');

    const refreshPrice = bindPriceRefresh(editionSelect, marketPriceEl, () => ({ cardID, setCode, rarityName: rarity }));
    await refreshPrice();
    marketPriceEl.placeholder = '0.00';
}
