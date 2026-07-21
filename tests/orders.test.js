import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

describe('orders.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  function loadDeps() {
    loadScript('site.js');
    loadScript('orders.js');
  }

  it('populates the entry ID and quantity buttons, then opens the modal', () => {
    document.body.innerHTML = `
      <input id="markOwnedEntryID" />
      <input type="hidden" id="markOwnedQuantity" />
      <button data-qty-target="markOwnedQuantity" data-qty-value="2"></button>
      <div id="markOwnedModal"></div>
      <button id="btn" data-entry-id="42" data-quantity="2"></button>`;
    loadDeps();

    openMarkOwnedModal(document.getElementById('btn'));

    expect(document.getElementById('markOwnedEntryID').value).toBe('42');
    expect(document.getElementById('markOwnedQuantity').value).toBe('2');
    expect(bootstrap.Modal).toHaveBeenCalledWith(document.getElementById('markOwnedModal'));
  });

  it('clamps quantity above 3 down to 3', () => {
    document.body.innerHTML = `
      <input id="markOwnedEntryID" />
      <input type="hidden" id="markOwnedQuantity" />
      <div id="markOwnedModal"></div>
      <button id="btn" data-entry-id="1" data-quantity="10"></button>`;
    loadDeps();

    openMarkOwnedModal(document.getElementById('btn'));

    expect(document.getElementById('markOwnedQuantity').value).toBe('3');
  });

  it('clamps quantity below 1 up to 1, and a non-numeric quantity defaults to 1', () => {
    document.body.innerHTML = `
      <input id="markOwnedEntryID" />
      <input type="hidden" id="markOwnedQuantity" />
      <div id="markOwnedModal"></div>
      <button id="btn" data-entry-id="1" data-quantity="not-a-number"></button>`;
    loadDeps();

    openMarkOwnedModal(document.getElementById('btn'));

    expect(document.getElementById('markOwnedQuantity').value).toBe('1');
  });
});
