#!/usr/bin/env node
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import { generate } from './generate-openapi-offline.mjs';

const scriptsDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptsDir, '..', '..');
const generatedFiles = [
  'Services/ConduitLLM.Admin/openapi-admin.json',
  'Services/ConduitLLM.Gateway/openapi-gateway.json',
  'WebAdmin/src/generated/admin-api.ts',
  'WebAdmin/src/generated/gateway-api.ts',
];

generate('all');
const first = new Map(
  generatedFiles.map((file) => [file, readFileSync(path.join(repoRoot, file))]),
);

generate('all');
const changed = generatedFiles.filter(
  (file) => !first.get(file).equals(readFileSync(path.join(repoRoot, file))),
);

if (changed.length > 0) {
  console.error(`OpenAPI generation is not byte-stable: ${changed.join(', ')}`);
  process.exit(1);
}

console.log('OpenAPI contracts and WebAdmin types are byte-identical across two generations.');
