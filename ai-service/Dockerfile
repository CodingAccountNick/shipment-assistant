FROM python:3.12-slim

WORKDIR /app

# Default Ollama address for containers (the host machine)
ENV OLLAMA_URL=http://host.docker.internal:11434/api/generate
ENV OLLAMA_EMBED_URL=http://host.docker.internal:11434/api/embed

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

EXPOSE 8001
CMD ["fastapi", "run", "main.py", "--port", "8001"]