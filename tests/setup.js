import { beforeEach, vi } from 'vitest';

// jsdom doesn't implement these — polyfill with the minimum real behavior the scripts rely on.
if (typeof globalThis.CSS === 'undefined') {
  globalThis.CSS = {};
}
if (!globalThis.CSS.escape) {
  globalThis.CSS.escape = (value) => String(value).replace(/[^a-zA-Z0-9_-]/g, (ch) => `\\${ch}`);
}
if (!Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = () => {};
}

// External libraries not present in jsdom. Re-stubbed fresh before every test so assertions
// on call counts (e.g. `Chart` constructor calls) don't leak between tests.
beforeEach(() => {
  globalThis.flatpickr = vi.fn(() => ({}));

  globalThis.Chart = vi.fn().mockImplementation(function (ctx, config) {
    this.ctx = ctx;
    this.config = config;
    this.data = config?.data ?? {};
    this.options = config?.options ?? {};
    this.update = vi.fn();
    this.destroy = vi.fn();
  });
  globalThis.Chart.defaults = { color: '', borderColor: '' };

  const modalInstances = new Map();
  globalThis.bootstrap = {
    Modal: vi.fn().mockImplementation(function (element) {
      this.element = element;
      this.show = vi.fn();
      this.hide = vi.fn();
      modalInstances.set(element, this);
    })
  };
  globalThis.bootstrap.Modal.getInstance = vi.fn((element) => modalInstances.get(element));

  if (!navigator.clipboard) {
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: vi.fn().mockResolvedValue(undefined) },
      configurable: true
    });
  } else {
    navigator.clipboard.writeText = vi.fn().mockResolvedValue(undefined);
  }
});
