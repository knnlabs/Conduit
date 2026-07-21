import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import chokidar from 'chokidar';

const sdkRoot = fileURLToPath(new URL('../../SDKs/Node/', import.meta.url));
const watchedPaths = [
  'Common/src',
  'Common/tsconfig.json',
  'Common/tsup.config.ts',
  'Gateway/src',
  'Gateway/tsconfig.json',
  'Gateway/tsup.config.ts',
].map((entry) => path.join(sdkRoot, entry));

let buildProcess;
let buildRunning = false;
let buildPending = false;
let debounceTimer;

function runBuild() {
  if (buildRunning) {
    buildPending = true;
    return;
  }

  buildRunning = true;
  console.log('[sdk-watch] SDK source changed; rebuilding workspace sequentially...');
  buildProcess = spawn('npm', ['run', 'build'], {
    cwd: sdkRoot,
    stdio: 'inherit',
  });

  buildProcess.on('exit', (code, signal) => {
    buildProcess = undefined;
    buildRunning = false;

    if (signal) {
      console.log(`[sdk-watch] SDK build stopped by ${signal}.`);
    } else if (code === 0) {
      console.log('[sdk-watch] SDK workspace rebuilt successfully.');
    } else {
      console.error(`[sdk-watch] SDK workspace build failed with code ${code}.`);
    }

    if (buildPending) {
      buildPending = false;
      runBuild();
    }
  });
}

function scheduleBuild() {
  clearTimeout(debounceTimer);
  debounceTimer = setTimeout(runBuild, 300);
}

const watcher = chokidar.watch(watchedPaths, {
  ignoreInitial: true,
  awaitWriteFinish: {
    stabilityThreshold: 200,
    pollInterval: 50,
  },
});

watcher.on('all', scheduleBuild);
watcher.on('ready', () => {
  console.log('[sdk-watch] Watching Common and Gateway SDK sources.');
});
watcher.on('error', (error) => {
  console.error('[sdk-watch] File watcher error:', error);
});

async function shutdown(signal) {
  clearTimeout(debounceTimer);
  if (buildProcess) {
    buildProcess.kill('SIGTERM');
  }
  await watcher.close();
  process.exit(signal === 'SIGINT' ? 130 : 0);
}

process.on('SIGINT', () => void shutdown('SIGINT'));
process.on('SIGTERM', () => void shutdown('SIGTERM'));
