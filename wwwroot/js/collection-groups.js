function setSelect(id, value) {
    document.getElementById(id).value = value;
}

function openEditModal(btn) {
    const ds = btn.dataset;
    document.getElementById('editEntryID').value = ds.entryId;
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

// Updates the Orders page header from the X-Orders-* headers an order edit sends back; a no-op anywhere else.
function updateOrdersSummary(rawCount, rawCards, rawTotal) {
    if (rawCount === null || rawCount === undefined) return;

    const summaryEl = document.getElementById('ordersSummary');
    if (summaryEl) summaryEl.textContent = `${formatCountLabel(Number(rawCount) || 0, 'order')} · ${formatCountLabel(Number(rawCards) || 0, 'card')}`;

    const totalEl = document.getElementById('ordersTotal');
    if (totalEl) totalEl.textContent = '$' + (Number(rawTotal) || 0).toFixed(2);
}

// Appends a row the server sent to replace one that left the list (X-Ajax-Append), unless the list already shows it.
function appendRow(list, html) {
    const template = document.createElement('template');
    template.innerHTML = html.trim();
    const row = template.content.firstElementChild;
    if (!row || (row.id && document.getElementById(row.id))) return;

    list.appendChild(row);
}

// A list marked data-reload-when-empty reloads once its last row is gone, so the page can show what is left elsewhere.
function reloadIfListEmptied(list) {
    if (list && list.hasAttribute('data-reload-when-empty') && list.children.length === 0) {
        reloadPage();
    }
}

function reloadPage() {
    window.location.reload();
}

function applyAjaxGroupResponse(form, html, append) {
    const targetId = form.dataset.ajaxTarget || form.closest('[id^="group-row-"]')?.id;
    const target = targetId ? document.getElementById(targetId) : null;

    if (target && append) {
        const list = target.parentElement;
        target.remove();
        if (list && html && html.trim().length > 0) appendRow(list, html);
        reloadIfListEmptied(list);
    } else if (target) {
        const wasExpanded = target.querySelector('.collapse')?.classList.contains('show') ?? false;

        if (html && html.trim().length > 0) {
            target.outerHTML = html;

            if (wasExpanded) {
                const newTarget = document.getElementById(targetId);
                newTarget?.querySelector('.collapse')?.classList.add('show');
                newTarget?.querySelector('[data-bs-toggle="collapse"]')?.setAttribute('aria-expanded', 'true');
            }
        } else {
            const list = target.parentElement;
            target.remove();
            reloadIfListEmptied(list);
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
        const response = await postAjax(url, formData);

        if (!response.ok) {
            alert('Something went wrong saving your change. Please refresh and try again.');
            return;
        }

        const html = await response.text();
        // Number(null) is 0, so a response that leaves the header out (it didn't change the count) must skip this.
        const totalCount = response.headers.get('X-Total-Count');
        if (totalCount !== null) updateTotalCountBadge(Number(totalCount));
        updateCartBadge(response.headers.get('X-Cart-Count'), response.headers.get('X-Cart-Total'));
        updateReviewBadge(response.headers.get('X-Review-Count'));
        updateReviewOpenCounts(response.headers.get('X-Review-Open-Counts'));
        updateOrdersSummary(response.headers.get('X-Orders-Count'), response.headers.get('X-Orders-Cards'), response.headers.get('X-Orders-Total'));
        applyAjaxGroupResponse(form, html, response.headers.get('X-Ajax-Append') === 'true');
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
