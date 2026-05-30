(function () {
    'use strict';
    var input = document.querySelector('input[data-autocomplete-url]');
    if (!input) return;

    var url = input.dataset.autocompleteURL;
    var datalist = document.getElementById('card-suggestions');
    if (!datalist) return;

    var debounceTimer;
    input.addEventListener('input', function () {
        clearTimeout(debounceTimer);
        var q = this.value.trim();
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
