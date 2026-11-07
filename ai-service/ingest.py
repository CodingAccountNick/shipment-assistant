from pathlib import Path

import numpy as np

from db import connect
from embeddings import embed

DOCS_DIR = Path(__file__).parent / "docs"


def split_into_chunks(text: str) -> list[str]:
    """One chunk per '## ' section, each starting with the document's title for context."""
    lines = text.splitlines()
    title = lines[0].lstrip("# ").strip() if lines else ""

    chunks, current = [], []
    for line in lines[1:]:
        if line.startswith("## ") and current:
            chunks.append(current)
            current = []
        current.append(line)
    if current:
        chunks.append(current)

    return [f"{title}\n" + "\n".join(c).strip() for c in chunks if "".join(c).strip()]


def main():
    files = sorted(DOCS_DIR.glob("*.md"))
    rows = [(f.name, chunk) for f in files for chunk in split_into_chunks(f.read_text(encoding="utf-8"))]

    # One call embeds all chunks. The prefix tells the model these are documents.
    vectors = embed(["search_document: " + content for _, content in rows])

    with connect() as conn:
        with conn.transaction():   # all or nothing: never a half-built index
            conn.execute("""
                CREATE TABLE IF NOT EXISTS document_chunks (
                    id        serial PRIMARY KEY,
                    source    text NOT NULL,
                    content   text NOT NULL,
                    embedding vector(768) NOT NULL
                )
            """)
            conn.execute("DELETE FROM document_chunks")   # rebuild from scratch each time
            for (source, content), vector in zip(rows, vectors):
                conn.execute(
                    "INSERT INTO document_chunks (source, content, embedding) VALUES (%s, %s, %s)",
                    (source, content, np.array(vector)),
                )

    print(f"Stored {len(rows)} chunks from {len(files)} documents.")


if __name__ == "__main__":
    main()



    #python ingest.py
    #docker compose exec db psql -U shipments -d shipments -c "SELECT id, source, left(content, 60) AS starts_with FROM document_chunks;"