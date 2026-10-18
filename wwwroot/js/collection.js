async function openAddModal(btn) {
    const printingID = btn.dataset.printingId;
    document.getElementById('atcPrintingID').value = printingID;

    const rarity = btn.dataset.rarityName || '';
    document.getElementById('atcRarityDisplay').textContent = rarity;
    document.getElementById('atcAcquisitionGroup').style.display = 'block';
    setQuantityButtons('atcQuantity', 1);
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

    new bootstrap.Modal(document.getElementById('addModal')).show();

    const editionSelect = document.getElementById('atcEdition');

    await initLiveMarketPrice(editionSelect, marketPriceEl, () => ({ printingID }));
}
