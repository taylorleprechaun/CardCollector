const CHART_COLORS = [
    '#4e79a7', '#f28e2b', '#e15759', '#76b7b2', '#59a14f',
    '#edc948', '#b07aa1', '#ff9da7', '#9c755f', '#bab0ac',
    '#499894', '#86bcb6', '#e15759', '#ff9da7', '#79706e',
    '#d37295', '#fabfd2', '#b6992d', '#f1ce63', '#a0cbe8'
];

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

let setValueChart = null;
let valueChart = null;

function updateSetValueChart(labels, data) {
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

async function calculateValue() {
    const btn = document.getElementById('calcValueBtn');
    const statusDiv = document.getElementById('calcStatus');
    const spinner = document.getElementById('calcSpinner');
    const result = document.getElementById('calcResult');
    const error = document.getElementById('calcError');

    btn.disabled = true;
    statusDiv.style.display = '';
    spinner.style.display = 'flex';
    result.style.display = 'none';
    error.style.display = 'none';

    try {
        const resp = await fetch('/api/stats/calculate-value', { method: 'POST' });
        if (!resp.ok) throw new Error('Server returned ' + resp.status);

        const data = await resp.json();
        const formatted = '$' + data.totalValue.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

        spinner.style.display = 'none';
        result.style.display = '';
        result.textContent = `Current market value: ${formatted} across ${data.cardCount} owned entries.`;

        const today = new Date().toISOString().slice(0, 10);
        const idx = historyDates.indexOf(today);
        if (idx >= 0) {
            historyValues[idx] = data.totalValue;
            historyCardCounts[idx] = data.cardCount;
        } else {
            historyDates.push(today);
            historyValues.push(data.totalValue);
            historyCardCounts.push(data.cardCount);
        }
        buildValueChart(historyDates, historyValues, historyCardCounts);

        if (data.setValueLabels.length > 0)
            updateSetValueChart(data.setValueLabels, data.setValueData);

        if (data.topCards && data.topCards.length > 0)
            updateTopCards(data.topCards);
    } catch (err) {
        spinner.style.display = 'none';
        error.style.display = '';
        error.textContent = 'Failed to calculate market value. Please try again.';
    } finally {
        btn.disabled = false;
    }
}

if (setLabels.length > 0) buildHorizontalBar('setChart', setLabels, setCounts);
if (setValueLabels.length > 0) setValueChart = buildValueBar('setValueChart', setValueLabels, setValueData);
if (historyDates.length > 0) buildValueChart(historyDates, historyValues, historyCardCounts);
