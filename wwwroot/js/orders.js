function openMarkOwnedModal(btn) {
    document.getElementById('markOwnedEntryID').value = btn.dataset.entryId;
    document.getElementById('markOwnedQuantity').value = btn.dataset.quantity;
    new bootstrap.Modal(document.getElementById('markOwnedModal')).show();
}
