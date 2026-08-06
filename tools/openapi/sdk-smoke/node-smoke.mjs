import OpenAI from 'openai';

const baseURL = process.env.CONDUIT_SMOKE_BASE_URL;
if (!baseURL) throw new Error('CONDUIT_SMOKE_BASE_URL is required.');

const client = new OpenAI({
  apiKey: process.env.CONDUIT_SMOKE_API_KEY ?? 'smoke-key',
  baseURL,
  maxRetries: 0,
  timeout: 10_000,
});

const chatModel = process.env.CONDUIT_SMOKE_CHAT_MODEL ?? 'chat-model';
const embeddingModel = process.env.CONDUIT_SMOKE_EMBEDDING_MODEL ?? 'embedding-model';
const imageModel = process.env.CONDUIT_SMOKE_IMAGE_MODEL ?? 'image-model';
const speechModel = process.env.CONDUIT_SMOKE_SPEECH_MODEL ?? 'speech-model';

const models = await client.models.list();
if (!models.data.length) throw new Error('Models list was empty.');
await client.models.retrieve(chatModel);

const chat = await client.chat.completions.create({
  model: chatModel,
  messages: [{ role: 'user', content: 'SDK smoke test' }],
});
if (chat.choices[0]?.message?.content !== 'ok') throw new Error('Chat completion text was incorrect.');

const modelResponse = await client.responses.create({
  model: chatModel,
  input: 'SDK smoke test',
  store: false,
});
if (modelResponse.output_text !== 'ok') throw new Error('Responses output text was incorrect.');

const responseStream = await client.responses.create({
  model: chatModel,
  input: 'SDK smoke test',
  store: false,
  stream: true,
});
let streamedResponseText = '';
for await (const event of responseStream) {
  if (event.type === 'response.output_text.delta') streamedResponseText += event.delta;
}
if (streamedResponseText !== 'ok') throw new Error('Responses stream output text was incorrect.');

const embedding = await client.embeddings.create({
  model: embeddingModel,
  input: 'SDK smoke test',
  encoding_format: 'float',
});
if (!embedding.data[0]?.embedding?.length) throw new Error('Embedding response had no vector.');

const image = await client.images.generate({
  model: imageModel,
  prompt: 'A small blue square',
});
if (!image.data?.length) throw new Error('Image response had no data.');

const speech = await client.audio.speech.create({
  model: speechModel,
  input: 'SDK smoke test',
  voice: 'alloy',
});
if ((await speech.arrayBuffer()).byteLength === 0) throw new Error('Speech response was empty.');

console.log('Official Node SDK smoke test passed.');
