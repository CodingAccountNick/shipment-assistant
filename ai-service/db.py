import os
import psycopg
from pgvector.psycopg import register_vector

DATABASE_URL = os.getenv("DATABASE_URL", "postgresql://shipments:devpassword@localhost:5432/shipments")


def connect() -> psycopg.Connection:
    conn = psycopg.connect(DATABASE_URL, autocommit=True)
    conn.execute("CREATE EXTENSION IF NOT EXISTS vector")   # turn on pgvector in this database
    register_vector(conn)                                    # let psycopg send and receive vectors
    return conn