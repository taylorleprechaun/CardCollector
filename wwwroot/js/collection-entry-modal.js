async function openModal(setCode, setName, action, rarityName, tcgDate) {
    const isOrder = action === 'Order';
    const form = document.getElementById('orderForm');

    document.getElementById('atcSetCode').value = setCode;
    document.getElementById('atcSetNameLabel').textContent = setName;
    document.getElementById('orderModalLabel').textContent = isOrder ? 'Order Printing' : 'Already Own This Printing';

    const submitBtn = document.getElementById('modalSubmitBtn');
    submitBtn.textContent = isOrder ? 'Confirm Order' : 'Add to Collection';
    submitBtn.className = isOrder ? 'btn btn-primary' : 'btn btn-success';

    document.getElementById('atcRarityName').value = rarityName || '';
    document.getElementById('atcRarityDisplay').textContent = rarityName || '';

    document.getElementById('atcAcquisitionGroup').style.display = isOrder ? 'none' : 'block';
    document.getElementById('atcCondition').value = CardDefaults.Condition;
    document.getElementById('atcEdition').value = CardDefaults.Edition;
    document.getElementById('atcAcquisition').value = CardDefaults.Acquisition;
    setQuantityButtons('atcQuantity', 1);
    setPickerDate('atcPurchaseDate', isOrder ? new Date() : (tcgDate || null));
    document.getElementById('atcPurchasePrice').value = '';
    document.getElementById('atcSetAsPreferred').checked = true;

    const marketPriceEl = document.getElementById('atcMarketPrice');

    form.action = form.dataset.pageUrl + '?handler=' + action;

    new bootstrap.Modal(document.getElementById('orderModal')).show();

    const cardID = document.querySelector('#orderForm [name="CardID"]').value;
    const editionSelect = document.getElementById('atcEdition');

    await initLiveMarketPrice(editionSelect, marketPriceEl, () => ({ cardID, setCode, rarityName }));
}
