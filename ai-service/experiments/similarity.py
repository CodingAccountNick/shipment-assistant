import math
import requests

EMBED_URL = "http://localhost:11434/api/embed"
EMBED_MODEL = "nomic-embed-text"


def embed(texts: list[str]) -> list[list[float]]:
    """Turn each text into a vector (a list of numbers)."""
    response = requests.post(EMBED_URL, json={"model": EMBED_MODEL, "input": texts}, timeout=60)
    response.raise_for_status()
    return response.json()["embeddings"]


def cosine_similarity(a: list[float], b: list[float]) -> float:
    """1.0 = same meaning, around 0 = unrelated."""
    dot = sum(x * y for x, y in zip(a, b))
    length_a = math.sqrt(sum(x * x for x in a))
    length_b = math.sqrt(sum(y * y for y in b))
    return dot / (length_a * length_b)


question = "What if I miss the courier?"

sentences = [
    "If the recipient is not at the address, the driver leaves a card and tries again the next working day.",
    "Shipments can be delayed by bad weather or customs checks.",
    "Our office is open Monday to Friday, 9am to 5pm.",
    "Fragile items must be packed in double-walled boxes.",
]

#changed score significantly
vectors = embed(["search_query: " + question] + ["search_document: " + s for s in sentences])
#vectors = embed([question] + sentences)

question_vector, sentence_vectors = vectors[0], vectors[1:]

print(f"Each text became a vector of {len(question_vector)} numbers.")
print(f"The question's first 5 numbers: {[round(x, 3) for x in question_vector[:5]]}\n")

scores = [(cosine_similarity(question_vector, v), s) for v, s in zip(sentence_vectors, sentences)]
print(f'Question: "{question}"\n')
for score, sentence in sorted(scores, reverse=True):
    print(f"{score:.3f}  {sentence}")