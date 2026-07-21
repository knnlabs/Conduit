#!/usr/bin/env node
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import { generateToDirectory } from './generate-openapi-offline.mjs';

const scriptsDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptsDir, '..', '..');
const generatedFiles = [
  'Services/ConduitLLM.Admin/openapi-admin.json',
  'Services/ConduitLLM.Gateway/openapi-gateway.json',
  'WebAdmin/src/generated/admin-api.ts',
  'WebAdmin/src/generated/gateway-api.ts',
];

const workDirectory = mkdtempSync(path.join(tmpdir(), 'conduit-openapi-determinism-'));
try {
  const first = generateToDirectory('all', path.join(workDirectory, 'first'));
  const second = generateToDirectory('all', path.join(workDirectory, 'second'));
  const changed = generatedFiles.filter((file) => {
    const destination = path.join(repoRoot, file);
    return !readFileSync(first.get(destination)).equals(readFileSync(second.get(destination)));
  });

  if (changed.length > 0) {
    console.error(`OpenAPI generation is not byte-stable: ${changed.join(', ')}`);
    process.exitCode = 1;
  } else {
    console.log('OpenAPI contracts and WebAdmin types are byte-identical across two isolated generations.');
  }
} finally {
  rmSync(workDirectory, { recursive: true, force: true });
}
