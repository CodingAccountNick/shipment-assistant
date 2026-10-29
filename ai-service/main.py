from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import requests

from ask import ask

app = FastAPI(title="Shipment Assistant AI service")


class AskRequest(BaseModel):
    question: str


class AskResponse(BaseModel):
    answer: str


# Plain `def` (not `async def`) because ask() uses the blocking requests library.
# FastAPI runs it on a worker thread, so several questions can be answered at once.
@app.post("/ask", response_model=AskResponse)
def ask_endpoint(request: AskRequest) -> AskResponse:
    if not request.question.strip():
        raise HTTPException(status_code=400, detail="Question cannot be empty")

    try:
        return AskResponse(answer=ask(request.question))
    except requests.RequestException:
        # Ollama is down or returned an error
        raise HTTPException(status_code=503, detail="Model is not available")


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}
