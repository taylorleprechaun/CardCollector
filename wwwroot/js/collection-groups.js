function setSelect(id, value) {
    document.getElementById(id).value = value;
}

function openEditModal(btn) {
    const ds = btn.dataset;
    document.getElementById('editEntryID').value = ds.entryId;
    document.getElementById('editRarityName').value = ds.rarityName || '';
    document.getElementById('editRarityDisplay').textContent = ds.rarityName || '';
    setQuantityButtons('editQuantity', ds.quantity);
    setSelect('editCondition', ds.condition);
    setSelect('editEdition', ds.edition);
    setSelect('editAcquisition', ds.acquisitionMethod);
    setPickerDate('editPurchaseDate', ds.purchaseDate);
    document.getElementById('editPurchasePrice').value = ds.purchasePrice;
    document.getElementById('editMarketPrice').value = ds.marketPrice;

    const form = document.getElementById('editForm');
    const row = btn.closest('[id^="group-row-"]');
    if (form && row) form.dataset.ajaxTarget = row.id;

    new bootstrap.Modal(document.getElementById('editModal')).show();
}

function updateTotalCountBadge(newTotal) {
    if (newTotal === null || Number.isNaN(newTotal)) return;

    const badge = document.getElementById('totalCountBadge');
    if (!badge) return;

    badge.textContent = `${newTotal} ${badge.dataset.suffix || ''}`.trim();
}

function applyAjaxGroupResponse(form, html) {
    const targetId = form.dataset.ajaxTarget || form.closest('[id^="group-row-"]')?.id;
    const target = targetId ? document.getElementById(targetId) : null;

    if (target) {
        const wasExpanded = target.querySelector('.collapse')?.classList.contains('show') ?? false;

        if (html && html.trim().length > 0) {
            target.outerHTML = html;

            if (wasExpanded) {
                const newTarget = document.getElementById(targetId);
                newTarget?.querySelector('.collapse')?.classList.add('show');
                newTarget?.querySelector('[data-bs-toggle="collapse"]')?.setAttribute('aria-expanded', 'true');
            }
        } else {
            target.remove();
        }
    }

    const openModal = form.closest('.modal');
    if (openModal) {
        bootstrap.Modal.getInstance(openModal)?.hide();
    }
}

async function submitAjaxForm(form, submitter) {
    // submitter.formAction resolves to the current page URL (not falsy) even when the
    // button has no formaction attribute, so only use it when the attribute is actually set.
    const url = (submitter?.hasAttribute('formaction') ? submitter.formAction : null) || form.action;
    const formData = new FormData(form, submitter ?? undefined);

    try {
        const response = await fetch(url, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (!response.ok) {
            alert('Something went wrong saving your change. Please refresh and try again.');
            return;
        }

        const html = await response.text();
        updateTotalCountBadge(Number(response.headers.get('X-Total-Count')));
        updateCartBadge(response.headers.get('X-Cart-Count'), response.headers.get('X-Cart-Total'));
        applyAjaxGroupResponse(form, html);
    } catch (err) {
        console.error('AJAX form submission failed', err);
        alert('Something went wrong saving your change. Please refresh and try again.');
    }
}

document.addEventListener('submit', (event) => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || !form.classList.contains('ajax-form')) return;
    if (event.defaultPrevented) return; // e.g. the Remove button's confirm() was cancelled

    event.preventDefault();
    submitAjaxForm(form, event.submitter);
});
