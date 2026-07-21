import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

describe('buy-list.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  describe('applyBuyListUpdate', () => {
    it('itemRemoved removes the row and shows the empty-results message when no rows remain', () => {
      setupRow();
      buyListState = { itemsCount: 1, totalCost: 15, totalBudget: null };

      applyBuyListUpdate({ itemRemoved: true });

      expect(document.querySelector('.list-group-item')).toBeNull();
      expect(document.getElementById('buyListResults').textContent).toContain('No plan items match your search.');
      expect(buyListState.itemsCount).toBe(0);
    });

    it('missing row for the current image ID is a no-op', () => {
      document.body.innerHTML = '<input id="poImageID" value="999" /><div id="buyListResults"></div>';
      loadDeps();

      expect(() => applyBuyListUpdate({ itemRemoved: true })).not.toThrow();
    });

    it('no rowHtml and not removed is a no-op', () => {
      setupRow();
      buyListState = { itemsCount: 1, totalCost: 15, totalBudget: null };

      applyBuyListUpdate({ itemRemoved: false, rowHtml: null });

      expect(buyListState.totalCost).toBe(15);
    });

    it('patches the row HTML and updates totals when a new row is returned', () => {
      setupRow();
      buyListState = { itemsCount: 1, totalCost: 15, totalBudget: 100 };

      applyBuyListUpdate({
        itemRemoved: false,
        rowHtml: '<div class="list-group-item" data-image-id="10" data-line-total="20" data-mass-entry-line="1 Dark Magician [LOB]"></div>'
      });

      expect(document.querySelector('.list-group-item').dataset.lineTotal).toBe('20');
      expect(buyListState.totalCost).toBe(20);
    });

    function setupRow() {
      document.body.innerHTML = `
        <input id="poImageID" value="10" />
        <div id="buyListResults">
          <div class="list-group-item" data-image-id="10" data-line-total="15" data-mass-entry-line="2 Dark Magician [LOB]"></div>
        </div>
        <textarea id="massEntryText">2 Dark Magician [LOB]</textarea>
        <div id="buyListStat-items"><span class="stat-tile-value"></span></div>
        <div id="buyListStat-totalCost"><span class="stat-tile-value"></span></div>`;
      loadDeps();
    }
  });

  describe('openPurchaseModal', () => {
    it('populates the form fields from the button dataset and clamps quantity into 1-3', () => {
      document.body.innerHTML = `
        <input id="poCardID" /><input id="poImageID" /><input id="poSetCode" /><input id="poRarityName" />
        <input id="poMarketPrice" /><span id="poCardName"></span><span id="poSetNameLabel"></span><span id="poRarityLabel"></span>
        <input type="hidden" id="poQuantity" />
        <div id="purchaseOrderModal"></div>
        <button id="btn" data-card-id="1" data-image-id="10" data-set-code="LOB-EN001" data-rarity-name="Ultra Rare"
                data-price="9.99" data-card-name="Dark Magician" data-set-name="Legend" data-quantity-needed="5"></button>`;
      loadDeps();

      openPurchaseModal(document.getElementById('btn'));

      expect(document.getElementById('poCardID').value).toBe('1');
      expect(document.getElementById('poSetCode').value).toBe('LOB-EN001');
      expect(document.getElementById('poCardName').textContent).toBe('Dark Magician');
      expect(document.getElementById('poQuantity').value).toBe('3'); // clamped from 5
    });

    it('quantityNeeded below 1 clamps up to 1', () => {
      document.body.innerHTML = `
        <input id="poCardID" /><input id="poImageID" /><input id="poSetCode" /><input id="poRarityName" />
        <input id="poMarketPrice" /><span id="poCardName"></span><span id="poSetNameLabel"></span><span id="poRarityLabel"></span>
        <input type="hidden" id="poQuantity" />
        <div id="purchaseOrderModal"></div>
        <button id="btn" data-card-id="1" data-image-id="10" data-set-code="LOB-EN001" data-quantity-needed="0"></button>`;
      loadDeps();

      openPurchaseModal(document.getElementById('btn'));

      expect(document.getElementById('poQuantity').value).toBe('1');
    });
  });

  describe('submitAddToCart', () => {
    it('fetch throwing logs an error, shows an alert, and re-enables the button', async () => {
      setupForm();
      const alertSpy = vi.spyOn(globalThis, 'alert').mockImplementation(() => {});
      vi.spyOn(console, 'error').mockImplementation(() => {});
      globalThis.fetch = vi.fn().mockRejectedValue(new Error('network down'));

      await submitAddToCart();

      expect(alertSpy).toHaveBeenCalled();
      expect(document.getElementById('addToCartBtn').disabled).toBe(false);
    });

    it('non-ok response shows an alert and re-enables the button', async () => {
      setupForm();
      const alertSpy = vi.spyOn(globalThis, 'alert').mockImplementation(() => {});
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: false });

      await submitAddToCart();

      expect(alertSpy).toHaveBeenCalled();
      expect(document.getElementById('addToCartBtn').disabled).toBe(false);
    });

    it('successful submission updates the cart badge and patches the row', async () => {
      setupForm();
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve({ count: 2, total: 15, itemRemoved: false, rowHtml: null })
      });

      await submitAddToCart();

      expect(document.getElementById('navCartCount').textContent).toBe('2');
      expect(document.getElementById('addToCartBtn').disabled).toBe(false);
    });

    function setupForm() {
      document.body.innerHTML = `
        <form id="purchaseOrderForm"></form>
        <button id="addToCartBtn"></button>
        <input id="poImageID" value="10" />
        <div id="purchaseOrderModal"></div>
        <span id="navCartBadge" class="d-none"><span id="navCartCount"></span></span>`;
      loadDeps();
    }
  });

  describe('updateBuyListTotals', () => {
    it('null buyListState (page had no stats to hydrate) is a no-op', () => {
      document.body.innerHTML = '';
      loadDeps();
      buyListState = null;

      expect(() => updateBuyListTotals(false, 0, 5)).not.toThrow();
    });

    it('null totalBudget displays an em dash for remaining budget', () => {
      document.body.innerHTML = `
        <div id="buyListStat-items"><span class="stat-tile-value"></span></div>
        <div id="buyListStat-totalCost"><span class="stat-tile-value"></span></div>
        <div id="buyListStat-remainingBudget"><span class="stat-tile-value"></span></div>`;
      loadDeps();
      buyListState = { itemsCount: 2, totalCost: 10, totalBudget: null };

      updateBuyListTotals(false, 0, 5);

      expect(document.querySelector('#buyListStat-remainingBudget .stat-tile-value').textContent).toBe('—');
    });

    it('numeric totalBudget shows the remaining amount as currency', () => {
      document.body.innerHTML = `
        <div id="buyListStat-items"><span class="stat-tile-value"></span></div>
        <div id="buyListStat-totalCost"><span class="stat-tile-value"></span></div>
        <div id="buyListStat-remainingBudget"><span class="stat-tile-value"></span></div>`;
      loadDeps();
      buyListState = { itemsCount: 2, totalCost: 15, totalBudget: 100 };

      updateBuyListTotals(false, 0, 0);

      expect(document.querySelector('#buyListStat-remainingBudget .stat-tile-value').textContent).toBe('$85.00');
    });
  });

  describe('updateMassEntryLine', () => {
    it('a null new line removes the old line entirely', () => {
      document.body.innerHTML = '<textarea id="massEntryText">line one\nline two\nline three</textarea>';
      loadDeps();

      updateMassEntryLine('line two', null);

      expect(document.getElementById('massEntryText').value).toBe('line one\nline three');
    });

    it('missing textarea is a no-op', () => {
      document.body.innerHTML = '';
      loadDeps();

      expect(() => updateMassEntryLine('a', 'b')).not.toThrow();
    });

    it('old line not found is a no-op', () => {
      document.body.innerHTML = '<textarea id="massEntryText">line one</textarea>';
      loadDeps();

      updateMassEntryLine('does not exist', 'replacement');

      expect(document.getElementById('massEntryText').value).toBe('line one');
    });

    it('replaces the matching line with the new line', () => {
      document.body.innerHTML = '<textarea id="massEntryText">line one\nline two\nline three</textarea>';
      loadDeps();

      updateMassEntryLine('line two', 'line two updated');

      expect(document.getElementById('massEntryText').value).toBe('line one\nline two updated\nline three');
    });
  });

  function loadDeps() {
    loadScript('site.js');
    loadScript('buy-list.js');
  }
});
