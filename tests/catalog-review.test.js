import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

describe('catalog-review.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
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
