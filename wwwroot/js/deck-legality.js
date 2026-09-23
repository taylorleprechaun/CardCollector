if (!document.ccDeckLegalityListenersBound) {
    document.ccDeckLegalityListenersBound = true;

    document.addEventListener('change', (event) => {
        const control = event.target.closest?.('[data-auto-submit]');
        if (!control) return;

        const form = control.closest('form');
        if (!form) return;

        // Switching the view or the event is a fresh look at that combination — a list-date override
        // picked for the previous one shouldn't silently carry over and apply to the new one too.
        if (control.name === 'view' || control.name === 'eventId') {
            const listDate = form.querySelector('[name="listDate"]');
            if (listDate) listDate.value = '';
        }

        form.submit();
    });
}
