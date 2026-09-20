const STRATEGY_FIELD_NAME = 'Input.Strategies';

const STRATEGY_HINTS = {
    duplicate: 'That strategy is already in the list.',
    limit: 'The strategy limit has been reached.'
};

function addStrategy(list, rawName) {
    const name = normalizeStrategyName(rawName);
    if (!name) return 'blank';

    const lowered = name.toLowerCase();
    if (getStrategyNames(list).some(existing => existing.toLowerCase() === lowered)) return 'duplicate';
    if (list.children.length >= Number(list.dataset.max || 10)) return 'limit';

    list.appendChild(createStrategyItem(name));
    updateStrategyButtons(list);
    return 'added';
}

function addStrategyFromInput() {
    const input = document.getElementById('strategyInput');
    const list = document.getElementById('strategyList');
    const hint = document.getElementById('strategyHint');
    if (!input || !list) return;

    const status = addStrategy(list, input.value);
    if (hint) hint.textContent = STRATEGY_HINTS[status] || '';
    if (status === 'added' || status === 'blank') input.value = '';
    input.focus();
}

function createStrategyButton(action, iconClass, label) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'btn btn-sm btn-outline-secondary py-0 px-1';
    button.dataset.action = action;
    button.setAttribute('aria-label', label);

    const icon = document.createElement('i');
    icon.className = `bi ${iconClass}`;
    icon.setAttribute('aria-hidden', 'true');
    button.appendChild(icon);
    return button;
}

function createStrategyItem(name) {
    const item = document.createElement('li');
    item.className = 'd-flex align-items-center gap-2 border rounded px-2 py-1';

    const hidden = document.createElement('input');
    hidden.type = 'hidden';
    hidden.name = STRATEGY_FIELD_NAME;
    hidden.value = name;

    const label = document.createElement('span');
    label.className = 'flex-grow-1 text-break';
    label.textContent = name;

    item.append(
        hidden,
        label,
        createStrategyButton('up', 'bi-chevron-up', `Move ${name} up`),
        createStrategyButton('down', 'bi-chevron-down', `Move ${name} down`),
        createStrategyButton('remove', 'bi-x-lg', `Remove ${name}`));
    return item;
}

function fillPickerDate(id, value) {
    if (typeof setPickerDate === 'function') {
        setPickerDate(id, value);
        return;
    }

    const el = document.getElementById(id);
    if (el) el.value = value || '';
}

function getStrategyNames(list) {
    return Array.from(list.querySelectorAll(`input[name="${STRATEGY_FIELD_NAME}"]`)).map(input => input.value);
}

function handleStrategyListClick(event) {
    const button = event.target.closest('button[data-action]');
    if (!button) return;

    const item = button.closest('li');
    const list = item.parentElement;
    const hint = document.getElementById('strategyHint');
    if (hint) hint.textContent = '';

    if (button.dataset.action === 'remove') item.remove();
    else moveStrategy(item, button.dataset.action === 'up' ? -1 : 1);

    updateStrategyButtons(list);
}

function initFormatsPage() {
    const input = document.getElementById('strategyInput');
    const list = document.getElementById('strategyList');
    const modal = document.getElementById('formatModal');
    const form = document.getElementById('formatForm');
    if (!input || !list || !modal || !form) return;

    input.addEventListener('keydown', function (event) {
        if (event.key !== 'Enter') return;
        event.preventDefault();
        addStrategyFromInput();
    });
    document.getElementById('strategyAddBtn')?.addEventListener('click', addStrategyFromInput);
    list.addEventListener('click', handleStrategyListClick);
    document.getElementById('Input_IsOngoing')?.addEventListener('change', syncEndDateState);

    // Text typed into the strategy box but never added would otherwise be lost on save.
    form.addEventListener('submit', function () {
        if (normalizeStrategyName(input.value)) addStrategyFromInput();
    });

    setStrategies(list, JSON.parse(list.dataset.initial || '[]'));
    syncEndDateState();

    if (modal.dataset.openOnLoad === 'true') new bootstrap.Modal(modal).show();
}

function moveStrategy(item, direction) {
    const list = item.parentElement;
    if (direction < 0 && item.previousElementSibling) list.insertBefore(item, item.previousElementSibling);
    if (direction > 0 && item.nextElementSibling) list.insertBefore(item.nextElementSibling, item);
}

function normalizeStrategyName(value) {
    return (value || '').trim();
}

function openDeleteFormatModal(trigger) {
    document.getElementById('deleteFormatID').value = trigger.dataset.formatId;
    document.getElementById('deleteFormatName').textContent = trigger.dataset.formatName;
    new bootstrap.Modal(document.getElementById('deleteFormatModal')).show();
}

function openFormatModal(trigger) {
    const data = trigger ? trigger.dataset : {};
    const isEdit = Boolean(data.formatId);

    document.getElementById('formatModalLabel').textContent = isEdit ? 'Edit Format' : 'Add Format';
    document.getElementById('Input_ID').value = isEdit ? data.formatId : '0';
    document.getElementById('Input_Name').value = data.formatName || '';
    document.getElementById('Input_Notes').value = data.formatNotes || '';
    fillPickerDate('Input_StartDate', data.formatStart || '');
    fillPickerDate('Input_EndDate', data.formatEnd || '');
    document.getElementById('Input_IsOngoing').checked = isEdit && !data.formatEnd;
    syncEndDateState();

    setStrategies(document.getElementById('strategyList'), isEdit ? JSON.parse(data.formatStrategies || '[]') : []);
    document.getElementById('strategyInput').value = '';
    document.getElementById('strategyHint').textContent = '';
    document.getElementById('formatErrors')?.remove();

    new bootstrap.Modal(document.getElementById('formatModal')).show();
}

function setStrategies(list, names) {
    list.replaceChildren();
    names.forEach(name => addStrategy(list, name));
    updateStrategyButtons(list);
}

function syncEndDateState() {
    const end = document.getElementById('Input_EndDate');
    const ongoing = document.getElementById('Input_IsOngoing');
    if (!end || !ongoing) return;

    if (ongoing.checked) fillPickerDate('Input_EndDate', '');
    end.disabled = ongoing.checked;
    if (end._flatpickr && end._flatpickr.altInput) end._flatpickr.altInput.disabled = ongoing.checked;
}

function updateStrategyButtons(list) {
    const items = Array.from(list.children);
    items.forEach(function (item, index) {
        item.querySelector('[data-action="up"]').disabled = index === 0;
        item.querySelector('[data-action="down"]').disabled = index === items.length - 1;
    });
}

initFormatsPage();
