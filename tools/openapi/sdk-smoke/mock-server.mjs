import { spawn } from 'node:child_process';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const directory = path.dirname(fileURLToPath(import.meta.url));
const json = (response, value) => {
  response.writeHead(200, { 'content-type': 'application/json', 'x-request-id': 'smoke' });
  response.end(JSON.stringify(value));
};

const readJsonBody = async (request) => {
  const chunks = [];
  for await (const chunk of request) chunks.push(chunk);
  return chunks.length ? JSON.parse(Buffer.concat(chunks).toString('utf8')) : {};
};

const responseObject = {
  id: 'resp_smoke',
  object: 'response',
  created_at: 1,
  completed_at: 2,
  status: 'completed',
  error: null,
  incomplete_details: null,
  instructions: null,
  model: 'chat-model',
  output: [{
    id: 'msg_smoke',
    type: 'message',
    role: 'assistant',
    status: 'completed',
    content: [{ type: 'output_text', text: 'ok', annotations: [], logprobs: [] }],
  }],
  output_text: 'ok',
  parallel_tool_calls: true,
  metadata: {},
  temperature: 1,
  text: { format: { type: 'text' } },
  tool_choice: 'auto',
  tools: [],
  top_p: 1,
  truncation: 'disabled',
  usage: {
    input_tokens: 1,
    input_tokens_details: { cached_tokens: 0, cache_write_tokens: 0 },
    output_tokens: 1,
    output_tokens_details: { reasoning_tokens: 0 },
    total_tokens: 2,
  },
};

const server = http.createServer(async (request, response) => {
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
  if (request.method === 'POST' && url.pathname === '/v1/responses') {
    const body = await readJsonBody(request);
    if (body.store !== false) {
      response.writeHead(400, { 'content-type': 'application/json' });
      return response.end(JSON.stringify({
        error: {
          message: "The stateless subset requires 'store' to be false.",
          type: 'invalid_request_error',
          code: 'unsupported_parameter',
          param: 'store',
        },
      }));
    }
    if (body.stream === true) {
      response.writeHead(200, {
        'content-type': 'text/event-stream',
        'cache-control': 'no-cache',
        'x-request-id': 'smoke',
      });
      const pendingItem = {
        id: 'msg_smoke',
        type: 'message',
        role: 'assistant',
        status: 'in_progress',
        content: [],
      };
      const completedItem = responseObject.output[0];
      const emptyPart = { type: 'output_text', text: '', annotations: [], logprobs: [] };
      const completedPart = completedItem.content[0];
      const initialResponse = {
        ...responseObject,
        status: 'in_progress',
        completed_at: null,
        output: [],
        output_text: null,
        usage: null,
      };
      const events = [
        { type: 'response.created', response: initialResponse, sequence_number: 1 },
        { type: 'response.in_progress', response: initialResponse, sequence_number: 2 },
        {
          type: 'response.output_item.added',
          output_index: 0,
          item: pendingItem,
          sequence_number: 3,
        },
        {
          type: 'response.content_part.added',
          item_id: 'msg_smoke',
          output_index: 0,
          content_index: 0,
          part: emptyPart,
          sequence_number: 4,
        },
        {
          type: 'response.output_text.delta',
          item_id: 'msg_smoke',
          output_index: 0,
          content_index: 0,
          delta: 'ok',
          logprobs: [],
          sequence_number: 5,
        },
        {
          type: 'response.output_text.done',
          item_id: 'msg_smoke',
          output_index: 0,
          content_index: 0,
          text: 'ok',
          logprobs: [],
          sequence_number: 6,
        },
        {
          type: 'response.content_part.done',
          item_id: 'msg_smoke',
          output_index: 0,
          content_index: 0,
          part: completedPart,
          sequence_number: 7,
        },
        {
          type: 'response.output_item.done',
          output_index: 0,
          item: completedItem,
          sequence_number: 8,
        },
        { type: 'response.completed', response: responseObject, sequence_number: 9 },
      ];
      for (const event of events) {
        response.write(`event: ${event.type}\ndata: ${JSON.stringify(event)}\n\n`);
      }
      return response.end();
    }
    return json(response, responseObject);
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
    await run(process.env.CONDUIT_SMOKE_PYTHON ?? 'python', ['python-smoke.py'], environment);
  } finally {
    server.close();
  }
});
