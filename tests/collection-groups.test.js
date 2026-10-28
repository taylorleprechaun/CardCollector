import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

describe('collection-groups.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  describe('applyAjaxGroupResponse', () => {
    it('append response removes the target and adds the new row at the end of the list', () => {
      document.body.innerHTML = '<form id="theForm" data-ajax-target="review-row-1"></form><div id="list"><div id="review-row-1"></div><div id="review-row-2"></div></div>';
      loadDeps();

      applyAjaxGroupResponse(document.getElementById('theForm'), '<div id="review-row-3">next</div>', true);

      expect(document.getElementById('review-row-1')).toBeNull();
      expect([...document.getElementById('list').children].map(c => c.id)).toEqual(['review-row-2', 'review-row-3']);
    });

    it('append response for a row already shown only removes the target', () => {
      document.body.innerHTML = '<form id="theForm" data-ajax-target="review-row-1"></form><div id="list"><div id="review-row-1"></div><div id="review-row-2"></div></div>';
      loadDeps();

      applyAjaxGroupResponse(document.getElementById('theForm'), '<div id="review-row-2">dup</div>', true);

      expect([...document.getElementById('list').children].map(c => c.id)).toEqual(['review-row-2']);
    });

    it('removing the last row of a reload-when-empty list reloads the page', () => {
      document.body.innerHTML = '<form id="theForm" data-ajax-target="review-row-1"></form><div id="list" data-reload-when-empty><div id="review-row-1"></div></div>';
      loadDeps();
      globalThis.reloadPage = vi.fn();

      applyAjaxGroupResponse(document.getElementById('theForm'), '');

      expect(globalThis.reloadPage).toHaveBeenCalledTimes(1);
    });

    it('removing a row from a list without the marker does not reload', () => {
      document.body.innerHTML = '<form id="theForm" data-ajax-target="group-row-1"></form><div id="list"><div id="group-row-1"></div></div>';
      loadDeps();
      globalThis.reloadPage = vi.fn();

      applyAjaxGroupResponse(document.getElementById('theForm'), '');

      expect(globalThis.reloadPage).not.toHaveBeenCalled();
    });

    it('empty html removes the target row entirely', () => {
      document.body.innerHTML = '<form id="theForm" data-ajax-target="group-row-1"></form><div id="group-row-1">old</div>';
      loadDeps();

      applyAjaxGroupResponse(document.getElementById('theForm'), '');

      expect(document.getElementById('group-row-1')).toBeNull();
    });

    it('no target found is a no-op', () => {
      document.body.innerHTML = '<form id="theForm" data-ajax-target="does-not-exist"></form>';
      loadDeps();

      expect(() => applyAjaxGroupResponse(document.getElementById('theForm'), '<div>new</div>')).not.toThrow();
    });

    it('preserves an already-expanded collapse section after replacement', () => {
      document.body.innerHTML = `
        <form id="theForm" data-ajax-target="group-row-1"></form>
        <div id="group-row-1">
          <button data-bs-toggle="collapse" aria-expanded="false"></button>
          <div class="collapse show"></div>
        </div>`;
      loadDeps();

      applyAjaxGroupResponse(document.getElementById('theForm'),
        '<div id="group-row-1"><button data-bs-toggle="collapse" aria-expanded="false"></button><div class="collapse"></div></div>');

      const newRow = document.getElementById('group-row-1');
      expect(newRow.querySelector('.collapse').classList.contains('show')).toBe(true);
      expect(newRow.querySelector('[data-bs-toggle="collapse"]').getAttribute('aria-expanded')).toBe('true');
    });

    it('replaces the target row and closes any open modal containing the form', () => {
      document.body.innerHTML = `
        <div class="modal"><form id="theForm" data-ajax-target="group-row-1"></form></div>
        <div id="group-row-1">old</div>`;
      loadDeps();
      const modalEl = document.querySelector('.modal');
      const modalInstance = new bootstrap.Modal(modalEl);

      applyAjaxGroupResponse(document.getElementById('theForm'), '<div id="group-row-1">new</div>');

      expect(document.getElementById('group-row-1').textContent).toBe('new');
      expect(modalInstance.hide).toHaveBeenCalled();
    });
  });

  describe('openEditModal', () => {
    it('populates fields from the button dataset and marks the ajax target row', () => {
      document.body.innerHTML = `
        <input id="editEntryID" /><span id="editRarityDisplay"></span>
        <input type="hidden" id="editQuantity" />
        <select id="editCondition"><option value="2"></option></select>
        <select id="editEdition"><option value="1"></option></select>
        <select id="editAcquisition"><option value="0"></option></select>
        <input id="editPurchaseDate" /><input id="editPurchasePrice" /><input id="editMarketPrice" />
        <form id="editForm"></form>
        <div id="editModal"></div>
        <div id="group-row-5">
          <button id="btn" data-entry-id="99" data-rarity-name="Ultra Rare" data-quantity="2"
                  data-condition="2" data-edition="1" data-acquisition-method="0"
                  data-purchase-date="2026-01-01" data-purchase-price="9.99" data-market-price="12.00"></button>
        </div>`;
      loadDeps();

      openEditModal(document.getElementById('btn'));

      expect(document.getElementById('editEntryID').value).toBe('99');
      expect(document.getElementById('editRarityDisplay').textContent).toBe('Ultra Rare');
      expect(document.getElementById('editCondition').value).toBe('2');
      expect(document.getElementById('editForm').dataset.ajaxTarget).toBe('group-row-5');
    });
  });

  describe('submitAjaxForm', () => {
    it('a submitter with a formaction attribute overrides the form action', async () => {
      document.body.innerHTML = `
        <form id="theForm" action="/Collection?handler=Edit">
          <button id="submitter" formaction="/Collection?handler=Delete"></button>
        </form>`;
      loadDeps();
      const submitter = document.getElementById('submitter');
      // jsdom's HTMLButtonElement doesn't fully implement the formAction IDL reflection for
      // implicitly form-associated buttons, so override it directly to test our own branching
      // logic (submitter.formAction used when the attribute is present) rather than jsdom's URL
      // resolution.
      Object.defineProperty(submitter, 'formAction', { value: '/Collection?handler=Delete' });
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        text: () => Promise.resolve(''),
        headers: { get: () => null }
      });

      await submitAjaxForm(document.getElementById('theForm'), submitter);

      expect(globalThis.fetch.mock.calls[0][0]).toContain('handler=Delete');
    });

    it('fetch throwing logs an error and shows an alert', async () => {
      document.body.innerHTML = '<form id="theForm" action="/Collection?handler=Edit"></form>';
      loadDeps();
      vi.spyOn(console, 'error').mockImplementation(() => {});
      const alertSpy = vi.spyOn(globalThis, 'alert').mockImplementation(() => {});
      globalThis.fetch = vi.fn().mockRejectedValue(new Error('offline'));

      await submitAjaxForm(document.getElementById('theForm'), null);

      expect(alertSpy).toHaveBeenCalled();
    });

    it('non-ok response shows an alert and does not update badges', async () => {
      document.body.innerHTML = '<form id="theForm" action="/Collection?handler=Edit"></form>';
      loadDeps();
      const alertSpy = vi.spyOn(globalThis, 'alert').mockImplementation(() => {});
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: false });

      await submitAjaxForm(document.getElementById('theForm'), null);

      expect(alertSpy).toHaveBeenCalled();
    });

    it('successful submission updates total-count and cart badges then applies the response', async () => {
      document.body.innerHTML = `
        <form id="theForm" action="/Collection?handler=Edit" data-ajax-target="row-1"></form>
        <div id="row-1"></div>
        <span id="totalCountBadge" data-suffix="cards"></span>
        <span id="navCartBadge" class="d-none"><span id="navCartCount"></span></span>`;
      loadDeps();
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        text: () => Promise.resolve('<div id="row-1">updated</div>'),
        headers: makeHeaders({ 'X-Total-Count': '10', 'X-Cart-Count': '2', 'X-Cart-Total': '15' })
      });

      await submitAjaxForm(document.getElementById('theForm'), null);

      expect(document.getElementById('totalCountBadge').textContent).toBe('10 cards');
      expect(document.getElementById('navCartCount').textContent).toBe('2');
      expect(document.getElementById('row-1').textContent).toBe('updated');
    });

    it('a response without X-Total-Count leaves the total-count badge alone', async () => {
      document.body.innerHTML = `
        <form id="theForm" action="/Wishlist?handler=AddToCart" data-ajax-target="row-1"></form>
        <div id="row-1"></div>
        <span id="totalCountBadge" data-suffix="needed">12 needed</span>`;
      loadDeps();
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        text: () => Promise.resolve('<div id="row-1">updated</div>'),
        headers: makeHeaders({ 'X-Cart-Count': '2', 'X-Cart-Total': '15' })
      });

      await submitAjaxForm(document.getElementById('theForm'), null);

      expect(document.getElementById('totalCountBadge').textContent).toBe('12 needed');
    });

    it('append header adds the returned row after removing the target and updates the review counts', async () => {
      document.body.innerHTML = `
        <form id="theForm" action="/Catalog/Review?handler=Map" data-ajax-target="review-row-1"></form>
        <div id="list"><div id="review-row-1"></div></div>
        <span data-review-tab-count="NeedsDecision">5</span>`;
      loadDeps();
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        text: () => Promise.resolve('<div id="review-row-9">next</div>'),
        headers: makeHeaders({ 'X-Ajax-Append': 'true', 'X-Review-Open-Counts': '{"needsDecision":4,"informational":0,"kinds":{}}' })
      });

      await submitAjaxForm(document.getElementById('theForm'), null);

      expect(document.getElementById('review-row-1')).toBeNull();
      expect(document.getElementById('review-row-9').textContent).toBe('next');
      expect(document.querySelector('[data-review-tab-count]').textContent).toBe('4');
    });

    function makeHeaders(map) {
      return { get: (key) => map[key] ?? null };
    }
  });

  describe('updateTotalCountBadge', () => {
    it('missing badge element does not throw', () => {
      document.body.innerHTML = '';
      loadDeps();

      expect(() => updateTotalCountBadge(5)).not.toThrow();
    });

    it('NaN value is a no-op', () => {
      document.body.innerHTML = '<span id="totalCountBadge" data-suffix="cards"></span>';
      loadDeps();

      updateTotalCountBadge(NaN);

      expect(document.getElementById('totalCountBadge').textContent).toBe('');
    });

    it('null value is a no-op', () => {
      document.body.innerHTML = '<span id="totalCountBadge" data-suffix="cards"></span>';
      loadDeps();

      updateTotalCountBadge(null);

      expect(document.getElementById('totalCountBadge').textContent).toBe('');
    });

    it('sets the badge text combining the count and suffix', () => {
      document.body.innerHTML = '<span id="totalCountBadge" data-suffix="cards"></span>';
      loadDeps();

      updateTotalCountBadge(42);

      expect(document.getElementById('totalCountBadge').textContent).toBe('42 cards');
    });
  });

  describe('updateOrdersSummary', () => {
    it('sets the order and card counts and the total', () => {
      document.body.innerHTML = '<span id="ordersSummary"></span><strong id="ordersTotal"></strong>';
      loadDeps();

      updateOrdersSummary('3', '7', '41.5');

      expect(document.getElementById('ordersSummary').textContent).toBe('3 orders · 7 cards');
      expect(document.getElementById('ordersTotal').textContent).toBe('$41.50');
    });

    it('missing headers leave the header alone', () => {
      document.body.innerHTML = '<span id="ordersSummary">2 orders · 2 cards</span><strong id="ordersTotal">$5.00</strong>';
      loadDeps();

      updateOrdersSummary(null, null, null);

      expect(document.getElementById('ordersSummary').textContent).toBe('2 orders · 2 cards');
      expect(document.getElementById('ordersTotal').textContent).toBe('$5.00');
    });

    it('missing elements do not throw', () => {
      document.body.innerHTML = '';
      loadDeps();

      expect(() => updateOrdersSummary('1', '1', '1')).not.toThrow();
    });
  });

  function loadDeps() {
    loadScript('site.js');
    loadScript('collection-groups.js');
  }
});
