import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './loadScript.js';

function buildDom({ withExistingDecks = true } = {}) {
  document.body.innerHTML = `
    <div id="deckImportModal">
      <h5 id="deckImportModalLabel"></h5>
      <form id="deckImportForm" data-import-url="/import" data-link-url="/link" data-parse-url="/parse" data-unlink-url="/unlink">
        <input type="hidden" name="__RequestVerificationToken" value="test-token" />
        <div id="deckImportErrors" hidden>old error</div>
        <input type="hidden" name="eventID" id="deckImportEventID" />
        <textarea name="text" id="deckImportText">stale text</textarea>
        <div id="deckImportPreview">stale preview</div>
        <a id="deckImportOpenLink" href="#" hidden>Open</a>
        <input name="name" id="deckImportName" />
        <div id="deckImportLinkOthers" hidden>
          <input type="checkbox" name="linkOthers" value="true" id="deckImportLinkOthersInput" />
          <label id="deckImportLinkOthersLabel"></label>
        </div>
        ${withExistingDecks ? `
        <details id="deckImportExistingGroup" open>
          <select name="deckID" id="deckImportExisting">
            <option value="">None</option>
            <option value="7">Sample Deck</option>
          </select>
        </details>` : ''}
        <button type="button" id="deckImportRemove" hidden>Remove deck</button>
        <button type="submit" id="deckImportSubmit">Save</button>
      </form>
    </div>`;
  loadScript('site.js');
  loadScript('deck-import.js');
  globalThis.reloadDeckPage = vi.fn();
}

function makeTrigger(data) {
  const button = document.createElement('button');
  Object.entries(data).forEach(([key, value]) => { button.dataset[key] = value; });
  return button;
}

function jsonResponse(status, body) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

function deferred() {
  let resolve;
  const promise = new Promise((done) => { resolve = done; });
  return { promise, resolve };
}

describe('deck-import.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    delete globalThis.fetch;
    delete globalThis.reloadDeckPage;
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  describe('openDeckImportModal', () => {
    it('fills the modal for an event with a decklist URL and no deck', () => {
      buildDom();

      openDeckImportModal(makeTrigger({
        eventId: '12', eventDeck: 'Sample Deck', eventHasDeck: 'false', eventOthers: '0', eventUrl: 'https://decks.example.test/one'
      }));

      expect(document.getElementById('deckImportModalLabel').textContent).toBe('Import deck');
      expect(document.getElementById('deckImportEventID').value).toBe('12');
      expect(document.getElementById('deckImportName').value).toBe('Sample Deck');
      expect(document.getElementById('deckImportOpenLink').hidden).toBe(false);
      expect(document.getElementById('deckImportOpenLink').getAttribute('href')).toBe('https://decks.example.test/one');
      expect(document.getElementById('deckImportRemove').hidden).toBe(true);
      expect(bootstrap.Modal).toHaveBeenCalledWith(document.getElementById('deckImportModal'));
    });

    it('is titled Add deck and hides the DuelingBook link when the event has no URL', () => {
      buildDom();

      openDeckImportModal(makeTrigger({ eventId: '12', eventDeck: 'Sample Deck', eventHasDeck: 'false', eventOthers: '0' }));

      expect(document.getElementById('deckImportModalLabel').textContent).toBe('Add deck');
      expect(document.getElementById('deckImportOpenLink').hidden).toBe(true);
    });

    it('is titled Change deck and offers Remove deck when the event already has one', () => {
      buildDom();

      openDeckImportModal(makeTrigger({ eventId: '12', eventDeck: 'Sample Deck', eventHasDeck: 'true', eventOthers: '0' }));

      expect(document.getElementById('deckImportModalLabel').textContent).toBe('Change deck');
      expect(document.getElementById('deckImportRemove').hidden).toBe(false);
    });

    it('does not offer a link that is not an http or https address', () => {
      buildDom();

      openDeckImportModal(makeTrigger({ eventId: '12', eventHasDeck: 'false', eventOthers: '0', eventUrl: 'javascript:alert(1)' }));

      expect(document.getElementById('deckImportOpenLink').hidden).toBe(true);
    });

    it('hides the link-others checkbox when no other event shares the URL', () => {
      buildDom();

      openDeckImportModal(makeTrigger({ eventId: '12', eventHasDeck: 'false', eventOthers: '0' }));

      expect(document.getElementById('deckImportLinkOthers').hidden).toBe(true);
    });

    it('shows the link-others checkbox ticked, with the count, when other events share the URL', () => {
      buildDom();
      document.getElementById('deckImportLinkOthersInput').checked = false;

      openDeckImportModal(makeTrigger({ eventId: '12', eventHasDeck: 'false', eventOthers: '3' }));

      expect(document.getElementById('deckImportLinkOthers').hidden).toBe(false);
      expect(document.getElementById('deckImportLinkOthersInput').checked).toBe(true);
      expect(document.getElementById('deckImportLinkOthersLabel').textContent)
        .toBe('Also link 3 other events that use this decklist URL');
    });

    it('uses the singular for one other event', () => {
      buildDom();

      openDeckImportModal(makeTrigger({ eventId: '12', eventHasDeck: 'false', eventOthers: '1' }));

      expect(document.getElementById('deckImportLinkOthersLabel').textContent)
        .toBe('Also link 1 other event that use this decklist URL');
    });

    it('clears what the previous use left behind', () => {
      buildDom();
      document.getElementById('deckImportExisting').value = '7';

      openDeckImportModal(makeTrigger({ eventId: '12', eventHasDeck: 'false', eventOthers: '0' }));

      expect(document.getElementById('deckImportText').value).toBe('');
      expect(document.getElementById('deckImportPreview').textContent).toBe('');
      expect(document.getElementById('deckImportErrors').hidden).toBe(true);
      expect(document.getElementById('deckImportExisting').value).toBe('');
      expect(document.getElementById('deckImportExistingGroup').open).toBe(false);
    });

    it('works when there are no existing decks to choose from', () => {
      buildDom({ withExistingDecks: false });

      expect(() => openDeckImportModal(makeTrigger({ eventId: '12', eventHasDeck: 'false', eventOthers: '0' }))).not.toThrow();
    });
  });

  describe('previewDeckList', () => {
    it('shows the parsed counts', async () => {
      buildDom();
      document.getElementById('deckImportText').value = 'ydke://x';
      globalThis.fetch = vi.fn().mockResolvedValue(jsonResponse(200, { main: 60, extra: 15, side: 15, unknown: 2 }));

      await previewDeckList();

      expect(document.getElementById('deckImportPreview').textContent).toBe('60 main · 15 extra · 15 side · 2 unknown');
    });

    it('posts the form, including the antiforgery token, to the parse URL', async () => {
      buildDom();
      document.getElementById('deckImportText').value = 'ydke://x';
      globalThis.fetch = vi.fn().mockResolvedValue(jsonResponse(200, { main: 1, extra: 0, side: 0, unknown: 0 }));

      await previewDeckList();

      const [url, options] = globalThis.fetch.mock.calls[0];
      expect(url).toBe('/parse');
      expect(options.method).toBe('POST');
      expect(options.headers['X-Requested-With']).toBe('XMLHttpRequest');
      expect(options.body.get('__RequestVerificationToken')).toBe('test-token');
      expect(options.body.get('text')).toBe('ydke://x');
    });

    it('shows the server message in red when the list cannot be parsed', async () => {
      buildDom();
      document.getElementById('deckImportText').value = 'nonsense';
      globalThis.fetch = vi.fn().mockResolvedValue(jsonResponse(400, { errors: ['That does not look like a deck.'] }));

      await previewDeckList();

      const preview = document.getElementById('deckImportPreview');
      expect(preview.textContent).toBe('That does not look like a deck.');
      expect(preview.classList.contains('text-danger')).toBe(true);
    });

    it('clears the preview and skips the request when the box is empty', async () => {
      buildDom();
      document.getElementById('deckImportText').value = '   ';
      globalThis.fetch = vi.fn();

      await previewDeckList();

      expect(document.getElementById('deckImportPreview').textContent).toBe('');
      expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('shows nothing when the server fails', async () => {
      buildDom();
      document.getElementById('deckImportText').value = 'ydke://x';
      globalThis.fetch = vi.fn().mockResolvedValue(jsonResponse(500, null));

      await previewDeckList();

      expect(document.getElementById('deckImportPreview').textContent).toBe('');
    });

    it('shows nothing when the request throws', async () => {
      buildDom();
      document.getElementById('deckImportText').value = 'ydke://x';
      vi.spyOn(console, 'error').mockImplementation(() => {});
      globalThis.fetch = vi.fn().mockRejectedValue(new Error('offline'));

      await previewDeckList();

      expect(document.getElementById('deckImportPreview').textContent).toBe('');
    });

    it('ignores a slow response that has been overtaken by a newer one', async () => {
      buildDom();
      const text = document.getElementById('deckImportText');
      const slow = deferred();
      globalThis.fetch = vi.fn()
        .mockReturnValueOnce(slow.promise)
        .mockResolvedValueOnce(jsonResponse(200, { main: 40, extra: 0, side: 0, unknown: 0 }));

      text.value = 'first';
      const first = previewDeckList();
      text.value = 'second';
      await previewDeckList();
      slow.resolve(jsonResponse(200, { main: 99, extra: 0, side: 0, unknown: 0 }));
      await first;

      expect(document.getElementById('deckImportPreview').textContent).toBe('40 main · 0 extra · 0 side · 0 unknown');
    });

    it('waits for typing to pause before asking the server', async () => {
      vi.useFakeTimers();
      buildDom();
      globalThis.fetch = vi.fn().mockResolvedValue(jsonResponse(200, { main: 1, extra: 0, side: 0, unknown: 0 }));
      const text = document.getElementById('deckImportText');
      text.value = 'ydke://x';

      text.dispatchEvent(new Event('input', { bubbles: true }));
      text.dispatchEvent(new Event('input', { bubbles: true }));
      await vi.advanceTimersByTimeAsync(299);
      expect(globalThis.fetch).not.toHaveBeenCalled();
      await vi.advanceTimersByTimeAsync(1);

      expect(globalThis.fetch).toHaveBeenCalledTimes(1);
    });
  });

  describe('submitting', () => {
    it('imports the pasted list, sending the token, and reloads on success', async () => {
      buildDom();
      const form = document.getElementById('deckImportForm');
      document.getElementById('deckImportText').value = 'ydke://x';
      globalThis.fetch = vi.fn().mockResolvedValue(jsonResponse(200, { ok: true }));

      await postDeckImport(deckImportUrl(form), form);

      const [url, options] = globalThis.fetch.mock.calls[0];
      expect(url).toBe('/import');
      expect(options.body.get('__RequestVerificationToken')).toBe('test-token');
      expect(options.body.get('text')).toBe('ydke://x');
      expect(globalThis.reloadDeckPage).toHaveBeenCalledTimes(1);
    });

    it('links the chosen existing deck instead of importing', () => {
      buildDom();
      document.getElementById('deckImportExisting').value = '7';

      expect(deckImportUrl(document.getElementById('deckImportForm'))).toBe('/link');
    });

    it('imports when no existing deck is chosen, or there are none to choose', () => {
      buildDom();
      expect(deckImportUrl(document.getElementById('deckImportForm'))).toBe('/import');

      buildDom({ withExistingDecks: false });
      expect(deckImportUrl(document.getElementById('deckImportForm'))).toBe('/import');
    });

    it('takes over the form submit and posts it', async () => {
      buildDom();
      globalThis.fetch = vi.fn().mockResolvedValue(jsonResponse(200, { ok: true }));
      const form = document.getElementById('deckImportForm');
      const submit = new Event('submit', { bubbles: true, cancelable: true });

      form.dispatchEvent(submit);

      expect(submit.defaultPrevented).toBe(true);
      await vi.waitFor(() => expect(globalThis.reloadDeckPage).toHaveBeenCalled());
      expect(globalThis.fetch.mock.calls[0][0]).toBe('/import');
    });

    it('shows the server errors and stays open when the list is rejected', async () => {
      buildDom();
      globalThis.fetch = vi.fn().mockResolvedValue(jsonResponse(400, { errors: ['The main deck is empty.', 'Deck name is required.'] }));
      const form = document.getElementById('deckImportForm');

      await postDeckImport('/import', form);

      const box = document.getElementById('deckImportErrors');
      expect(box.hidden).toBe(false);
      expect(box.textContent).toBe('The main deck is empty. Deck name is required.');
      expect(globalThis.reloadDeckPage).not.toHaveBeenCalled();
      expect(document.getElementById('deckImportSubmit').disabled).toBe(false);
    });

    it('explains when the event or deck no longer exists', async () => {
      buildDom();
      globalThis.fetch = vi.fn().mockResolvedValue(jsonResponse(404, {}));

      await postDeckImport('/link', document.getElementById('deckImportForm'));

      expect(document.getElementById('deckImportErrors').textContent).toContain('no longer exists');
    });

    it('shows a generic message for a server failure', async () => {
      buildDom();
      globalThis.fetch = vi.fn().mockResolvedValue(jsonResponse(500, {}));

      await postDeckImport('/import', document.getElementById('deckImportForm'));

      expect(document.getElementById('deckImportErrors').textContent).toContain('Something went wrong');
    });

    it('shows a generic message and re-enables the buttons when the request throws', async () => {
      buildDom();
      vi.spyOn(console, 'error').mockImplementation(() => {});
      globalThis.fetch = vi.fn().mockRejectedValue(new Error('offline'));

      await postDeckImport('/import', document.getElementById('deckImportForm'));

      expect(document.getElementById('deckImportErrors').textContent).toContain('Something went wrong');
      expect(document.getElementById('deckImportSubmit').disabled).toBe(false);
    });

    it('disables the buttons while the request is in flight', async () => {
      buildDom();
      const pending = deferred();
      globalThis.fetch = vi.fn().mockReturnValue(pending.promise);

      const posting = postDeckImport('/import', document.getElementById('deckImportForm'));

      expect(document.getElementById('deckImportSubmit').disabled).toBe(true);
      pending.resolve(jsonResponse(200, { ok: true }));
      await posting;
      expect(document.getElementById('deckImportSubmit').disabled).toBe(false);
    });

    it('posts to the unlink URL when Remove deck is clicked', async () => {
      buildDom();
      globalThis.fetch = vi.fn().mockResolvedValue(jsonResponse(200, { ok: true }));

      document.getElementById('deckImportRemove').click();

      await vi.waitFor(() => expect(globalThis.reloadDeckPage).toHaveBeenCalled());
      expect(globalThis.fetch.mock.calls[0][0]).toBe('/unlink');
    });

    it('ignores clicks elsewhere on the page', () => {
      buildDom();
      globalThis.fetch = vi.fn();

      document.getElementById('deckImportText').click();

      expect(globalThis.fetch).not.toHaveBeenCalled();
    });
  });
});
