function setSelect(id, value) {
    document.getElementById(id).value = value;
}

function openEditModal(entryID, qty, condition, edition, acquisition, purchaseDate, purchasePrice, marketPriceAtEntry, rarityName) {
    document.getElementById('editEntryID').value = entryID;
    document.getElementById('editRarityName').value = rarityName || '';
    document.getElementById('editRarityDisplay').textContent = rarityName || '';
    document.getElementById('editQuantity').value = qty;
    setSelect('editCondition', condition);
    setSelect('editEdition', edition);
    setSelect('editAcquisition', acquisition);
    document.getElementById('editPurchaseDate').value = purchaseDate;
    document.getElementById('editPurchasePrice').value = purchasePrice;
    document.getElementById('editMarketPrice').value = marketPriceAtEntry;
    new bootstrap.Modal(document.getElementById('editModal')).show();
}

function openAddModal(btn) {
    document.getElementById('addCardID').value = btn.dataset.cardId;
    document.getElementById('addImageID').value = btn.dataset.imageId;
    document.getElementById('addSetCode').value = btn.dataset.setCode;

    const rarity = btn.dataset.rarityName || '';
    document.getElementById('atcRarityName').value = rarity;
    document.getElementById('atcRarityDisplay').textContent = rarity;
    document.getElementById('atcAcquisitionGroup').style.display = 'block';
    document.getElementById('atcQuantity').value = 1;
    setSelect('atcCondition', '4');
    setSelect('atcEdition', '0');
    setSelect('atcAcquisition', '1');
    document.getElementById('atcPurchaseDate').value = '';
    document.getElementById('atcPurchasePrice').value = '';
    document.getElementById('atcMarketPrice').value = '';
    document.getElementById('atcSetAsPreferred').checked = true;
    new bootstrap.Modal(document.getElementById('addModal')).show();
}
