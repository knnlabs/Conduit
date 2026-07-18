#!/usr/bin/env node
// Mock OpenAI-compatible provider for the #929 parity gate (S2/S3/S5 load runs).
// Deterministic responses so spend reconciliation is exact; no dependencies.
//
//   node mock-provider.js [port]           (default 9099)
//
// Endpoints:
//   POST /v1/chat/completions   -> fixed usage (100 prompt / 50 completion) unless
//                                  overridden by x-mock-prompt-tokens / x-mock-completion-tokens
//   POST /v1/images/generations -> 1x1 PNG as b64_json
//   POST /v1/models | GET /v1/models -> minimal model list (connectivity checks)
//   GET  /control               -> current control state
//   POST /control               -> {"failRate":0..1,"failStatus":500,"delayMs":0}
//   GET  /stats                 -> request counters
//   POST /reset                 -> zero counters
//
// Failure injection (S5 backpressure): failRate applies to /v1/* endpoints.

const http = require('http');
const port = Number(process.argv[2] || 9099);

const control = { failRate: 0, failStatus: 500, delayMs: 0 };
const stats = { chat: 0, images: 0, failed: 0, other: 0, startedAt: new Date().toISOString() };

// deterministic "random" for failRate so runs are reproducible
let seq = 0;
function shouldFail() {
  if (control.failRate <= 0) return false;
  seq++;
  return (seq % 1000) < control.failRate * 1000;
}

const PNG_1X1 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==';

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

const server = http.createServer(async (req, res) => {
  const url = req.url.split('?')[0];

  if (url === '/control' && req.method === 'GET') return json(res, 200, control);
  if (url === '/control' && req.method === 'POST') {
    try { Object.assign(control, JSON.parse(await readBody(req))); } catch { /* keep old */ }
    return json(res, 200, control);
  }
  if (url === '/stats') return json(res, 200, stats);
  if (url === '/reset' && req.method === 'POST') {
    stats.chat = 0; stats.images = 0; stats.failed = 0; stats.other = 0; seq = 0;
    return json(res, 200, stats);
  }

  const body = await readBody(req);
  if (control.delayMs > 0) await new Promise((r) => setTimeout(r, control.delayMs));

  if (url.endsWith('/models')) {
    return json(res, 200, { object: 'list', data: [{ id: 'parity-mock', object: 'model', owned_by: 'mock' }] });
  }

  if (shouldFail()) {
    stats.failed++;
    return json(res, control.failStatus, { error: { message: 'injected failure (parity gate S5)', type: 'server_error' } });
  }

  if (url.endsWith('/chat/completions')) {
    stats.chat++;
    let model = 'parity-mock';
    try { model = JSON.parse(body).model || model; } catch { /* default */ }
    const p = Number(req.headers['x-mock-prompt-tokens'] || 100);
    const c = Number(req.headers['x-mock-completion-tokens'] || 50);
    return json(res, 200, {
      id: 'chatcmpl-mock-' + ++seq,
      object: 'chat.completion',
      created: Math.floor(Date.now() / 1000),
      model,
      choices: [{ index: 0, message: { role: 'assistant', content: 'ok' }, finish_reason: 'stop' }],
      usage: { prompt_tokens: p, completion_tokens: c, total_tokens: p + c },
    });
  }

  if (url.endsWith('/images/generations')) {
    stats.images++;
    return json(res, 200, { created: Math.floor(Date.now() / 1000), data: [{ b64_json: PNG_1X1 }] });
  }

  // ---- MiniMax-compatible video API (parity gate S3: real completed-task webhooks) ----
  if (url.endsWith('/video_generation') && req.method === 'POST') {
    stats.videos = (stats.videos || 0) + 1;
    return json(res, 200, { task_id: 'mockvid-' + ++seq, base_resp: { status_code: 0, status_msg: 'ok' } });
  }
  if (url.endsWith('/query/video_generation')) {
    const taskId = new URLSearchParams(req.url.split('?')[1] || '').get('task_id') || 'unknown';
    return json(res, 200, {
      task_id: taskId,
      status: 'Success',
      file_id: 'file-' + taskId,
      video_width: 1280,
      video_height: 720,
      video: { url: `http://host.docker.internal:${port}/files/${taskId}.mp4`, duration: 6 },
      base_resp: { status_code: 0, status_msg: 'ok' },
    });
  }
  if (url.startsWith('/files/')) {
    // minimal ftyp box — enough to be stored as an mp4 payload
    const bytes = Buffer.from('AAAAGGZ0eXBpc29tAAACAGlzb21pc28y', 'base64');
    res.writeHead(200, { 'content-type': 'video/mp4', 'content-length': bytes.length });
    return res.end(bytes);
  }

  stats.other++;
  return json(res, 200, { ok: true, note: 'parity mock: unmatched route ' + url });
});

server.listen(port, () => console.log(`[mock-provider] listening on :${port}`));
