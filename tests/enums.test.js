import { describe, expect, it } from 'vitest';
import { loadScript } from './loadScript.js';

describe('enums.js', () => {
  it('exposes CardDefaults matching the server-side enum string values', () => {
    loadScript('enums.js');

    expect(globalThis.CardDefaults).toEqual({
      Acquisition: '1', // AcquisitionMethod.Purchased
      Condition: '4',   // CardCondition.NearMint
      Edition: '0'       // CardEdition.FirstEdition
    });
  });
});
