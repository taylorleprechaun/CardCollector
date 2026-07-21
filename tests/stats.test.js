import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

// stats.js reads these as page-injected globals (normally emitted by an inline <script> block
// in Stats.cshtml before stats.js loads) and uses several of them in top-level statements that
// run immediately on load, so every test must define them before calling loadScript('stats.js').
function setPageGlobals(overrides = {}) {
  Object.assign(globalThis, {
    setLabels: [],
    setCounts: [],
    setValueLabels: [],
    setValueData: [],
    historyDates: [],
    historyValues: [],
    historyCardCounts: [],
    ...overrides
  });
}

describe('stats.js', () => {
  beforeEach(() => {
    document.documentElement.removeAttribute('data-bs-theme');
  });

  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
    for (const key of ['setLabels', 'setCounts', 'setValueLabels', 'setValueData', 'historyDates', 'historyValues', 'historyCardCounts', 'trackedCardImageMap']) {
      delete globalThis[key];
    }
  });

  describe('applyChartTheme', () => {
    it('sets Chart.defaults color and border color from CSS custom properties', () => {
      setPageGlobals();
      loadScript('stats.js');

      expect(typeof globalThis.Chart.defaults.color).toBe('string');
      expect(typeof globalThis.Chart.defaults.borderColor).toBe('string');
    });
  });

  describe('buildHorizontalBar / buildValueBar', () => {
    it('buildValueBar formats x-axis ticks as currency', () => {
      document.body.innerHTML = '<canvas id="setValueChart"></canvas>';
      setPageGlobals();
      loadScript('stats.js');

      const chart = buildValueBar('setValueChart', ['LOB'], [12.5]);

      expect(chart.config.options.scales.x.ticks.callback(5)).toBe('$5.00');
    });

    it('missing canvas element returns null without constructing a Chart', () => {
      document.body.innerHTML = '';
      setPageGlobals();
      loadScript('stats.js');
      globalThis.Chart.mockClear();

      const result = buildHorizontalBar('doesNotExist', [], []);

      expect(result).toBeNull();
      expect(globalThis.Chart).not.toHaveBeenCalled();
    });

    it('present canvas constructs a bar Chart with the given labels/data', () => {
      document.body.innerHTML = '<canvas id="setChart"></canvas>';
      setPageGlobals();
      loadScript('stats.js');
      globalThis.Chart.mockClear();

      const chart = buildHorizontalBar('setChart', ['LOB'], [5]);

      expect(chart).not.toBeNull();
      expect(chart.config.type).toBe('bar');
      expect(chart.config.data.labels).toEqual(['LOB']);
    });
  });

  describe('buildPriceHistoryChart', () => {
    it('missing canvas or wrapper is a no-op', () => {
      document.body.innerHTML = '';
      setPageGlobals();
      loadScript('stats.js');

      expect(() => buildPriceHistoryChart([{ label: 'A', dates: [], values: [] }])).not.toThrow();
    });

    it('single series hides the legend; multiple series shows it', () => {
      document.body.innerHTML = '<canvas id="cardHistoryChart"></canvas><div id="cardHistoryChartWrapper" style="display: none;"></div>';
      setPageGlobals();
      loadScript('stats.js');

      buildPriceHistoryChart([{ label: 'A', dates: ['2026-01-01'], values: [1] }]);
      expect(document.getElementById('cardHistoryChartWrapper').style.display).toBe('');
    });
  });

  describe('buildValueChart', () => {
    it('missing canvas is a no-op', () => {
      document.body.innerHTML = '';
      setPageGlobals();
      loadScript('stats.js');

      expect(() => buildValueChart([], [], [])).not.toThrow();
    });

    it('updates the existing chart in place on a second call', () => {
      document.body.innerHTML = '<canvas id="valueChart"></canvas>';
      setPageGlobals();
      loadScript('stats.js');
      buildValueChart(['2026-01-01'], [10], [5]);
      globalThis.Chart.mockClear();

      buildValueChart(['2026-01-02'], [20], [6]);

      expect(globalThis.Chart).not.toHaveBeenCalled();
    });
  });

  describe('getChartColors / chartColorAt', () => {
    it('cycles via modulo past the end of the palette', () => {
      setPageGlobals();
      loadScript('stats.js');

      expect(chartColorAt(8)).toBe(chartColorAt(0));
    });

    it('returns the dark palette when data-bs-theme is dark', () => {
      document.documentElement.setAttribute('data-bs-theme', 'dark');
      setPageGlobals();
      loadScript('stats.js');

      expect(chartColorAt(0)).toBe('#3987e5');
    });

    it('returns the light palette by default', () => {
      setPageGlobals();
      loadScript('stats.js');

      expect(chartColorAt(0)).toBe('#2a78d6');
    });
  });

  describe('hexToRgba', () => {
    it('converts a hex color and alpha into an rgba() string', () => {
      setPageGlobals();
      loadScript('stats.js');

      expect(hexToRgba('#2a78d6', 0.1)).toBe('rgba(42,120,214,0.1)');
    });
  });

  describe('loadCardPriceHistory', () => {
    it('blank input is a no-op that does not call fetch', async () => {
      setupInputs();
      document.getElementById('cardHistoryInput').value = '   ';
      globalThis.fetch = vi.fn();

      await loadCardPriceHistory();

      expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('empty series shows a not-found message', async () => {
      setupInputs();
      document.getElementById('cardHistoryInput').value = 'Dark Magician';
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve([]) });

      await loadCardPriceHistory();

      expect(document.getElementById('cardHistoryStatus').textContent).toBe('No price history found for this card.');
    });

    it('fetch throwing is caught and shows a failure message', async () => {
      setupInputs();
      document.getElementById('cardHistoryInput').value = 'Dark Magician';
      globalThis.fetch = vi.fn().mockRejectedValue(new Error('offline'));

      await loadCardPriceHistory();

      expect(document.getElementById('cardHistoryStatus').textContent).toBe('Failed to load price history.');
    });

    it('non-ok response shows a failure message', async () => {
      setupInputs();
      document.getElementById('cardHistoryInput').value = 'Dark Magician';
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: false });

      await loadCardPriceHistory();

      expect(document.getElementById('cardHistoryStatus').textContent).toBe('Failed to load price history.');
    });

    it('successful response with data builds the chart and hides the status message', async () => {
      setupInputs();
      document.getElementById('cardHistoryInput').value = 'Dark Magician';
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve([{ label: 'LOB — Ultra Rare', dates: ['2026-01-01'], values: [10] }])
      });

      await loadCardPriceHistory();

      expect(document.getElementById('cardHistoryStatus').style.display).toBe('none');
      expect(document.getElementById('cardHistoryChartWrapper').style.display).toBe('');
    });

    function setupInputs() {
      document.body.innerHTML = `
        <input id="cardHistoryInput" />
        <span id="cardHistoryStatus"></span>
        <div id="cardHistoryChartWrapper"></div>
        <canvas id="cardHistoryChart"></canvas>`;
      setPageGlobals();
      loadScript('stats.js');
    }
  });

  describe('medalBadge', () => {
    it('returns a medal badge for the first three ranks', () => {
      setPageGlobals();
      loadScript('stats.js');

      expect(medalBadge(0)).toContain('medal-badge-gold');
      expect(medalBadge(1)).toContain('medal-badge-silver');
      expect(medalBadge(2)).toContain('medal-badge-bronze');
    });

    it('returns a plain rank number from the fourth place onward', () => {
      setPageGlobals();
      loadScript('stats.js');

      const result = medalBadge(3);
      expect(result).toContain('text-muted');
      expect(result).toContain('4');
    });
  });

  describe('refreshCardData', () => {
    it('an error event after completion is ignored', () => {
      setupRefreshUi();
      vi.spyOn(globalThis, 'confirm').mockReturnValue(true);
      let capturedSource;
      globalThis.EventSource = class extends FakeEventSource {
        constructor(url) {
          super(url);
          capturedSource = this;
        }
      };

      refreshCardData();
      capturedSource.listeners.complete({ data: JSON.stringify({ cardCount: 1 }) });
      document.getElementById('cardDataError').textContent = 'untouched';
      capturedSource.listeners.error();

      expect(document.getElementById('cardDataError').textContent).toBe('untouched');
    });

    it('an error event before completion shows the error message', () => {
      setupRefreshUi();
      vi.spyOn(globalThis, 'confirm').mockReturnValue(true);
      let capturedSource;
      globalThis.EventSource = class extends FakeEventSource {
        constructor(url) {
          super(url);
          capturedSource = this;
        }
      };

      refreshCardData();
      capturedSource.listeners.error();

      expect(document.getElementById('cardDataError').style.display).toBe('');
      expect(document.getElementById('refreshCardDataBtn').disabled).toBe(false);
    });

    it('cancelling the confirm dialog does not disable the button or open a stream', () => {
      setupRefreshUi();
      vi.spyOn(globalThis, 'confirm').mockReturnValue(false);

      refreshCardData();

      expect(document.getElementById('refreshCardDataBtn').disabled).toBe(false);
    });

    it('confirming disables the button and shows the progress UI', () => {
      setupRefreshUi();
      vi.spyOn(globalThis, 'confirm').mockReturnValue(true);

      refreshCardData();

      const btn = document.getElementById('refreshCardDataBtn');
      expect(btn.disabled).toBe(true);
      expect(document.getElementById('cardDataProgress').style.display).toBe('');
    });

    it('the complete event re-enables the button and shows the result message', () => {
      setupRefreshUi();
      vi.spyOn(globalThis, 'confirm').mockReturnValue(true);
      let capturedSource;
      const OriginalFakeEventSource = FakeEventSource;
      globalThis.EventSource = class extends OriginalFakeEventSource {
        constructor(url) {
          super(url);
          capturedSource = this;
        }
      };

      refreshCardData();
      capturedSource.listeners.complete({ data: JSON.stringify({ cardCount: 12345 }) });

      expect(document.getElementById('refreshCardDataBtn').disabled).toBe(false);
      expect(document.getElementById('cardDataResult').textContent).toBe('Card data updated: 12,345 cards loaded.');
    });

    class FakeEventSource {
      constructor(url) {
        this.url = url;
        this.listeners = {};
        this.close = vi.fn();
      }
      addEventListener(type, handler) {
        this.listeners[type] = handler;
      }
    }

    function setupRefreshUi() {
      document.body.innerHTML = `
        <button id="refreshCardDataBtn"></button>
        <div id="cardDataStatus"></div>
        <div id="cardDataProgress"></div>
        <div id="cardDataResult"></div>
        <div id="cardDataError"></div>`;
      setPageGlobals();
      globalThis.EventSource = FakeEventSource;
      loadScript('stats.js');
    }
  });

  describe('themechange listener', () => {
    it('does nothing to charts that were never created', () => {
      document.body.innerHTML = '';
      setPageGlobals();
      loadScript('stats.js');

      expect(() => document.dispatchEvent(new CustomEvent('themechange'))).not.toThrow();
    });

    it('rebuilds an existing card-history chart when a previous series was loaded', async () => {
      document.body.innerHTML = `
        <input id="cardHistoryInput" /><span id="cardHistoryStatus"></span>
        <div id="cardHistoryChartWrapper"></div><canvas id="cardHistoryChart"></canvas>`;
      setPageGlobals();
      loadScript('stats.js');
      document.getElementById('cardHistoryInput').value = 'Dark Magician';
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve([{ label: 'LOB — Ultra Rare', dates: ['2026-01-01'], values: [10] }])
      });
      await loadCardPriceHistory();
      globalThis.Chart.mockClear();

      document.dispatchEvent(new CustomEvent('themechange'));

      expect(globalThis.Chart).toHaveBeenCalled();
    });

    it('rebuilds an existing set chart by destroying and reconstructing it', () => {
      document.body.innerHTML = '<canvas id="setChart"></canvas>';
      setPageGlobals({ setLabels: ['LOB'], setCounts: [3] });
      loadScript('stats.js');
      globalThis.Chart.mockClear();

      document.dispatchEvent(new CustomEvent('themechange'));

      expect(globalThis.Chart).toHaveBeenCalled();
    });

    it('rebuilds an existing value-history chart by destroying and reconstructing it', () => {
      document.body.innerHTML = '<canvas id="valueChart"></canvas>';
      setPageGlobals({ historyDates: ['2026-01-01'], historyValues: [10], historyCardCounts: [5] });
      loadScript('stats.js');
      globalThis.Chart.mockClear();

      document.dispatchEvent(new CustomEvent('themechange'));

      expect(globalThis.Chart).toHaveBeenCalled();
    });
  });

  describe('top-level initialization', () => {
    it('builds the set chart immediately when setLabels is non-empty', () => {
      document.body.innerHTML = '<canvas id="setChart"></canvas>';
      setPageGlobals({ setLabels: ['LOB'], setCounts: [3] });

      loadScript('stats.js');

      expect(globalThis.Chart).toHaveBeenCalled();
    });

    it('builds the value-history chart immediately when historyDates is non-empty', () => {
      document.body.innerHTML = '<canvas id="valueChart" style="display: none;"></canvas>';
      setPageGlobals({ historyDates: ['2026-01-01'], historyValues: [10], historyCardCounts: [5] });

      loadScript('stats.js');

      expect(document.getElementById('valueChart').style.display).toBe('');
    });

    it('does not build the set chart when setLabels is empty', () => {
      document.body.innerHTML = '<canvas id="setChart"></canvas>';
      setPageGlobals();

      loadScript('stats.js');

      expect(globalThis.Chart).not.toHaveBeenCalled();
    });
  });

  describe('updateSetValueChart', () => {
    it('creates the chart on first call and reveals its wrapper', () => {
      document.body.innerHTML = `
        <canvas id="setValueChart"></canvas>
        <div id="setValueChartWrapper" style="display: none;"></div>
        <div id="noSetValueMsg"></div>`;
      setPageGlobals();
      loadScript('stats.js');

      updateSetValueChart(['LOB'], [10]);

      expect(document.getElementById('setValueChartWrapper').style.display).toBe('');
    });

    it('updates the existing chart in place on subsequent calls instead of recreating it', () => {
      document.body.innerHTML = `
        <canvas id="setValueChart"></canvas>
        <div id="setValueChartWrapper"></div><div id="noSetValueMsg"></div>`;
      setPageGlobals();
      loadScript('stats.js');
      updateSetValueChart(['LOB'], [10]);
      globalThis.Chart.mockClear();

      updateSetValueChart(['LOB', 'SDK'], [10, 20]);

      expect(globalThis.Chart).not.toHaveBeenCalled();
    });
  });

  describe('updateTopCards', () => {
    it('missing wrapper or tbody is a no-op', () => {
      document.body.innerHTML = '';
      setPageGlobals();
      loadScript('stats.js');

      expect(() => updateTopCards([])).not.toThrow();
    });

    it('renders one row per card and reveals the wrapper', () => {
      document.body.innerHTML = `
        <div id="topCardsWrapper" style="display: none;"></div>
        <div id="noTopCardsMsg"></div>
        <table><tbody id="topCardsBody"></tbody></table>`;
      setPageGlobals();
      loadScript('stats.js');

      updateTopCards([{ cardName: 'Dark Magician', setName: 'Legend', rarityName: 'Ultra Rare', value: 12.5 }]);

      const body = document.getElementById('topCardsBody');
      expect(body.querySelectorAll('tr').length).toBe(1);
      expect(body.textContent).toContain('Dark Magician');
      expect(document.getElementById('topCardsWrapper').style.display).toBe('');
    });
  });
});
