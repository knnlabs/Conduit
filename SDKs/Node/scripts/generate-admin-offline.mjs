#!/usr/bin/env node
// Regenerates the Admin OpenAPI spec and the Admin SDK's generated types without
// needing a running Admin API instance:
//   1. `dotnet build` with OpenApiGenerateDocumentsOnBuild=true exports
//      Services/ConduitLLM.Admin/openapi-admin.json at build time.
//   2. openapi-typescript converts it to SDKs/Node/Admin/src/generated/admin-api.ts.
//   3. prettier formats the output so the result is byte-stable across machines.
//
// CI runs this and fails if the committed files differ from the regenerated ones,
// so backend DTO changes and SDK types can never silently drift apart.
//
// Usage: node generate-admin-offline.mjs   (or `npm run generate:admin:offline`)
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const scriptsDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptsDir, '..', '..', '..');
const adminProject = path.join(repoRoot, 'Services', 'ConduitLLM.Admin', 'ConduitLLM.Admin.csproj');
const specPath = path.join(repoRoot, 'Services', 'ConduitLLM.Admin', 'openapi-admin.json');
const typesPath = path.join(scriptsDir, '..', 'Admin', 'src', 'generated', 'admin-api.ts');

const env = { ...process.env };
// Startup configuration requires a syntactically valid connection string, but
// build-time document generation never runs the host, so nothing ever connects.
env.DATABASE_URL ??= 'postgresql://conduit:conduitpass@localhost:5432/conduitdb';

function run(cmd, args, opts = {}) {
  console.log(`> ${cmd} ${args.join(' ')}`);
  const result = spawnSync(cmd, args, {
    stdio: 'inherit',
    shell: process.platform === 'win32',
    env,
    ...opts,
  });
  if (result.error) {
    console.error(result.error.message);
    process.exit(1);
  }
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

run('dotnet', ['build', adminProject, '-p:OpenApiGenerateDocumentsOnBuild=true', '--nologo', '-v', 'q']);
run('npx', ['openapi-typescript', specPath, '-o', typesPath], { cwd: scriptsDir });
run('npx', ['prettier', '--write', typesPath], { cwd: scriptsDir });

console.log(`Regenerated ${path.relative(repoRoot, specPath)}`);
console.log(`Regenerated ${path.relative(repoRoot, typesPath)}`);
