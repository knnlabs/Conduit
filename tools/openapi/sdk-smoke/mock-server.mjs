import { spawn } from 'node:child_process';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const directory = path.dirname(fileURLToPath(import.meta.url));
const json = (response, value) => {
  response.writeHead(200, { 'content-type': 'application/json', 'x-request-id': 'smoke' });
  response.end(JSON.stringify(value));
};

const server = http.createServer((request, response) => {
  const url = new URL(request.url, 'http://localhost');
  if (request.method === 'GET' && url.pathname === '/v1/models') {
    return json(response, {
      object: 'list',
      data: [{ id: 'chat-model', object: 'model', created: 1, owned_by: 'conduit' }],
    });
  }
  if (request.method === 'GET' && url.pathname === '/v1/models/chat-model') {
    return json(response, { id: 'chat-model', object: 'model', created: 1, owned_by: 'conduit' });
  }
  if (request.method === 'POST' && url.pathname === '/v1/chat/completions') {
    return json(response, {
      id: 'chatcmpl_smoke',
      object: 'chat.completion',
      created: 1,
      model: 'chat-model',
      choices: [{ index: 0, message: { role: 'assistant', content: 'ok' }, finish_reason: 'stop' }],
      usage: { prompt_tokens: 1, completion_tokens: 1, total_tokens: 2 },
    });
  }
  if (request.method === 'POST' && url.pathname === '/v1/embeddings') {
    return json(response, {
      object: 'list',
      model: 'embedding-model',
      data: [{ object: 'embedding', index: 0, embedding: [0.1] }],
      usage: { prompt_tokens: 1, total_tokens: 1 },
    });
  }
  if (request.method === 'POST' && url.pathname === '/v1/images/generations') {
    return json(response, { created: 1, data: [{ b64_json: 'AA==' }] });
  }
  if (request.method === 'POST' && url.pathname === '/v1/audio/speech') {
    response.writeHead(200, { 'content-type': 'audio/mpeg', 'x-request-id': 'smoke' });
    return response.end(Buffer.from([1, 2, 3]));
  }

  response.writeHead(404, { 'content-type': 'application/json' });
  response.end(JSON.stringify({ error: { message: 'not found', type: 'invalid_request_error' } }));
});

const run = (command, args, environment) =>
  new Promise((resolve, reject) => {
    const child = spawn(command, args, { cwd: directory, env: environment, stdio: 'inherit' });
    child.on('error', reject);
    child.on('exit', (code) =>
      code === 0 ? resolve() : reject(new Error(`${command} exited with ${code}`)));
  });

server.listen(0, '127.0.0.1', async () => {
  const address = server.address();
  const environment = {
    ...process.env,
    CONDUIT_SMOKE_BASE_URL: `http://127.0.0.1:${address.port}/v1`,
  };

  try {
    await run(process.execPath, ['node-smoke.mjs'], environment);
    await run('python', ['python-smoke.py'], environment);
  } finally {
    server.close();
  }
});
