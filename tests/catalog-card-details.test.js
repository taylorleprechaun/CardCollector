import { afterEach, describe, expect, it } from 'vitest';
import { loadScript } from './loadScript.js';

describe('catalog-card-details.js', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  describe('handleFieldEditClick', () => {
    it('Edit fields shows the form and hides the read-only view', () => {
      setupPanel();

      handleFieldEditClick({ target: document.getElementById('open') });

      expect(document.getElementById('view').hidden).toBe(true);
      expect(document.getElementById('editor').hidden).toBe(false);
      expect(document.getElementById('open').hidden).toBe(true);
      expect(document.activeElement).toBe(document.getElementById('name'));
    });

    it('Cancel puts the read-only view back and resets the form', () => {
      setupPanel();
      handleFieldEditClick({ target: document.getElementById('open') });
      document.getElementById('name').value = 'Half-made edit';

      handleFieldEditClick({ target: document.getElementById('cancel') });

      expect(document.getElementById('view').hidden).toBe(false);
      expect(document.getElementById('editor').hidden).toBe(true);
      expect(document.getElementById('open').hidden).toBe(false);
      expect(document.getElementById('name').value).toBe('Synthetic Dragon');
    });

    it('a click outside any edit button changes nothing', () => {
      setupPanel();

      handleFieldEditClick({ target: document.getElementById('view') });

      expect(document.getElementById('view').hidden).toBe(false);
      expect(document.getElementById('editor').hidden).toBe(true);
    });

    it('a click on an icon inside the button still toggles', () => {
      setupPanel();

      handleFieldEditClick({ target: document.getElementById('openIcon') });

      expect(document.getElementById('editor').hidden).toBe(false);
    });
  });

  function setupPanel() {
    document.body.innerHTML = `
      <section data-field-panel>
        <button id="open" type="button" data-field-edit="open"><i id="openIcon"></i>Edit fields</button>
        <div id="view" data-field-view>Read-only fields</div>
        <div id="editor" data-field-editor hidden>
          <form>
            <input id="name" value="Synthetic Dragon" />
            <button id="cancel" type="button" data-field-edit="cancel">Cancel</button>
          </form>
        </div>
      </section>`;
    loadScript('catalog-card-details.js');
  }
});
