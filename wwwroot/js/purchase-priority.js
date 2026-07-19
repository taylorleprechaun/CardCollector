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

    const addLineBtn = document.getElementById('addPurchaseLineBtn');
    if (addLineBtn) addLineBtn.addEventListener('click', addPurchaseLine);

    const addToCartBtn = document.getElementById('addToCartBtn');
    if (addToCartBtn) addToCartBtn.addEventListener('click', submitAddToCart);
});

function setSelectValue(id, value) {
    const el = document.getElementById(id);
    if (el) el.value = value;
}

function cloneLineTemplate() {
    const template = document.getElementById('purchaseLineTemplate');
    return template.content.cloneNode(true);
}

function openPurchaseModal(btn) {
    const ds = btn.dataset;
    document.getElementById('poCardID').value = ds.cardId;
    document.getElementById('poImageID').value = ds.imageId;
    document.getElementById('poSetCode').value = ds.setCode;
    document.getElementById('poRarityName').value = ds.rarityName || '';
    document.getElementById('poCardName').textContent = ds.cardName;
    document.getElementById('poSetNameLabel').textContent = ds.setName;
    document.getElementById('poRarityLabel').textContent = ds.rarityName || '';

    const container = document.getElementById('purchaseLinesContainer');
    container.innerHTML = '';
    container.appendChild(cloneLineTemplate());
    initDatePickers(container);

    const qty = Math.min(3, Math.max(1, Number(ds.quantityNeeded) || 1));
    setSelectValue('lineCondition0', CardDefaults.Condition);
    setSelectValue('lineEdition0', CardDefaults.Edition);
    setQuantityButtons('lineQuantity0', qty);
    setPickerDate('linePurchaseDate0', new Date());
    if (ds.price) document.getElementById('linePrice0').value = Number(ds.price).toFixed(2);
    bindLineMarketPrice(0);

    new bootstrap.Modal(document.getElementById('purchaseOrderModal')).show();
}

function addPurchaseLine() {
    const container = document.getElementById('purchaseLinesContainer');
    container.appendChild(cloneLineTemplate());
    reindexPurchaseLines();
    initDatePickers(container);

    const lines = container.querySelectorAll('.purchase-line');
    const idx = lines.length - 1;
    setSelectValue(`lineCondition${idx}`, CardDefaults.Condition);
    setSelectValue(`lineEdition${idx}`, CardDefaults.Edition);
    setQuantityButtons(`lineQuantity${idx}`, 1);
    setPickerDate(`linePurchaseDate${idx}`, new Date());
    bindLineMarketPrice(idx);
}

// Mirrors Wishlist's Order/Own modal: live TCGPlayer price refetched whenever the line's
// Edition changes, since price varies by edition. CardID/SetCode/RarityName are shared
// across every line in this modal (all lines are the same printing), so they're read from
// the modal's hidden fields rather than per-line.
function bindLineMarketPrice(idx) {
    const editionSelect = document.getElementById(`lineEdition${idx}`);
    const marketPriceEl = document.getElementById(`lineMarketPrice${idx}`);
    if (!editionSelect || !marketPriceEl) return;

    const getParams = () => ({
        cardID: document.getElementById('poCardID').value,
        setCode: document.getElementById('poSetCode').value,
        rarityName: document.getElementById('poRarityName').value
    });

    const refreshPrice = bindPriceRefresh(editionSelect, marketPriceEl, getParams);
    refreshPrice();
}

function removePurchaseLine(btn) {
    const container = document.getElementById('purchaseLinesContainer');
    const lines = container.querySelectorAll('.purchase-line');
    if (lines.length <= 1) return; // always keep at least one line

    btn.closest('.purchase-line').remove();
    reindexPurchaseLines();
}

function reindexPurchaseLines() {
    const lines = document.querySelectorAll('#purchaseLinesContainer .purchase-line');
    lines.forEach((line, idx) => {
        line.dataset.lineIndex = idx;

        line.querySelectorAll('[name]').forEach(el => {
            el.name = el.name.replace(/Lines\[\d+\]/, `Lines[${idx}]`);
        });
        line.querySelectorAll('[id]').forEach(el => {
            el.id = el.id.replace(/\d+$/, idx);
        });
        line.querySelectorAll('[data-qty-target]').forEach(el => {
            el.dataset.qtyTarget = el.dataset.qtyTarget.replace(/\d+$/, idx);
        });
        line.querySelectorAll('[onclick*="setPickerDate"]').forEach(el => {
            const onclick = el.getAttribute('onclick');
            el.setAttribute('onclick', onclick.replace(/linePurchaseDate\d+/, `linePurchaseDate${idx}`));
        });
        line.querySelectorAll('[onclick*="lineMarketPrice"]').forEach(el => {
            const onclick = el.getAttribute('onclick');
            el.setAttribute('onclick', onclick
                .replace(/lineMarketPrice\d+/, `lineMarketPrice${idx}`)
                .replace(/linePrice\d+/, `linePrice${idx}`));
        });
    });
}

async function submitAddToCart() {
    const form = document.getElementById('purchaseOrderForm');
    const addToCartBtn = document.getElementById('addToCartBtn');
    const formData = new FormData(form);
    const url = window.location.pathname + '?handler=AddToCart';

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
        markRowInCart(document.getElementById('poImageID').value, document.getElementById('poSetCode').value);
        bootstrap.Modal.getInstance(document.getElementById('purchaseOrderModal'))?.hide();
    } catch (err) {
        console.error('Add to cart failed', err);
        alert('Something went wrong adding this to your cart. Please try again.');
    } finally {
        addToCartBtn.disabled = false;
    }
}

function markRowInCart(imageID, setCode) {
    const row = document.querySelector(`tr[data-image-id="${CSS.escape(imageID)}"][data-set-code="${CSS.escape(setCode)}"]`);
    if (!row) return;

    const container = row.querySelector('.cart-order-badges');
    if (!container || container.querySelector('.badge-in-cart')) return;

    const badge = document.createElement('span');
    badge.className = 'badge bg-info text-dark ms-1 badge-in-cart';
    badge.title = 'Already staged in your cart';
    badge.textContent = 'In Cart';
    container.appendChild(badge);
}

function updateCartBadge(count, total) {
    const badge = document.getElementById('navCartBadge');
    const countEl = document.getElementById('navCartCount');
    if (!badge || !countEl) return;

    countEl.textContent = count;
    badge.classList.toggle('d-none', !count);
    badge.title = total ? `$${Number(total).toFixed(2)} staged` : '';
}
