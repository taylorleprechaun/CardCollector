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

  describe('persistCartLineQuantity', () => {
    it('logs a warning and does not throw when fetch rejects', async () => {
      document.body.innerHTML = '';
      loadScript('cart.js');
      globalThis.fetch = vi.fn().mockRejectedValue(new Error('offline'));
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

      await expect(persistCartLineQuantity(7, 2)).resolves.toBeUndefined();
      expect(warnSpy).toHaveBeenCalled();
    });

    it('logs a warning when the server responds with a non-ok status', async () => {
      document.body.innerHTML = '';
      loadScript('cart.js');
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: false, status: 500 });
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

      await persistCartLineQuantity(7, 2);

      expect(warnSpy).toHaveBeenCalledWith('Failed to save cart line quantity, status', 500);
    });

    it('omits the token field when no token is present on the page', async () => {
      document.body.innerHTML = '';
      loadScript('cart.js');
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true });

      await persistCartLineQuantity(7, 2);

      const options = globalThis.fetch.mock.calls[0][1];
      expect(options.body.has('__RequestVerificationToken')).toBe(false);
    });

    it('sends id, quantity, and the anti-forgery token when present', async () => {
      document.body.innerHTML = `
        <form id="cartSubmitForm"><input name="__RequestVerificationToken" value="tok123" /></form>`;
      loadScript('cart.js');
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true });

      await persistCartLineQuantity(7, 2);

      const [url, options] = globalThis.fetch.mock.calls[0];
      expect(url).toBe('/Cart?handler=UpdateQuantity');
      expect(options.body.get('id')).toBe('7');
      expect(options.body.get('quantity')).toBe('2');
      expect(options.body.get('__RequestVerificationToken')).toBe('tok123');
    });
  });

  describe('recomputeCartTotal', () => {
    it('missing #cartTotal is a no-op', () => {
      document.body.innerHTML = '';
      loadScript('cart.js');

      expect(() => recomputeCartTotal()).not.toThrow();
    });

    it('non-numeric price or missing quantity element are treated as zero', () => {
      document.body.innerHTML = `
        <span id="cartTotal"></span>
        <input id="linePrice1" value="not-a-number" />
        <input id="lineQuantity1" value="2" />
        <input id="linePrice2" value="5.00" />`;
      loadScript('cart.js');

      recomputeCartTotal();

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

      recomputeCartTotal();

      expect(document.getElementById('cartTotal').textContent).toBe('$40.00');
    });
  });

  describe('selectCartLineQuantity', () => {
    it('updates the quantity buttons and persists the new quantity', async () => {
      document.body.innerHTML = `
        <input type="hidden" id="lineQuantity1" />
        <div id="group">
          <button id="btn1" data-qty-target="lineQuantity1" data-qty-value="1" data-pending-order-line-id="42"></button>
          <button id="btn3" data-qty-target="lineQuantity1" data-qty-value="3" data-pending-order-line-id="42"></button>
        </div>`;
      loadScript('site.js');
      loadScript('cart.js');
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true });

      selectCartLineQuantity(document.getElementById('btn3'));

      expect(document.getElementById('lineQuantity1').value).toBe('3');
      expect(globalThis.fetch).toHaveBeenCalledWith('/Cart?handler=UpdateQuantity', expect.objectContaining({ method: 'POST' }));
    });
  });
});
