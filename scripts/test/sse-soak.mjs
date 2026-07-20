const baseUrl = (process.env.CONDUIT_BASE_URL ?? "http://localhost:5000").replace(/\/$/, "");
const apiKey = process.env.CONDUIT_API_KEY;
const model = process.env.CONDUIT_MODEL;
const concurrency = Number(process.env.CONDUIT_STREAM_CONCURRENCY ?? 20);
const requestsPerWorker = Number(process.env.CONDUIT_STREAM_REQUESTS_PER_WORKER ?? 5);
const maxTokens = Number(process.env.CONDUIT_STREAM_MAX_TOKENS ?? 128);

if (!apiKey || !model) {
  throw new Error("CONDUIT_API_KEY and CONDUIT_MODEL are required");
}

const firstFlushMs = [];
const durationsMs = [];
let failures = 0;

async function runRequest(worker, iteration) {
  const started = performance.now();
  const response = await fetch(`${baseUrl}/v1/chat/completions`, {
    method: "POST",
    headers: {
      authorization: `Bearer ${apiKey}`,
      "content-type": "application/json",
      accept: "text/event-stream",
    },
    body: JSON.stringify({
      model,
      stream: true,
      max_tokens: maxTokens,
      messages: [{ role: "user", content: `Count from 1 to 20 slowly. Run ${worker}-${iteration}.` }],
    }),
  });

  if (!response.ok || !response.body) {
    throw new Error(`HTTP ${response.status}: ${await response.text()}`);
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffered = "";
  let sawFirstEvent = false;
  let sawDone = false;

  while (true) {
    const { value, done } = await reader.read();
    if (done) break;
    buffered += decoder.decode(value, { stream: true });
    const frames = buffered.split("\n\n");
    buffered = frames.pop() ?? "";
    for (const frame of frames) {
      if (!frame.includes("data:")) continue;
      if (!sawFirstEvent) {
        sawFirstEvent = true;
        firstFlushMs.push(performance.now() - started);
      }
      if (frame.includes("data: [DONE]")) sawDone = true;
    }
  }

  durationsMs.push(performance.now() - started);
  if (!sawFirstEvent || !sawDone) {
    throw new Error(`Incomplete stream: first=${sawFirstEvent}, done=${sawDone}`);
  }
}

async function worker(workerId) {
  for (let iteration = 0; iteration < requestsPerWorker; iteration += 1) {
    try {
      await runRequest(workerId, iteration);
    } catch (error) {
      failures += 1;
      console.error(`[${workerId}-${iteration}] ${error.message}`);
    }
  }
}

function percentile(values, fraction) {
  if (values.length === 0) return null;
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * fraction))];
}

await Promise.all(Array.from({ length: concurrency }, (_, index) => worker(index)));

console.log(JSON.stringify({
  requests: concurrency * requestsPerWorker,
  failures,
  firstFlushMs: {
    p50: percentile(firstFlushMs, 0.50),
    p95: percentile(firstFlushMs, 0.95),
    p99: percentile(firstFlushMs, 0.99),
  },
  durationMs: {
    p50: percentile(durationsMs, 0.50),
    p95: percentile(durationsMs, 0.95),
    p99: percentile(durationsMs, 0.99),
  },
}, null, 2));

if (failures > 0) process.exitCode = 1;
