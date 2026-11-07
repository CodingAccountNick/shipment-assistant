from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from contextlib import asynccontextmanager
from ask import ask
import ingest
import requests
import logging

@asynccontextmanager
async def lifespan(app):
    # On startup: (re)build the document index
    try:
        ingest.main()
    except Exception:
        logger.exception("Document ingest failed; answers won't use the policy documents")
    yield


logger = logging.getLogger("uvicorn.error")

app = FastAPI(title="Shipment AI Service", lifespan=lifespan)


class AskRequest(BaseModel):
    question: str
    context: str | None = None   # NEW: facts the answer should be based on


class AskResponse(BaseModel):
    answer: str


# Plain `def` (not `async def`) because ask() uses the blocking requests library.
# FastAPI runs it on a worker thread, so several questions can be answered at once.
@app.post("/ask", response_model=AskResponse)
def ask_endpoint(request: AskRequest) -> AskResponse:
    if not request.question.strip():
        raise HTTPException(status_code=400, detail="Question cannot be empty")
    try:
        answer = ask(request.question, request.context)   # CHANGED: pass the context on
    except requests.RequestException:
        raise HTTPException(status_code=503, detail="AI model is not available")
    return AskResponse(answer=answer)


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}
