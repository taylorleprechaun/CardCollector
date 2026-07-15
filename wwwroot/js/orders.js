function openMarkOwnedModal(btn) {
    document.getElementById('markOwnedEntryID').value = btn.dataset.entryId;
    const quantity = Math.min(3, Math.max(1, Number(btn.dataset.quantity) || 1));
    setQuantityButtons('markOwnedQuantity', quantity);
    new bootstrap.Modal(document.getElementById('markOwnedModal')).show();
}
