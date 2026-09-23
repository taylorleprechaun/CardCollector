import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

function buildDom() {
  document.body.innerHTML = `
    <form id="deckLegalityForm">
      <input type="radio" name="view" value="AtEvent" data-auto-submit />
      <input type="radio" name="view" value="Current" data-auto-submit />
      <select name="eventId" data-auto-submit></select>
      <select name="listDate" data-auto-submit>
        <option value="">Auto</option>
        <option value="2024-01-01">Jan 1</option>
      </select>
      <input type="text" />
    </form>`;
  loadScript('deck-legality.js');
}

describe('deck-legality.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('ignores a change on a control without data-auto-submit', () => {
    buildDom();
    const form = document.getElementById('deckLegalityForm');
    form.submit = vi.fn();
    const plainInput = form.querySelector('input[type="text"]');

    plainInput.dispatchEvent(new Event('change', { bubbles: true }));

    expect(form.submit).not.toHaveBeenCalled();
  });

  it('leaves the list-date select alone when it is the control that changed', () => {
    buildDom();
    const form = document.getElementById('deckLegalityForm');
    form.submit = vi.fn();
    const listDate = form.querySelector('[name="listDate"]');
    listDate.value = '2024-01-01';

    listDate.dispatchEvent(new Event('change', { bubbles: true }));

    expect(listDate.value).toBe('2024-01-01');
    expect(form.submit).toHaveBeenCalledTimes(1);
  });

  it('only binds its listener once even if the script loads twice', () => {
    buildDom();
    loadScript('deck-legality.js');
    const form = document.getElementById('deckLegalityForm');
    form.submit = vi.fn();
    const eventSelect = form.querySelector('[name="eventId"]');

    eventSelect.dispatchEvent(new Event('change', { bubbles: true }));

    expect(form.submit).toHaveBeenCalledTimes(1);
  });

  it('resets the list-date select when the event changes', () => {
    buildDom();
    const form = document.getElementById('deckLegalityForm');
    form.submit = vi.fn();
    const listDate = form.querySelector('[name="listDate"]');
    listDate.value = '2024-01-01';
    const eventSelect = form.querySelector('[name="eventId"]');

    eventSelect.dispatchEvent(new Event('change', { bubbles: true }));

    expect(listDate.value).toBe('');
  });

  it('resets the list-date select when the view changes', () => {
    buildDom();
    const form = document.getElementById('deckLegalityForm');
    form.submit = vi.fn();
    const listDate = form.querySelector('[name="listDate"]');
    listDate.value = '2024-01-01';
    const viewRadio = form.querySelector('[name="view"]');

    viewRadio.dispatchEvent(new Event('change', { bubbles: true }));

    expect(listDate.value).toBe('');
    expect(form.submit).toHaveBeenCalledTimes(1);
  });

  it('submits the enclosing form when a data-auto-submit control changes', () => {
    buildDom();
    const form = document.getElementById('deckLegalityForm');
    form.submit = vi.fn();
    const radio = form.querySelector('input[type="radio"]');

    radio.dispatchEvent(new Event('change', { bubbles: true }));

    expect(form.submit).toHaveBeenCalledTimes(1);
  });
});
