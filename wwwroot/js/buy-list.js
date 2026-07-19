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
    const row = document.querySelector(`.list-group-item[data-image-id="${CSS.escape(imageID)}"][data-set-code="${CSS.escape(setCode)}"]`);
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
    badge.title = total ? `$${Number(total).toFixed(2)} staged` : '';
}
