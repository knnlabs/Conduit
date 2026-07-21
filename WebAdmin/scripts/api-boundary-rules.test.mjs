import assert from 'node:assert/strict';
import test from 'node:test';

import { usesGenericModelTransport } from './api-boundary-rules.mjs';

test('rejects every generic compatibility method in model-family services', () => {
  for (const method of ['get', 'post', 'put', 'delete']) {
    assert.equal(usesGenericModelTransport(`return this.client['${method}']('/api/Model');`), true);
  }
});

test('allows generated contract operations', () => {
  assert.equal(
    usesGenericModelTransport(
      "return this.client['executeContractOperation']('/api/Model', method, operation);",
    ),
    false,
  );
});
