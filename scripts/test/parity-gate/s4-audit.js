#!/usr/bin/env node
// S4 crash fault-injection audit for the #929 parity gate.
// After a mid-load Gateway kill + restart + drain, categorizes every submitted task:
//   completedBilled      — task completed AND ledger row exists (correct)
//   completedUnbilled    — task completed but NO ledger row (LOST SPEND)
//   billedNotCompleted   — ledger row exists but task not completed (bill-before-state)
//   failed               — task failed (no spend expected)
//   stuck                — task still queued/processing after drain (lost task)
//   submitFailed         — driver never got a 202 (Gateway down; not a messaging loss)
//
//   node s4-audit.js --log s4-img.ndjson --keys parity-all-keys.json --since <ISO> \
//        [--gateway http://localhost:5000] [--container e909-postgres-1]

const fs = require('fs');
const { execFileSync } = require('child_process');

const arg = (n, d) => { const i = process.argv.indexOf('--' + n); return i > 0 ? process.argv[i + 1] : d; };
const logFile = arg('log');
const keysFile = arg('keys', 'parity-all-keys.json');
const since = arg('since');
const gateway = arg('gateway', 'http://localhost:5000');
const container = arg('container', 'e909-postgres-1');
if (!logFile || !since) { console.error('--log and --since required'); process.exit(1); }

const seed = JSON.parse(fs.readFileSync(keysFile, 'utf8'));
const keyByName = new Map(seed.groups.flatMap((g) => g.keys.map((k) => [k.name, k.key])));
const records = fs.readFileSync(logFile, 'utf8').split('\n').filter(Boolean).map(JSON.parse);

const sql = `COPY (SELECT "IdempotencyKey" FROM "VirtualKeyGroupTransactions" WHERE "IdempotencyKey" LIKE 'spend:task_%' AND "CreatedAt" >= '${since}') TO STDOUT WITH CSV`;
const billed = new Set(execFileSync('docker', ['exec', container, 'psql', '-U', 'conduit', '-d', 'conduitdb', '-c', sql], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 })
  .split('\n').filter(Boolean).map((l) => l.replace(/^spend:/, '')));

async function taskStatus(taskId, keyName) {
  const key = keyByName.get(keyName);
  try {
    const res = await fetch(`${gateway}/v1/images/generations/${taskId}/status`, { headers: { authorization: 'Bearer ' + key } });
    const j = await res.json().catch(() => null);
    return j?.status ?? `http_${res.status}`;
  } catch (e) { return 'status_error'; }
}

(async () => {
  const cats = { completedBilled: [], completedUnbilled: [], billedNotCompleted: [], failed: [], stuck: [], submitFailed: [], other: [] };
  const withTask = records.filter((r) => r.taskId);
  cats.submitFailed = records.filter((r) => !r.taskId);

  const CONC = 20;
  for (let i = 0; i < withTask.length; i += CONC) {
    await Promise.all(withTask.slice(i, i + CONC).map(async (r) => {
      const status = await taskStatus(r.taskId, r.key);
      const hasBill = billed.has(r.taskId);
      if (status === 'completed' && hasBill) cats.completedBilled.push(r.taskId);
      else if (status === 'completed') cats.completedUnbilled.push(r.taskId);
      else if (hasBill) cats.billedNotCompleted.push({ taskId: r.taskId, status });
      else if (status === 'failed' || status === 'cancelled') cats.failed.push(r.taskId);
      else if (status === 'queued' || status === 'pending' || status === 'processing' || status === 'running') cats.stuck.push({ taskId: r.taskId, status });
      else cats.other.push({ taskId: r.taskId, status });
    }));
  }

  const summary = {
    logFile, since,
    submitted: withTask.length,
    submitFailed: cats.submitFailed.length,
    completedBilled: cats.completedBilled.length,
    completedUnbilled_LOST_SPEND: cats.completedUnbilled.length,
    billedNotCompleted: cats.billedNotCompleted.length,
    failed: cats.failed.length,
    stuck_LOST_TASKS: cats.stuck.length,
    other: cats.other.length,
    samples: {
      completedUnbilled: cats.completedUnbilled.slice(0, 10),
      billedNotCompleted: cats.billedNotCompleted.slice(0, 10),
      stuck: cats.stuck.slice(0, 10),
      other: cats.other.slice(0, 10),
    },
  };
  console.log(JSON.stringify(summary, null, 2));
})();
