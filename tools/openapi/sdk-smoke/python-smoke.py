import os

from openai import OpenAI


base_url = os.environ.get("CONDUIT_SMOKE_BASE_URL")
if not base_url:
    raise RuntimeError("CONDUIT_SMOKE_BASE_URL is required.")

client = OpenAI(
    api_key=os.environ.get("CONDUIT_SMOKE_API_KEY", "smoke-key"),
    base_url=base_url,
)

chat_model = os.environ.get("CONDUIT_SMOKE_CHAT_MODEL", "chat-model")
embedding_model = os.environ.get("CONDUIT_SMOKE_EMBEDDING_MODEL", "embedding-model")
image_model = os.environ.get("CONDUIT_SMOKE_IMAGE_MODEL", "image-model")
speech_model = os.environ.get("CONDUIT_SMOKE_SPEECH_MODEL", "speech-model")

models = client.models.list()
if not models.data:
    raise RuntimeError("Models list was empty.")
client.models.retrieve(chat_model)

chat = client.chat.completions.create(
    model=chat_model,
    messages=[{"role": "user", "content": "SDK smoke test"}],
)
if not chat.choices:
    raise RuntimeError("Chat completion had no choices.")

model_response = client.responses.create(
    model=chat_model,
    input="SDK smoke test",
    store=False,
)
if model_response.output_text != "ok":
    raise RuntimeError("Responses output text was incorrect.")

streamed_response_text = ""
with client.responses.stream(
    model=chat_model,
    input="SDK smoke test",
    store=False,
) as response_stream:
    for event in response_stream:
        if event.type == "response.output_text.delta":
            streamed_response_text += event.delta
if streamed_response_text != "ok":
    raise RuntimeError("Responses stream output text was incorrect.")

embedding = client.embeddings.create(
    model=embedding_model,
    input="SDK smoke test",
)
if not embedding.data:
    raise RuntimeError("Embedding response had no data.")

image = client.images.generate(
    model=image_model,
    prompt="A small blue square",
)
if not image.data:
    raise RuntimeError("Image response had no data.")

speech = client.audio.speech.create(
    model=speech_model,
    input="SDK smoke test",
    voice="alloy",
)
if not speech.read():
    raise RuntimeError("Speech response was empty.")

print("Official Python SDK smoke test passed.")
