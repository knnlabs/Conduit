#!/usr/bin/env node
// Regenerates authoritative OpenAPI contracts and WebAdmin-local contract types
// without starting infrastructure or a long-running service.
//
// Usage: node generate-openapi-offline.mjs <admin|gateway|all>
import { spawnSync } from 'node:child_process';
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  renameSync,
  rmSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const scriptsDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptsDir, '..', '..');
const openApiTypescriptCli = path.join(scriptsDir, 'node_modules', 'openapi-typescript', 'bin', 'cli.js');
const prettierCli = path.join(scriptsDir, 'node_modules', 'prettier', 'bin', 'prettier.cjs');

const targets = {
  admin: {
    project: path.join(repoRoot, 'Services', 'ConduitLLM.Admin', 'ConduitLLM.Admin.csproj'),
    assembly: path.join(repoRoot, 'Services', 'ConduitLLM.Admin', 'bin', 'Debug', 'net10.0', 'ConduitLLM.Admin.dll'),
    spec: path.join(repoRoot, 'Services', 'ConduitLLM.Admin', 'openapi-admin.json'),
    types: [
      path.join(repoRoot, 'WebAdmin', 'src', 'generated', 'admin-api.ts'),
    ],
  },
  gateway: {
    project: path.join(repoRoot, 'Services', 'ConduitLLM.Gateway', 'ConduitLLM.Gateway.csproj'),
    assembly: path.join(repoRoot, 'Services', 'ConduitLLM.Gateway', 'bin', 'Debug', 'net10.0', 'ConduitLLM.Gateway.dll'),
    spec: path.join(repoRoot, 'Services', 'ConduitLLM.Gateway', 'openapi-gateway.json'),
    types: [path.join(repoRoot, 'WebAdmin', 'src', 'generated', 'gateway-api.ts')],
  },
};

const env = {
  ...process.env,
  CONDUIT_OPENAPI_GENERATION: 'true',
  Logging__EventLog__LogLevel__Default: 'None',
};
env.DATABASE_URL ??= 'postgresql://conduit:conduitpass@localhost:5432/conduitdb';

function run(command, args, options = {}) {
  console.log(`> ${command} ${args.join(' ')}`);
  const result = spawnSync(command, args, {
    stdio: 'inherit',
    shell: false,
    env,
    ...options,
  });

  if (result.error) {
    throw result.error;
  }
  if (result.status !== 0) {
    throw new Error(`${command} exited with code ${result.status ?? 'unknown'}`);
  }
}

function selectTargets(selection) {
  const names = selection === 'all' ? Object.keys(targets) : [selection];
  if (names.some((name) => !(name in targets))) {
    throw new Error('Usage: node generate-openapi-offline.mjs <admin|gateway|all>');
  }
  return names;
}

function filesEqual(left, right) {
  return existsSync(left) && readFileSync(left).equals(readFileSync(right));
}

function publishFile(source, destination) {
  if (filesEqual(destination, source)) {
    console.log(`Unchanged ${path.relative(repoRoot, destination)}`);
    return;
  }

  mkdirSync(path.dirname(destination), { recursive: true });
  const staged = `${destination}.${process.pid}.tmp`;
  try {
    copyFileSync(source, staged);
    renameSync(staged, destination);
  } finally {
    rmSync(staged, { force: true });
  }
  console.log(`Regenerated ${path.relative(repoRoot, destination)}`);
}

export function generateToDirectory(selection, outputDirectory) {
  const names = selectTargets(selection);
  mkdirSync(outputDirectory, { recursive: true });
  const artifacts = new Map();

  // Build sequentially because both projects share intermediate build assets.
  for (const name of names) {
    const target = targets[name];
    const targetDirectory = path.join(outputDirectory, name);
    mkdirSync(targetDirectory, { recursive: true });
    const generatedSpec = path.join(targetDirectory, path.basename(target.spec));

    run('dotnet', [
      'build',
      target.project,
      '--no-restore',
      '--nologo',
      '-v',
      'q',
    ]);
    run('dotnet', [target.assembly], {
      env: {
        ...env,
        CONDUIT_OPENAPI_OUTPUT: generatedSpec,
      },
    });
    artifacts.set(target.spec, generatedSpec);

    for (const destination of target.types) {
      const generatedTypes = path.join(targetDirectory, path.basename(destination));
      run(process.execPath, [openApiTypescriptCli, generatedSpec, '-o', generatedTypes], {
        cwd: scriptsDir,
      });
      run(process.execPath, [prettierCli, '--write', generatedTypes], {
        cwd: scriptsDir,
      });
      artifacts.set(destination, generatedTypes);
    }
  }

  return artifacts;
}

export function generate(selection) {
  const workDirectory = mkdtempSync(path.join(tmpdir(), 'conduit-openapi-'));
  try {
    const artifacts = generateToDirectory(selection, workDirectory);
    for (const [destination, source] of artifacts) {
      publishFile(source, destination);
    }
  } finally {
    rmSync(workDirectory, { recursive: true, force: true });
  }
}

const invokedDirectly =
  process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (invokedDirectly) {
  try {
    generate(process.argv[2] ?? 'all');
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  }
}
