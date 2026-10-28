import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

describe('cart.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  describe('DOMContentLoaded wiring', () => {
    it('disables the submit button and relabels it on form submit', () => {
      document.body.innerHTML = `
        <form id="cartSubmitForm"><button type="submit">Submit All Orders</button></form>`;
      loadScript('cart.js');
      document.dispatchEvent(new Event('DOMContentLoaded'));
      const submitBtn = document.querySelector('#cartSubmitForm button[type="submit"]');

      submitBtn.closest('form').dispatchEvent(new Event('submit'));

      expect(submitBtn.disabled).toBe(true);
      expect(submitBtn.textContent).toBe('Submitting…');
    });

    it('missing submit button is a no-op', () => {
      document.body.innerHTML = `<form id="cartSubmitForm"></form>`;
      loadScript('cart.js');
      document.dispatchEvent(new Event('DOMContentLoaded'));

      expect(() => document.getElementById('cartSubmitForm').dispatchEvent(new Event('submit'))).not.toThrow();
    });
  });

  describe('recomputeCartSummary', () => {
    it('missing #cartTotal and #cartSummary is a no-op', () => {
      document.body.innerHTML = '';
      loadScript('site.js');
      loadScript('cart.js');

      expect(() => recomputeCartSummary()).not.toThrow();
    });

    it('counts lines and cards (lines times quantity) into #cartSummary', () => {
      document.body.innerHTML = `
        <span id="cartSummary"></span>
        <input id="linePrice1" value="" />
        <input id="lineQuantity1" value="2" />
        <input id="linePrice2" value="10.00" />
        <input id="lineQuantity2" value="1" />`;
      loadScript('site.js');
      loadScript('cart.js');

      recomputeCartSummary();

      expect(document.getElementById('cartSummary').textContent).toBe('2 lines · 3 cards');
    });

    it('uses the singular for one line holding one card', () => {
      document.body.innerHTML = `
        <span id="cartSummary"></span>
        <input id="linePrice1" value="" />
        <input id="lineQuantity1" value="1" />`;
      loadScript('site.js');
      loadScript('cart.js');

      recomputeCartSummary();

      expect(document.getElementById('cartSummary').textContent).toBe('1 line · 1 card');
    });

    it('non-numeric price or missing quantity element are treated as zero', () => {
      document.body.innerHTML = `
        <span id="cartTotal"></span>
        <input id="linePrice1" value="not-a-number" />
        <input id="lineQuantity1" value="2" />
        <input id="linePrice2" value="5.00" />`;
      loadScript('cart.js');

      recomputeCartSummary();

      expect(document.getElementById('cartTotal').textContent).toBe('$0.00');
    });

    it('sums price times quantity across matching lines', () => {
      document.body.innerHTML = `
        <span id="cartTotal"></span>
        <input id="linePrice1" value="5.00" />
        <input id="lineQuantity1" value="2" />
        <input id="linePrice2" value="10.00" />
        <input id="lineQuantity2" value="3" />`;
      loadScript('cart.js');

      recomputeCartSummary();

      expect(document.getElementById('cartTotal').textContent).toBe('$40.00');
    });
  });

  describe('removeCartLine', () => {
    it('removes the row, updates the summary and nav badge, and keeps the other rows', async () => {
      document.body.innerHTML = `
        <span id="cartSummary"></span>
        <span id="navCartBadge"><span id="navCartCount"></span></span>
        <div id="list">${cartRow(1)}${cartRow(2)}</div>`;
      loadCart();
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, headers: makeHeaders({ 'X-Cart-Count': '1', 'X-Cart-Total': '0' }) });

      await removeCartLine(document.querySelector('#cart-row-1 .cart-remove-form'));

      expect(document.getElementById('cart-row-1')).toBeNull();
      expect(document.getElementById('cart-row-2')).not.toBeNull();
      expect(document.getElementById('cartSummary').textContent).toBe('1 line · 2 cards');
      expect(document.getElementById('navCartCount').textContent).toBe('1');
    });

    it('reloads once the last row is gone', async () => {
      document.body.innerHTML = `<div id="list">${cartRow(1)}</div>`;
      loadCart();
      const reloadSpy = vi.spyOn(globalThis, 'reloadCartPage').mockImplementation(() => {});
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, headers: makeHeaders({ 'X-Cart-Count': '0', 'X-Cart-Total': '0' }) });

      await removeCartLine(document.querySelector('#cart-row-1 .cart-remove-form'));

      expect(reloadSpy).toHaveBeenCalledTimes(1);
    });

    it('a failed remove keeps the row, re-enables the button, and alerts', async () => {
      document.body.innerHTML = `<div id="list">${cartRow(1)}</div>`;
      loadCart();
      vi.spyOn(console, 'error').mockImplementation(() => {});
      const alertSpy = vi.spyOn(globalThis, 'alert').mockImplementation(() => {});
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: false, status: 500 });

      await removeCartLine(document.querySelector('#cart-row-1 .cart-remove-form'));

      expect(document.getElementById('cart-row-1')).not.toBeNull();
      expect(document.querySelector('#cart-row-1 button[type="submit"]').disabled).toBe(false);
      expect(alertSpy).toHaveBeenCalled();
    });

    it('submitting a remove form posts it in place instead of navigating', async () => {
      document.body.innerHTML = `<div id="list">${cartRow(1)}${cartRow(2)}</div>`;
      loadCart();
      document.dispatchEvent(new Event('DOMContentLoaded'));
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, headers: makeHeaders({}) });
      const form = document.querySelector('#cart-row-1 .cart-remove-form');
      const event = new Event('submit', { cancelable: true });

      form.dispatchEvent(event);

      expect(event.defaultPrevented).toBe(true);
      expect(globalThis.fetch.mock.calls[0][0]).toContain('handler=Remove');
    });
  });

  describe('saveCartLine', () => {
    it('posts every field of the row with the anti-forgery token and shows nothing on success', async () => {
      document.body.innerHTML = `
        <form id="cartSubmitForm"><input name="__RequestVerificationToken" value="tok123" /></form>
        ${cartRow(7)}`;
      loadCart();
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true });
      const row = document.getElementById('cart-row-7');

      await saveCartLine(row);

      const [url, options] = globalThis.fetch.mock.calls[0];
      expect(url).toBe('/Cart?handler=UpdateLine');
      expect(options.body.get('PendingOrderLineID')).toBe('7');
      expect(options.body.get('Condition')).toBe('0');
      expect(options.body.get('Edition')).toBe('2');
      expect(options.body.get('Quantity')).toBe('2');
      expect(options.body.get('PurchaseDate')).toBe('2026-10-20');
      expect(options.body.get('PurchasePrice')).toBe('1.50');
      expect(options.body.get('MarketPriceAtEntry')).toBe('2.25');
      expect(options.body.get('__RequestVerificationToken')).toBe('tok123');
      expect(row.querySelector('[data-save-status]').textContent).toBe('');
    });

    it('omits the token field when no token is present on the page', async () => {
      document.body.innerHTML = cartRow(7);
      loadCart();
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true });

      await saveCartLine(document.getElementById('cart-row-7'));

      expect(globalThis.fetch.mock.calls[0][1].body.has('__RequestVerificationToken')).toBe(false);
    });

    it('a non-ok response warns and shows Couldn’t save', async () => {
      document.body.innerHTML = cartRow(7);
      loadCart();
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: false, status: 404 });
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
      const row = document.getElementById('cart-row-7');

      await saveCartLine(row);

      expect(warnSpy).toHaveBeenCalledWith('Failed to save cart line, status', 404);
      expect(row.querySelector('[data-save-status]').textContent).toBe('Couldn’t save');
    });

    it('a rejected fetch warns, shows Couldn’t save, and does not throw', async () => {
      document.body.innerHTML = cartRow(7);
      loadCart();
      globalThis.fetch = vi.fn().mockRejectedValue(new Error('offline'));
      vi.spyOn(console, 'warn').mockImplementation(() => {});
      const row = document.getElementById('cart-row-7');

      await expect(saveCartLine(row)).resolves.toBeUndefined();
      expect(row.querySelector('[data-save-status]').textContent).toBe('Couldn’t save');
    });

    it('a successful save clears an earlier Couldn’t save', async () => {
      document.body.innerHTML = cartRow(7);
      loadCart();
      vi.spyOn(console, 'warn').mockImplementation(() => {});
      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({ ok: false, status: 500 })
        .mockResolvedValue({ ok: true });
      const row = document.getElementById('cart-row-7');

      await saveCartLine(row);
      await saveCartLine(row);

      expect(row.querySelector('[data-save-status]').textContent).toBe('');
    });

    it('saves run one at a time and each reads the fields when it starts', async () => {
      document.body.innerHTML = cartRow(7);
      loadCart();
      let releaseFirst;
      globalThis.fetch = vi.fn()
        .mockImplementationOnce(() => new Promise(resolve => { releaseFirst = () => resolve({ ok: true }); }))
        .mockResolvedValue({ ok: true });
      const row = document.getElementById('cart-row-7');
      const priceEl = document.getElementById('linePrice7');

      const first = saveCartLine(row);
      priceEl.value = '9.99';
      const second = saveCartLine(row);
      await Promise.resolve();

      expect(globalThis.fetch).toHaveBeenCalledTimes(1);
      releaseFirst();
      await first;
      await second;
      expect(globalThis.fetch).toHaveBeenCalledTimes(2);
      expect(globalThis.fetch.mock.calls[1][1].body.get('PurchasePrice')).toBe('9.99');
    });
  });

  describe('row wiring', () => {
    it('typing a price saves once after the typing pauses', async () => {
      vi.useFakeTimers();
      document.body.innerHTML = cartRow(7);
      loadCart();
      initCartRow(document.getElementById('cart-row-7'));
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true });
      const priceEl = document.getElementById('linePrice7');

      priceEl.value = '3';
      priceEl.dispatchEvent(new Event('input', { bubbles: true }));
      priceEl.value = '3.5';
      priceEl.dispatchEvent(new Event('input', { bubbles: true }));
      await vi.advanceTimersByTimeAsync(599);
      expect(globalThis.fetch).not.toHaveBeenCalled();

      await vi.advanceTimersByTimeAsync(1);
      expect(globalThis.fetch).toHaveBeenCalledTimes(1);
      expect(globalThis.fetch.mock.calls[0][1].body.get('PurchasePrice')).toBe('3.5');
      vi.useRealTimers();
    });

    it('changing a select saves right away', () => {
      document.body.innerHTML = cartRow(7);
      loadCart();
      initCartRow(document.getElementById('cart-row-7'));
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true });

      document.getElementById('lineCondition7').dispatchEvent(new Event('change', { bubbles: true }));

      return vi.waitFor(() => expect(globalThis.fetch).toHaveBeenCalledWith('/Cart?handler=UpdateLine', expect.anything()));
    });

    it('clicking a quantity button saves the new quantity and updates the summary', async () => {
      document.body.innerHTML = `<span id="cartSummary"></span>${cartRow(7)}`;
      loadCart();
      initCartRow(document.getElementById('cart-row-7'));
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true });

      // Stands in for the button's own onclick (selectQuantity), which runs before the row's click listener.
      document.getElementById('lineQuantity7').value = '3';
      document.querySelector('#cart-row-7 [data-qty-value="3"]').click();

      expect(document.getElementById('cartSummary').textContent).toBe('1 line · 3 cards');
      await vi.waitFor(() => expect(globalThis.fetch).toHaveBeenCalled());
      expect(globalThis.fetch.mock.calls[0][1].body.get('Quantity')).toBe('3');
    });

    it('closing the date picker saves the line', async () => {
      document.body.innerHTML = cartRow(7);
      const onClose = [];
      document.getElementById('linePurchaseDate7')._flatpickr = { config: { onClose } };
      loadCart();
      initCartRow(document.getElementById('cart-row-7'));
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true });

      onClose.forEach(hook => hook());

      await vi.waitFor(() => expect(globalThis.fetch).toHaveBeenCalledTimes(1));
    });

    it('a refreshed market price after an edition change is saved', async () => {
      document.body.innerHTML = cartRow(7);
      loadCart();
      initCartRow(document.getElementById('cart-row-7'));
      globalThis.fetch = vi.fn((url) => Promise.resolve(url.startsWith('/api/price')
        ? { ok: true, json: () => Promise.resolve({ price: 4 }) }
        : { ok: true }));

      document.getElementById('lineEdition7').dispatchEvent(new Event('change', { bubbles: true }));

      await vi.waitFor(() => {
        const saves = globalThis.fetch.mock.calls.filter(([url]) => url === '/Cart?handler=UpdateLine');
        expect(saves.length).toBe(2);
        expect(saves[1][1].body.get('MarketPriceAtEntry')).toBe('4.00');
      });
    });
  });

  function cartRow(id) {
    return `
      <div class="list-group-item" id="cart-row-${id}">
        <input type="hidden" name="Lines[${id}].PendingOrderLineID" value="${id}" />
        <select name="Lines[${id}].Condition" id="lineCondition${id}"><option value="0" selected>NM</option></select>
        <select name="Lines[${id}].Edition" id="lineEdition${id}" data-printing-id="101" data-market-price-target="lineMarketPrice${id}">
          <option value="2" selected>Unlimited</option>
        </select>
        <input type="hidden" name="Lines[${id}].Quantity" id="lineQuantity${id}" value="2" />
        <div>
          <button type="button" data-qty-target="lineQuantity${id}" data-qty-value="1">1</button>
          <button type="button" data-qty-target="lineQuantity${id}" data-qty-value="3">3</button>
        </div>
        <input class="cc-date-picker" name="Lines[${id}].PurchaseDate" id="linePurchaseDate${id}" value="2026-10-20" />
        <input type="number" name="Lines[${id}].PurchasePrice" id="linePrice${id}" value="1.50" />
        <input type="number" name="Lines[${id}].MarketPriceAtEntry" id="lineMarketPrice${id}" value="2.25" />
        <span data-save-status></span>
        <form class="cart-remove-form" action="/Cart?handler=Remove">
          <input type="hidden" name="id" value="${id}" />
          <button type="submit">Remove</button>
        </form>
      </div>`;
  }

  function loadCart() {
    loadScript('site.js');
    loadScript('cart.js');
  }

  function makeHeaders(map) {
    return { get: (key) => map[key] ?? null };
  }
});
