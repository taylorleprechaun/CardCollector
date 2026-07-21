import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

describe('collection-entry-modal.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  it('action=Order sets order-specific label, button text/class, and hides the acquisition group', async () => {
    setupForm();

    await openModal('LOB-EN001', 'Legend', 'Order', 'Ultra Rare', '2002-03-08');

    expect(document.getElementById('orderModalLabel').textContent).toBe('Order Printing');
    expect(document.getElementById('modalSubmitBtn').textContent).toBe('Confirm Order');
    expect(document.getElementById('modalSubmitBtn').className).toBe('btn btn-primary');
    expect(document.getElementById('atcAcquisitionGroup').style.display).toBe('none');
    expect(document.getElementById('orderForm').action).toContain('handler=Order');
  });

  it('action=Own sets own-specific label, button text/class, and shows the acquisition group', async () => {
    setupForm();

    await openModal('LOB-EN001', 'Legend', 'Own', 'Ultra Rare', '2002-03-08');

    expect(document.getElementById('orderModalLabel').textContent).toBe('Already Own This Printing');
    expect(document.getElementById('modalSubmitBtn').textContent).toBe('Add to Collection');
    expect(document.getElementById('modalSubmitBtn').className).toBe('btn btn-success');
    expect(document.getElementById('atcAcquisitionGroup').style.display).toBe('block');
  });

  it('applies card defaults, checks setAsPreferred, and clears purchase price', async () => {
    setupForm();

    await openModal('LOB-EN001', 'Legend', 'Own', 'Ultra Rare', null);

    expect(document.getElementById('atcCondition').value).toBe(CardDefaults.Condition);
    expect(document.getElementById('atcEdition').value).toBe(CardDefaults.Edition);
    expect(document.getElementById('atcAcquisition').value).toBe(CardDefaults.Acquisition);
    expect(document.getElementById('atcSetAsPreferred').checked).toBe(true);
    expect(document.getElementById('atcPurchasePrice').value).toBe('');
  });

  it('blank rarityName defaults the rarity fields to empty strings', async () => {
    setupForm();

    await openModal('LOB-EN001', 'Legend', 'Order', null, null);

    expect(document.getElementById('atcRarityName').value).toBe('');
    expect(document.getElementById('atcRarityDisplay').textContent).toBe('');
  });

  function setupForm() {
    document.body.innerHTML = `
      <form id="orderForm" data-page-url="/Card/1">
        <input name="CardID" value="1" />
      </form>
      <input id="atcSetCode" /><span id="atcSetNameLabel"></span><span id="orderModalLabel"></span>
      <button id="modalSubmitBtn"></button>
      <input id="atcRarityName" /><span id="atcRarityDisplay"></span>
      <div id="atcAcquisitionGroup"></div>
      <select id="atcCondition"><option value="4"></option></select>
      <select id="atcEdition"><option value="0"></option></select>
      <select id="atcAcquisition"><option value="1"></option></select>
      <input type="hidden" id="atcQuantity" /><input id="atcPurchaseDate" /><input id="atcPurchasePrice" />
      <input type="checkbox" id="atcSetAsPreferred" /><input id="atcMarketPrice" />
      <div id="orderModal"></div>`;
    loadScript('enums.js');
    loadScript('site.js');
    loadScript('collection-entry-modal.js');
    globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve({ price: null }) });
  }
});
