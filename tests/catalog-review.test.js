import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

describe('catalog-review.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  describe('submitOnChange', () => {
    it('changing an auto-submit select submits its form', () => {
      document.body.innerHTML = '<form id="filters"><select id="kind" data-auto-submit><option value="">All</option><option value="CardAdded">Card Added</option></select></form>';
      loadScript('catalog-review.js');
      const form = document.getElementById('filters');
      form.requestSubmit = vi.fn();
      const select = document.getElementById('kind');
      select.value = 'CardAdded';

      submitOnChange({ target: select });

      expect(form.requestSubmit).toHaveBeenCalledTimes(1);
    });

    it('changing a select without the marker does nothing', () => {
      document.body.innerHTML = '<form id="filters"><select id="kind"><option value="">All</option></select></form>';
      loadScript('catalog-review.js');
      const form = document.getElementById('filters');
      form.requestSubmit = vi.fn();

      submitOnChange({ target: document.getElementById('kind') });

      expect(form.requestSubmit).not.toHaveBeenCalled();
    });
  });

  describe('confirmBeforeSubmit', () => {
    it('cancelled confirmation stops the submit', () => {
      const form = setupForm('Dismiss every informational item?');
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const event = submitEvent(form);

      confirmBeforeSubmit(event);

      expect(window.confirm).toHaveBeenCalledWith('Dismiss every informational item?');
      expect(event.defaultPrevented).toBe(true);
    });

    it('accepted confirmation lets the submit through', () => {
      const form = setupForm('Dismiss every informational item?');
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const event = submitEvent(form);

      confirmBeforeSubmit(event);

      expect(event.defaultPrevented).toBe(false);
    });

    it('form without data-confirm is left alone', () => {
      document.body.innerHTML = '<form id="plain"></form>';
      loadScript('catalog-review.js');
      const confirmSpy = vi.spyOn(window, 'confirm');
      const event = submitEvent(document.getElementById('plain'));

      confirmBeforeSubmit(event);

      expect(confirmSpy).not.toHaveBeenCalled();
      expect(event.defaultPrevented).toBe(false);
    });
  });

  function setupForm(message) {
    document.body.innerHTML = `<form id="dismissAll" data-confirm="${message}"></form>`;
    loadScript('catalog-review.js');
    return document.getElementById('dismissAll');
  }

  function submitEvent(form) {
    const event = new Event('submit', { bubbles: true, cancelable: true });
    Object.defineProperty(event, 'target', { value: form });
    return event;
  }
});
