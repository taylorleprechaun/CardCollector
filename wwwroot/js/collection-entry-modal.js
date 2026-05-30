function openModal(setCode, setName, action, rarityName) {
    const isOrder = action === 'Order';
    const form = document.getElementById('orderForm');

    document.getElementById('atcSetCode').value = setCode;
    document.getElementById('atcSetNameLabel').textContent = setName;
    document.getElementById('orderModalLabel').textContent = isOrder ? 'Order Printing' : 'Already Own This Printing';

    const submitBtn = document.getElementById('modalSubmitBtn');
    submitBtn.textContent = isOrder ? 'Confirm Order' : 'Mark as Owned';
    submitBtn.className = isOrder ? 'btn btn-primary' : 'btn btn-success';

    document.getElementById('atcRarityName').value = rarityName || '';
    document.getElementById('atcRarityDisplay').textContent = rarityName || '';

    document.getElementById('atcAcquisitionGroup').style.display = isOrder ? 'none' : 'block';
    document.getElementById('atcCondition').value = '4';
    document.getElementById('atcEdition').value = '0';
    document.getElementById('atcAcquisition').value = '1';
    document.getElementById('atcQuantity').value = 1;
    document.getElementById('atcPurchaseDate').value = '';
    document.getElementById('atcPurchasePrice').value = '';
    document.getElementById('atcMarketPrice').value = '';
    document.getElementById('atcSetAsPreferred').checked = true;

    form.action = form.dataset.pageUrl + '?handler=' + action;

    new bootstrap.Modal(document.getElementById('orderModal')).show();
}
