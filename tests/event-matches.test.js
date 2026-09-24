import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

function formFields(prefix) {
  return `
    <div data-match-errors hidden><ul></ul></div>
    <input type="hidden" name="Input.ID" value="0" data-match-field="id" />
    <input id="${prefix}Round" name="Input.Round" data-match-field="round" />
    <input type="checkbox" id="${prefix}IsBye" name="Input.IsBye" value="true" data-match-field="bye" />
    <div data-bye-hide>
      <input id="${prefix}Opponent" name="Input.OpponentDeck" data-match-field="opponent" />
      <input type="radio" name="${prefix}ScorePreset" data-score-preset data-won="2" data-lost="0" data-tied="0" id="${prefix}P20" />
      <input type="radio" name="${prefix}ScorePreset" data-score-preset data-won="2" data-lost="1" data-tied="0" id="${prefix}P21" />
      <input type="radio" name="${prefix}ScorePreset" data-score-preset data-won="1" data-lost="1" data-tied="0" id="${prefix}P11" />
      <input type="radio" name="${prefix}ScorePreset" data-score-preset data-won="1" data-lost="2" data-tied="0" id="${prefix}P12" />
      <input type="radio" name="${prefix}ScorePreset" data-score-preset data-won="1" data-lost="1" data-tied="1" id="${prefix}P111" />
      <button type="button" data-custom-toggle aria-expanded="false">Custom score</button>
      <div data-custom-score hidden>
        <input type="number" name="Input.GamesWon" data-match-field="won" value="0" min="0" max="2" />
        <input type="number" name="Input.GamesLost" data-match-field="lost" value="0" min="0" max="2" />
        <input type="number" name="Input.GamesTied" data-match-field="tied" value="0" min="0" />
      </div>
      <input type="radio" name="Input.Result" value="Win" data-match-result id="${prefix}ResultWin" />
      <input type="radio" name="Input.Result" value="Loss" data-match-result id="${prefix}ResultLoss" />
      <input type="radio" name="Input.Result" value="Tie" data-match-result id="${prefix}ResultTie" checked />
      <div data-result-hint hidden>Suggested: <strong data-result-suggestion></strong>
        <button type="button" data-result-reset>Use suggestion</button></div>
      <input type="radio" name="Input.WonDiceRoll" value="true" data-match-dice id="${prefix}DiceWon" />
      <input type="radio" name="Input.WonDiceRoll" value="false" data-match-dice id="${prefix}DiceLost" />
      <input type="radio" name="Input.WonDiceRoll" value="" data-match-dice id="${prefix}DiceNone" checked />
    </div>
    <textarea name="Input.Notes" data-match-field="notes"></textarea>
    <button type="submit">Save</button>`;
}

function rowHtml(id, { round = '1', opponent = 'Test Opponent', bye = 'false', won = 0, lost = 0, tied = 0, result = 'Tie', dice = '', notes = '' } = {}) {
  return `<div class="list-group-item" id="match-row-${id}" data-match-id="${id}" data-round="${round}"
      data-opponent="${opponent}" data-bye="${bye}" data-games-won="${won}" data-games-lost="${lost}"
      data-games-tied="${tied}" data-result="${result}" data-dice="${dice}" data-notes="${notes}">
    <button type="button" data-match-action="edit">Edit</button>
    <button type="button" data-match-action="delete">Delete</button></div>`;
}

function buildDom({ rows = '', summary = '', round = '3' } = {}) {
  document.body.innerHTML = `
    <p id="matchSummary">${summary}</p>
    <form id="addForm" method="post" action="/Tournaments/Events/Details?id=5&handler=AddMatch" data-match-form="add">
      ${formFields('add')}
    </form>
    <div id="matchEmpty" hidden></div>
    <div id="matchList">${rows}</div>
    <div id="editMatchModal" class="modal"><form id="editForm" method="post" action="/Tournaments/Events/Details?id=5&handler=EditMatch" data-match-form="edit">
      ${formFields('edit')}
    </form></div>
    <div id="deleteMatchModal" class="modal"><form id="deleteForm" method="post" action="/Tournaments/Events/Details?id=5&handler=DeleteMatch" data-match-form="delete">
      <div data-match-errors hidden><ul></ul></div>
      <input type="hidden" id="deleteMatchID" name="matchID" />
      <strong id="deleteMatchName"></strong>
      <button type="submit">Delete</button>
    </form></div>`;
  const addRound = document.querySelector('#addForm [data-match-field="round"]');
  addRound.value = round;
  addRound.dataset.suggested = round;
  loadScript('site.js');
  loadScript('event-matches.js');
}

const addForm = () => document.getElementById('addForm');
const editForm = () => document.getElementById('editForm');
const field = (form, name) => form.querySelector(`[data-match-field="${name}"]`);
const checkedResult = (form) => form.querySelector('[data-match-result]:checked')?.value;

function typeScore(form, won, lost, tied = 0) {
  [['won', won], ['lost', lost], ['tied', tied]].forEach(([name, value]) => {
    field(form, name).value = String(value);
    field(form, name).dispatchEvent(new Event('input', { bubbles: true }));
  });
}

function pickResult(form, value) {
  form.querySelector(`[data-match-result][value="${value}"]`).click();
}

function mockFetch(response) {
  globalThis.fetch = vi.fn().mockResolvedValue({
    ok: response.status === undefined || response.status < 400,
    status: response.status ?? 200,
    json: async () => response.body
  });
}

const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

async function submit(form) {
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
  await flush();
}

describe('event-matches.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    delete globalThis.fetch;
    vi.restoreAllMocks();
  });

  describe('suggestMatchResult', () => {
    it.each([
      [2, 0, 0, 'Win'],
      [2, 1, 0, 'Win'],
      [1, 2, 0, 'Loss'],
      [0, 2, 0, 'Loss'],
      [1, 1, 0, 'Tie'],
      [1, 1, 1, 'Tie'],
      [0, 0, 0, 'Tie'],
      [1, 0, 0, 'Win']
    ])('%i-%i-%i suggests %s', (won, lost, tied, expected) => {
      buildDom();

      expect(suggestMatchResult(won, lost, tied)).toBe(expected);
    });
  });

  describe('result follows the score', () => {
    it('updates the result when the scores change and no result has been picked', () => {
      buildDom();

      typeScore(addForm(), 2, 0);

      expect(checkedResult(addForm())).toBe('Win');
    });

    it('leaves a manually picked result alone when the scores change afterwards', () => {
      buildDom();
      typeScore(addForm(), 2, 1);

      pickResult(addForm(), 'Loss');
      typeScore(addForm(), 2, 0);

      expect(checkedResult(addForm())).toBe('Loss');
    });

    it('shows the suggestion and lets the user go back to it after an override', () => {
      buildDom();
      typeScore(addForm(), 2, 1);
      pickResult(addForm(), 'Loss');
      const hint = addForm().querySelector('[data-result-hint]');

      expect(hint.hidden).toBe(false);
      expect(hint.querySelector('[data-result-suggestion]').textContent).toBe('Win');

      addForm().querySelector('[data-result-reset]').click();

      expect(checkedResult(addForm())).toBe('Win');
      expect(hint.hidden).toBe(true);
    });

    it('does not count picking the suggested result as an override', () => {
      buildDom();
      typeScore(addForm(), 2, 1);

      pickResult(addForm(), 'Win');
      typeScore(addForm(), 0, 2);

      expect(checkedResult(addForm())).toBe('Loss');
    });
  });

  describe('score presets', () => {
    it('fills the scores and result from a preset', () => {
      buildDom();

      document.getElementById('addP12').click();

      expect(field(addForm(), 'won').value).toBe('1');
      expect(field(addForm(), 'lost').value).toBe('2');
      expect(checkedResult(addForm())).toBe('Loss');
    });

    it('lets a 1-1 score be recorded as a loss, for a match ended by the end-of-match procedure', () => {
      buildDom();

      document.getElementById('addP11').click();
      expect(checkedResult(addForm())).toBe('Tie');

      pickResult(addForm(), 'Loss');

      expect(field(addForm(), 'won').value).toBe('1');
      expect(field(addForm(), 'lost').value).toBe('1');
      expect(checkedResult(addForm())).toBe('Loss');
      expect(addForm().querySelector('[data-result-hint]').hidden).toBe(false);
    });

    it('selects the matching preset when the scores are typed, and none for a custom score', () => {
      buildDom();

      typeScore(addForm(), 2, 1);
      expect(document.getElementById('addP21').checked).toBe(true);

      typeScore(addForm(), 3, 0);
      expect(addForm().querySelector('[data-score-preset]:checked')).toBeNull();
    });
  });

  describe('custom score', () => {
    const toggle = (form) => form.querySelector('[data-custom-toggle]');
    const customSection = (form) => form.querySelector('[data-custom-score]');

    it('keeps the number boxes hidden until Custom score is used', () => {
      buildDom();

      expect(customSection(addForm()).hidden).toBe(true);
      expect(toggle(addForm()).getAttribute('aria-expanded')).toBe('false');
    });

    it('shows and hides the number boxes with the Custom score button', () => {
      buildDom();

      toggle(addForm()).click();
      expect(customSection(addForm()).hidden).toBe(false);
      expect(toggle(addForm()).getAttribute('aria-expanded')).toBe('true');

      toggle(addForm()).click();
      expect(customSection(addForm()).hidden).toBe(true);
      expect(toggle(addForm()).getAttribute('aria-expanded')).toBe('false');
    });

    it('lets the result and preset follow a score typed into the boxes', () => {
      buildDom();
      toggle(addForm()).click();

      typeScore(addForm(), 2, 0);

      expect(checkedResult(addForm())).toBe('Win');
      expect(document.getElementById('addP20').checked).toBe(true);
    });

    it('accepts more than one tied game', () => {
      buildDom();
      toggle(addForm()).click();

      typeScore(addForm(), 0, 0, 3);

      expect(field(addForm(), 'tied').value).toBe('3');
      expect(checkedResult(addForm())).toBe('Tie');
    });

    it('opens by default when editing a round whose score is not one of the quick fills', () => {
      buildDom({ rows: rowHtml(9, { won: 1, lost: 0, tied: 0, result: 'Win' }) });

      document.querySelector('#match-row-9 [data-match-action="edit"]').click();

      expect(customSection(editForm()).hidden).toBe(false);
      expect(toggle(editForm()).getAttribute('aria-expanded')).toBe('true');
      expect(field(editForm(), 'won').value).toBe('1');
    });

    it('opens by default for a round with no games recorded', () => {
      buildDom({ rows: rowHtml(9, { won: 0, lost: 0, tied: 0, result: 'Win' }) });

      document.querySelector('#match-row-9 [data-match-action="edit"]').click();

      expect(customSection(editForm()).hidden).toBe(false);
    });

    it('stays hidden when editing a round with one of the quick-fill scores', () => {
      buildDom({ rows: rowHtml(9, { won: 2, lost: 1, result: 'Win' }) });

      document.querySelector('#match-row-9 [data-match-action="edit"]').click();

      expect(customSection(editForm()).hidden).toBe(true);
      expect(document.getElementById('editP21').checked).toBe(true);
    });

    it('closes again when the next round is edited after a custom one', () => {
      buildDom({ rows: rowHtml(8, { won: 1, lost: 0 }) + rowHtml(9, { won: 2, lost: 0, result: 'Win' }) });

      document.querySelector('#match-row-8 [data-match-action="edit"]').click();
      document.querySelector('#match-row-9 [data-match-action="edit"]').click();

      expect(customSection(editForm()).hidden).toBe(true);
    });

    it('stays hidden for a bye', () => {
      buildDom({ rows: rowHtml(9, { bye: 'true', opponent: 'Bye', result: 'Win' }) });

      document.querySelector('#match-row-9 [data-match-action="edit"]').click();

      expect(customSection(editForm()).hidden).toBe(true);
    });

    it('is hidden again after a round is added and the form starts over', async () => {
      buildDom();
      toggle(addForm()).click();
      typeScore(addForm(), 1, 0);
      mockFetch({ body: { rowHtml: rowHtml(1), previousMatchId: null, summaryHtml: '', roundCount: 1, nextRound: '2' } });

      await submit(addForm());

      expect(customSection(addForm()).hidden).toBe(true);
      expect(field(addForm(), 'won').value).toBe('0');
    });

    it('is hidden again when the bye switch is turned on and off', () => {
      buildDom();
      toggle(addForm()).click();

      field(addForm(), 'bye').click();
      field(addForm(), 'bye').click();

      expect(customSection(addForm()).hidden).toBe(true);
    });
  });

  describe('bye toggle', () => {
    it('hides the opponent and game fields and clears the score, dice roll and result', () => {
      buildDom();
      typeScore(addForm(), 1, 2);
      document.getElementById('addDiceWon').click();

      field(addForm(), 'bye').click();

      expect(addForm().querySelector('[data-bye-hide]').hidden).toBe(true);
      expect(field(addForm(), 'lost').value).toBe('0');
      expect(addForm().querySelector('[data-match-dice]:checked').value).toBe('');
      expect(checkedResult(addForm())).toBe('Win');
    });

    it('shows the fields again with a fresh suggestion when the bye is switched off', () => {
      buildDom();
      field(addForm(), 'bye').click();

      field(addForm(), 'bye').click();

      expect(addForm().querySelector('[data-bye-hide]').hidden).toBe(false);
      expect(checkedResult(addForm())).toBe('Tie');
    });
  });

  describe('edit modal', () => {
    it('fills the form from the row and opens the modal', () => {
      buildDom({ rows: rowHtml(9, { round: 'Top 8', opponent: 'Sample Opponent', won: 2, lost: 1, result: 'Win', dice: 'true', notes: 'line one' }) });

      document.querySelector('#match-row-9 [data-match-action="edit"]').click();

      expect(field(editForm(), 'id').value).toBe('9');
      expect(field(editForm(), 'round').value).toBe('Top 8');
      expect(field(editForm(), 'opponent').value).toBe('Sample Opponent');
      expect(field(editForm(), 'won').value).toBe('2');
      expect(field(editForm(), 'lost').value).toBe('1');
      expect(document.getElementById('editP21').checked).toBe(true);
      expect(editForm().querySelector('[data-match-dice]:checked').value).toBe('true');
      expect(field(editForm(), 'notes').value).toBe('line one');
      expect(checkedResult(editForm())).toBe('Win');
      expect(bootstrap.Modal).toHaveBeenCalledWith(document.getElementById('editMatchModal'));
    });

    it('keeps a stored result that differs from the score when the score is edited', () => {
      buildDom({ rows: rowHtml(9, { won: 1, lost: 2, result: 'Win' }) });
      document.querySelector('#match-row-9 [data-match-action="edit"]').click();

      expect(editForm().querySelector('[data-result-hint]').hidden).toBe(false);
      typeScore(editForm(), 0, 2);

      expect(checkedResult(editForm())).toBe('Win');
    });

    it('lets a stored result that matches the score follow later score edits', () => {
      buildDom({ rows: rowHtml(9, { won: 2, lost: 0, result: 'Win' }) });
      document.querySelector('#match-row-9 [data-match-action="edit"]').click();

      typeScore(editForm(), 0, 2);

      expect(checkedResult(editForm())).toBe('Loss');
    });

    it('opens a bye with the game fields hidden', () => {
      buildDom({ rows: rowHtml(9, { bye: 'true', opponent: 'Bye', result: 'Win' }) });

      document.querySelector('#match-row-9 [data-match-action="edit"]').click();

      expect(field(editForm(), 'bye').checked).toBe(true);
      expect(editForm().querySelector('[data-bye-hide]').hidden).toBe(true);
    });

    it('clears a stale error box when it opens', () => {
      buildDom({ rows: rowHtml(9) });
      const box = editForm().querySelector('[data-match-errors]');
      box.hidden = false;
      box.querySelector('ul').innerHTML = '<li>old</li>';

      document.querySelector('#match-row-9 [data-match-action="edit"]').click();

      expect(box.hidden).toBe(true);
      expect(box.querySelectorAll('li').length).toBe(0);
    });
  });

  describe('delete modal', () => {
    it('names the round and its opponent, then opens the modal', () => {
      buildDom({ rows: rowHtml(4, { round: '3', opponent: 'Sample Opponent' }) });

      document.querySelector('#match-row-4 [data-match-action="delete"]').click();

      expect(document.getElementById('deleteMatchID').value).toBe('4');
      expect(document.getElementById('deleteMatchName').textContent).toBe('Round 3 vs Sample Opponent');
      expect(bootstrap.Modal).toHaveBeenCalledWith(document.getElementById('deleteMatchModal'));
    });
  });

  describe('adding a round', () => {
    it('adds the row after the round it follows, patches the summary, and resets the form for the next round with focus on the opponent', async () => {
      buildDom({ rows: rowHtml(1), summary: 'old' });
      document.getElementById('matchEmpty').hidden = true;
      field(addForm(), 'opponent').value = 'Sample Opponent';
      typeScore(addForm(), 2, 0);
      pickResult(addForm(), 'Loss');
      mockFetch({ body: { rowHtml: rowHtml(2, { round: '3' }), previousMatchId: 1, summaryHtml: 'new summary', roundCount: 2, nextRound: '4' } });

      await submit(addForm());

      const [url, options] = globalThis.fetch.mock.calls[0];
      expect(url).toContain('handler=AddMatch');
      expect(options.headers['X-Requested-With']).toBe('XMLHttpRequest');
      expect(options.body.get('Input.OpponentDeck')).toBe('Sample Opponent');
      expect(document.querySelectorAll('#matchList .list-group-item').length).toBe(2);
      expect(document.getElementById('matchSummary').innerHTML).toBe('new summary');
      expect(field(addForm(), 'round').value).toBe('4');
      expect(field(addForm(), 'opponent').value).toBe('');
      expect(field(addForm(), 'won').value).toBe('0');
      expect(checkedResult(addForm())).toBe('Tie');
      expect(addForm().querySelector('[data-result-hint]').hidden).toBe(true);
      expect(document.activeElement).toBe(field(addForm(), 'opponent'));
    });

    it('slots a re-added round between the rounds around it instead of at the end', async () => {
      buildDom({ rows: rowHtml(1, { round: '1' }) + rowHtml(2, { round: '2' }) + rowHtml(4, { round: '4' }) + rowHtml(5, { round: '5' }) });
      const untouched = document.getElementById('match-row-4');
      mockFetch({ body: { rowHtml: rowHtml(9, { round: '3' }), previousMatchId: 2, summaryHtml: '', roundCount: 5, nextRound: '6' } });

      await submit(addForm());

      const order = [...document.querySelectorAll('#matchList .list-group-item')].map((row) => row.dataset.round);
      expect(order).toEqual(['1', '2', '3', '4', '5']);
      expect(document.getElementById('match-row-4')).toBe(untouched);
    });

    it('puts a round that belongs first at the top of the list', async () => {
      buildDom({ rows: rowHtml(2, { round: '2' }) });
      mockFetch({ body: { rowHtml: rowHtml(9, { round: '1' }), previousMatchId: null, summaryHtml: '', roundCount: 2, nextRound: '3' } });

      await submit(addForm());

      expect(document.querySelector('#matchList .list-group-item').dataset.round).toBe('1');
    });

    it('falls back to the end of the list when the round it follows is not on the page', async () => {
      buildDom({ rows: rowHtml(1, { round: '1' }) });
      mockFetch({ body: { rowHtml: rowHtml(9, { round: '3' }), previousMatchId: 404, summaryHtml: '', roundCount: 2, nextRound: '4' } });

      await submit(addForm());

      const order = [...document.querySelectorAll('#matchList .list-group-item')].map((row) => row.dataset.round);
      expect(order).toEqual(['1', '3']);
    });

    it('shows the list and hides the empty state after the first round', async () => {
      buildDom();
      document.getElementById('matchList').hidden = true;
      document.getElementById('matchEmpty').hidden = false;
      document.getElementById('matchSummary').hidden = true;
      mockFetch({ body: { rowHtml: rowHtml(1), summaryHtml: 's', roundCount: 1, nextRound: '2' } });

      await submit(addForm());

      expect(document.getElementById('matchList').hidden).toBe(false);
      expect(document.getElementById('matchEmpty').hidden).toBe(true);
      expect(document.getElementById('matchSummary').hidden).toBe(false);
    });

    it('shows the server validation errors inline and changes nothing else', async () => {
      buildDom({ rows: rowHtml(1) });
      field(addForm(), 'opponent').value = 'Kept';
      mockFetch({ status: 400, body: { errors: ['Opponent deck is required.', 'Round is required.'] } });

      await submit(addForm());

      const box = addForm().querySelector('[data-match-errors]');
      expect(box.hidden).toBe(false);
      expect([...box.querySelectorAll('li')].map((li) => li.textContent)).toEqual(['Opponent deck is required.', 'Round is required.']);
      expect(document.querySelectorAll('#matchList .list-group-item').length).toBe(1);
      expect(field(addForm(), 'opponent').value).toBe('Kept');
    });

    it('explains a missing event or round, and a generic failure', async () => {
      buildDom();
      mockFetch({ status: 404, body: {} });
      await submit(addForm());
      expect(addForm().querySelector('[data-match-errors] li').textContent).toContain('no longer exists');

      mockFetch({ status: 500, body: {} });
      await submit(addForm());
      expect(addForm().querySelector('[data-match-errors] li').textContent).toContain('Something went wrong');
    });

    it('shows a generic error when the request itself fails', async () => {
      buildDom();
      vi.spyOn(console, 'error').mockImplementation(() => {});
      globalThis.fetch = vi.fn().mockRejectedValue(new Error('offline'));

      await submit(addForm());

      expect(addForm().querySelector('[data-match-errors] li').textContent).toContain('Something went wrong');
    });

    it('disables the submit button while saving and re-enables it afterwards', async () => {
      buildDom();
      const button = addForm().querySelector('[type="submit"]');
      let disabledDuringRequest = null;
      globalThis.fetch = vi.fn().mockImplementation(async () => {
        disabledDuringRequest = button.disabled;
        return { ok: true, status: 200, json: async () => ({ rowHtml: rowHtml(1), summaryHtml: '', roundCount: 1, nextRound: '2' }) };
      });

      await submit(addForm());

      expect(disabledDuringRequest).toBe(true);
      expect(button.disabled).toBe(false);
    });
  });

  describe('editing a round', () => {
    it('replaces only the edited row, patches the summary, and closes the modal', async () => {
      buildDom({ rows: rowHtml(1, { opponent: 'First' }) + rowHtml(2, { opponent: 'Second' }) + rowHtml(3, { opponent: 'Third' }) });
      document.querySelector('#match-row-2 [data-match-action="edit"]').click();
      const before = document.getElementById('match-row-3');
      mockFetch({ body: { rowHtml: rowHtml(2, { opponent: 'Changed' }), summaryHtml: 'edited', roundCount: 3, nextRound: '4' } });

      await submit(editForm());

      expect(document.getElementById('match-row-2').dataset.opponent).toBe('Changed');
      expect(document.getElementById('match-row-1').dataset.opponent).toBe('First');
      expect(document.getElementById('match-row-3')).toBe(before);
      expect(document.getElementById('matchSummary').innerHTML).toBe('edited');
      expect(bootstrap.Modal.getInstance(document.getElementById('editMatchModal')).hide).toHaveBeenCalled();
    });

    it('updates the add form round only when the user has not typed their own', async () => {
      buildDom({ rows: rowHtml(1) + rowHtml(2), round: '3' });
      document.querySelector('#match-row-1 [data-match-action="edit"]').click();
      mockFetch({ body: { rowHtml: rowHtml(1), summaryHtml: '', roundCount: 2, nextRound: '4' } });

      await submit(editForm());
      expect(field(addForm(), 'round').value).toBe('4');

      field(addForm(), 'round').value = 'Top 8';
      mockFetch({ body: { rowHtml: rowHtml(1), summaryHtml: '', roundCount: 2, nextRound: '5' } });
      await submit(editForm());

      expect(field(addForm(), 'round').value).toBe('Top 8');
    });

    it('keeps the modal open and shows the errors when the save is rejected', async () => {
      buildDom({ rows: rowHtml(1) });
      document.querySelector('#match-row-1 [data-match-action="edit"]').click();
      mockFetch({ status: 400, body: { errors: ['Round is required.'] } });

      await submit(editForm());

      expect(editForm().querySelector('[data-match-errors] li').textContent).toBe('Round is required.');
      expect(bootstrap.Modal.getInstance(document.getElementById('editMatchModal')).hide).not.toHaveBeenCalled();
    });
  });

  describe('deleting a round', () => {
    it('removes only that row and closes the modal', async () => {
      buildDom({ rows: rowHtml(1) + rowHtml(2) });
      document.querySelector('#match-row-1 [data-match-action="delete"]').click();
      mockFetch({ body: { summaryHtml: 'after delete', roundCount: 1, nextRound: '2' } });

      await submit(document.getElementById('deleteForm'));

      expect(document.getElementById('match-row-1')).toBeNull();
      expect(document.getElementById('match-row-2')).not.toBeNull();
      expect(document.getElementById('matchSummary').innerHTML).toBe('after delete');
      expect(bootstrap.Modal.getInstance(document.getElementById('deleteMatchModal')).hide).toHaveBeenCalled();
    });

    it('shows the empty state when the last round is deleted', async () => {
      buildDom({ rows: rowHtml(1) });
      document.querySelector('#match-row-1 [data-match-action="delete"]').click();
      mockFetch({ body: { summaryHtml: '', roundCount: 0, nextRound: '1' } });

      await submit(document.getElementById('deleteForm'));

      expect(document.getElementById('matchList').hidden).toBe(true);
      expect(document.getElementById('matchEmpty').hidden).toBe(false);
      expect(document.getElementById('matchSummary').hidden).toBe(true);
    });

    it('moves the add form back to the round that was deleted', async () => {
      buildDom({ rows: rowHtml(1) + rowHtml(2), round: '3' });
      document.querySelector('#match-row-2 [data-match-action="delete"]').click();
      mockFetch({ body: { summaryHtml: '', roundCount: 1, nextRound: '2' } });

      await submit(document.getElementById('deleteForm'));

      expect(field(addForm(), 'round').value).toBe('2');
    });

    it('shows an error in the modal when the round no longer exists', async () => {
      buildDom({ rows: rowHtml(1) });
      document.querySelector('#match-row-1 [data-match-action="delete"]').click();
      mockFetch({ status: 404, body: {} });

      await submit(document.getElementById('deleteForm'));

      expect(document.querySelector('#deleteForm [data-match-errors] li').textContent).toContain('no longer exists');
      expect(document.getElementById('match-row-1')).not.toBeNull();
    });
  });

  describe('add round box', () => {
    it('puts the cursor on the opponent when the box is opened', () => {
      buildDom();
      document.body.insertAdjacentHTML('afterbegin', '<div id="addRoundPanel"></div>');
      document.getElementById('addRoundPanel').append(addForm());

      document.getElementById('addRoundPanel').dispatchEvent(new Event('shown.bs.collapse', { bubbles: true }));

      expect(document.activeElement).toBe(field(addForm(), 'opponent'));
    });

    it('ignores other collapses on the page', () => {
      buildDom();
      document.body.insertAdjacentHTML('afterbegin', '<div id="somethingElse"></div>');

      document.getElementById('somethingElse').dispatchEvent(new Event('shown.bs.collapse', { bubbles: true }));

      expect(document.activeElement).not.toBe(field(addForm(), 'opponent'));
    });
  });

  describe('unrelated events', () => {
    it('ignores submits from forms that are not round forms', () => {
      buildDom();
      document.body.insertAdjacentHTML('beforeend', '<form id="other"></form>');
      globalThis.fetch = vi.fn();

      const event = new Event('submit', { bubbles: true, cancelable: true });
      document.getElementById('other').dispatchEvent(event);

      expect(event.defaultPrevented).toBe(false);
      expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('ignores clicks that are not on a round action', () => {
      buildDom({ rows: rowHtml(1) });

      document.getElementById('match-row-1').click();

      expect(bootstrap.Modal).not.toHaveBeenCalled();
    });
  });
});
