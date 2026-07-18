#!/usr/bin/env node
// Seed the #929 parity-gate dataset via the Admin API: 10 virtual-key groups
// ("parity-g01".."parity-g10", $250 balance each) x 5 keys per group = 50 keys.
// Idempotent: skips groups that already exist by name; keys are only created for
// groups created in this run (key values are non-retrievable later).
//
//   CONDUIT_MASTER_KEY=... node seed-parity.js [adminBaseUrl] [outFile]
//     default adminBaseUrl http://localhost:5002, outFile parity-keys.json

const fs = require('fs');

const adminBase = process.argv[2] || 'http://localhost:5002';
const outFile = process.argv[3] || 'parity-keys.json';
const masterKey = process.env.CONDUIT_MASTER_KEY;
if (!masterKey) { console.error('CONDUIT_MASTER_KEY not set'); process.exit(1); }

const GROUPS = Number(process.env.PARITY_GROUPS ?? 10);
const KEYS_PER_GROUP = Number(process.env.PARITY_KEYS_PER_GROUP ?? 5);
const BALANCE = Number(process.env.PARITY_BALANCE ?? 250);
const PREFIX = process.env.PARITY_PREFIX ?? 'parity-g';
const headers = { 'X-Master-Key': masterKey, 'content-type': 'application/json' };

async function api(method, path, body) {
  const res = await fetch(adminBase + path, { method, headers, body: body ? JSON.stringify(body) : undefined });
  const text = await res.text();
  if (!res.ok) throw new Error(`${method} ${path} -> ${res.status}: ${text.slice(0, 300)}`);
  return text ? JSON.parse(text) : null;
}

(async () => {
  const existing = await api('GET', '/api/virtualkeygroups?pageSize=100');
  const byName = new Map((existing?.items ?? []).map((g) => [g.groupName, g]));
  const out = { seededAt: new Date().toISOString(), adminBase, groups: [] };

  for (let g = 1; g <= GROUPS; g++) {
    const name = `${PREFIX}${String(g).padStart(2, '0')}`;
    let group = byName.get(name);
    let created = false;
    if (!group) {
      group = await api('POST', '/api/virtualkeygroups', { groupName: name, initialBalance: BALANCE });
      created = true;
      console.log(`created group ${name} id=${group.id}`);
    } else {
      console.log(`group ${name} exists (id=${group.id}) — keys not recreated`);
    }
    const entry = { id: group.id, name, keys: [] };
    if (created) {
      for (let k = 1; k <= KEYS_PER_GROUP; k++) {
        const keyName = `${name}-k${k}`;
        const resp = await api('POST', '/api/virtualkeys', { keyName, virtualKeyGroupId: group.id });
        entry.keys.push({ id: resp.keyInfo?.id ?? resp.id, name: keyName, key: resp.virtualKey });
      }
      console.log(`  ${KEYS_PER_GROUP} keys created`);
    }
    out.groups.push(entry);
  }

  fs.writeFileSync(outFile, JSON.stringify(out, null, 2));
  console.log(`wrote ${outFile}: ${out.groups.length} groups, ${out.groups.reduce((n, g) => n + g.keys.length, 0)} new keys`);
})().catch((e) => { console.error(e.message); process.exit(1); });
