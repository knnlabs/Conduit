#!/usr/bin/env node
// Spend reconciliation for the #929 parity gate (S2 invariants).
// Joins the driver's send log (taskId per key/seq) against the ledger
// (VirtualKeyGroupTransactions, IdempotencyKey = 'spend:{taskId}') and checks:
//   1. exactly-once: each completed task has exactly one keyed ledger row; no dupes
//   2. zero loss: no completed task missing a ledger row
//   3. per-key ordering: ledger insertion order (Id asc) matches seq order per key
//   4. per-group money: SUM(debits) == completed-count x unit cost, to the cent
//
//   node reconcile-spend.js --log img-log.ndjson --since "2026-07-18T04:00:00Z" \
//        [--unit 0.05] [--container e909-postgres-1] [--db conduitdb] [--user conduit]

const fs = require('fs');
const { execFileSync } = require('child_process');

const arg = (name, dflt) => {
  const i = process.argv.indexOf('--' + name);
  return i > 0 ? process.argv[i + 1] : dflt;
};
const logFile = arg('log', 'images-log.ndjson');
const since = arg('since');
const unit = Number(arg('unit', 0.05));
const container = arg('container', 'e909-postgres-1');
const db = arg('db', 'conduitdb');
const user = arg('user', 'conduit');
if (!since) { console.error('--since <ISO timestamp of run start> required'); process.exit(1); }

// --- load send log ---
const records = fs.readFileSync(logFile, 'utf8').split('\n').filter(Boolean).map((l) => JSON.parse(l));
const completedTasks = records.filter((r) => r.status === 'completed' && r.taskId);
const nonCompleted = records.filter((r) => r.status !== 'completed');
const byTask = new Map(completedTasks.map((r) => [r.taskId, r]));

// --- pull keyed ledger rows ---
const sql = `COPY (SELECT "Id","VirtualKeyGroupId","Amount","ReferenceId","IdempotencyKey" FROM "VirtualKeyGroupTransactions" WHERE "IdempotencyKey" LIKE 'spend:task_%' AND "CreatedAt" >= '${since}' ORDER BY "Id") TO STDOUT WITH CSV`;
const csv = execFileSync('docker', ['exec', container, 'psql', '-U', user, '-d', db, '-c', sql], { encoding: 'utf8', maxBuffer: 256 * 1024 * 1024 });
const rows = csv.split('\n').filter(Boolean).map((l) => {
  const [id, groupId, amount, refId, ikey] = l.split(',');
  return { id: Number(id), groupId: Number(groupId), amount: Number(amount), keyId: refId, taskId: ikey.replace(/^spend:/, '') };
});

const problems = [];

// 1 + 2: exactly-once & zero loss
const ledgerByTask = new Map();
for (const r of rows) {
  if (ledgerByTask.has(r.taskId)) problems.push({ type: 'DUPLICATE_LEDGER_ROW', taskId: r.taskId });
  ledgerByTask.set(r.taskId, r);
}
const missing = completedTasks.filter((t) => !ledgerByTask.has(t.taskId));
for (const m of missing.slice(0, 20)) problems.push({ type: 'MISSING_SPEND', taskId: m.taskId, key: m.key, seq: m.seq });
const unexpected = rows.filter((r) => !byTask.has(r.taskId));

// 3: per-key ordering (ledger Id ascending must yield ascending seq per key)
let orderViolations = 0;
const perKeyLastSeq = new Map();
for (const r of rows) {
  const t = byTask.get(r.taskId);
  if (!t) continue;
  const last = perKeyLastSeq.get(t.key) ?? 0;
  if (t.seq <= last) { orderViolations++; if (orderViolations <= 20) problems.push({ type: 'ORDER_VIOLATION', key: t.key, seq: t.seq, after: last }); }
  else perKeyLastSeq.set(t.key, t.seq);
}

// 4: per-group money
const groupExpected = new Map();
for (const t of completedTasks) groupExpected.set(t.groupId, (groupExpected.get(t.groupId) ?? 0) + 1);
const groupApplied = new Map();
for (const r of rows) { if (byTask.has(r.taskId)) groupApplied.set(r.groupId, +((groupApplied.get(r.groupId) ?? 0) + r.amount).toFixed(6)); }
const moneyMismatches = [];
for (const [gid, count] of groupExpected) {
  const expected = +(count * unit).toFixed(6);
  const applied = groupApplied.get(gid) ?? 0;
  if (Math.abs(expected - applied) > 0.000001) moneyMismatches.push({ groupId: gid, expected, applied });
}
problems.push(...moneyMismatches.map((m) => ({ type: 'MONEY_MISMATCH', ...m })));

const summary = {
  logFile, since, unit,
  driver: { records: records.length, completed: completedTasks.length, nonCompleted: nonCompleted.length },
  ledger: { keyedRows: rows.length, distinctTasks: ledgerByTask.size, unexpectedRows: unexpected.length },
  checks: {
    exactlyOnce: rows.length === ledgerByTask.size && missing.length === 0,
    zeroLoss: missing.length === 0,
    missingCount: missing.length,
    perKeyOrdering: orderViolations === 0,
    orderViolations,
    perGroupMoney: moneyMismatches.length === 0,
    totalApplied: +rows.filter((r) => byTask.has(r.taskId)).reduce((s, r) => s + r.amount, 0).toFixed(6),
    totalExpected: +(completedTasks.length * unit).toFixed(6),
  },
  problems: problems.slice(0, 50),
  PASS: missing.length === 0 && orderViolations === 0 && moneyMismatches.length === 0 && rows.length === ledgerByTask.size,
};
console.log(JSON.stringify(summary, null, 2));
process.exit(summary.PASS ? 0 : 1);
