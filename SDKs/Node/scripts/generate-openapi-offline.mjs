#!/usr/bin/env node
// Regenerates authoritative OpenAPI contracts and their generated Node SDK types
// without starting infrastructure or a long-running service.
//
// Usage: node generate-openapi-offline.mjs <admin|gateway|all>
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const scriptsDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptsDir, '..', '..', '..');
const openApiTypescriptCli = path.join(scriptsDir, 'node_modules', 'openapi-typescript', 'bin', 'cli.js');
const prettierCli = path.join(scriptsDir, 'node_modules', 'prettier', 'bin', 'prettier.cjs');

const targets = {
  admin: {
    project: path.join(repoRoot, 'Services', 'ConduitLLM.Admin', 'ConduitLLM.Admin.csproj'),
    spec: path.join(repoRoot, 'Services', 'ConduitLLM.Admin', 'openapi-admin.json'),
    types: [
      path.join(repoRoot, 'WebAdmin', 'src', 'generated', 'admin-api.ts'),
    ],
  },
  gateway: {
    project: path.join(repoRoot, 'Services', 'ConduitLLM.Gateway', 'ConduitLLM.Gateway.csproj'),
    spec: path.join(repoRoot, 'Services', 'ConduitLLM.Gateway', 'openapi-gateway.json'),
    types: [
      path.join(scriptsDir, '..', 'Gateway', 'src', 'generated', 'gateway-api.ts'),
      path.join(repoRoot, 'WebAdmin', 'src', 'generated', 'gateway-api.ts'),
    ],
  },
};

const env = {
  ...process.env,
  CONDUIT_OPENAPI_GENERATION: 'true',
  Logging__EventLog__LogLevel__Default: 'None',
};
env.DATABASE_URL ??= 'postgresql://conduit:conduitpass@localhost:5432/conduitdb';

function run(command, args, options = {}) {
  const { attempts = 1, ...spawnOptions } = options;
  for (let attempt = 1; attempt <= attempts; attempt++) {
    console.log(`> ${command} ${args.join(' ')}`);
    const result = spawnSync(command, args, {
      stdio: 'inherit',
      shell: false,
      env,
      ...spawnOptions,
    });

    if (result.error) {
      throw result.error;
    }
    if (result.status === 0) {
      return;
    }
    if (attempt === attempts) {
      process.exit(result.status ?? 1);
    }

    // Windows file scanners can briefly retain the newly generated type file.
    Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 250);
  }
}

export function generate(selection) {
  const names = selection === 'all' ? Object.keys(targets) : [selection];
  if (names.some((name) => !(name in targets))) {
    console.error('Usage: node generate-openapi-offline.mjs <admin|gateway|all>');
    process.exit(2);
  }

  // Build sequentially because both projects share intermediate build assets.
  for (const name of names) {
    const target = targets[name];
    run('dotnet', [
      'build',
      target.project,
      '-p:OpenApiGenerateDocumentsOnBuild=true',
      '--no-restore',
      '--nologo',
      '-v',
      'q',
    ]);
    for (const types of target.types) {
      run(process.execPath, [openApiTypescriptCli, target.spec, '-o', types], {
        cwd: scriptsDir,
      });
      if (process.platform === 'win32') {
        Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 500);
      }
      run(process.execPath, [prettierCli, '--write', types], {
        cwd: scriptsDir,
        attempts: 3,
      });
      console.log(`Regenerated ${path.relative(repoRoot, types)}`);
    }

    console.log(`Regenerated ${path.relative(repoRoot, target.spec)}`);
  }
}

const invokedDirectly =
  process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (invokedDirectly) {
  generate(process.argv[2] ?? 'all');
}
