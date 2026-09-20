import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

function buildDom({ openOnLoad = 'false', errors = false } = {}) {
  document.body.innerHTML = `
    <div id="eventModal" data-open-on-load="${openOnLoad}">
      <h5 id="eventModalLabel"></h5>
      <form id="eventForm">
        ${errors ? '<div id="eventErrors"></div>' : ''}
        <input type="hidden" id="Input_ID" value="0" />
        <input id="Input_Date" />
        <select id="Input_EventType">
          <option value="Locals">Locals</option>
          <option value="Regional">Regional</option>
        </select>
        <input id="Input_Location" />
        <input id="Input_DeckName" />
        <input id="Input_DecklistURL" />
        <input id="Input_Finish" />
        <input id="Input_Players" />
        <input id="Input_TopCut" />
        <input id="Input_FinishNote" />
        <textarea id="Input_Notes"></textarea>
      </form>
    </div>
    <div id="deleteEventModal"></div>
    <input id="deleteEventID" />
    <strong id="deleteEventName"></strong><span id="deleteEventRounds"></span>`;
  globalThis.setPickerDate = vi.fn((id, value) => { document.getElementById(id).value = value; });
  loadScript('events.js');
}

function makeTrigger(data) {
  const button = document.createElement('button');
  Object.entries(data).forEach(([key, value]) => { button.dataset[key] = value; });
  return button;
}

describe('events.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    delete globalThis.setPickerDate;
    vi.restoreAllMocks();
  });

  describe('openEventModal', () => {
    it('fills the form from the edit button and shows the modal', () => {
      buildDom();
      const trigger = makeTrigger({
        eventId: '12',
        eventDate: '2024-05-04',
        eventDeck: 'Sample Deck',
        eventFinish: '2',
        eventFinishNote: 'a note',
        eventLocation: 'Test Hobby Shop',
        eventNotes: 'line one\nline two',
        eventPlayers: '16',
        eventTopCut: 'Top 8',
        eventType: 'Regional',
        eventUrl: 'https://example.test/deck'
      });

      openEventModal(trigger);

      expect(document.getElementById('eventModalLabel').textContent).toBe('Edit Event');
      expect(document.getElementById('Input_ID').value).toBe('12');
      expect(globalThis.setPickerDate).toHaveBeenCalledWith('Input_Date', '2024-05-04');
      expect(document.getElementById('Input_EventType').value).toBe('Regional');
      expect(document.getElementById('Input_Location').value).toBe('Test Hobby Shop');
      expect(document.getElementById('Input_DeckName').value).toBe('Sample Deck');
      expect(document.getElementById('Input_DecklistURL').value).toBe('https://example.test/deck');
      expect(document.getElementById('Input_Finish').value).toBe('2');
      expect(document.getElementById('Input_Players').value).toBe('16');
      expect(document.getElementById('Input_TopCut').value).toBe('Top 8');
      expect(document.getElementById('Input_FinishNote').value).toBe('a note');
      expect(document.getElementById('Input_Notes').value).toBe('line one\nline two');
      expect(bootstrap.Modal).toHaveBeenCalledWith(document.getElementById('eventModal'));
    });

    it('resets the form for a new event, defaulting the type to Locals', () => {
      buildDom();
      document.getElementById('Input_ID').value = '12';
      document.getElementById('Input_Location').value = 'Left over';
      document.getElementById('Input_EventType').value = 'Regional';

      openEventModal(null);

      expect(document.getElementById('eventModalLabel').textContent).toBe('Add Event');
      expect(document.getElementById('Input_ID').value).toBe('0');
      expect(document.getElementById('Input_Location').value).toBe('');
      expect(document.getElementById('Input_EventType').value).toBe('Locals');
      expect(globalThis.setPickerDate).toHaveBeenCalledWith('Input_Date', '');
    });

    it('clears blank optional fields when editing an event that has none', () => {
      buildDom();
      document.getElementById('Input_Players').value = '99';

      openEventModal(makeTrigger({ eventId: '3', eventLocation: 'Shop', eventDeck: 'Deck', eventType: 'Locals' }));

      expect(document.getElementById('Input_Players').value).toBe('');
      expect(document.getElementById('Input_DecklistURL').value).toBe('');
    });

    it('removes a stale validation error box', () => {
      buildDom({ errors: true });

      openEventModal(null);

      expect(document.getElementById('eventErrors')).toBeNull();
    });
  });

  describe('openDeleteEventModal', () => {
    it('shows the event and how many rounds go with it', () => {
      buildDom();

      openDeleteEventModal(makeTrigger({ eventId: '8', eventLocation: 'Test Hobby Shop', eventRounds: '5' }));

      expect(document.getElementById('deleteEventID').value).toBe('8');
      expect(document.getElementById('deleteEventName').textContent).toBe('Test Hobby Shop');
      expect(document.getElementById('deleteEventRounds').textContent).toBe(' and its 5 rounds');
      expect(bootstrap.Modal).toHaveBeenCalledWith(document.getElementById('deleteEventModal'));
    });

    it('uses the singular for one round', () => {
      buildDom();

      openDeleteEventModal(makeTrigger({ eventId: '8', eventLocation: 'Shop', eventRounds: '1' }));

      expect(document.getElementById('deleteEventRounds').textContent).toBe(' and its 1 round');
    });

    it('says nothing about rounds when there are none', () => {
      buildDom();

      openDeleteEventModal(makeTrigger({ eventId: '8', eventLocation: 'Shop', eventRounds: '0' }));

      expect(document.getElementById('deleteEventRounds').textContent).toBe('');
    });
  });

  describe('page init', () => {
    it('reopens the modal when the server asked for it after a failed save', () => {
      buildDom({ openOnLoad: 'true' });

      expect(bootstrap.Modal).toHaveBeenCalledWith(document.getElementById('eventModal'));
    });

    it('leaves the modal closed on a normal load', () => {
      buildDom();

      expect(bootstrap.Modal).not.toHaveBeenCalled();
    });
  });
});
