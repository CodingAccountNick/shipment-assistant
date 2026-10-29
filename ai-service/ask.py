import requests


# pip install "fastapi[standard]"
#fastapi dev main.py --port 8001
OLLAMA_URL = "http://localhost:11434/api/generate"
MODEL = "llama3.2"

SYSTEM_PROMPT = """You are an assistant for a logistics company.
Answer questions about shipments, deliveries and transport clearly and briefly.
If you don't know the answer, say so instead of guessing."""

def ask(question: str) -> str:
    response = requests.post(
        OLLAMA_URL,
       json={"model": MODEL,
              "system": SYSTEM_PROMPT,
              "prompt": question, 
              "stream": False},
        timeout=120,
    )
    response.raise_for_status()
    return response.json()["response"]


if __name__ == "__main__":
    question = input("Ask about shipments: ")
    print(ask(question))