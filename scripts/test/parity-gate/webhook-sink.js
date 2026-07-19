#!/usr/bin/env node
// Webhook sink for the #929 parity gate (S3 throughput + deferral timing).
// Records every receipt with a monotonic timestamp; can fail the first N attempts
// per delivery key to exercise the deferred-retry ladder (~4s/8s gaps expected).
//
//   node webhook-sink.js [port] [logFile]    (default 9098, receipts.ndjson next to cwd)
//
// Endpoints:
//   POST /hook            -> 200 (or injected failure); logged
//   POST /hook/fail       -> alias of /hook but subject to fail-first-N even when
//                            global failFirstN is 0 (uses control.pathFailFirstN)
//   GET  /stats           -> counters incl. per-status, distinct keys, first/last receipt
//   GET  /attempts?key=K  -> attempt timestamps for one delivery key (deferral timing)
//   POST /control         -> {"failFirstN":2,"failStatus":500,"pathFailFirstN":2}
//   POST /reset           -> clear counters/attempts and rotate log
//
// Delivery key = `${taskId}:${eventType}` parsed from the JSON body (falls back to
// x-webhook-key header, then raw-body hash) — matches the runbook dedup key.

const http = require('http');
const fs = require('fs');
const path = require('path');

const port = Number(process.argv[2] || 9098);
let logFile = process.argv[3] || path.join(process.cwd(), 'receipts.ndjson');

const control = { failFirstN: 0, failStatus: 500, pathFailFirstN: 2 };
const attempts = new Map(); // key -> [{t, status}]
const stats = { received: 0, ok: 0, failed: 0, distinctKeys: 0, firstAt: null, lastAt: null };
let logStream = fs.createWriteStream(logFile, { flags: 'a' });

function json(res, code, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(code, { 'content-type': 'application/json', 'content-length': Buffer.byteLength(body) });
  res.end(body);
}

function readBody(req) {
  return new Promise((resolve) => {
    let data = '';
    req.on('data', (c) => (data += c));
    req.on('end', () => resolve(data));
  });
}

function deliveryKey(req, body) {
  try {
    const b = JSON.parse(body);
    const taskId = b.taskId ?? b.TaskId ?? b.task_id ?? b.id;
    const evt = b.eventType ?? b.EventType ?? b.event_type ?? b.event ?? b.status ?? '';
    if (taskId) return `${taskId}:${evt}`;
  } catch { /* not json */ }
  if (req.headers['x-webhook-key']) return String(req.headers['x-webhook-key']);
  let h = 0;
  for (let i = 0; i < body.length; i++) h = (h * 31 + body.charCodeAt(i)) | 0;
  return 'raw:' + h;
}

const server = http.createServer(async (req, res) => {
  const url = req.url.split('?')[0];
  const q = new URLSearchParams(req.url.split('?')[1] || '');

  if (url === '/stats') {
    return json(res, 200, { ...control, ...stats, distinctKeys: attempts.size, logFile });
  }
  if (url === '/attempts') {
    const key = q.get('key');
    if (key) return json(res, 200, { key, attempts: attempts.get(key) ?? [] });
    // no key: return keys with >1 attempt (retry candidates) and their gaps
    const retried = {};
    for (const [k, list] of attempts) {
      if (list.length > 1) retried[k] = list.map((a, i) => ({ ...a, gapMs: i ? a.t - list[i - 1].t : 0 }));
    }
    return json(res, 200, retried);
  }
  if (url === '/control' && req.method === 'POST') {
    try { Object.assign(control, JSON.parse(await readBody(req))); } catch { /* keep old */ }
    return json(res, 200, control);
  }
  if (url === '/reset' && req.method === 'POST') {
    attempts.clear();
    stats.received = 0; stats.ok = 0; stats.failed = 0; stats.firstAt = null; stats.lastAt = null;
    logStream.end();
    logFile = logFile.replace(/(\.\d+)?\.ndjson$/, '') + '.' + Date.now() + '.ndjson';
    logStream = fs.createWriteStream(logFile, { flags: 'a' });
    return json(res, 200, { ok: true, logFile });
  }

  const body = await readBody(req);
  const t = Date.now();
  const key = deliveryKey(req, body);
  const list = attempts.get(key) ?? [];
  const attemptNo = list.length + 1;

  const failN = url === '/hook/fail' ? Math.max(control.failFirstN, control.pathFailFirstN) : control.failFirstN;
  const fail = attemptNo <= failN;
  const status = fail ? control.failStatus : 200;

  list.push({ t, status });
  attempts.set(key, list);
  stats.received++;
  fail ? stats.failed++ : stats.ok++;
  stats.firstAt = stats.firstAt ?? t;
  stats.lastAt = t;
  logStream.write(JSON.stringify({ t, key, attemptNo, status, path: url, bytes: body.length }) + '\n');

  return json(res, status, fail ? { error: 'injected failure (parity gate S3)' } : { ok: true });
});

server.listen(port, () => console.log(`[webhook-sink] listening on :${port}, log: ${logFile}`));
