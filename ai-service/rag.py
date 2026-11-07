import logging
from dataclasses import dataclass

import numpy as np

from db import connect
from embeddings import embed

logger = logging.getLogger("uvicorn.error")

TOP_K = 3          # at most this many chunks per question



MIN_SCORE = 0.57   # tuned with eval_retrieval.py: unrelated ≤ 0.505, real ≥ 0.639
#MIN_SCORE = 0.5    # ignore chunks less similar than this


@dataclass
class Document:
    source: str
    content: str
    score: float


def search_documents(question: str) -> list[Document]:
    """Find the policy chunks closest in meaning to the question."""
    try:
        query_vector = np.array(embed(["search_query: " + question])[0])
        with connect() as conn:
            rows = conn.execute(
                """
                SELECT source, content, 1 - (embedding <=> %s) AS score
                FROM document_chunks
                ORDER BY embedding <=> %s
                LIMIT %s
                """,
                (query_vector, query_vector, TOP_K),
            ).fetchall()
    except Exception:
        # No documents is better than no answer: log it and carry on
        logger.exception("Document search failed; answering without documents")
        return []

    documents = [Document(source, content, score) for source, content, score in rows if score >= MIN_SCORE]
    for d in documents:
        logger.info("Found %s (score %.3f)", d.source, d.score)
    return documents