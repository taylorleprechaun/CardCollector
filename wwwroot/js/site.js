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

(function () {
    'use strict';
    const input = document.querySelector('input[data-autocomplete-url]');
    if (!input) return;

    const url = input.dataset.autocompleteUrl;
    const datalist = document.getElementById('card-suggestions');
    if (!datalist) return;

    let debounceTimer;
    input.addEventListener('input', function () {
        clearTimeout(debounceTimer);
        const q = this.value.trim();
        if (q.length < 2) { datalist.innerHTML = ''; return; }
        debounceTimer = setTimeout(function () {
            fetch(url + '&q=' + encodeURIComponent(q))
                .then(function (r) { return r.json(); })
                .then(function (names) {
                    datalist.innerHTML = names
                        .map(function (n) {
                            return '<option value="' + n.replace(/&/g, '&amp;').replace(/"/g, '&quot;') + '">';
                        })
                        .join('');
                });
        }, 300);
    });
})();
