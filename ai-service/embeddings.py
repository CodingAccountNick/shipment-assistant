import os
import requests

OLLAMA_EMBED_URL = os.getenv("OLLAMA_EMBED_URL", "http://localhost:11434/api/embed")
EMBED_MODEL = "nomic-embed-text"


def embed(texts: list[str]) -> list[list[float]]:
    """Turn each text into a vector of 768 numbers."""
    response = requests.post(OLLAMA_EMBED_URL, json={"model": EMBED_MODEL, "input": texts}, timeout=120)
    response.raise_for_status()
    return response.json()["embeddings"]