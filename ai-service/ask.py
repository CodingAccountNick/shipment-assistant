import logging
import os

import requests

from rag import Document, search_documents

logger = logging.getLogger("uvicorn.error")

OLLAMA_URL = os.getenv("OLLAMA_URL", "http://localhost:11434/api/generate")
MODEL = "llama3.2"

SYSTEM_PROMPT = """You are an assistant for a logistics company.
Answer questions about shipments, deliveries and transport clearly and briefly.
If you don't know the answer, say so instead of guessing."""


def build_prompt(question: str, context: str | None, documents: list[Document]) -> str:
    sections = []
    if context:
        sections.append(f"Shipment information:\n{context}")
    if documents:
        sections.append("Company policies:\n" + "\n\n".join(d.content for d in documents))

    if not sections:
        return question   # nothing found: answer from general knowledge

    return (
        "Use the information below to answer the question. "
        "If the information doesn't contain the answer, say you don't know.\n\n"
        + "\n\n".join(sections)
        + f"\n\nQuestion: {question}"
    )


def ask(question: str, context: str | None = None) -> str:
    documents = search_documents(question)                 # Retrieval
    prompt = build_prompt(question, context, documents)    # Augmentation
    logger.info("Prompt sent to the model:\n%s", prompt)

    response = requests.post(                              # Generation
        OLLAMA_URL,
        json={"model": MODEL, "system": SYSTEM_PROMPT, "prompt": prompt, "stream": False},
        timeout=120,
    )
    response.raise_for_status()
    return response.json()["response"]