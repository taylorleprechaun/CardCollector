const CHART_COLORS = [
    '#4e79a7', '#f28e2b', '#e15759', '#76b7b2', '#59a14f',
    '#edc948', '#b07aa1', '#ff9da7', '#9c755f', '#bab0ac',
    '#499894', '#86bcb6', '#e15759', '#ff9da7', '#79706e',
    '#d37295', '#fabfd2', '#b6992d', '#f1ce63', '#a0cbe8'
];

function applyChartTheme() {
    const styles = getComputedStyle(document.documentElement);
    Chart.defaults.color = styles.getPropertyValue('--bs-body-color').trim();
    Chart.defaults.borderColor = styles.getPropertyValue('--bs-border-color-translucent').trim();
}
applyChartTheme();

function buildHorizontalBar(id, labels, data) {
    const ctx = document.getElementById(id);
    if (!ctx) return null;
    return new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                data: data,
                backgroundColor: CHART_COLORS.slice(0, data.length),
                borderWidth: 0
            }]
        },
        options: {
            indexAxis: 'y',
            plugins: { legend: { display: false } },
            scales: {
                x: { beginAtZero: true, ticks: { precision: 0 } },
                y: { ticks: { font: { size: 11 }, autoSkip: false } }
            },
            responsive: true,
            maintainAspectRatio: false
        }
    });
}

function buildValueBar(id, labels, data) {
    const ctx = document.getElementById(id);
    if (!ctx) return null;
    return new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                data: data,
                backgroundColor: CHART_COLORS.slice(0, data.length),
                borderWidth: 0
            }]
        },
        options: {
            indexAxis: 'y',
            plugins: { legend: { display: false } },
            scales: {
                x: {
                    beginAtZero: true,
                    ticks: { callback: val => '$' + val.toFixed(2) }
                },
                y: { ticks: { font: { size: 11 }, autoSkip: false } }
            },
            responsive: true,
            maintainAspectRatio: false
        }
    });
}

let setChart = null;
let setValueChart = null;
let valueChart = null;
let lastSetValueData = null;

function updateSetValueChart(labels, data) {
    lastSetValueData = { labels, data };
    if (setValueChart) {
        setValueChart.data.labels = labels;
        setValueChart.data.datasets[0].data = data;
        setValueChart.update();
        return;
    }
    const wrapper = document.getElementById('setValueChartWrapper');
    if (wrapper) wrapper.style.display = '';
    const noMsg = document.getElementById('noSetValueMsg');
    if (noMsg) noMsg.style.display = 'none';
    setValueChart = buildValueBar('setValueChart', labels, data);
}

function buildValueChart(dates, values, cardCounts) {
    const ctx = document.getElementById('valueChart');
    if (!ctx) return;

    ctx.style.display = '';
    const noMsg = document.getElementById('noHistoryMsg');
    if (noMsg) noMsg.style.display = 'none';

    const valuePoints = dates.map((d, i) => ({ x: d, y: values[i] }));
    const countPoints = dates.map((d, i) => ({ x: d, y: cardCounts[i] }));

    if (valueChart) {
        valueChart.data.datasets[0].data = valuePoints;
        valueChart.data.datasets[1].data = countPoints;
        valueChart.update();
        return;
    }

    valueChart = new Chart(ctx, {
        type: 'line',
        data: {
            datasets: [
                {
                    label: 'Market Value (USD)',
                    data: valuePoints,
                    yAxisID: 'y',
                    fill: true,
                    backgroundColor: 'rgba(78,121,167,0.15)',
                    borderColor: '#4e79a7',
                    pointRadius: 4,
                    tension: 0.3
                },
                {
                    label: 'Cards Owned',
                    data: countPoints,
                    yAxisID: 'y2',
                    fill: false,
                    borderColor: '#f28e2b',
                    pointRadius: 4,
                    tension: 0.3
                }
            ]
        },
        options: {
            plugins: { legend: { display: true } },
            scales: {
                x: {
                    type: 'time',
                    time: {
                        unit: 'day',
                        tooltipFormat: 'MMM d, yyyy',
                        displayFormats: { day: 'MMM d' }
                    },
                    ticks: { maxRotation: 45 }
                },
                y: {
                    beginAtZero: true,
                    position: 'left',
                    ticks: {
                        callback: val => '$' + val.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                    }
                },
                y2: {
                    beginAtZero: true,
                    position: 'right',
                    grid: { drawOnChartArea: false },
                    ticks: { precision: 0 }
                }
            },
            responsive: true,
            maintainAspectRatio: true
        }
    });
}

const MEDAL_STYLES = [
    'background-color:#FFD700;color:#000',
    'background-color:#C0C0C0;color:#000',
    'background-color:#CD7F32;color:#fff'
];

function medalBadge(i) {
    if (i < 3)
        return `<span class="badge rounded-pill" style="${MEDAL_STYLES[i]}">${i + 1}</span>`;
    return `<span class="text-muted">${i + 1}</span>`;
}

function updateTopCards(topCards) {
    const wrapper = document.getElementById('topCardsWrapper');
    const noMsg = document.getElementById('noTopCardsMsg');
    const tbody = document.getElementById('topCardsBody');
    if (!wrapper || !tbody) return;

    tbody.innerHTML = topCards.map((c, i) =>
        `<tr>
            <td class="text-center">${medalBadge(i)}</td>
            <td>${c.cardName}</td>
            <td>${c.setName}</td>
            <td>${c.rarityName}</td>
            <td class="text-end">$${c.value.toFixed(2)}</td>
        </tr>`
    ).join('');

    if (noMsg) noMsg.style.display = 'none';
    wrapper.style.display = '';
}

if (setLabels.length > 0) setChart = buildHorizontalBar('setChart', setLabels, setCounts);
if (setValueLabels.length > 0) {
    lastSetValueData = { labels: setValueLabels, data: setValueData };
    setValueChart = buildValueBar('setValueChart', setValueLabels, setValueData);
}
if (historyDates.length > 0) buildValueChart(historyDates, historyValues, historyCardCounts);

let cardHistoryChart = null;
let lastCardHistorySeries = null;

function buildPriceHistoryChart(series) {
    const ctx = document.getElementById('cardHistoryChart');
    const wrapper = document.getElementById('cardHistoryChartWrapper');
    if (!ctx || !wrapper) return;

    lastCardHistorySeries = series;
    const datasets = series.map((s, i) => ({
        label: s.label,
        data: s.dates.map((d, j) => ({ x: d, y: s.values[j] })),
        borderColor: CHART_COLORS[i % CHART_COLORS.length],
        backgroundColor: 'transparent',
        pointRadius: 4,
        tension: 0.3,
        fill: false
    }));

    if (cardHistoryChart) {
        cardHistoryChart.data.datasets = datasets;
        cardHistoryChart.options.plugins.legend.display = series.length > 1;
        cardHistoryChart.update();
    } else {
        cardHistoryChart = new Chart(ctx, {
            type: 'line',
            data: { datasets },
            options: {
                plugins: { legend: { display: series.length > 1 } },
                scales: {
                    x: {
                        type: 'time',
                        time: { unit: 'day', tooltipFormat: 'MMM d, yyyy', displayFormats: { day: 'MMM d' } },
                        ticks: { maxRotation: 45 }
                    },
                    y: {
                        beginAtZero: true,
                        ticks: { callback: val => '$' + val.toFixed(2) }
                    }
                },
                responsive: true,
                maintainAspectRatio: false
            }
        });
    }

    wrapper.style.display = '';
}

async function loadCardPriceHistory() {
    const input = document.getElementById('cardHistoryInput');
    const status = document.getElementById('cardHistoryStatus');
    const wrapper = document.getElementById('cardHistoryChartWrapper');
    const cardName = input?.value?.trim();
    if (!cardName) return;

    status.textContent = 'Loading…';
    status.style.display = '';
    if (wrapper) wrapper.style.display = 'none';

    try {
        const resp = await fetch(`/api/stats/card-price-history?cardName=${encodeURIComponent(cardName)}`);
        if (!resp.ok) {
            status.textContent = 'Failed to load price history.';
            return;
        }
        const series = await resp.json();
        if (!series.length) {
            status.textContent = 'No price history found for this card.';
            return;
        }
        status.style.display = 'none';
        buildPriceHistoryChart(series);

        const imgUrl = typeof trackedCardImageMap !== 'undefined' ? trackedCardImageMap[cardName] : null;
        const sideImg = document.getElementById('cardHistoryImg');
        const sideWrapper = document.getElementById('cardHistoryImageWrapper');
        if (imgUrl && sideImg && sideWrapper) {
            sideImg.src = imgUrl;
            sideWrapper.style.display = '';
        }
    } catch {
        status.textContent = 'Failed to load price history.';
    }
}

(function () {
    const cardNames = typeof trackedCardImageMap !== 'undefined' ? Object.keys(trackedCardImageMap) : [];
    const input = document.getElementById('cardHistoryInput');
    const dropdown = document.getElementById('cardHistoryDropdown');
    if (!input || !dropdown || !cardNames.length) return;

    const typeahead = buildTypeahead(input, dropdown, loadCardPriceHistory, loadCardPriceHistory);

    input.addEventListener('input', () => {
        const q = input.value.trim();
        if (q.length < 2) { dropdown.style.display = 'none'; return; }
        typeahead.show(cardNames.filter(n => n.toLowerCase().includes(q.toLowerCase())).slice(0, 20));
    });
})();

function refreshCardData() {
    if (!confirm('This will redownload all card data from yaml-yugi and YGOProDeck. This may take a minute. Continue?')) return;

    const btn = document.getElementById('refreshCardDataBtn');
    const statusDiv = document.getElementById('cardDataStatus');
    const progressEl = document.getElementById('cardDataProgress');
    const resultEl = document.getElementById('cardDataResult');
    const errorEl = document.getElementById('cardDataError');

    btn.disabled = true;
    statusDiv.style.display = '';
    progressEl.style.display = '';
    progressEl.textContent = 'Downloading card data…';
    resultEl.style.display = 'none';
    errorEl.style.display = 'none';

    const source = new EventSource('/api/admin/refresh-card-data/stream');
    let completed = false;

    source.addEventListener('complete', e => {
        completed = true;
        source.close();
        const data = JSON.parse(e.data);
        progressEl.style.display = 'none';
        resultEl.textContent = `Card data updated: ${data.cardCount.toLocaleString()} cards loaded.`;
        resultEl.style.display = '';
        btn.disabled = false;
    });

    source.addEventListener('error', () => {
        source.close();
        if (completed) return;
        progressEl.style.display = 'none';
        errorEl.textContent = 'Failed to download card data. Check server logs.';
        errorEl.style.display = '';
        btn.disabled = false;
    });
}

document.getElementById('refreshCardDataBtn')?.addEventListener('click', refreshCardData);

document.addEventListener('themechange', function () {
    applyChartTheme();

    if (setChart) {
        setChart.destroy();
        setChart = setLabels.length > 0 ? buildHorizontalBar('setChart', setLabels, setCounts) : null;
    }
    if (setValueChart) {
        setValueChart.destroy();
        setValueChart = null;
        if (lastSetValueData) setValueChart = buildValueBar('setValueChart', lastSetValueData.labels, lastSetValueData.data);
    }
    if (valueChart) {
        valueChart.destroy();
        valueChart = null;
        buildValueChart(historyDates, historyValues, historyCardCounts);
    }
    if (cardHistoryChart) {
        cardHistoryChart.destroy();
        cardHistoryChart = null;
        if (lastCardHistorySeries) buildPriceHistoryChart(lastCardHistorySeries);
    }
});

