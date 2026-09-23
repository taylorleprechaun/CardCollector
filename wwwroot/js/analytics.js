// Same validated palette as stats.js: bars take the first color, the event-count line the second.
const ANALYTICS_COLORS_LIGHT = ['#2a78d6', '#1baf7a'];
const ANALYTICS_COLORS_DARK = ['#3987e5', '#199e70'];

let analyticsTrendChart = null;

function getAnalyticsColors() {
    return document.documentElement.getAttribute('data-bs-theme') === 'dark' ? ANALYTICS_COLORS_DARK : ANALYTICS_COLORS_LIGHT;
}

function applyAnalyticsChartTheme() {
    const styles = getComputedStyle(document.documentElement);
    Chart.defaults.color = styles.getPropertyValue('--bs-body-color').trim();
    Chart.defaults.borderColor = styles.getPropertyValue('--bs-border-color-translucent').trim();
}

// Turns the by-format rows ({ label, events, winRate }) into chart series. Win rate becomes a percentage
// rounded to one decimal; a format with no matches has no bar (null) rather than a 0% bar.
function shapeTrendData(points) {
    const rows = Array.isArray(points) ? points : [];
    return {
        labels: rows.map(p => p.label),
        events: rows.map(p => p.events),
        winRates: rows.map(p => (p.winRate === null || p.winRate === undefined) ? null : Math.round(p.winRate * 1000) / 10)
    };
}

function readTrendData(canvas) {
    try {
        return JSON.parse(canvas.dataset.trend || '[]');
    } catch {
        return [];
    }
}

function buildTrendChart() {
    const canvas = document.getElementById('analyticsTrendChart');
    if (!canvas) return null;

    const trend = shapeTrendData(readTrendData(canvas));
    if (trend.labels.length === 0) return null;

    const [barColor, lineColor] = getAnalyticsColors();
    return new Chart(canvas, {
        type: 'bar',
        data: {
            labels: trend.labels,
            datasets: [
                {
                    label: 'Match win %',
                    data: trend.winRates,
                    backgroundColor: barColor,
                    borderWidth: 0,
                    yAxisID: 'y',
                    order: 2
                },
                {
                    label: 'Events',
                    type: 'line',
                    data: trend.events,
                    borderColor: lineColor,
                    backgroundColor: lineColor,
                    pointRadius: 3,
                    tension: 0.2,
                    yAxisID: 'y1',
                    order: 1
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                tooltip: {
                    callbacks: {
                        label: context => context.dataset.yAxisID === 'y'
                            ? `Match win %: ${context.parsed.y === null ? '—' : context.parsed.y + '%'}`
                            : `Events: ${context.parsed.y}`
                    }
                }
            },
            scales: {
                y: { beginAtZero: true, max: 100, ticks: { callback: value => value + '%' } },
                y1: { beginAtZero: true, position: 'right', grid: { drawOnChartArea: false }, ticks: { precision: 0 } },
                x: { ticks: { autoSkip: true, maxRotation: 60 } }
            }
        }
    });
}

function cellSortValue(row, columnIndex) {
    const cell = row.cells[columnIndex];
    if (!cell) return '';
    return cell.dataset.sortValue !== undefined ? cell.dataset.sortValue : cell.textContent.trim();
}

// Sorts the table body only; the totals row lives in <tfoot> so it stays last.
function sortTable(table, columnIndex, type, direction) {
    const body = table.tBodies[0];
    if (!body) return;

    const factor = direction === 'ascending' ? 1 : -1;
    const rows = Array.from(body.rows);
    rows.sort((a, b) => {
        const left = cellSortValue(a, columnIndex);
        const right = cellSortValue(b, columnIndex);
        const compared = type === 'number'
            ? (parseFloat(left) || 0) - (parseFloat(right) || 0)
            : left.localeCompare(right, undefined, { sensitivity: 'base' });
        return compared * factor;
    });
    rows.forEach(row => body.appendChild(row));

    table.querySelectorAll('thead th').forEach(th => th.removeAttribute('aria-sort'));
    const header = table.tHead?.rows[0]?.cells[columnIndex];
    if (header) header.setAttribute('aria-sort', direction);
}

// Text columns sort A-Z first; number columns sort highest first. Clicking the same column again reverses it.
function nextSortDirection(header, type) {
    const current = header.getAttribute('aria-sort');
    if (current === 'ascending') return 'descending';
    if (current === 'descending') return 'ascending';
    return type === 'number' ? 'descending' : 'ascending';
}

function initSortableTables(root = document) {
    root.querySelectorAll('table[data-sortable-table]').forEach(table => {
        table.querySelectorAll('thead .cc-sort-button').forEach(button => {
            button.addEventListener('click', () => {
                const header = button.closest('th');
                const type = button.dataset.sort || 'text';
                sortTable(table, header.cellIndex, type, nextSortDirection(header, type));
            });
        });
    });
}

function showTabFromHash() {
    const id = window.location.hash.replace('#', '');
    if (!id) return false;

    const button = document.querySelector(`#analyticsTabs [data-bs-target="#${CSS.escape(id)}"]`);
    if (!button) return false;

    bootstrap.Tab.getOrCreateInstance(button).show();
    return true;
}

// Keeps the open tab in the URL hash, so refreshing or applying filters returns to it.
function initAnalyticsTabs() {
    const tabs = document.getElementById('analyticsTabs');
    if (!tabs) return;

    showTabFromHash();

    tabs.querySelectorAll('[data-bs-toggle="tab"]').forEach(button => {
        button.addEventListener('shown.bs.tab', () => {
            history.replaceState(null, '', button.dataset.bsTarget);
        });
    });

    const form = document.getElementById('analyticsFilterForm');
    if (form) {
        form.addEventListener('submit', () => {
            form.action = window.location.pathname + window.location.hash;
        });
    }
}

applyAnalyticsChartTheme();
analyticsTrendChart = buildTrendChart();
initSortableTables();
initAnalyticsTabs();

document.addEventListener('themechange', function () {
    applyAnalyticsChartTheme();
    if (!analyticsTrendChart) return;

    analyticsTrendChart.destroy();
    analyticsTrendChart = buildTrendChart();
});
