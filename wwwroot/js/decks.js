function openDeleteDeckModal(trigger) {
    const events = Number(trigger.dataset.deckEvents || 0);

    document.getElementById('deleteDeckID').value = trigger.dataset.deckId;
    document.getElementById('deleteDeckName').textContent = trigger.dataset.deckName;
    document.getElementById('deleteDeckEvents').textContent = events > 0
        ? ` ${events} ${events === 1 ? 'event' : 'events'} will lose the link to it (the events themselves are kept).`
        : '';
    new bootstrap.Modal(document.getElementById('deleteDeckModal')).show();
}

function openRenameDeckModal(trigger) {
    document.getElementById('renameDeckID').value = trigger.dataset.deckId;
    document.getElementById('renameDeckName').value = trigger.dataset.deckName || '';
    document.getElementById('renameDeckNotes').value = trigger.dataset.deckNotes || '';
    new bootstrap.Modal(document.getElementById('renameDeckModal')).show();
}
