import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';

const root = process.cwd();
const forbidden = [
  '@knn_labs/conduit-admin-client',
  '@knn_labs/conduit-gateway-client',
  '@knn_labs/conduit-common',
  'SDKs/Node/Admin',
  'SDKs\\Node\\Admin',
  'SDKs/Node/Gateway',
  'SDKs\\Node\\Gateway',
  'SDKs/Node/Common',
  'SDKs\\Node\\Common',
];
const violations = [];

async function scan(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      await scan(fullPath);
      continue;
    }
    if (!/\.(?:ts|tsx|js|mjs|json)$/.test(entry.name)) continue;
    const contents = await readFile(fullPath, 'utf8');
    if (forbidden.some((value) => contents.includes(value))) {
      violations.push(path.relative(root, fullPath));
    }
  }
}

await scan(path.join(root, 'src'));
const packageJson = await readFile(path.join(root, 'package.json'), 'utf8');
if (forbidden.some((value) => packageJson.includes(value))) violations.push('package.json');
const packageLock = await readFile(path.join(root, 'package-lock.json'), 'utf8');
if (forbidden.some((value) => packageLock.includes(value))) violations.push('package-lock.json');

if (violations.length > 0) {
  console.error(`Forbidden SDK coupling found:\n${violations.join('\n')}`);
  process.exit(1);
}

console.log('WebAdmin Admin and Gateway API boundaries are local and contract-derived.');
