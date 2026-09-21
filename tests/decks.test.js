import { afterEach, describe, expect, it } from 'vitest';
import { loadScript } from './loadScript.js';

function buildDom() {
  document.body.innerHTML = `
    <div id="renameDeckModal"></div>
    <input id="renameDeckID" />
    <input id="renameDeckName" />
    <textarea id="renameDeckNotes"></textarea>
    <div id="deleteDeckModal"></div>
    <input id="deleteDeckID" />
    <strong id="deleteDeckName"></strong><span id="deleteDeckEvents"></span>`;
  loadScript('decks.js');
}

function makeTrigger(data) {
  const button = document.createElement('button');
  Object.entries(data).forEach(([key, value]) => { button.dataset[key] = value; });
  return button;
}

describe('decks.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  describe('openRenameDeckModal', () => {
    it('fills the form from the row and shows the modal', () => {
      buildDom();

      openRenameDeckModal(makeTrigger({ deckId: '4', deckName: 'Sample Deck', deckNotes: 'a note' }));

      expect(document.getElementById('renameDeckID').value).toBe('4');
      expect(document.getElementById('renameDeckName').value).toBe('Sample Deck');
      expect(document.getElementById('renameDeckNotes').value).toBe('a note');
      expect(bootstrap.Modal).toHaveBeenCalledWith(document.getElementById('renameDeckModal'));
    });

    it('leaves the notes empty when the deck has none', () => {
      buildDom();

      openRenameDeckModal(makeTrigger({ deckId: '4', deckName: 'Sample Deck' }));

      expect(document.getElementById('renameDeckNotes').value).toBe('');
    });
  });

  describe('openDeleteDeckModal', () => {
    it('warns how many events will lose their link', () => {
      buildDom();

      openDeleteDeckModal(makeTrigger({ deckId: '4', deckName: 'Sample Deck', deckEvents: '3' }));

      expect(document.getElementById('deleteDeckID').value).toBe('4');
      expect(document.getElementById('deleteDeckName').textContent).toBe('Sample Deck');
      expect(document.getElementById('deleteDeckEvents').textContent).toContain('3 events will lose the link');
      expect(bootstrap.Modal).toHaveBeenCalledWith(document.getElementById('deleteDeckModal'));
    });

    it('uses the singular for one event', () => {
      buildDom();

      openDeleteDeckModal(makeTrigger({ deckId: '4', deckName: 'Sample Deck', deckEvents: '1' }));

      expect(document.getElementById('deleteDeckEvents').textContent).toContain('1 event will lose the link');
    });

    it('says nothing about links when no event uses the deck', () => {
      buildDom();

      openDeleteDeckModal(makeTrigger({ deckId: '4', deckName: 'Sample Deck', deckEvents: '0' }));

      expect(document.getElementById('deleteDeckEvents').textContent).toBe('');
    });
  });
});
