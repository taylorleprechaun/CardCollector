import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

function buildDom({ initial = '[]', max = 10, openOnLoad = 'false' } = {}) {
  document.body.innerHTML = `
    <div id="formatModal" data-open-on-load="${openOnLoad}">
      <h5 id="formatModalLabel"></h5>
      <form id="formatForm">
        <input type="hidden" id="Input_ID" value="0" />
        <input id="Input_Name" />
        <input id="Input_StartDate" />
        <input id="Input_EndDate" />
        <input type="checkbox" id="Input_IsOngoing" />
        <textarea id="Input_Notes"></textarea>
        <input id="strategyInput" />
        <button type="button" id="strategyAddBtn">Add</button>
        <div id="strategyHint"></div>
        <ol id="strategyList" data-max="${max}" data-initial='${initial}'></ol>
        <div id="formatErrors"></div>
      </form>
    </div>
    <div id="deleteFormatModal"></div>
    <input id="deleteFormatID" />
    <span id="deleteFormatName"></span>`;
  loadScript('formats.js');
}

function chipNames() {
  return Array.from(document.querySelectorAll('#strategyList input[name="Input.Strategies"]')).map(i => i.value);
}

function chipButton(index, action) {
  return document.querySelectorAll('#strategyList li')[index].querySelector(`[data-action="${action}"]`);
}

function makeTrigger(data) {
  const button = document.createElement('button');
  Object.entries(data).forEach(([key, value]) => { button.dataset[key] = value; });
  return button;
}

describe('formats.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  describe('addStrategy', () => {
    it('trims the name and adds a chip with a hidden form field', () => {
      buildDom();
      const list = document.getElementById('strategyList');

      const status = addStrategy(list, '  Alpha Deck  ');

      expect(status).toBe('added');
      expect(chipNames()).toEqual(['Alpha Deck']);
    });

    it('ignores blank input', () => {
      buildDom();
      const list = document.getElementById('strategyList');

      expect(addStrategy(list, '   ')).toBe('blank');
      expect(addStrategy(list, null)).toBe('blank');
      expect(chipNames()).toEqual([]);
    });

    it('rejects a case-insensitive duplicate', () => {
      buildDom();
      const list = document.getElementById('strategyList');
      addStrategy(list, 'Alpha Deck');

      const status = addStrategy(list, 'ALPHA deck');

      expect(status).toBe('duplicate');
      expect(chipNames()).toEqual(['Alpha Deck']);
    });

    it('rejects a new strategy once the limit is reached', () => {
      buildDom({ max: 2 });
      const list = document.getElementById('strategyList');
      addStrategy(list, 'One');
      addStrategy(list, 'Two');

      const status = addStrategy(list, 'Three');

      expect(status).toBe('limit');
      expect(chipNames()).toEqual(['One', 'Two']);
    });
  });

  describe('reordering', () => {
    it('moves a chip up and down', () => {
      buildDom();
      const list = document.getElementById('strategyList');
      ['One', 'Two', 'Three'].forEach(name => addStrategy(list, name));

      chipButton(2, 'up').click();
      expect(chipNames()).toEqual(['One', 'Three', 'Two']);

      chipButton(0, 'down').click();
      expect(chipNames()).toEqual(['Three', 'One', 'Two']);
    });

    it('disables up on the first chip and down on the last', () => {
      buildDom();
      const list = document.getElementById('strategyList');
      ['One', 'Two'].forEach(name => addStrategy(list, name));

      expect(chipButton(0, 'up').disabled).toBe(true);
      expect(chipButton(0, 'down').disabled).toBe(false);
      expect(chipButton(1, 'up').disabled).toBe(false);
      expect(chipButton(1, 'down').disabled).toBe(true);
    });

    it('removes a chip and refreshes the remaining buttons', () => {
      buildDom();
      const list = document.getElementById('strategyList');
      ['One', 'Two'].forEach(name => addStrategy(list, name));

      chipButton(0, 'remove').click();

      expect(chipNames()).toEqual(['Two']);
      expect(chipButton(0, 'up').disabled).toBe(true);
      expect(chipButton(0, 'down').disabled).toBe(true);
    });
  });

  describe('strategy input', () => {
    it('adds a chip on Enter without submitting the form', () => {
      buildDom();
      const input = document.getElementById('strategyInput');
      input.value = 'Alpha Deck';
      const event = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key: 'Enter' });

      input.dispatchEvent(event);

      expect(event.defaultPrevented).toBe(true);
      expect(chipNames()).toEqual(['Alpha Deck']);
      expect(input.value).toBe('');
    });

    it('adds a chip from the Add button', () => {
      buildDom();
      document.getElementById('strategyInput').value = 'Beta Deck';

      document.getElementById('strategyAddBtn').click();

      expect(chipNames()).toEqual(['Beta Deck']);
    });

    it('explains a rejected duplicate and keeps the typed text', () => {
      buildDom({ initial: '["Alpha Deck"]' });
      const input = document.getElementById('strategyInput');
      input.value = 'alpha deck';

      document.getElementById('strategyAddBtn').click();

      expect(document.getElementById('strategyHint').textContent).toBe('That strategy is already in the list.');
      expect(input.value).toBe('alpha deck');
    });

    it('adds text left in the box when the form is submitted', () => {
      buildDom();
      document.getElementById('strategyInput').value = 'Pending Deck';

      document.getElementById('formatForm').dispatchEvent(new Event('submit', { cancelable: true }));

      expect(chipNames()).toEqual(['Pending Deck']);
    });
  });

  describe('page initialisation', () => {
    it('renders the strategies the server sent back after a failed save', () => {
      buildDom({ initial: '["First","Second"]' });

      expect(chipNames()).toEqual(['First', 'Second']);
    });

    it('reopens the modal when the server asked for it', () => {
      buildDom({ openOnLoad: 'true' });

      expect(bootstrap.Modal).toHaveBeenCalledWith(document.getElementById('formatModal'));
    });

    it('leaves the modal closed otherwise', () => {
      buildDom();

      expect(bootstrap.Modal).not.toHaveBeenCalled();
    });
  });

  describe('openFormatModal', () => {
    it('opens a blank Add form and clears leftover errors', () => {
      buildDom({ initial: '["Stale"]' });
      document.getElementById('Input_Name').value = 'Old';

      openFormatModal(null);

      expect(document.getElementById('formatModalLabel').textContent).toBe('Add Format');
      expect(document.getElementById('Input_ID').value).toBe('0');
      expect(document.getElementById('Input_Name').value).toBe('');
      expect(document.getElementById('Input_IsOngoing').checked).toBe(false);
      expect(chipNames()).toEqual([]);
      expect(document.getElementById('formatErrors')).toBeNull();
      expect(bootstrap.Modal).toHaveBeenCalledWith(document.getElementById('formatModal'));
    });

    it('fills the form from the trigger when editing', () => {
      buildDom();
      const trigger = makeTrigger({
        formatId: '7',
        formatName: 'Alpha Era',
        formatStart: '2024-01-01',
        formatEnd: '2024-03-31',
        formatNotes: 'Some notes',
        formatStrategies: '["First","Second"]'
      });

      openFormatModal(trigger);

      expect(document.getElementById('formatModalLabel').textContent).toBe('Edit Format');
      expect(document.getElementById('Input_ID').value).toBe('7');
      expect(document.getElementById('Input_Name').value).toBe('Alpha Era');
      expect(document.getElementById('Input_StartDate').value).toBe('2024-01-01');
      expect(document.getElementById('Input_EndDate').value).toBe('2024-03-31');
      expect(document.getElementById('Input_Notes').value).toBe('Some notes');
      expect(document.getElementById('Input_IsOngoing').checked).toBe(false);
      expect(chipNames()).toEqual(['First', 'Second']);
    });

    it('ticks Ongoing and disables the end date for a format with no end date', () => {
      buildDom();
      const trigger = makeTrigger({ formatId: '7', formatName: 'Current', formatStart: '2025-01-01', formatStrategies: '[]' });

      openFormatModal(trigger);

      expect(document.getElementById('Input_IsOngoing').checked).toBe(true);
      expect(document.getElementById('Input_EndDate').disabled).toBe(true);
    });
  });

  describe('Ongoing checkbox', () => {
    it('clears and disables the end date when ticked, and re-enables it when cleared', () => {
      buildDom();
      const end = document.getElementById('Input_EndDate');
      const ongoing = document.getElementById('Input_IsOngoing');
      end.value = '2024-03-31';

      ongoing.checked = true;
      ongoing.dispatchEvent(new Event('change'));
      expect(end.value).toBe('');
      expect(end.disabled).toBe(true);

      ongoing.checked = false;
      ongoing.dispatchEvent(new Event('change'));
      expect(end.disabled).toBe(false);
    });

    it('also disables the flatpickr display input', () => {
      buildDom();
      const end = document.getElementById('Input_EndDate');
      end._flatpickr = { altInput: document.createElement('input'), clear: vi.fn(), setDate: vi.fn() };
      const ongoing = document.getElementById('Input_IsOngoing');

      ongoing.checked = true;
      ongoing.dispatchEvent(new Event('change'));

      expect(end._flatpickr.altInput.disabled).toBe(true);
    });
  });

  describe('openDeleteFormatModal', () => {
    it('fills the confirmation with the format and opens the modal', () => {
      buildDom();

      openDeleteFormatModal(makeTrigger({ formatId: '7', formatName: 'Alpha Era' }));

      expect(document.getElementById('deleteFormatID').value).toBe('7');
      expect(document.getElementById('deleteFormatName').textContent).toBe('Alpha Era');
      expect(bootstrap.Modal).toHaveBeenCalledWith(document.getElementById('deleteFormatModal'));
    });
  });
});
