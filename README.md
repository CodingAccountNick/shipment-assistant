# Shipment Assistant

An AI assistant that answers questions about shipments in plain language: where a package is, when it will arrive, and why it's delayed.

> 🟡 **Work in progress.** I'm building this in the open, so expect frequent changes.

## Structure

| Folder | What it is | Status |
|---|---|---|
| `ai-service/` | Python service that talks to a locally hosted LLM (Llama 3.2 via Ollama) | In progress |
| Visual Studio solution | .NET side of the project | Planned |

## Running the AI service

You'll need [Ollama](https://ollama.com) running locally with the model pulled:

```bash
ollama pull llama3.2
```

Then:

```bash
cd ai-service
python -m venv .venv
.venv\Scripts\activate        # Windows (macOS/Linux: source .venv/bin/activate)
pip install -r requirements.txt
python ask.py
```

## Roadmap

- [x] Send a question to a local Llama 3.2 model and return its answer
- [ ] REST API (FastAPI) with typed request and response models
- [ ] Connect the model to real shipment data so its answers are based on actual records
- [ ] Visual Studio (.NET) solution
- [ ] Web chat interface
- [ ] Docker setup

## Tech

Python · Ollama · Llama 3.2 · FastAPI (planned) · .NET (planned)
