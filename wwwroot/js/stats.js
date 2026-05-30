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
                y: { ticks: { font: { size: 11 } } }
            },
            responsive: true,
            maintainAspectRatio: true
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
                y: { ticks: { font: { size: 11 } } }
            },
            responsive: true,
            maintainAspectRatio: true
        }
    });
}

const PIE_LEGEND_GAP_PLUGIN = {
    id: 'legendGap',
    afterLayout(chart) {
        const legend = chart.legend;
        if (!legend || legend.options.position !== 'right') return;
        const gap = 12;
        chart.chartArea.right -= gap;
        chart.chartArea.width -= gap;
    }
};

function buildPie(id, labels, data) {
    const ctx = document.getElementById(id);
    if (!ctx) return null;
    return new Chart(ctx, {
        type: 'pie',
        plugins: [PIE_LEGEND_GAP_PLUGIN],
        data: {
            labels: labels,
            datasets: [{
                data: data,
                backgroundColor: CHART_COLORS.slice(0, data.length),
                borderWidth: 1
            }]
        },
        options: {
            plugins: {
                legend: {
                    position: 'right',
                    align: 'start',
                    labels: {
                        font: { size: 12 },
                        usePointStyle: true,
                        pointStyle: 'circle',
                        pointStyleWidth: 8
                    }
                }
            },
            responsive: true,
            maintainAspectRatio: true
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
    const ctx = document.getElementById('setValueChart');
    if (ctx) ctx.style.display = '';
    const noMsg = document.getElementById('noSetValueMsg');
    if (noMsg) noMsg.style.display = 'none';
    setValueChart = buildValueBar('setValueChart', labels, data);
}

function buildValueChart(dates, values) {
    const ctx = document.getElementById('valueChart');
    if (!ctx) return;

    ctx.style.display = '';
    const noMsg = document.getElementById('noHistoryMsg');
    if (noMsg) noMsg.style.display = 'none';

    if (valueChart) {
        valueChart.data.labels = dates;
        valueChart.data.datasets[0].data = values;
        valueChart.update();
        return;
    }

    valueChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: dates,
            datasets: [{
                label: 'Market Value (USD)',
                data: values,
                fill: true,
                backgroundColor: 'rgba(78,121,167,0.15)',
                borderColor: '#4e79a7',
                pointRadius: 4,
                tension: 0.3
            }]
        },
        options: {
            plugins: { legend: { display: false } },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: val => '$' + val.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                    }
                }
            },
            responsive: true,
            maintainAspectRatio: true
        }
    });
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
        } else {
            historyDates.push(today);
            historyValues.push(data.totalValue);
        }
        buildValueChart(historyDates, historyValues);

        if (data.setValueLabels.length > 0)
            updateSetValueChart(data.setValueLabels, data.setValueData);
    } catch (err) {
        spinner.style.display = 'none';
        error.style.display = '';
        error.textContent = 'Failed to calculate market value. Please try again.';
    } finally {
        btn.disabled = false;
    }
}

if (rarityLabels.length > 0) buildPie('rarityChart', rarityLabels, rarityCounts);
if (setLabels.length > 0) buildHorizontalBar('setChart', setLabels, setCounts);
if (acqLabels.length > 0) buildPie('acquisitionChart', acqLabels, acqCounts);
if (setValueLabels.length > 0) setValueChart = buildValueBar('setValueChart', setValueLabels, setValueData);
if (historyDates.length > 0) buildValueChart(historyDates, historyValues);
