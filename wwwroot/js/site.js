function bindPriceRefresh(editionSelect, marketPriceEl, getParams) {
    async function refreshPrice() {
        const { cardID, setCode, rarityName } = getParams();
        if (!(cardID && setCode && rarityName)) return;
        try {
            const resp = await fetch(`/api/price?cardID=${cardID}&setCode=${encodeURIComponent(setCode)}&rarityName=${encodeURIComponent(rarityName)}&edition=${encodeURIComponent(editionSelect.value)}`);
            if (resp.ok) {
                const { price } = await resp.json();
                if (price) marketPriceEl.value = price.toFixed(2);
            }
        } catch (err) { console.warn('Failed to fetch price:', err); }
    }

    if (editionSelect._priceChangeHandler) editionSelect.removeEventListener('change', editionSelect._priceChangeHandler);
    editionSelect._priceChangeHandler = refreshPrice;
    editionSelect.addEventListener('change', refreshPrice);

    return refreshPrice;
}

function setPickerDate(id, value) {
    var el = document.getElementById(id);
    if (!el) return;
    if (el._flatpickr) {
        value ? el._flatpickr.setDate(value) : el._flatpickr.clear();
    } else {
        el.value = value instanceof Date ? value.toLocaleDateString('en-CA') : (value || '');
    }
}

document.querySelectorAll('.cc-date-picker').forEach(function (el) {
    flatpickr(el, {
        dateFormat: 'Y-m-d',
        altInput: true,
        altFormat: 'm/d/Y',
        altInputClass: 'form-control form-control-sm',
        allowInput: true,
        disableMobile: true,
        onReady: function (selectedDates, dateStr, instance) {
            var todayBtn = document.createElement('button');
            todayBtn.textContent = 'Today';
            todayBtn.type = 'button';
            todayBtn.className = 'flatpickr-today-btn';
            todayBtn.addEventListener('click', function () {
                instance.setDate(new Date());
                instance.close();
            });
            instance.calendarContainer.appendChild(todayBtn);
        }
    });
});

function buildTypeahead(input, dropdown, onSelect, onEnterWithoutSelection) {
    let highlighted = -1;

    function items() { return Array.from(dropdown.querySelectorAll('[data-ti]')); }

    function setHighlight(idx) {
        items().forEach((el, i) => el.classList.toggle('cc-typeahead-item--active', i === idx));
        const el = items()[idx];
        if (el) el.scrollIntoView({ block: 'nearest' });
        highlighted = idx;
    }

    function show(names) {
        dropdown.innerHTML = '';
        highlighted = -1;
        if (!names.length) { dropdown.style.display = 'none'; return; }
        names.forEach(name => {
            const el = document.createElement('div');
            el.setAttribute('data-ti', '');
            el.className = 'cc-typeahead-item px-3 py-2';
            el.textContent = name;
            el.addEventListener('mouseover', () => setHighlight(items().indexOf(el)));
            el.addEventListener('pointerdown', e => {
                e.preventDefault();
                input.value = name;
                dropdown.style.display = 'none';
                if (onSelect) onSelect(name);
            });
            dropdown.appendChild(el);
        });
        dropdown.style.display = 'block';
    }

    input.addEventListener('keydown', e => {
        const its = items();
        if (e.key === 'ArrowDown') { e.preventDefault(); setHighlight(Math.min(highlighted + 1, its.length - 1)); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); setHighlight(Math.max(highlighted - 1, -1)); }
        else if (e.key === 'Enter') {
            if (highlighted >= 0 && its[highlighted]) {
                e.preventDefault();
                input.value = its[highlighted].textContent;
                dropdown.style.display = 'none';
                if (onSelect) onSelect(input.value);
            } else {
                dropdown.style.display = 'none';
                if (onEnterWithoutSelection) onEnterWithoutSelection();
            }
        } else if (e.key === 'Escape') {
            dropdown.style.display = 'none';
        }
    });

    input.addEventListener('blur', () => setTimeout(() => { dropdown.style.display = 'none'; highlighted = -1; }, 150));

    return { show };
}

(function () {
    'use strict';
    const input = document.querySelector('input[data-autocomplete-url]');
    if (!input) return;

    const url = input.dataset.autocompleteUrl;
    const dropdown = document.getElementById('card-suggestions-dropdown');
    if (!dropdown) return;

    const typeahead = buildTypeahead(input, dropdown, null);

    let debounceTimer;
    input.addEventListener('input', function () {
        clearTimeout(debounceTimer);
        const q = this.value.trim();
        if (q.length < 2) { dropdown.style.display = 'none'; return; }
        debounceTimer = setTimeout(function () {
            fetch(url + '&q=' + encodeURIComponent(q))
                .then(function (r) { return r.json(); })
                .then(function (names) { typeahead.show(names); });
        }, 300);
    });
})();

(function () {
    const setInput = document.getElementById('filter-set-input');
    const setDropdown = document.getElementById('set-filter-dropdown');
    if (!setInput || !setDropdown) return;

    const setNames = Array.from(document.querySelectorAll('#set-filter-datalist option')).map(o => o.value);
    if (!setNames.length) return;

    const typeahead = buildTypeahead(setInput, setDropdown, null);

    setInput.addEventListener('input', () => {
        const q = setInput.value.trim().toLowerCase();
        if (q.length < 2) { setDropdown.style.display = 'none'; return; }
        typeahead.show(setNames.filter(n => n.toLowerCase().includes(q)).slice(0, 20));
    });
})();

(function () {
    const root = document.documentElement;
    const btn = document.getElementById('themeToggleBtn');
    if (!btn) return;

    btn.addEventListener('click', function () {
        const next = root.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
        root.setAttribute('data-bs-theme', next);
        localStorage.setItem('cc-theme', next);
        document.dispatchEvent(new CustomEvent('themechange', { detail: { theme: next } }));
    });
})();
