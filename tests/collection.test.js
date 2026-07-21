import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

describe('collection.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  it('applies card defaults and checks setAsPreferred', async () => {
    setupForm();

    await openAddModal(document.getElementById('btn'));

    expect(document.getElementById('atcCondition').value).toBe(CardDefaults.Condition);
    expect(document.getElementById('atcEdition').value).toBe(CardDefaults.Edition);
    expect(document.getElementById('atcAcquisition').value).toBe(CardDefaults.Acquisition);
    expect(document.getElementById('atcSetAsPreferred').checked).toBe(true);
  });

  it('blank rarityName defaults to an empty string', async () => {
    document.body.innerHTML = `
      <input id="addCardID" /><input id="addImageID" /><input id="addSetCode" />
      <input id="atcRarityName" /><span id="atcRarityDisplay"></span>
      <div id="atcAcquisitionGroup"></div>
      <input type="hidden" id="atcQuantity" />
      <select id="atcCondition"><option value="4"></option></select>
      <select id="atcEdition"><option value="0"></option></select>
      <select id="atcAcquisition"><option value="1"></option></select>
      <input id="atcPurchaseDate" /><input id="atcPurchasePrice" />
      <input type="checkbox" id="atcSetAsPreferred" /><input id="atcMarketPrice" />
      <form id="addForm"></form>
      <div id="addModal"></div>
      <button id="btn" data-card-id="1" data-image-id="10" data-set-code="LOB-EN001"></button>`;
    loadScript('enums.js');
    loadScript('site.js');
    loadScript('collection-groups.js');
    loadScript('collection.js');
    globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve({ price: null }) });

    await openAddModal(document.getElementById('btn'));

    expect(document.getElementById('atcRarityName').value).toBe('');
    expect(document.getElementById('atcRarityDisplay').textContent).toBe('');
  });

  it('populates fields from the button dataset, shows the acquisition group, and marks the ajax target row', async () => {
    setupForm();

    await openAddModal(document.getElementById('btn'));

    expect(document.getElementById('addCardID').value).toBe('1');
    expect(document.getElementById('addSetCode').value).toBe('LOB-EN001');
    expect(document.getElementById('atcRarityDisplay').textContent).toBe('Ultra Rare');
    expect(document.getElementById('atcAcquisitionGroup').style.display).toBe('block');
    expect(document.getElementById('addForm').dataset.ajaxTarget).toBe('group-row-5');
  });

  function setupForm() {
    document.body.innerHTML = `
      <input id="addCardID" /><input id="addImageID" /><input id="addSetCode" />
      <input id="atcRarityName" /><span id="atcRarityDisplay"></span>
      <div id="atcAcquisitionGroup" style="display: none;"></div>
      <input type="hidden" id="atcQuantity" />
      <select id="atcCondition"><option value="4"></option></select>
      <select id="atcEdition"><option value="0"></option></select>
      <select id="atcAcquisition"><option value="1"></option></select>
      <input id="atcPurchaseDate" /><input id="atcPurchasePrice" />
      <input type="checkbox" id="atcSetAsPreferred" /><input id="atcMarketPrice" />
      <form id="addForm"></form>
      <div id="addModal"></div>
      <div id="group-row-5">
        <button id="btn" data-card-id="1" data-image-id="10" data-set-code="LOB-EN001" data-rarity-name="Ultra Rare"
                data-tcg-date="2002-03-08"></button>
      </div>`;
    loadScript('enums.js');
    loadScript('site.js');
    loadScript('collection-groups.js'); // provides setSelect, used by openAddModal
    loadScript('collection.js');
    globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve({ price: null }) });
  }
});
