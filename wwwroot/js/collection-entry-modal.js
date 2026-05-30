async function openModal(setCode, setName, action, rarityName) {
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
    document.getElementById('atcSetAsPreferred').checked = true;

    const marketPriceEl = document.getElementById('atcMarketPrice');
    marketPriceEl.value = '';
    marketPriceEl.placeholder = 'Loading…';

    form.action = form.dataset.pageURL + '?handler=' + action;

    new bootstrap.Modal(document.getElementById('orderModal')).show();

    const cardID = document.querySelector('#orderForm [name="CardID"]').value;
    if (cardID && setCode && rarityName) {
        try {
            const resp = await fetch(`/api/price?cardID=${cardID}&setCode=${encodeURIComponent(setCode)}&rarityName=${encodeURIComponent(rarityName)}`);
            if (resp.ok) {
                const { price } = await resp.json();
                if (price) marketPriceEl.value = price.toFixed(2);
            }
        } catch { }
    }
    marketPriceEl.placeholder = '0.00';
}
