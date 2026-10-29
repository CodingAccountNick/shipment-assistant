import requests

OLLAMA_URL = "http://localhost:11434/api/generate"
MODEL = "llama3.2"

def ask(question: str) -> str:
    response = requests.post(
        OLLAMA_URL,
        json={"model": MODEL, "prompt": question, "stream": False},
        timeout=120,
    )
    response.raise_for_status()
    return response.json()["response"]


if __name__ == "__main__":
    question = input("Ask about shipments: ")
    print(ask(question))