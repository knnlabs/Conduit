#!/usr/bin/env node
// Chat-completion load driver for the #929 parity gate.
// Sends requests at a fixed rate, round-robin across all seeded keys, embedding a
// per-key sequence number for ordering reconciliation. Writes an NDJSON send log.
//
//   node load-chat.js --keys parity-keys.json --rate 17 --total 10000 \
//        [--model smoke-mock] [--gateway http://localhost:5000] [--log send-log.ndjson]

const fs = require('fs');

const arg = (name, dflt) => {
  const i = process.argv.indexOf('--' + name);
  return i > 0 ? process.argv[i + 1] : dflt;
};
const keysFile = arg('keys', 'parity-keys.json');
const rate = Number(arg('rate', 17));
const total = Number(arg('total', 10000));
const model = arg('model', 'smoke-mock');
const gateway = arg('gateway', 'http://localhost:5000');
const logFile = arg('log', 'send-log.ndjson');

const seed = JSON.parse(fs.readFileSync(keysFile, 'utf8'));
const keys = seed.groups.flatMap((g) => g.keys.map((k) => ({ ...k, group: g.name, groupId: g.id })));
if (!keys.length) { console.error('no keys in ' + keysFile); process.exit(1); }

const log = fs.createWriteStream(logFile, { flags: 'a' });
const perKeySeq = new Map();
let sent = 0, done = 0, ok = 0, failed = 0, inFlight = 0, maxInFlight = 0;
const started = Date.now();

async function fire(i) {
  const k = keys[i % keys.length];
  const seq = (perKeySeq.get(k.name) ?? 0) + 1;
  perKeySeq.set(k.name, seq);
  const sentAt = Date.now();
  inFlight++; maxInFlight = Math.max(maxInFlight, inFlight);
  try {
    const res = await fetch(gateway + '/v1/chat/completions', {
      method: 'POST',
      headers: { authorization: 'Bearer ' + k.key, 'content-type': 'application/json' },
      body: JSON.stringify({
        model,
        messages: [{ role: 'user', content: `parity ${k.name} seq ${seq}` }],
      }),
    });
    const body = await res.text();
    let respId = null;
    try { respId = JSON.parse(body).id; } catch { /* non-json error body */ }
    res.ok ? ok++ : failed++;
    log.write(JSON.stringify({ sentAt, key: k.name, group: k.group, groupId: k.groupId, seq, status: res.status, latencyMs: Date.now() - sentAt, respId, err: res.ok ? undefined : body.slice(0, 200) }) + '\n');
  } catch (e) {
    failed++;
    log.write(JSON.stringify({ sentAt, key: k.name, group: k.group, groupId: k.groupId, seq, status: 0, latencyMs: Date.now() - sentAt, err: String(e).slice(0, 200) }) + '\n');
  } finally {
    inFlight--; done++;
  }
}

const interval = 1000 / rate;
const timer = setInterval(() => {
  if (sent >= total) { clearInterval(timer); return; }
  fire(sent++);
}, interval);

const progress = setInterval(() => {
  const elapsed = (Date.now() - started) / 1000;
  console.log(`[${elapsed.toFixed(0)}s] sent=${sent} done=${done} ok=${ok} failed=${failed} inFlight=${inFlight} effRate=${(done / elapsed).toFixed(1)}/s`);
  if (done >= total) {
    clearInterval(progress);
    log.end();
    console.log(JSON.stringify({ total, ok, failed, maxInFlight, elapsedSec: +elapsed.toFixed(1), targetRate: rate, effectiveRate: +(done / elapsed).toFixed(2), logFile }));
    process.exit(failed > 0 ? 2 : 0);
  }
}, 5000);
