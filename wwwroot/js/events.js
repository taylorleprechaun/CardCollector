function initEventsPage() {
    const modal = document.getElementById('eventModal');
    if (!modal) return;

    if (modal.dataset.openOnLoad === 'true') new bootstrap.Modal(modal).show();
}

function openDeleteEventModal(trigger) {
    const rounds = Number(trigger.dataset.eventRounds || 0);

    document.getElementById('deleteEventID').value = trigger.dataset.eventId;
    document.getElementById('deleteEventName').textContent = trigger.dataset.eventLocation;
    document.getElementById('deleteEventRounds').textContent = rounds > 0
        ? ` and its ${rounds} ${rounds === 1 ? 'round' : 'rounds'}`
        : '';
    new bootstrap.Modal(document.getElementById('deleteEventModal')).show();
}

function openEventModal(trigger) {
    const data = trigger ? trigger.dataset : {};
    const isEdit = Boolean(data.eventId);

    document.getElementById('eventModalLabel').textContent = isEdit ? 'Edit Event' : 'Add Event';
    document.getElementById('Input_ID').value = isEdit ? data.eventId : '0';
    setPickerDate('Input_Date', data.eventDate || '');
    document.getElementById('Input_EventType').value = data.eventType || 'Locals';
    document.getElementById('Input_Location').value = data.eventLocation || '';
    document.getElementById('Input_DeckName').value = data.eventDeck || '';
    document.getElementById('Input_DecklistURL').value = data.eventUrl || '';
    document.getElementById('Input_Finish').value = data.eventFinish || '';
    document.getElementById('Input_Players').value = data.eventPlayers || '';
    document.getElementById('Input_TopCut').value = data.eventTopCut || '';
    document.getElementById('Input_FinishNote').value = data.eventFinishNote || '';
    document.getElementById('Input_Notes').value = data.eventNotes || '';
    document.getElementById('eventErrors')?.remove();

    new bootstrap.Modal(document.getElementById('eventModal')).show();
}

initEventsPage();
