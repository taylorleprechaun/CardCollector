import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

describe('wishlist.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  describe('fetchWishlistOrderMarketPrice', () => {
    it('fetch throwing is caught, warns, and still resets the placeholder', async () => {
      document.body.innerHTML = '<input id="woMarketPrice" />';
      loadDeps();
      globalThis.fetch = vi.fn().mockRejectedValue(new Error('offline'));
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

      await fetchWishlistOrderMarketPrice(1, 'LOB-EN001', 'Ultra Rare');

      expect(warnSpy).toHaveBeenCalled();
      expect(document.getElementById('woMarketPrice').placeholder).toBe('0.00');
    });

    it('missing any of cardID/setCode/rarityName clears the value and does not call fetch', async () => {
      document.body.innerHTML = '<input id="woMarketPrice" value="stale" />';
      loadDeps();
      globalThis.fetch = vi.fn();

      await fetchWishlistOrderMarketPrice(null, 'LOB-EN001', 'Ultra Rare');

      expect(document.getElementById('woMarketPrice').value).toBe('');
      expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('non-ok response leaves the value blank', async () => {
      document.body.innerHTML = '<input id="woMarketPrice" />';
      loadDeps();
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: false });

      await fetchWishlistOrderMarketPrice(1, 'LOB-EN001', 'Ultra Rare');

      expect(document.getElementById('woMarketPrice').value).toBe('');
    });

    it('successful fetch sets the price formatted to two decimals', async () => {
      document.body.innerHTML = '<input id="woMarketPrice" />';
      loadDeps();
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve({ price: 5 }) });

      await fetchWishlistOrderMarketPrice(1, 'LOB-EN001', 'Ultra Rare');

      expect(document.getElementById('woMarketPrice').value).toBe('5.00');
      expect(document.getElementById('woMarketPrice').placeholder).toBe('0.00');
    });
  });

  describe('openWishlistOrderModal', () => {
    it('populates fields, clamps quantity to the 1-3 range, and fetches the market price', async () => {
      setupForm();
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve({ price: 12.5 }) });

      await openWishlistOrderModal(document.getElementById('btn'));

      expect(document.getElementById('woCardName').textContent).toBe('Blue-Eyes');
      expect(document.getElementById('woQuantity').value).toBe('3'); // clamped from 7
      expect(document.getElementById('wishlistOrderForm').dataset.ajaxTarget).toBe('wishlist-row-20');
      expect(document.getElementById('woMarketPrice').value).toBe('12.50');
    });

    function setupForm() {
      document.body.innerHTML = `
        <input id="woCardID" /><input id="woImageID" /><input id="woSetCode" /><input id="woRarityName" />
        <span id="woCardName"></span><span id="woSetNameLabel"></span><span id="woRarityLabel"></span>
        <input type="hidden" id="woQuantity" /><input id="woMarketPrice" />
        <form id="wishlistOrderForm"></form>
        <div id="wishlistOrderModal"></div>
        <div id="wishlist-row-20">
          <button id="btn" data-card-id="1" data-image-id="20" data-set-code="LOB-EN002" data-rarity-name="Secret Rare"
                  data-card-name="Blue-Eyes" data-set-name="Legend" data-quantity-needed="7" data-filter-params="page=2"></button>
        </div>`;
      loadDeps();
    }
  });

  describe('openWishlistOwnModal', () => {
    it('no ajax target row found leaves the form dataset unset', async () => {
      document.body.innerHTML = `
        <form id="wishlistForm"></form>
        <input id="wlCardID" /><input id="wlImageID" /><input id="wlSetCode" /><span id="wlSetName"></span>
        <input id="wlRarityName" /><span id="wlRarityDisplay"></span><span id="wishlistModalLabel"></span>
        <select id="wlCondition"></select><select id="wlEdition"></select><select id="wlAcquisition"></select>
        <input type="hidden" id="wlQuantity" /><input id="wlPurchaseDate" /><input id="wlPurchasePrice" /><input id="wlMarketPrice" />
        <div id="wishlistModal"></div>
        <button id="btn" data-card-id="1" data-image-id="10" data-set-code="LOB-EN001"></button>`;
      loadDeps();
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve({ price: null }) });

      await openWishlistOwnModal(document.getElementById('btn'));

      expect(document.getElementById('wishlistForm').dataset.ajaxTarget).toBeUndefined();
    });

    it('populates form fields, applies card defaults, and marks the ajax target row', async () => {
      setupForm();
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve({ price: null }) });

      await openWishlistOwnModal(document.getElementById('btn'));

      expect(document.getElementById('wlCardID').value).toBe('1');
      expect(document.getElementById('wlSetName').textContent).toBe('Legend');
      expect(document.getElementById('wlCondition').value).toBe(CardDefaults.Condition);
      expect(document.getElementById('wishlistModalLabel').textContent).toBe('Already Own This Printing');
      expect(document.getElementById('wishlistForm').dataset.ajaxTarget).toBe('wishlist-row-10');
      expect(document.getElementById('wishlistForm').action).toContain('handler=Own');
    });

    function setupForm() {
      document.body.innerHTML = `
        <form id="wishlistForm"></form>
        <input id="wlCardID" /><input id="wlImageID" /><input id="wlSetCode" /><span id="wlSetName"></span>
        <input id="wlRarityName" /><span id="wlRarityDisplay"></span><span id="wishlistModalLabel"></span>
        <select id="wlCondition"><option value="4"></option></select>
        <select id="wlEdition"><option value="0"></option></select>
        <select id="wlAcquisition"><option value="1"></option></select>
        <input type="hidden" id="wlQuantity" />
        <input id="wlPurchaseDate" /><input id="wlPurchasePrice" /><input id="wlMarketPrice" />
        <div id="wishlistModal"></div>
        <div id="wishlist-row-10">
          <button id="btn" data-card-id="1" data-image-id="10" data-set-code="LOB-EN001" data-rarity-name="Ultra Rare"
                  data-set-name="Legend" data-filter-params="page=1"></button>
        </div>`;
      loadDeps();
    }
  });

  function loadDeps() {
    loadScript('enums.js');
    loadScript('site.js');
    loadScript('wishlist.js');
  }
});
