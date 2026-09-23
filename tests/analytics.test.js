import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

function tableHtml() {
  return `
    <table data-sortable-table>
      <thead>
        <tr>
          <th><button type="button" class="cc-sort-button" data-sort="text">Deck</button></th>
          <th><button type="button" class="cc-sort-button" data-sort="number">Events</button></th>
          <th><button type="button" class="cc-sort-button" data-sort="number">Win %</button></th>
        </tr>
      </thead>
      <tbody>
        <tr><td>beta deck</td><td>2</td><td data-sort-value="0.5">50.0%</td></tr>
        <tr><td>Alpha Deck</td><td>10</td><td data-sort-value="-1">—</td></tr>
        <tr><td>Gamma Deck</td><td>1</td><td data-sort-value="0.75">75.0%</td></tr>
      </tbody>
      <tfoot><tr><th>Total</th><td>13</td><td>60.0%</td></tr></tfoot>
    </table>`;
}

function trendCanvasHtml(points) {
  const json = JSON.stringify(points).replace(/"/g, '&quot;');
  return `<canvas id="analyticsTrendChart" data-trend="${json}"></canvas>`;
}

function tabsHtml() {
  return `
    <form id="analyticsFilterForm" action="/Tournaments/Analytics"></form>
    <ul id="analyticsTabs">
      <li><button type="button" id="tab-deck-format" data-bs-toggle="tab" data-bs-target="#deck-format">Deck × Format</button></li>
      <li><button type="button" id="tab-matchups" data-bs-toggle="tab" data-bs-target="#matchups">Matchups</button></li>
    </ul>`;
}

function firstColumn(table) {
  return Array.from(table.tBodies[0].rows).map(row => row.cells[0].textContent);
}

describe('analytics.js', () => {
  let tabShow;

  beforeEach(() => {
    document.documentElement.removeAttribute('data-bs-theme');
    tabShow = vi.fn();
    globalThis.bootstrap.Tab = { getOrCreateInstance: vi.fn(() => ({ show: tabShow })) };
    history.replaceState(null, '', '/Tournaments/Analytics');
  });

  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  describe('shapeTrendData', () => {
    it('turns win rates into one-decimal percentages and keeps missing ones as null', () => {
      loadScript('analytics.js');

      const shaped = shapeTrendData([
        { label: 'Early Format', events: 3, winRate: 0.640995 },
        { label: 'Late Format', events: 1, winRate: null }
      ]);

      expect(shaped.labels).toEqual(['Early Format', 'Late Format']);
      expect(shaped.events).toEqual([3, 1]);
      expect(shaped.winRates).toEqual([64.1, null]);
    });

    it('returns empty series for anything that is not an array', () => {
      loadScript('analytics.js');

      expect(shapeTrendData(null)).toEqual({ labels: [], events: [], winRates: [] });
    });
  });

  describe('buildTrendChart', () => {
    it('builds a bar chart with an events line on a second axis', () => {
      document.body.innerHTML = trendCanvasHtml([{ label: 'Early Format', events: 2, winRate: 0.5 }]);
      loadScript('analytics.js');

      const chart = analyticsTrendChart;

      expect(chart).not.toBeNull();
      expect(chart.config.type).toBe('bar');
      expect(chart.config.data.labels).toEqual(['Early Format']);
      expect(chart.config.data.datasets[0].data).toEqual([50]);
      expect(chart.config.data.datasets[1].type).toBe('line');
      expect(chart.config.data.datasets[1].yAxisID).toBe('y1');
      expect(chart.config.options.scales.y.ticks.callback(40)).toBe('40%');
    });

    it('formats tooltips for both series, with a dash for a format without matches', () => {
      document.body.innerHTML = trendCanvasHtml([{ label: 'Early Format', events: 2, winRate: 0.5 }]);
      loadScript('analytics.js');
      const label = analyticsTrendChart.config.options.plugins.tooltip.callbacks.label;

      expect(label({ dataset: { yAxisID: 'y' }, parsed: { y: 62.5 } })).toBe('Match win %: 62.5%');
      expect(label({ dataset: { yAxisID: 'y' }, parsed: { y: null } })).toBe('Match win %: —');
      expect(label({ dataset: { yAxisID: 'y1' }, parsed: { y: 4 } })).toBe('Events: 4');
    });

    it('does not build a chart when there is no canvas (empty state)', () => {
      loadScript('analytics.js');

      expect(analyticsTrendChart).toBeNull();
      expect(globalThis.Chart).not.toHaveBeenCalled();
    });

    it('does not build a chart when the trend data is empty or unreadable', () => {
      document.body.innerHTML = '<canvas id="analyticsTrendChart" data-trend="not json"></canvas>';
      loadScript('analytics.js');

      expect(analyticsTrendChart).toBeNull();
      expect(globalThis.Chart).not.toHaveBeenCalled();
    });

    it('uses the dark palette in dark mode', () => {
      document.documentElement.setAttribute('data-bs-theme', 'dark');
      document.body.innerHTML = trendCanvasHtml([{ label: 'Early Format', events: 2, winRate: 0.5 }]);
      loadScript('analytics.js');

      expect(analyticsTrendChart.config.data.datasets[0].backgroundColor).toBe(ANALYTICS_COLORS_DARK[0]);
    });

    it('rebuilds the chart when the theme changes', () => {
      document.body.innerHTML = trendCanvasHtml([{ label: 'Early Format', events: 2, winRate: 0.5 }]);
      loadScript('analytics.js');
      const original = analyticsTrendChart;

      document.documentElement.setAttribute('data-bs-theme', 'dark');
      document.dispatchEvent(new CustomEvent('themechange', { detail: { theme: 'dark' } }));

      expect(original.destroy).toHaveBeenCalled();
      expect(analyticsTrendChart).not.toBe(original);
      expect(analyticsTrendChart.config.data.datasets[0].backgroundColor).toBe(ANALYTICS_COLORS_DARK[0]);
    });
  });

  describe('sortable tables', () => {
    it('sorts a text column A to Z first, ignoring case, then reverses on a second click', () => {
      document.body.innerHTML = tableHtml();
      loadScript('analytics.js');
      const table = document.querySelector('table');
      const button = table.querySelectorAll('.cc-sort-button')[0];

      button.click();
      expect(firstColumn(table)).toEqual(['Alpha Deck', 'beta deck', 'Gamma Deck']);
      expect(button.closest('th').getAttribute('aria-sort')).toBe('ascending');

      button.click();
      expect(firstColumn(table)).toEqual(['Gamma Deck', 'beta deck', 'Alpha Deck']);
      expect(button.closest('th').getAttribute('aria-sort')).toBe('descending');
    });

    it('sorts a number column highest first, using the sort value when there is one', () => {
      document.body.innerHTML = tableHtml();
      loadScript('analytics.js');
      const table = document.querySelector('table');

      table.querySelectorAll('.cc-sort-button')[2].click();

      expect(firstColumn(table)).toEqual(['Gamma Deck', 'beta deck', 'Alpha Deck']);
    });

    it('sorts plain numeric text numerically, not alphabetically', () => {
      document.body.innerHTML = tableHtml();
      loadScript('analytics.js');
      const table = document.querySelector('table');

      table.querySelectorAll('.cc-sort-button')[1].click();

      expect(firstColumn(table)).toEqual(['Alpha Deck', 'beta deck', 'Gamma Deck']);
    });

    it('keeps the totals row last and clears the previous column\'s sort marker', () => {
      document.body.innerHTML = tableHtml();
      loadScript('analytics.js');
      const table = document.querySelector('table');
      const [deckButton, eventsButton] = table.querySelectorAll('.cc-sort-button');

      deckButton.click();
      eventsButton.click();

      expect(table.rows[table.rows.length - 1].cells[0].textContent).toBe('Total');
      expect(deckButton.closest('th').hasAttribute('aria-sort')).toBe(false);
      expect(eventsButton.closest('th').getAttribute('aria-sort')).toBe('descending');
    });

    it('ignores a table without a body', () => {
      document.body.innerHTML = '<table><thead><tr><th>Deck</th></tr></thead></table>';
      loadScript('analytics.js');

      expect(() => sortTable(document.querySelector('table'), 0, 'text', 'ascending')).not.toThrow();
    });
  });

  describe('tabs', () => {
    it('opens the tab named in the URL hash', () => {
      history.replaceState(null, '', '/Tournaments/Analytics#matchups');
      document.body.innerHTML = tabsHtml();
      loadScript('analytics.js');

      expect(globalThis.bootstrap.Tab.getOrCreateInstance).toHaveBeenCalledWith(document.getElementById('tab-matchups'));
      expect(tabShow).toHaveBeenCalled();
    });

    it('leaves the default tab alone when the hash is missing or unknown', () => {
      history.replaceState(null, '', '/Tournaments/Analytics#nope');
      document.body.innerHTML = tabsHtml();
      loadScript('analytics.js');

      expect(tabShow).not.toHaveBeenCalled();
      expect(showTabFromHash()).toBe(false);

      history.replaceState(null, '', '/Tournaments/Analytics');
      expect(showTabFromHash()).toBe(false);
    });

    it('writes the shown tab into the URL hash', () => {
      document.body.innerHTML = tabsHtml();
      loadScript('analytics.js');

      document.getElementById('tab-matchups').dispatchEvent(new Event('shown.bs.tab'));

      expect(window.location.hash).toBe('#matchups');
    });

    it('keeps the open tab when the filter form is submitted', () => {
      history.replaceState(null, '', '/Tournaments/Analytics#matchups');
      document.body.innerHTML = tabsHtml();
      loadScript('analytics.js');
      const form = document.getElementById('analyticsFilterForm');

      form.dispatchEvent(new Event('submit'));

      expect(form.getAttribute('action')).toBe('/Tournaments/Analytics#matchups');
    });

    it('does nothing when the page has no tabs (empty state)', () => {
      loadScript('analytics.js');

      expect(globalThis.bootstrap.Tab.getOrCreateInstance).not.toHaveBeenCalled();
    });
  });
});
