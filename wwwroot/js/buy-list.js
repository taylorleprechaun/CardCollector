const BUY_LIST_EMPTY_RESULTS_HTML = `<div class="alert alert-info text-center py-5">
    <span>No plan items match your search.</span>
</div>`;

let buyListState = null;

document.addEventListener('DOMContentLoaded', function () {
    const copyBtn = document.getElementById('copyMassEntryBtn');
    const textarea = document.getElementById('massEntryText');
    if (copyBtn && textarea) {
        copyBtn.addEventListener('click', async function () {
            try {
                await navigator.clipboard.writeText(textarea.value);
                const originalText = copyBtn.textContent;
                copyBtn.textContent = 'Copied!';
                setTimeout(function () {
                    copyBtn.textContent = originalText;
                }, 1500);
            } catch (err) {
                textarea.select();
                document.execCommand('copy');
            }
        });
    }

    const addToCartBtn = document.getElementById('addToCartBtn');
    if (addToCartBtn) addToCartBtn.addEventListener('click', submitAddToCart);

    const itemsEl = document.getElementById('buyListStat-items');
    const totalCostEl = document.getElementById('buyListStat-totalCost');
    const remainingEl = document.getElementById('buyListStat-remainingBudget');
    if (itemsEl && totalCostEl) {
        buyListState = {
            itemsCount: Number(itemsEl.dataset.rawValue) || 0,
            totalCost: Number(totalCostEl.dataset.rawValue) || 0,
            totalBudget: remainingEl && remainingEl.dataset.rawValue !== '' ? Number(remainingEl.dataset.rawValue) : null
        };
    }
});

function openPurchaseModal(btn) {
    const ds = btn.dataset;
    document.getElementById('poCardID').value = ds.cardId;
    document.getElementById('poImageID').value = ds.imageId;
    document.getElementById('poSetCode').value = ds.setCode;
    document.getElementById('poRarityName').value = ds.rarityName || '';
    document.getElementById('poMarketPrice').value = ds.price || '';
    document.getElementById('poCardName').textContent = ds.cardName;
    document.getElementById('poSetNameLabel').textContent = ds.setName;
    document.getElementById('poRarityLabel').textContent = ds.rarityName || '';

    const qty = Math.min(3, Math.max(1, Number(ds.quantityNeeded) || 1));
    setQuantityButtons('poQuantity', qty);

    new bootstrap.Modal(document.getElementById('purchaseOrderModal')).show();
}

async function submitAddToCart() {
    const form = document.getElementById('purchaseOrderForm');
    const addToCartBtn = document.getElementById('addToCartBtn');
    const formData = new FormData(form);

    // Preserve MaxPricePerCard etc. so the server applies the same per-card cap the page shows.
    const params = new URLSearchParams(window.location.search);
    params.set('handler', 'AddToCart');
    const url = window.location.pathname + '?' + params.toString();

    addToCartBtn.disabled = true;
    try {
        const response = await fetch(url, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (!response.ok) {
            alert('Something went wrong adding this to your cart. Please try again.');
            return;
        }

        const result = await response.json();
        updateCartBadge(result.count, result.total);
        applyBuyListUpdate(result);
        bootstrap.Modal.getInstance(document.getElementById('purchaseOrderModal'))?.hide();
    } catch (err) {
        console.error('Add to cart failed', err);
        alert('Something went wrong adding this to your cart. Please try again.');
    } finally {
        addToCartBtn.disabled = false;
    }
}

// Only patches this one row's delta — the plan is never re-run, so nothing else reorders or disappears.
function applyBuyListUpdate(result) {
    const imageID = document.getElementById('poImageID').value;
    const row = document.querySelector(`#buyListResults .list-group-item[data-image-id="${CSS.escape(imageID)}"]`);
    if (!row) return;

    const oldLineTotal = Number(row.dataset.lineTotal) || 0;
    const oldMassEntryLine = row.dataset.massEntryLine || '';

    if (result.itemRemoved) {
        row.remove();
        const resultsContainer = document.getElementById('buyListResults');
        if (resultsContainer && resultsContainer.querySelectorAll('.list-group-item').length === 0) {
            resultsContainer.innerHTML = BUY_LIST_EMPTY_RESULTS_HTML;
        }
        updateBuyListTotals(true, oldLineTotal, 0);
        updateMassEntryLine(oldMassEntryLine, null);
        return;
    }

    if (!result.rowHtml) return;

    row.outerHTML = result.rowHtml;
    const newRow = document.querySelector(`#buyListResults .list-group-item[data-image-id="${CSS.escape(imageID)}"]`);
    const newLineTotal = newRow ? (Number(newRow.dataset.lineTotal) || 0) : 0;
    const newMassEntryLine = newRow ? (newRow.dataset.massEntryLine || '') : '';

    updateBuyListTotals(false, oldLineTotal, newLineTotal);
    updateMassEntryLine(oldMassEntryLine, newMassEntryLine);
}

function updateBuyListTotals(itemRemoved, oldLineTotal, newLineTotal) {
    if (!buyListState) return;

    if (itemRemoved) buyListState.itemsCount -= 1;
    // Round to cents to avoid floating-point drift accumulating over a long session.
    buyListState.totalCost = Math.round((buyListState.totalCost + (newLineTotal - oldLineTotal)) * 100) / 100;

    const itemsEl = document.querySelector('#buyListStat-items .stat-tile-value');
    if (itemsEl) itemsEl.textContent = buyListState.itemsCount.toLocaleString('en-US');

    const totalCostEl = document.querySelector('#buyListStat-totalCost .stat-tile-value');
    if (totalCostEl) totalCostEl.textContent = buyListState.totalCost.toLocaleString('en-US', { style: 'currency', currency: 'USD' });

    const remainingEl = document.querySelector('#buyListStat-remainingBudget .stat-tile-value');
    if (remainingEl) {
        remainingEl.textContent = buyListState.totalBudget === null
            ? '—'
            : (buyListState.totalBudget - buyListState.totalCost).toLocaleString('en-US', { style: 'currency', currency: 'USD' });
    }
}

// Ambiguous-rarity rows (see "Verify rarity" badge) could share an identical line and mismatch here — accepted, rare, low-severity.
function updateMassEntryLine(oldLine, newLine) {
    const textarea = document.getElementById('massEntryText');
    if (!textarea) return;

    const lines = textarea.value.split('\n');
    const idx = lines.indexOf(oldLine);
    if (idx === -1) return;

    if (newLine === null) lines.splice(idx, 1);
    else lines[idx] = newLine;

    textarea.value = lines.join('\n');
}
