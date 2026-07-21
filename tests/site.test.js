import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

describe('site.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  describe('bindPriceRefresh / initLiveMarketPrice', () => {
    it('fetch throwing is caught and does not propagate', async () => {
      const { editionSelect, marketPriceEl } = setupElements();
      globalThis.fetch = vi.fn().mockRejectedValue(new Error('network down'));
      vi.spyOn(console, 'warn').mockImplementation(() => {});

      const refresh = bindPriceRefresh(editionSelect, marketPriceEl, () => ({ cardID: 1, setCode: 'LOB-EN001', rarityName: 'Ultra Rare' }));

      await expect(refresh()).resolves.toBeUndefined();
    });

    it('initLiveMarketPrice shows a loading placeholder then settles to 0.00 after refresh', async () => {
      const { editionSelect, marketPriceEl } = setupElements();
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve({ price: null }) });
      marketPriceEl.value = 'stale';

      await initLiveMarketPrice(editionSelect, marketPriceEl, () => ({ cardID: 1, setCode: 'LOB-EN001', rarityName: 'Ultra Rare' }));

      expect(marketPriceEl.value).toBe('');
      expect(marketPriceEl.placeholder).toBe('0.00');
    });

    it('missing cardID/setCode/rarityName short-circuits without calling fetch', async () => {
      const { editionSelect, marketPriceEl } = setupElements();
      globalThis.fetch = vi.fn();

      const refresh = bindPriceRefresh(editionSelect, marketPriceEl, () => ({ cardID: null, setCode: 'LOB-EN001', rarityName: 'Ultra Rare' }));
      await refresh();

      expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('rebinding removes the previous change listener before attaching the new one', () => {
      const { editionSelect, marketPriceEl } = setupElements();
      const removeSpy = vi.spyOn(editionSelect, 'removeEventListener');

      bindPriceRefresh(editionSelect, marketPriceEl, () => ({}));
      bindPriceRefresh(editionSelect, marketPriceEl, () => ({}));

      expect(removeSpy).toHaveBeenCalledTimes(1);
    });

    it('response not ok leaves the market price unchanged', async () => {
      const { editionSelect, marketPriceEl } = setupElements();
      marketPriceEl.value = 'unchanged';
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: false });

      const refresh = bindPriceRefresh(editionSelect, marketPriceEl, () => ({ cardID: 1, setCode: 'LOB-EN001', rarityName: 'Ultra Rare' }));
      await refresh();

      expect(marketPriceEl.value).toBe('unchanged');
    });

    it('successful fetch sets the market price formatted to two decimals', async () => {
      const { editionSelect, marketPriceEl } = setupElements();
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve({ price: 9.5 }) });

      const refresh = bindPriceRefresh(editionSelect, marketPriceEl, () => ({ cardID: 1, setCode: 'LOB-EN001', rarityName: 'Ultra Rare' }));
      await refresh();

      expect(marketPriceEl.value).toBe('9.50');
    });

    function setupElements() {
      document.body.innerHTML = '<select id="edition"></select><input id="marketPrice" />';
      loadScript('site.js');
      return {
        editionSelect: document.getElementById('edition'),
        marketPriceEl: document.getElementById('marketPrice')
      };
    }
  });

  describe('buildTypeahead', () => {
    function setupTypeahead() {
      document.body.innerHTML = '<input id="input" /><div id="dropdown"></div>';
      loadScript('site.js');
      return {
        input: document.getElementById('input'),
        dropdown: document.getElementById('dropdown')
      };
    }

    it('ArrowDown then Enter selects the highlighted item', () => {
      const { input, dropdown } = setupTypeahead();
      const onSelect = vi.fn();
      const typeahead = buildTypeahead(input, dropdown, onSelect, null);
      typeahead.show(['Dark Magician', 'Blue-Eyes White Dragon']);

      input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
      input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));

      expect(input.value).toBe('Dark Magician');
      expect(onSelect).toHaveBeenCalledWith('Dark Magician');
      expect(dropdown.style.display).toBe('none');
    });

    it('ArrowUp does not go below the first item', () => {
      const { input, dropdown } = setupTypeahead();
      const onSelect = vi.fn();
      const typeahead = buildTypeahead(input, dropdown, onSelect, null);
      typeahead.show(['Dark Magician', 'Blue-Eyes White Dragon']);

      input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp' }));
      input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));

      expect(onSelect).not.toHaveBeenCalled();
    });

    it('blur hides the dropdown after a short delay', () => {
      vi.useFakeTimers();
      const { input, dropdown } = setupTypeahead();
      const typeahead = buildTypeahead(input, dropdown, null, null);
      typeahead.show(['Dark Magician']);

      input.dispatchEvent(new FocusEvent('blur'));
      expect(dropdown.style.display).toBe('block');

      vi.advanceTimersByTime(150);
      expect(dropdown.style.display).toBe('none');
    });

    it('Enter without a highlighted selection calls onEnterWithoutSelection', () => {
      const { input, dropdown } = setupTypeahead();
      const onEnterWithoutSelection = vi.fn();
      const typeahead = buildTypeahead(input, dropdown, null, onEnterWithoutSelection);
      typeahead.show(['Dark Magician']);

      input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));

      expect(onEnterWithoutSelection).toHaveBeenCalled();
    });

    it('Escape hides the dropdown', () => {
      const { input, dropdown } = setupTypeahead();
      const typeahead = buildTypeahead(input, dropdown, null, null);
      typeahead.show(['Dark Magician']);

      input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));

      expect(dropdown.style.display).toBe('none');
    });

    it('mouseover highlights the hovered item', () => {
      const { input, dropdown } = setupTypeahead();
      const typeahead = buildTypeahead(input, dropdown, null, null);
      typeahead.show(['Dark Magician', 'Blue-Eyes White Dragon']);
      const items = dropdown.querySelectorAll('[data-ti]');

      items[1].dispatchEvent(new MouseEvent('mouseover'));

      expect(items[1].classList.contains('cc-typeahead-item--active')).toBe(true);
      expect(items[0].classList.contains('cc-typeahead-item--active')).toBe(false);
    });

    it('pointerdown on an item selects it', () => {
      const { input, dropdown } = setupTypeahead();
      const onSelect = vi.fn();
      const typeahead = buildTypeahead(input, dropdown, onSelect, null);
      typeahead.show(['Dark Magician']);
      const item = dropdown.querySelector('[data-ti]');

      // jsdom doesn't implement PointerEvent — a plain Event with the matching type is enough
      // since the handler only reads e.preventDefault(), which any dispatched Event provides.
      item.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, cancelable: true }));

      expect(input.value).toBe('Dark Magician');
      expect(onSelect).toHaveBeenCalledWith('Dark Magician');
      expect(dropdown.style.display).toBe('none');
    });

    it('show renders one item per name and hides the dropdown when empty', () => {
      const { input, dropdown } = setupTypeahead();
      const typeahead = buildTypeahead(input, dropdown, null, null);

      typeahead.show(['Dark Magician', 'Blue-Eyes White Dragon']);
      expect(dropdown.querySelectorAll('[data-ti]').length).toBe(2);
      expect(dropdown.style.display).toBe('block');

      typeahead.show([]);
      expect(dropdown.style.display).toBe('none');
    });
  });

  describe('card-name autocomplete IIFE', () => {
    it('typing a long enough query debounces then fetches and shows suggestions', async () => {
      vi.useFakeTimers();
      document.body.innerHTML = `
        <input data-autocomplete-url="/api/browse/autocomplete?x=1" />
        <div id="card-suggestions-dropdown"></div>`;
      loadScript('site.js');
      globalThis.fetch = vi.fn().mockResolvedValue({ json: () => Promise.resolve(['Dark Magician']) });

      const input = document.querySelector('input[data-autocomplete-url]');
      input.value = 'dark';
      input.dispatchEvent(new Event('input'));

      await vi.advanceTimersByTimeAsync(300);

      expect(globalThis.fetch).toHaveBeenCalledWith(expect.stringContaining('q=dark'));
      expect(document.getElementById('card-suggestions-dropdown').querySelectorAll('[data-ti]').length).toBe(1);
    });

    it('typing a short query hides the dropdown without fetching', () => {
      document.body.innerHTML = `
        <input data-autocomplete-url="/api/browse/autocomplete?x=1" />
        <div id="card-suggestions-dropdown" style="display: block;"></div>`;
      loadScript('site.js');
      globalThis.fetch = vi.fn();

      const input = document.querySelector('input[data-autocomplete-url]');
      input.value = 'a';
      input.dispatchEvent(new Event('input'));

      expect(document.getElementById('card-suggestions-dropdown').style.display).toBe('none');
      expect(globalThis.fetch).not.toHaveBeenCalled();
    });
  });

  describe('initDatePickers', () => {
    it('calls flatpickr for each .cc-date-picker element not already initialized', () => {
      document.body.innerHTML = '<input class="cc-date-picker" id="a" /><input class="cc-date-picker" id="b" />';
      loadScript('site.js');

      expect(globalThis.flatpickr).toHaveBeenCalledTimes(2);
    });

    it('skips elements that already have a flatpickr instance attached', () => {
      document.body.innerHTML = '<input class="cc-date-picker" id="a" />';
      loadScript('site.js');
      globalThis.flatpickr.mockClear();
      document.getElementById('a')._flatpickr = {};

      initDatePickers();

      expect(globalThis.flatpickr).not.toHaveBeenCalled();
    });
  });

  describe('selectQuantity', () => {
    it('sets the hidden input value and marks only the clicked button active', () => {
      document.body.innerHTML = `
        <input type="hidden" id="qty" />
        <div id="group">
          <button data-qty-target="qty" data-qty-value="1" class="btn-primary"></button>
          <button data-qty-target="qty" data-qty-value="2" class="btn-outline-secondary"></button>
        </div>`;
      loadScript('site.js');
      const [btn1, btn2] = document.querySelectorAll('#group button');

      selectQuantity(btn2);

      expect(document.getElementById('qty').value).toBe('2');
      expect(btn2.classList.contains('btn-primary')).toBe(true);
      expect(btn2.classList.contains('btn-outline-secondary')).toBe(false);
      expect(btn1.classList.contains('btn-primary')).toBe(false);
      expect(btn1.classList.contains('btn-outline-secondary')).toBe(true);
    });
  });

  describe('set-name filter IIFE', () => {
    it('filters the local option list case-insensitively and caps results at 20', () => {
      const options = Array.from({ length: 25 }, (_, i) => `<option value="Set Alpha ${i}"></option>`).join('');
      document.body.innerHTML = `
        <input id="filter-set-input" />
        <div id="set-filter-dropdown"></div>
        <datalist id="set-filter-datalist">${options}</datalist>`;
      loadScript('site.js');

      const input = document.getElementById('filter-set-input');
      input.value = 'ALPHA';
      input.dispatchEvent(new Event('input'));

      expect(document.getElementById('set-filter-dropdown').querySelectorAll('[data-ti]').length).toBe(20);
    });

    it('missing set-filter elements is a no-op at load time', () => {
      document.body.innerHTML = '';
      expect(() => loadScript('site.js')).not.toThrow();
    });
  });

  describe('setPickerDate', () => {
    it('element with an attached flatpickr instance and a falsy value calls clear', () => {
      document.body.innerHTML = '<input id="picker" />';
      loadScript('site.js');
      const el = document.getElementById('picker');
      el._flatpickr = { setDate: vi.fn(), clear: vi.fn() };

      setPickerDate('picker', null);

      expect(el._flatpickr.clear).toHaveBeenCalled();
    });

    it('element with an attached flatpickr instance and a value calls setDate', () => {
      document.body.innerHTML = '<input id="picker" />';
      loadScript('site.js');
      const el = document.getElementById('picker');
      el._flatpickr = { setDate: vi.fn(), clear: vi.fn() };

      setPickerDate('picker', '2026-01-01');

      expect(el._flatpickr.setDate).toHaveBeenCalledWith('2026-01-01');
      expect(el._flatpickr.clear).not.toHaveBeenCalled();
    });

    it('element without flatpickr and a Date value formats as en-CA', () => {
      document.body.innerHTML = '<input id="picker" />';
      loadScript('site.js');

      setPickerDate('picker', new Date(2026, 0, 15));

      expect(document.getElementById('picker').value).toBe('2026-01-15');
    });

    it('element without flatpickr and a falsy value clears to empty string', () => {
      document.body.innerHTML = '<input id="picker" value="old" />';
      loadScript('site.js');

      setPickerDate('picker', null);

      expect(document.getElementById('picker').value).toBe('');
    });

    it('element without flatpickr and a string value assigns it directly', () => {
      document.body.innerHTML = '<input id="picker" />';
      loadScript('site.js');

      setPickerDate('picker', '2026-02-01');

      expect(document.getElementById('picker').value).toBe('2026-02-01');
    });

    it('missing element is a no-op', () => {
      document.body.innerHTML = '';
      loadScript('site.js');
      expect(() => setPickerDate('doesNotExist', '2026-01-01')).not.toThrow();
    });
  });

  describe('setQuantityButtons', () => {
    it('missing hidden element is a no-op', () => {
      document.body.innerHTML = '';
      loadScript('site.js');

      expect(() => setQuantityButtons('doesNotExist', 2)).not.toThrow();
    });

    it('sets hidden value and activates the matching button by value', () => {
      document.body.innerHTML = `
        <input type="hidden" id="qty" />
        <button data-qty-target="qty" data-qty-value="1"></button>
        <button data-qty-target="qty" data-qty-value="3"></button>`;
      loadScript('site.js');

      setQuantityButtons('qty', 3);

      expect(document.getElementById('qty').value).toBe('3');
      const [btn1, btn3] = document.querySelectorAll('button');
      expect(btn3.classList.contains('btn-primary')).toBe(true);
      expect(btn1.classList.contains('btn-primary')).toBe(false);
    });
  });

  describe('theme toggle', () => {
    it('clicking the toggle button flips data-bs-theme, persists to localStorage, and dispatches themechange', () => {
      document.body.innerHTML = '<button id="themeToggleBtn"></button>';
      document.documentElement.setAttribute('data-bs-theme', 'light');
      const setItemSpy = vi.spyOn(Storage.prototype, 'setItem');
      const dispatchSpy = vi.fn();
      document.addEventListener('themechange', dispatchSpy);
      loadScript('site.js');

      document.getElementById('themeToggleBtn').click();

      expect(document.documentElement.getAttribute('data-bs-theme')).toBe('dark');
      expect(setItemSpy).toHaveBeenCalledWith('cc-theme', 'dark');
      expect(dispatchSpy).toHaveBeenCalledTimes(1);
    });

    it('missing toggle button is a no-op at load time', () => {
      document.body.innerHTML = '';
      expect(() => loadScript('site.js')).not.toThrow();
    });
  });

  describe('updateCartBadge', () => {
    it('coerces string count/total (as from response headers) and shows the badge', () => {
      setupBadge();
      updateCartBadge('3', '12.5');

      const badge = document.getElementById('navCartBadge');
      expect(document.getElementById('navCartCount').textContent).toBe('3');
      expect(badge.title).toBe('$12.50 staged');
      expect(badge.classList.contains('d-none')).toBe(false);
    });

    it('count of zero hides the badge and clears the title', () => {
      setupBadge();
      updateCartBadge(0, 0);

      const badge = document.getElementById('navCartBadge');
      expect(badge.classList.contains('d-none')).toBe(true);
      expect(badge.title).toBe('');
    });

    it('missing badge element is a no-op and does not throw', () => {
      document.body.innerHTML = '';
      loadScript('site.js');
      expect(() => updateCartBadge(3, 10)).not.toThrow();
    });

    it('non-numeric total coerces to zero', () => {
      setupBadge();
      updateCartBadge(2, 'not-a-number');

      expect(document.getElementById('navCartBadge').title).toBe('');
    });

    it('null count is a no-op', () => {
      setupBadge();
      updateCartBadge(null, 10);
      expect(document.getElementById('navCartCount').textContent).toBe('');
    });

    it('undefined count is a no-op', () => {
      setupBadge();
      updateCartBadge(undefined, 10);
      expect(document.getElementById('navCartCount').textContent).toBe('');
    });

    function setupBadge() {
      document.body.innerHTML = `
        <span id="navCartBadge" class="d-none"><span id="navCartCount"></span></span>`;
      loadScript('site.js');
    }
  });
});
