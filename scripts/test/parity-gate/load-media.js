#!/usr/bin/env node
// Media load driver for the #929 parity gate.
//
// images mode (S2 — spend ordering/exactly-once): per-key strictly-sequential loops
//   (submit async image gen, poll to terminal state, then next) so per-key spend
//   publish order == seq order; aggregate rate is paced across keys. Every task's
//   spend lands as ledger IdempotencyKey `spend:{task_id}`.
//
// videos mode (S3 — webhook throughput): fire-and-forget async video submissions
//   carrying webhook_url; the webhook sink records deliveries.
//
//   node load-media.js images --keys parity-keys.json --rate 17 --perkey 200 [--log img-log.ndjson]
//   node load-media.js videos --keys parity-keys.json --rate 17 --total 10000 \
//        --webhook http://host.docker.internal:9098/hook [--log vid-log.ndjson]

const fs = require('fs');

const mode = process.argv[2];
const arg = (name, dflt) => {
  const i = process.argv.indexOf('--' + name);
  return i > 0 ? process.argv[i + 1] : dflt;
};
const keysFile = arg('keys', 'parity-keys.json');
const rate = Number(arg('rate', 17));
const gateway = arg('gateway', 'http://localhost:5000');
const logFile = arg('log', mode + '-log.ndjson');
const model = arg('model', mode === 'images' ? 'smoke-image' : 'smoke-video');

const seed = JSON.parse(fs.readFileSync(keysFile, 'utf8'));
const keys = seed.groups.flatMap((g) => g.keys.map((k) => ({ ...k, group: g.name, groupId: g.id })));
if (!keys.length) { console.error('no keys'); process.exit(1); }
const log = fs.createWriteStream(logFile, { flags: 'a' });
const started = Date.now();
let submitted = 0, completed = 0, failed = 0, errors = 0;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function post(path, key, body) {
  const res = await fetch(gateway + path, {
    method: 'POST',
    headers: { authorization: 'Bearer ' + key, 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
  const text = await res.text();
  let json = null;
  try { json = JSON.parse(text); } catch { /* error body */ }
  return { status: res.status, json, text };
}

async function imagesMode() {
  const perKey = Number(arg('perkey', 200));
  const cycleMs = (keys.length / rate) * 1000; // per-key inter-submission gap at target aggregate rate

  async function keyLoop(k, idx) {
    await sleep((cycleMs / keys.length) * idx); // stagger starts
    for (let seq = 1; seq <= perKey; seq++) {
      const cycleStart = Date.now();
      const submittedAt = Date.now();
      let rec = { key: k.name, group: k.group, groupId: k.groupId, seq, submittedAt };
      try {
        const sub = await post('/v1/conduit/images/generations/async', k.key, { model, prompt: `parity ${k.name} seq ${seq}`, n: 1 });
        if (sub.status !== 202 && sub.status !== 200) {
          errors++;
          rec = { ...rec, status: 'submit_error', httpStatus: sub.status, err: sub.text.slice(0, 200) };
        } else {
          submitted++;
          const taskId = sub.json.task_id;
          rec.taskId = taskId;
          // poll to terminal state so this key's next task can't overlap (ordering guarantee)
          let state = 'pending';
          const deadline = Date.now() + 120000;
          while (Date.now() < deadline) {
            await sleep(400);
            const st = await fetch(`${gateway}/v1/images/generations/${taskId}/status`, { headers: { authorization: 'Bearer ' + k.key } });
            const sj = await st.json().catch(() => null);
            state = sj?.status ?? 'unknown';
            if (state === 'completed' || state === 'failed' || state === 'cancelled' || state === 'timedout') break;
          }
          rec.status = state;
          rec.completedAt = Date.now();
          state === 'completed' ? completed++ : failed++;
        }
      } catch (e) {
        errors++;
        rec = { ...rec, status: 'driver_error', err: String(e).slice(0, 200) };
      }
      log.write(JSON.stringify(rec) + '\n');
      const wait = cycleMs - (Date.now() - cycleStart);
      if (wait > 0) await sleep(wait);
    }
  }

  const progress = setInterval(() => {
    const el = (Date.now() - started) / 1000;
    console.log(`[${el.toFixed(0)}s] submitted=${submitted} completed=${completed} failed=${failed} errors=${errors} rate=${(submitted / el).toFixed(1)}/s`);
  }, 10000);

  await Promise.all(keys.map((k, i) => keyLoop(k, i)));
  clearInterval(progress);
}

async function videosMode() {
  const total = Number(arg('total', 10000));
  const webhook = arg('webhook', 'http://host.docker.internal:9098/hook');
  let sent = 0;
  const t0 = Date.now();
  const intervalMs = 1000 / rate;
  await new Promise((resolve) => {
    // absolute-time pacing: fire however many submissions are due this tick so
    // timer drift can't erode the effective rate
    const timer = setInterval(() => {
      if (sent >= total) { clearInterval(timer); resolve(); return; }
      const due = Math.min(total, Math.floor((Date.now() - t0) / intervalMs) + 1);
      while (sent < due) {
      const k = keys[sent % keys.length];
      const submittedAt = Date.now();
      sent++;
      post('/v1/conduit/videos/generations/async', k.key, { model, prompt: `parity webhook load`, webhook_url: webhook })
        .then((sub) => {
          const okStatus = sub.status === 202 || sub.status === 200;
          okStatus ? submitted++ : errors++;
          log.write(JSON.stringify({ key: k.name, submittedAt, taskId: sub.json?.taskId, httpStatus: sub.status, err: okStatus ? undefined : sub.text.slice(0, 150) }) + '\n');
        })
        .catch((e) => { errors++; log.write(JSON.stringify({ key: k.name, submittedAt, err: String(e).slice(0, 150) }) + '\n'); });
      }
    }, 10);
  });
  // let in-flight submissions land
  await sleep(5000);
}

const progress2 = setInterval(() => {
  if (mode === 'videos') {
    const el = (Date.now() - started) / 1000;
    console.log(`[${el.toFixed(0)}s] videos submitted=${submitted} errors=${errors} rate=${(submitted / el).toFixed(1)}/s`);
  }
}, 10000);

(mode === 'images' ? imagesMode() : mode === 'videos' ? videosMode() : Promise.reject(new Error('mode must be images|videos')))
  .then(() => {
    clearInterval(progress2);
    log.end();
    const el = (Date.now() - started) / 1000;
    console.log(JSON.stringify({ mode, submitted, completed, failed, errors, elapsedSec: +el.toFixed(1), effectiveRate: +(submitted / el).toFixed(2), logFile }));
    process.exit(errors > 0 ? 2 : 0);
  })
  .catch((e) => { console.error(e); process.exit(1); });
